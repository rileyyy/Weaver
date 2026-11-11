using Weaver.Runner.Tasks;

namespace Weaver.Runner.Tests.Tasks;

[TestFixture]
public class TaskKindTests
{
    [TestCase("when-resolution")]
    [TestCase("WHEN-RESOLUTION")]
    public void FromCommandName_FindsTheKind_IgnoringCase(string name) =>
        Assert.That(TaskKind.FromCommandName(name), Is.SameAs(TaskKind.WhenResolution));

    [TestCase("WhenResolution")]
    [TestCase("whenresolution")]
    public void FromConfigKey_FindsTheKind_IgnoringCase(string key) =>
        Assert.That(TaskKind.FromConfigKey(key), Is.SameAs(TaskKind.WhenResolution));

    [Test]
    public void Mentions_IsFoundByItsCommandNameAndConfigKey()
    {
        Assert.Multiple(() =>
        {
            Assert.That(TaskKind.FromCommandName("mentions"), Is.SameAs(TaskKind.Mentions));
            Assert.That(TaskKind.FromConfigKey("Mentions"), Is.SameAs(TaskKind.Mentions));
        });
    }

    [TestCase("")]
    [TestCase("when")]
    [TestCase("WhenResolution")]
    public void FromCommandName_IsNullForAnUnknownName(string name) =>
        Assert.That(TaskKind.FromCommandName(name), Is.Null);

    [TestCase("")]
    [TestCase("When")]
    [TestCase("when-resolution")]
    public void FromConfigKey_IsNullForAnUnknownKey(string key) =>
        Assert.That(TaskKind.FromConfigKey(key), Is.Null);

    [Test]
    public void ConfigKeys_HaveNoHyphens_SoTheyCanBeSetFromTheEnvironment() =>
        Assert.That(TaskKind.All.Select(k => k.ConfigKey), Has.None.Contain("-"));
}
