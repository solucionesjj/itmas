using System.ComponentModel.DataAnnotations;
using System.Text;
using Itmas3.Api.Agenda;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Agenda;
using Itmas3.Api.Security;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Events;
using Itmas3.Application.Agenda.ExportCalendar;
using Itmas3.Application.Agenda.Outlook;
using Itmas3.Application.Agenda.Queries;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Agenda;

/// <summary>
/// Events of the infrastructure agenda (SPEC-0006 §12.1). Every action also requires <c>agenda.read</c> (§7). Updates use
/// optimistic concurrency: GET returns an <c>ETag</c> and PUT requires the matching <c>If-Match</c>. Dates are local to the zone
/// of the organization; for all-day events the last day is inclusive (P-24).
/// </summary>
[ApiController]
[Route("api/v1/agenda-events")]
public sealed class AgendaEventsController(IAgendaQueries queries, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AgendaPermissions.Read)]
    public async Task<ActionResult<AgendaEventListResponse>> List(
        [FromQuery, Required] DateOnly? from,
        [FromQuery, Required] DateOnly? to,
        [FromQuery] Guid? type,
        [FromQuery, RegularExpression(AgendaContract.StatusFilterPattern)] string? status,
        [FromQuery] Guid? responsible,
        [FromQuery] bool mine,
        [FromQuery, MaxLength(10)] string[]? tags,
        [FromQuery, StringLength(100)] string? search,
        CancellationToken cancellationToken)
    {
        if (InvalidRange(from!.Value, to!.Value) is { } invalid)
        {
            return invalid;
        }

        var result = await queries.ListEventsAsync(from.Value, to.Value, Filter(type, status, responsible, mine, tags, search), cancellationToken);
        return new AgendaEventListResponse([.. result.Items.Select(AgendaMapping.Of)], result.Truncated, result.TimeZone, result.OutlookEnabled);
    }

    /// <summary>
    /// Exports the <b>Scheduled</b> events of the range and filters as an iCalendar file (RF-21, CA-19), for whoever is not invited or
    /// when Outlook is not configured. Same range and filters as the list, except the status, which is always Scheduled. The file
    /// carries no e-mail addresses and nothing a person typed can start a new property (see <c>IcalendarWriter</c>). More than
    /// <see cref="AgendaLimits.MaxResults"/> events is a <c>400</c>: a truncated file would look complete.
    /// </summary>
    [HttpGet("export.ics")]
    [RequirePermission(AgendaPermissions.Read)]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK, "text/calendar")]
    public async Task<IActionResult> ExportCalendar(
        [FromQuery, Required] DateOnly? from,
        [FromQuery, Required] DateOnly? to,
        [FromQuery] Guid? type,
        [FromQuery] Guid? responsible,
        [FromQuery] bool mine,
        [FromQuery, MaxLength(10)] string[]? tags,
        [FromQuery, StringLength(100)] string? search,
        [FromServices] ExportCalendarHandler handler,
        CancellationToken cancellationToken)
    {
        if (InvalidRange(from!.Value, to!.Value) is { } invalid)
        {
            return invalid;
        }

        var query = new ExportCalendarQuery(from.Value, to.Value, Filter(type, null, responsible, mine, tags, search));
        var result = await handler.HandleAsync(query, cancellationToken);
        if (result.TooMany)
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Hay más actividades programadas de las que se pueden exportar.",
                detail: "Se pueden exportar hasta 1 000 actividades a la vez. Acota el periodo o los filtros.");
        }

        // UTF-8 without a byte-order mark; an attachment, so a browser never renders it.
        return File(new UTF8Encoding(false).GetBytes(result.Content), "text/calendar; charset=utf-8", result.FileName);
    }

    [HttpGet("summary")]
    [RequirePermission(AgendaPermissions.Read)]
    public async Task<AgendaSummaryResponse> Summary(
        [FromQuery, Required, Range(2000, 2200)] int? year,
        [FromQuery] Guid? type,
        [FromQuery] Guid? responsible,
        [FromQuery] bool mine,
        [FromQuery, MaxLength(10)] string[]? tags,
        [FromQuery, StringLength(100)] string? search,
        CancellationToken cancellationToken)
    {
        var summary = await queries.GetSummaryAsync(year!.Value, Filter(type, null, responsible, mine, tags, search), cancellationToken);
        return new AgendaSummaryResponse(
            summary.Year,
            [.. summary.Months.Select(month => new AgendaMonthResponse(month.Month, month.Draft, month.Scheduled, month.Completed, month.Cancelled, month.Overdue))],
            [.. summary.ByType.Select(row => new AgendaTypeCountResponse(row.TypeId, row.Count))]);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(AgendaPermissions.Read)]
    public async Task<ActionResult<AgendaEventResponse>> Get(Guid id, CancellationToken cancellationToken)
    {
        if (await queries.GetEventAsync(id, cancellationToken) is not { } detail)
        {
            return NotFound();
        }

        Response.Headers.ETag = EntityTags.Format(detail.Revision);
        return AgendaMapping.Of(detail);
    }

    [HttpPost]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Create)]
    [ProducesResponseType<AgendaEventResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        AgendaEventRequest request, [FromServices] CreateEventHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(request.ToInput(), cancellationToken);
        if (await queries.GetEventAsync(result.Id, cancellationToken) is not { } detail)
        {
            return NotFound();
        }

        Response.Headers.ETag = EntityTags.Format(detail.Revision);
        return CreatedAtAction(nameof(Get), new { id = detail.Id }, AgendaMapping.Of(detail));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Update)]
    public async Task<IActionResult> Update(
        Guid id,
        AgendaEventRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateEventHandler handler,
        CancellationToken cancellationToken)
    {
        if (!EntityTags.TryParse(ifMatch, out var expectedRevision))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, title: "Falta la cabecera If-Match con la versión de la actividad.");
        }

        await handler.HandleAsync(new UpdateEventCommand(id, expectedRevision, request.ToInput()), cancellationToken);
        return await CurrentAsync(id, cancellationToken);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Update)]
    public async Task<IActionResult> Delete(Guid id, [FromServices] DeleteDraftEventHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Replaces the tags in any status, without changing the <c>ETag</c> or Outlook (RF-24); the last change wins.</summary>
    [HttpPut("{id:guid}/tags")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Update)]
    public async Task<SetTagsResponse> SetTags(
        Guid id, SetTagsRequest request, [FromServices] SetEventTagsHandler handler, CancellationToken cancellationToken) =>
        new(await handler.HandleAsync(new SetEventTagsCommand(id, request.Tags), cancellationToken));

    /// <summary>Schedules up to 50 drafts at once with one result each. Outlook failures do not make it an error (RF-20).</summary>
    [HttpPost("schedule")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Publish)]
    [ServiceFilter<AgendaPublishLimitFilter>]
    public async Task<ScheduleEventsResponse> Schedule(
        ScheduleEventsRequest request, [FromServices] ScheduleEventsHandler handler, CancellationToken cancellationToken)
    {
        var results = await handler.HandleAsync(new ScheduleEventsCommand(request.Ids), cancellationToken);
        return new ScheduleEventsResponse([.. results.Select(AgendaMapping.Of)]);
    }

    [HttpPost("{id:guid}/cancel")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Publish)]
    [ServiceFilter<AgendaPublishLimitFilter>]
    public async Task<IActionResult> Cancel(
        Guid id, CancelEventRequest request, [FromServices] CancelEventHandler handler, CancellationToken cancellationToken)
    {
        await handler.HandleAsync(new CancelEventCommand(id, request.Reason), cancellationToken);
        return await CurrentAsync(id, cancellationToken);
    }

    /// <summary>Records the execution of a scheduled event and, afterwards, changes only its notes and evidence links (RN-9).</summary>
    [HttpPut("{id:guid}/completion")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Complete)]
    public async Task<IActionResult> Complete(
        Guid id, CompleteEventRequest request, [FromServices] CompleteEventHandler handler, CancellationToken cancellationToken)
    {
        var command = new CompleteEventCommand(
            id,
            AgendaCodes.ParseResult(request.Result)!.Value,
            request.ExecutedAt,
            request.Notes,
            [.. (request.EvidenceLinks ?? []).Select(link => new EvidenceLinkInput(link.Label, link.Url))]);
        await handler.HandleAsync(command, cancellationToken);
        return await CurrentAsync(id, cancellationToken);
    }

    /// <summary>Retries the publication of one event in Outlook, idempotently (RF-19).</summary>
    [HttpPost("{id:guid}/outlook-sync")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Publish)]
    [ServiceFilter<AgendaPublishLimitFilter>]
    public async Task<OutlookSyncResponse> SyncOne(Guid id, [FromServices] SyncWithOutlookHandler handler, CancellationToken cancellationToken) =>
        new(AgendaMapping.Of((await handler.SyncOneAsync(id, cancellationToken)).Outlook));

    /// <summary>Retries every scheduled event that has not started and is pending, in error or not published, up to 50 (RF-19).</summary>
    [HttpPost("outlook-sync")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Publish)]
    [ServiceFilter<AgendaPublishLimitFilter>]
    public async Task<OutlookSyncAllResponse> SyncAll([FromServices] SyncWithOutlookHandler handler, CancellationToken cancellationToken)
    {
        var results = await handler.SyncPendingAsync(cancellationToken);
        return new OutlookSyncAllResponse([.. results.Select(result => new OutlookSyncResultResponse(result.Id, AgendaMapping.Of(result.Outlook)))]);
    }

    /// <summary>The problem to answer when the range is not 1 to 366 days (§12.1), or null when it is valid.</summary>
    private ActionResult? InvalidRange(DateOnly from, DateOnly to)
    {
        var days = to.DayNumber - from.DayNumber;
        if (days > 0 && days <= AgendaLimits.MaxRangeDays)
        {
            return null;
        }

        ModelState.AddModelError(nameof(to), $"El rango debe ir de 1 a {AgendaLimits.MaxRangeDays} días.");
        return ValidationProblem(ModelState);
    }

    /// <summary>The event as it is after the operation; a draft deleted meanwhile by someone else is a 404, not an error.</summary>
    private async Task<IActionResult> CurrentAsync(Guid id, CancellationToken cancellationToken)
    {
        if (await queries.GetEventAsync(id, cancellationToken) is not { } detail)
        {
            return NotFound();
        }

        Response.Headers.ETag = EntityTags.Format(detail.Revision);
        return Ok(AgendaMapping.Of(detail));
    }

    private AgendaFilter Filter(Guid? type, string? status, Guid? responsible, bool mine, string[]? tags, string? search) =>
        new(
            type,
            status switch
            {
                "draft" => AgendaStatusFilter.Draft,
                "scheduled" => AgendaStatusFilter.Scheduled,
                "completed" => AgendaStatusFilter.Completed,
                "cancelled" => AgendaStatusFilter.Cancelled,
                "overdue" => AgendaStatusFilter.Overdue,
                _ => null,
            },
            responsible,
            mine ? currentUser.ApplicationUserId : null,
            tags ?? [],
            search);
}
