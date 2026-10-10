using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;

namespace Core.Workflow.Workflows;

/// <summary>
/// FlowJoin cancels inbound ancestors when it runs. On a loop back into the same stage that
/// cancel drops the path that just arrived, so the next command bookmark is never created.
/// </summary>
internal sealed class FirstArrivalJoin : FlowJoin
{
    protected override ValueTask ExecuteAsync(ActivityExecutionContext context)
        => context.CompleteActivityAsync();
}

/// <summary>
/// Elsa treats an activity with several inbound links as a join and waits until every link has been visited.
/// Revision and cancel edges never run on the first pass, so the next stage never starts.
/// A wait-any join lets the first arriving path open that stage.
/// </summary>
internal static class FlowchartInboundJoins
{
    public static void PreferFirstArrival(Flowchart flow)
    {
        var groups = flow.Connections
            .GroupBy(connection => connection.Target.Activity)
            .Where(group => group.Count() > 1)
            .ToList();

        foreach (var group in groups)
        {
            var target = group.Key;
            var join = new FirstArrivalJoin
            {
                Id = $"{target.Id}Join",
                Mode = new(FlowJoinMode.WaitAny)
            };
            flow.Activities.Add(join);

            foreach (var connection in group)
            {
                flow.Connections.Remove(connection);
                var port = string.IsNullOrWhiteSpace(connection.Source.Port) ? "Done" : connection.Source.Port;
                flow.Connections.Add(new Connection(new Endpoint(connection.Source.Activity, port), new Endpoint(join)));
            }

            flow.Connections.Add(new Connection(new Endpoint(join, "Done"), new Endpoint(target)));
        }
    }
}
