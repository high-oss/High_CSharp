// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

public sealed class PortfolioResource
{
    private readonly ResolvedConfig _config;

    internal PortfolioResource(ResolvedConfig config) => _config = config;

    /// <summary>Open intraday and carry-forward positions with their P&amp;L snapshot.</summary>
    public Task<Positions> Positions(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<Positions>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/portfolio/positions", Auth = AuthKind.Bearer },
            cancellationToken);

    /// <summary>Demat holdings with their investment snapshot.</summary>
    public Task<Holdings> Holdings(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<Holdings>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/portfolio/holdings", Auth = AuthKind.Bearer },
            cancellationToken);

    /// <summary>Cash, margin and charge balances.</summary>
    public Task<Funds> Funds(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<Funds>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/portfolio/funds", Auth = AuthKind.Bearer },
            cancellationToken);

    /// <summary>Converts a position between product types. Never retried.</summary>
    public Task<PositionConvertResult> ConvertPosition(ConvertPositionRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<PositionConvertResult>(_config,
            new RequestOptions { Method = HttpMethod.Patch, Path = "/portfolio/positions/convert", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);

    /// <summary>Squares off every open position. Never retried.</summary>
    public Task<PositionExitAllResult> ExitAllPositions(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<PositionExitAllResult>(_config,
            new RequestOptions { Method = HttpMethod.Delete, Path = "/portfolio/positions/exit/all", Auth = AuthKind.Bearer },
            cancellationToken);

    /// <summary>Squares off one position. The API expects a body on this DELETE. Never retried.</summary>
    public Task<PositionExitResult> ExitPosition(ExitPositionRequest body, CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<PositionExitResult>(_config,
            new RequestOptions { Method = HttpMethod.Delete, Path = "/portfolio/positions/exit", Auth = AuthKind.Bearer, Body = body },
            cancellationToken);
}
