// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi;

/// <summary>Which HIGH environment to talk to.</summary>
public enum Environment
{
    Production,
    Sandbox,
}

/// <summary>The API host and datafeed socket host for one environment.</summary>
/// <param name="Api">The REST API host.</param>
/// <param name="Ws">
/// The datafeed socket host, reserved for the feed client that lands in its own
/// plan. Declared here so the feed reuses this configuration and these
/// credentials rather than introducing a second config surface.
/// </param>
public readonly record struct EnvironmentHosts(string Api, string Ws);

/// <summary>
/// The hosts for each environment. <see cref="EnvironmentHosts.Api"/> comes
/// from the spec's <c>servers</c> block, by its <c>x-environment</c> name.
/// A conformance test asserts this still matches the pinned spec.
/// </summary>
public static class Environments
{
    public static readonly IReadOnlyDictionary<Environment, EnvironmentHosts> Map =
        new Dictionary<Environment, EnvironmentHosts>
        {
            [Environment.Production] = new EnvironmentHosts("https://openapi.high.live", "wss://openapi.high.live"),
            [Environment.Sandbox] = new EnvironmentHosts("https://sandbox.high.live", "wss://sandbox.high.live"),
        };

    /// <summary>Parses an environment name (as it would appear in an env var or option), case-sensitive.</summary>
    public static bool TryParse(string value, out Environment environment)
    {
        switch (value)
        {
            case "production": environment = Environment.Production; return true;
            case "sandbox": environment = Environment.Sandbox; return true;
            default: environment = default; return false;
        }
    }

    /// <summary>The valid environment names, for error messages.</summary>
    public const string ValidNames = "production, sandbox";
}
