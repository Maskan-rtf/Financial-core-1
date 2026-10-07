using Elsa.Workflows;
using Elsa.Workflows.Activities.Flowchart.Activities;
using Elsa.Workflows.Activities.Flowchart.Models;

namespace Core.Workflow.Workflows;

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
            var join = new FlowJoin { Id = $"{target.Id}Join" };
            flow.Activities.Add(join);

            foreach (var connection in group)
            {
                flow.Connections.Remove(connection);
                flow.Connections.Add(new Connection(connection.Source.Activity, join));
            }

            flow.Connections.Add(new Connection(join, target));
        }
    }
}
