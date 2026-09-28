// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

// Reads the portfolio and the market session. Run with:
//   HIGH_ENVIRONMENT=sandbox HIGH_ACCESS_TOKEN=... dotnet run --project examples/QuickStart

using High.OpenApi;

var logLevelName = System.Environment.GetEnvironmentVariable("HIGH_LOG_LEVEL");
var options = new HighClientOptions();
if (logLevelName is not null && High.OpenApi.Logging.LogLevels.TryParse(logLevelName, out var parsedLevel))
{
    options.LogLevel = parsedLevel;
}

using var high = new HighClient(options);

try
{
    var fundsTask = high.Portfolio.Funds();
    var holdingsTask = high.Portfolio.Holdings();
    var statusTask = high.Market.Status();
    await Task.WhenAll(fundsTask, holdingsTask, statusTask);

    var funds = await fundsTask;
    var holdings = await holdingsTask;
    var status = await statusTask;

    Console.WriteLine($"Available balance: {funds.AvailableBalance}");
    Console.WriteLine($"Holdings: {holdings.TotalStocks} worth {holdings.Snapshot.CurrentValue}");
    Console.WriteLine($"NSE: {(status.ExchangeStatus.TryGetValue("NSE", out var nse) ? nse.Status : "unknown")}");
}
catch (HighApiError error)
{
    Console.Error.WriteLine($"[{error.Status}] {error.Code ?? "no code"}: {error.Message}");
    if (error.RequestId is not null) Console.Error.WriteLine($"requestId: {error.RequestId}");
    System.Environment.Exit(1);
}
