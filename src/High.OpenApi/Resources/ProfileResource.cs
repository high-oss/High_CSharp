// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using High.OpenApi.Http;
using High.OpenApi.Models;

namespace High.OpenApi.Resources;

public sealed class ProfileResource
{
    private readonly ResolvedConfig _config;

    internal ProfileResource(ResolvedConfig config) => _config = config;

    /// <summary>
    /// The account holder's identity, demat and bank details. PAN, Aadhaar and
    /// bank account numbers are masked to their last four characters.
    /// </summary>
    public Task<Profile> GetAsync(CancellationToken cancellationToken = default) =>
        HighHttpTransport.SendAsync<Profile>(_config,
            new RequestOptions { Method = HttpMethod.Get, Path = "/profile", Auth = AuthKind.Bearer },
            cancellationToken);
}
