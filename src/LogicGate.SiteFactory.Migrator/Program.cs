using LogicGate.SiteFactory.Migrator;

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, signal) =>
{
    signal.Cancel = true;
    cancellation.Cancel();
};
return await MigrationCommand.RunAsync(args,
    Environment.GetEnvironmentVariable("SITEFACTORY_MIGRATOR_CONNECTION_FILE"),
    Console.Out, Console.Error, cancellation.Token);
