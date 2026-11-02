using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class AgentAccountTests
{
    [Test]
    public void CreateAgent_AllowsTheNamesAgentRuntimesAreGiven()
    {
        var agent = User.CreateAgent("  Ollama: llama-home (Pi) ", "  Runs the nightly triage  ");

        Assert.Multiple(() =>
        {
            Assert.That(agent.Kind, Is.EqualTo(UserKind.Agent));
            Assert.That(agent.Username, Is.EqualTo("Ollama: llama-home (Pi)"));
            Assert.That(agent.NormalizedUsername, Is.EqualTo("ollama: llama-home (pi)"));
            Assert.That(agent.Description, Is.EqualTo("Runs the nightly triage"));
            Assert.That(agent.IsAdmin, Is.False);
        });
    }

    [TestCase(null)]
    [TestCase("   ")]
    public void CreateAgent_TreatsABlankDescriptionAsNone(string? description)
    {
        Assert.That(User.CreateAgent("Nightly triage", description).Description, Is.Null);
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("Line\nbreak")]
    public void CreateAgent_RejectsBlankOrMultiLineNames(string name)
    {
        Assert.Throws<DomainValidationException>(() => User.CreateAgent(name, null));
    }

    [Test]
    public void CreateAgent_RejectsOverlongNamesAndDescriptions()
    {
        Assert.Multiple(() =>
        {
            Assert.Throws<DomainValidationException>(() => User.CreateAgent(new string('a', User.MaxUsernameLength + 1), null));
            Assert.Throws<DomainValidationException>(
                () => User.CreateAgent("Nightly triage", new string('a', User.MaxDescriptionLength + 1)));
        });
    }

    [Test]
    public void GrantAdmin_RefusesAnAgent()
    {
        var agent = User.CreateAgent("Nightly triage", null);

        var ex = Assert.Throws<NotAHumanAccountException>(agent.GrantAdmin);

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Kind, Is.EqualTo(DomainErrorKind.Conflict));
            Assert.That(agent.IsAdmin, Is.False);
        });
    }
}
