using Erp.Seeder;

var (options, error) = SeedOptions.Parse(args, Environment.GetEnvironmentVariable(SeedOptions.PasswordVariable));
if (options is null)
{
    await Console.Error.WriteLineAsync(error);
    await Console.Error.WriteLineAsync(SeedOptions.Usage);
    return 2;
}

using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, press) =>
{
    press.Cancel = true;
    cancellation.Cancel();
};

using var host = SeederHost.Build();
return await SeedRun.ExecuteAsync(host, options, Console.Out, cancellation.Token);
