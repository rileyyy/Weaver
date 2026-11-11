using Weaver.Runner.Model;

namespace Weaver.Runner.Tests.Fakes;

/// <summary>Answers each prompt with <see cref="Answer"/>, and records what it was asked.</summary>
internal sealed class FakeModelClient : IModelClient
{
    public Func<ModelPrompt, string> Answer { get; set; } = _ => """{"skip":"no answer set"}""";

    public List<ModelPrompt> Prompts { get; } = [];

    public Task<string> CompleteAsync(ModelPrompt prompt, CancellationToken ct)
    {
        Prompts.Add(prompt);
        return Task.FromResult(Answer(prompt));
    }

    /// <summary>Answers by which item the prompt is about, found by its title.</summary>
    public void AnswerByTitle(Dictionary<string, string> answers) =>
        Answer = prompt => answers.First(a => prompt.User.Contains(a.Key, StringComparison.Ordinal)).Value;
}
