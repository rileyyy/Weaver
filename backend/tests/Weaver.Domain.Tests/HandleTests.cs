using Weaver.Domain.Exceptions;

namespace Weaver.Domain.Tests;

[TestFixture]
public class HandleTests
{
    [TestCase("claude-research")]
    [TestCase("llama3")]
    [TestCase("a")]
    [TestCase("runner-llama3-1-8b")]
    public void SetHandle_AcceptsLowerCaseLettersDigitsAndHyphens(string handle)
    {
        var agent = User.CreateAgent("Claude research", null);

        agent.SetHandle(handle);

        Assert.That(agent.Handle, Is.EqualTo(handle));
    }

    [Test]
    public void SetHandle_TrimsSurroundingSpace()
    {
        var agent = User.CreateAgent("Claude research", null);

        agent.SetHandle("  claude-research ");

        Assert.That(agent.Handle, Is.EqualTo("claude-research"));
    }

    [TestCase("Claude")]
    [TestCase("claude research")]
    [TestCase("claude_research")]
    [TestCase("@claude")]
    [TestCase("-claude")]
    [TestCase("claude-")]
    [TestCase("claude--research")]
    [TestCase("clåude")]
    public void SetHandle_RejectsAnythingButLowerCaseWordsJoinedByHyphens(string handle)
    {
        var agent = User.CreateAgent("Claude research", null);

        Assert.Throws<DomainValidationException>(() => agent.SetHandle(handle));
        Assert.That(agent.Handle, Is.Null);
    }

    [Test]
    public void SetHandle_RejectsAnOverlongHandle()
    {
        var agent = User.CreateAgent("Claude research", null);

        Assert.Throws<DomainValidationException>(() => agent.SetHandle(new string('a', Handle.MaxLength + 1)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public void SetHandle_WithNothing_ClearsTheHandle(string? handle)
    {
        var agent = User.CreateAgent("Claude research", null);
        agent.SetHandle("claude-research");

        agent.SetHandle(handle);

        Assert.That(agent.Handle, Is.Null);
    }

    [Test]
    public void ANewAccountHasNoHandle()
    {
        Assert.Multiple(() =>
        {
            Assert.That(User.CreateAgent("Claude research", null).Handle, Is.Null);
            Assert.That(User.CreateHuman("riley", "riley").Handle, Is.Null);
        });
    }
}
