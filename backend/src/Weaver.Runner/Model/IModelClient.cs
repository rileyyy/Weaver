namespace Weaver.Runner.Model;

public interface IModelClient
{
    /// <summary>
    /// The model's reply text. Throws <see cref="ModelUnavailableException"/> when the endpoint
    /// can't be reached or refuses the request, which stops the run; a reply that makes no sense
    /// is returned as is, for the caller to reject.
    /// </summary>
    Task<string> CompleteAsync(ModelPrompt prompt, CancellationToken ct);
}
