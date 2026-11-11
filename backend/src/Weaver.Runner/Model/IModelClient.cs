namespace Weaver.Runner.Model;

public interface IModelClient
{
    /// <summary>
    /// The model's reply text. Throws <see cref="ModelUnavailableException"/> when the endpoint
    /// can't be reached or would refuse any prompt, which stops the run, and
    /// <see cref="ModelRejectedException"/> when it turns down only this prompt. A reply that
    /// makes no sense is returned as is, for the caller to reject.
    /// </summary>
    Task<string> CompleteAsync(ModelPrompt prompt, CancellationToken ct);
}
