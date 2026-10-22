using System.ComponentModel.DataAnnotations;
using Itmas3.Api.Authorization;
using Itmas3.Api.Contracts.Agenda;
using Itmas3.Application.Abstractions.Identity;
using Itmas3.Application.Agenda;
using Itmas3.Application.Agenda.Queries;
using Itmas3.Application.Agenda.Series;
using Itmas3.Application.Agenda.Types;
using Microsoft.AspNetCore.Mvc;

namespace Itmas3.Api.Controllers.Agenda;

/// <summary>Existing tags with the number of events that have them, to suggest and to filter (SPEC-0006 RF-26).</summary>
[ApiController]
[Route("api/v1/agenda-tags")]
public sealed class AgendaTagsController(IAgendaQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AgendaPermissions.Read)]
    public async Task<AgendaTagListResponse> List(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, Range(1, AgendaLimits.MaxTagSuggestions)] int limit = AgendaLimits.MaxTagSuggestions,
        CancellationToken cancellationToken = default)
    {
        var tags = await queries.ListTagsAsync(search, limit, cancellationToken);
        return new AgendaTagListResponse([.. tags.Select(tag => new AgendaTagResponse(tag.Name, tag.Count))]);
    }
}

/// <summary>
/// The people that can be chosen as the responsible of an activity (SPEC-0006 RF-3, P-31): active ITMas users by name, without
/// e-mail addresses. Opened by <c>agenda.read</c> so that whoever plans does not need <c>users.read</c>.
/// </summary>
[ApiController]
[Route("api/v1/agenda-people")]
public sealed class AgendaPeopleController(IAgendaQueries queries, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AgendaPermissions.Read)]
    public async Task<AgendaPeopleResponse> Search(
        [FromQuery, StringLength(100)] string? search,
        [FromQuery, Range(1, AgendaLimits.MaxPeople)] int limit = AgendaLimits.MaxPeople,
        CancellationToken cancellationToken = default)
    {
        var meId = currentUser.ApplicationUserId;
        var people = await queries.SearchPeopleAsync(search, limit, meId, cancellationToken);
        return new AgendaPeopleResponse([.. people.Select(person => new AgendaPersonOptionResponse(person.Id, person.Name, person.Id == meId))]);
    }
}

/// <summary>
/// Activity types (SPEC-0006 RF-1, RF-2). A type is never deleted, only deactivated with PUT. Updates use <c>ETag</c> and
/// <c>If-Match</c>. Every action also requires <c>agenda.read</c> (§7).
/// </summary>
[ApiController]
[Route("api/v1/agenda-event-types")]
public sealed class AgendaEventTypesController(IAgendaQueries queries) : ControllerBase
{
    [HttpGet]
    [RequirePermission(AgendaPermissions.Read)]
    public async Task<AgendaEventTypeListResponse> List(CancellationToken cancellationToken) =>
        new([.. (await queries.ListEventTypesAsync(cancellationToken)).Select(AgendaMapping.Of)]);

    [HttpPost]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.TypesManage)]
    [ProducesResponseType<AgendaEventTypeResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(EventTypeRequest request, [FromServices] CreateEventTypeHandler handler, CancellationToken cancellationToken)
    {
        var type = await handler.HandleAsync(request.ToInput(), cancellationToken);
        if (await queries.GetEventTypeAsync(type.Id, cancellationToken) is not { } view)
        {
            return NotFound();
        }

        Response.Headers.ETag = EntityTags.Format(view.Revision);
        return Created($"/api/v1/agenda-event-types/{view.Id}", AgendaMapping.Of(view));
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.TypesManage)]
    public async Task<IActionResult> Update(
        Guid id,
        EventTypeRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        [FromServices] UpdateEventTypeHandler handler,
        CancellationToken cancellationToken)
    {
        if (!EntityTags.TryParse(ifMatch, out var expectedRevision))
        {
            return Problem(statusCode: StatusCodes.Status428PreconditionRequired, title: "Falta la cabecera If-Match con la versión del tipo.");
        }

        await handler.HandleAsync(new UpdateEventTypeCommand(id, expectedRevision, request.ToInput(), request.IsActive), cancellationToken);
        if (await queries.GetEventTypeAsync(id, cancellationToken) is not { } view)
        {
            return NotFound();
        }

        Response.Headers.ETag = EntityTags.Format(view.Revision);
        return Ok(AgendaMapping.Of(view));
    }
}

/// <summary>Series of activities (SPEC-0006 RF-4, RF-6). Every action also requires <c>agenda.read</c> (§7).</summary>
[ApiController]
[Route("api/v1/agenda-series")]
public sealed class AgendaSeriesController : ControllerBase
{
    /// <summary>
    /// With <c>dryRun=true</c>, previews the dates and warnings without saving anything (200); otherwise creates the series and its
    /// occurrences in Draft state (201).
    /// </summary>
    [HttpPost]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Create)]
    [ProducesResponseType<SeriesPreviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<SeriesCreatedResponse>(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create(
        AgendaSeriesRequest request,
        [FromQuery] bool dryRun,
        [FromServices] CreateSeriesHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new CreateSeriesCommand(request.Event.ToInput(), request.Rule.ToInput(), dryRun), cancellationToken);
        if (dryRun)
        {
            return Ok(new SeriesPreviewResponse(
                [.. result.Occurrences.Select(occurrence => new SeriesOccurrenceResponse(
                    AgendaMapping.Start(occurrence.Schedule.IsAllDay, occurrence.Schedule.StartLocal),
                    AgendaMapping.End(occurrence.Schedule.IsAllDay, occurrence.Schedule.EndLocal),
                    occurrence.Schedule.IsAllDay,
                    AgendaMapping.Of(occurrence.Warnings)))]));
        }

        return StatusCode(StatusCodes.Status201Created, new SeriesCreatedResponse(result.SeriesId!.Value, result.EventIds));
    }

    /// <summary>Changes the common fields of the occurrences that have not started; the dates never change (RF-6).</summary>
    [HttpPut("{id:guid}")]
    [RequirePermission(AgendaPermissions.Read)]
    [RequirePermission(AgendaPermissions.Update)]
    public async Task<SeriesUpdatedResponse> Update(
        Guid id, UpdateSeriesRequest request, [FromServices] UpdateSeriesHandler handler, CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new UpdateSeriesCommand(id, request.ToInput()), cancellationToken);
        return new SeriesUpdatedResponse(result.Updated);
    }
}
