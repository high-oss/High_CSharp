// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Resources;

namespace High.OpenApi;

/// <summary>
/// The HIGH Open API client.
///
/// <code>
/// var high = new HighClient(new HighClientOptions
/// {
///     Environment = Environment.Sandbox,
///     ApiKey = apiKey,
///     AccessToken = accessToken,
/// });
/// var funds = await high.Portfolio.Funds();
/// </code>
///
/// Deliberately not a record and not disposed by default when a caller
/// supplies their own <see cref="System.Net.Http.HttpClient"/> (e.g. from
/// <c>IHttpClientFactory</c>) — the client does not own a transport it did not
/// create. When it created its own (the common case), <see cref="Dispose"/>
/// releases it.
/// </summary>
public sealed class HighClient : IDisposable
{
    /// <summary>Where requests go and what they carry. Settled once, at construction.</summary>
    public ResolvedConfig Config { get; }

    public AuthResource Auth { get; }
    public MarketResource Market { get; }
    public OrdersResource Orders { get; }
    public PortfolioResource Portfolio { get; }
    public ScripsResource Scrips { get; }

    public HighClient(HighClientOptions? options = null)
    {
        Config = ConfigResolver.Resolve(options);
        Auth = new AuthResource(Config);
        Market = new MarketResource(Config);
        Orders = new OrdersResource(Config);
        Portfolio = new PortfolioResource(Config);
        Scrips = new ScripsResource(Config);
    }

    /// <summary>Never prints credentials — delegates to <see cref="ResolvedConfig.ToString"/>.</summary>
    public override string ToString() => $"HighClient {{ Config = {Config} }}";

    public void Dispose()
    {
        if (Config.OwnsHttpClient) Config.HttpClient.Dispose();
    }
}
