using Weaver.Runner.Tasks;

namespace Weaver.Runner.Tests;

[TestFixture]
public class RunnerCommandLineTests
{
    [Test]
    public void NoArguments_RunsOnTheSchedule()
    {
        Assert.That(RunnerCommandLine.Parse([]).Mode, Is.EqualTo(RunnerMode.Schedule));
    }

    [TestCase("when-resolution")]
    [TestCase("When-Resolution")]
    public void Run_RunsThatTaskKindOnce(string name)
    {
        var command = RunnerCommandLine.Parse(["--run", name]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Mode, Is.EqualTo(RunnerMode.RunOnce));
            Assert.That(command.Kind, Is.EqualTo(TaskKind.WhenResolution));
        });
    }

    [Test]
    public void Run_WithAnUnknownKind_SaysWhichKindsThereAre()
    {
        var command = RunnerCommandLine.Parse(["--run", "research"]);

        Assert.Multiple(() =>
        {
            Assert.That(command.Mode, Is.EqualTo(RunnerMode.Invalid));
            Assert.That(command.Error, Does.Contain("research").And.Contain("when-resolution"));
        });
    }

    [TestCase("--run", TestName = "RunWithoutAKind")]
    [TestCase("--frobnicate", TestName = "UnknownOption")]
    [TestCase("--run when-resolution extra", TestName = "ExtraArgument")]
    public void AnythingElse_IsInvalid(string commandLine)
    {
        Assert.That(RunnerCommandLine.Parse(commandLine.Split(' ')).Mode, Is.EqualTo(RunnerMode.Invalid));
    }

    [TestCase("--help")]
    [TestCase("-h")]
    public void Help_AsksForUsage(string arg)
    {
        Assert.That(RunnerCommandLine.Parse([arg]).Mode, Is.EqualTo(RunnerMode.Help));
    }
}
