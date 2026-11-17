using Elsa.Workflows;

namespace Core.Workflow.Workflows;

internal interface ICodeFirstWorkflow
{
    void PopulateBuilder(IWorkflowBuilder builder);
}
