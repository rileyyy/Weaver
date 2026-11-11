using Weaver.Runner.Configuration;

namespace Weaver.Runner.Tasks;

public interface ITaskRunFactory
{
    /// <summary>A fresh run of <paramref name="config"/>'s task kind, wired to its model and Weaver.</summary>
    ITaskRun Create(TaskConfig config);
}
