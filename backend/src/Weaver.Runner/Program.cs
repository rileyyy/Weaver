using System.Runtime.InteropServices;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Weaver.Runner;
using Weaver.Runner.Configuration;
using Weaver.Runner.Scheduling;
using Weaver.Runner.Tasks;

var command = RunnerCommandLine.Parse(args);
switch (command.Mode)
{
    case RunnerMode.Help:
        Console.WriteLine(RunnerCommandLine.Usage);
        return ExitCodes.Success;
    case RunnerMode.Invalid:
        Console.Error.WriteLine(command.Error);
        Console.Error.WriteLine(RunnerCommandLine.Usage);
        return ExitCodes.BadSetup;
}

// Arguments aren't passed on: --run is ours, not a configuration key.
var builder = Host.CreateApplicationBuilder();
builder.Configuration.AddJsonFile(builder.Configuration["ConfigFile"] ?? "runner.json", optional: true);
// Added again so environment variables still override the file added after the defaults.
builder.Configuration.AddEnvironmentVariables();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
});

RunnerConfig config;
try
{
    config = RunnerConfigReader.Read(builder.Configuration);
}
catch (RunnerConfigException e)
{
    Console.Error.WriteLine(e.Message);
    return ExitCodes.BadSetup;
}

builder.Services.AddSingleton(config);
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITaskRunFactory, TaskRunFactory>();
builder.Services.AddSingleton<TaskRunExecutor>();
builder.Services.AddSingleton<ScheduleLoop>();

if (command.Mode == RunnerMode.RunOnce)
{
    var kindConfig = config.For(command.Kind!);
    if (kindConfig is null)
    {
        Console.Error.WriteLine($"Task kind {command.Kind} isn't configured: set Tasks:{command.Kind!.ConfigKey} in runner.json or the environment.");
        return ExitCodes.BadSetup;
    }

    using var host = builder.Build();
    using var stop = new CancellationTokenSource();
    using var sigint = PosixSignalRegistration.Create(PosixSignal.SIGINT, Stop);
    using var sigterm = PosixSignalRegistration.Create(PosixSignal.SIGTERM, Stop);
    try
    {
        return await host.Services.GetRequiredService<TaskRunExecutor>().RunAsync(kindConfig, stop.Token);
    }
    catch (OperationCanceledException) when (stop.IsCancellationRequested)
    {
        return ExitCodes.RunStopped;
    }

    void Stop(PosixSignalContext context)
    {
        context.Cancel = true;
        stop.Cancel();
    }
}

if (!config.Tasks.Any(t => t.Schedule is not null))
{
    Console.Error.WriteLine("No task kind has a Cron schedule. Set one, or use --run <kind> to run a task kind once.");
    return ExitCodes.BadSetup;
}

builder.Services.AddHostedService<ScheduledTasksService>();
await builder.Build().RunAsync();
return Environment.ExitCode;
