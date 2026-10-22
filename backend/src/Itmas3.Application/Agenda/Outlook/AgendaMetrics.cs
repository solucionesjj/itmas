using System.Diagnostics.Metrics;

namespace Itmas3.Application.Agenda.Outlook;

/// <summary>
/// Metrics of the agenda (SPEC-0006 RNF-8): the result of every operation with Outlook, published as
/// <c>itmas.agenda.outlook.operations</c> by the meter <see cref="MeterName"/>. Tags: <c>operation</c> (<c>publish</c> or
/// <c>cancel</c>) and <c>result</c> (<c>success</c>, a normalized error code or <c>not-attempted</c>). No ids, titles or addresses.
/// </summary>
public static class AgendaMetrics
{
    public const string MeterName = "Itmas3.Agenda";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> OutlookOperations = Meter.CreateCounter<long>(
        "itmas.agenda.outlook.operations", description: "Operations with the shared Outlook calendar, by result.");

    public static void RecordOutlookOperation(string operation, string result) =>
        OutlookOperations.Add(1, new KeyValuePair<string, object?>("operation", operation), new KeyValuePair<string, object?>("result", result));
}
