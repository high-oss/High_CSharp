// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;
using System.Text.Json.Nodes;
using High.OpenApi;
using High.OpenApi.Models;
using Xunit;

namespace High.OpenApi.Tests;

/// <summary>
/// Checks the hand-written source against the vendored spec at the commit
/// pinned in <c>spec.lock.json</c>. This is the substitute for a generator's
/// own drift check: since models here are hand-written (see the plan's
/// "Generation decision"), nothing else would catch <c>ENVIRONMENTS</c> or
/// <c>ErrorCodes</c> silently falling out of step with the contract.
/// </summary>
public class ConformanceTests
{
    private static JsonNode Spec()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "spec", "openapi.json");
        return JsonNode.Parse(File.ReadAllText(path))!;
    }

    [Fact]
    public void Environments_host_per_environment_matches_the_pinned_specs_servers_block()
    {
        var servers = Spec()["servers"]!.AsArray();
        var fromSpec = servers.ToDictionary(
            s => s!["x-environment"]!.GetValue<string>(),
            s => new Uri(s!["url"]!.GetValue<string>()).GetLeftPart(UriPartial.Authority));

        Assert.Equal(fromSpec["production"], Environments.Map[Environment.Production].Api);
        Assert.Equal(fromSpec["sandbox"], Environments.Map[Environment.Sandbox].Api);
        Assert.Equal(2, fromSpec.Count);
    }

    [Fact]
    public void ErrorCodes_All_matches_the_pinned_specs_x_error_codes()
    {
        var fromSpec = Spec()["components"]!["schemas"]!["Error"]!["properties"]!["code"]!["x-error-codes"]!
            .AsArray().Select(v => v!.GetValue<string>()).OrderBy(s => s).ToList();
        var fromCode = ErrorCodes.All.OrderBy(s => s).ToList();

        Assert.Equal(fromSpec, fromCode);
    }

    [Fact]
    public void ExpiryType_enum_matches_the_pinned_specs_path_parameter_enum()
    {
        var parameters = Spec()["paths"]!["/scrips/{symbol}/{type}/expiries"]!["get"]!["parameters"]!.AsArray();
        var typeParam = parameters.Single(p => p!["name"]!.GetValue<string>() == "type");
        var fromSpec = typeParam!["schema"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).OrderBy(s => s).ToList();

        var fromCode = Enum.GetValues<ExpiryType>()
            .Select(v => JsonSerializer.Serialize(v, High.OpenApi.Json.HighJsonOptions.Default).Trim('"'))
            .OrderBy(s => s)
            .ToList();

        Assert.Equal(fromSpec, fromCode);
    }

    [Fact]
    public void The_spec_lock_pins_the_commit_the_task_named()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
        var lockFile = JsonNode.Parse(File.ReadAllText(Path.Combine(dir!.FullName, "spec.lock.json")))!;
        Assert.Equal("c1f37de4ec27245288ef9c9f83699c51f29b3b50", lockFile["commit"]!.GetValue<string>());
    }

    [Fact]
    public void InstrumentCategory_enum_matches_the_pinned_specs_InstrumentFile_schema()
    {
        var fromSpec = Spec()["components"]!["schemas"]!["InstrumentFile"]!["properties"]!["instrument"]!["enum"]!
            .AsArray().Select(v => v!.GetValue<string>()).OrderBy(s => s).ToList();

        var fromCode = Enum.GetValues<InstrumentCategory>()
            .Select(v => JsonSerializer.Serialize(v, High.OpenApi.Json.HighJsonOptions.Default).Trim('"'))
            .OrderBy(s => s)
            .ToList();

        Assert.Equal(fromSpec, fromCode);
    }

    [Fact]
    public void InstrumentsManifest_columns_example_has_exactly_17_entries_matching_the_scrip_master_header()
    {
        var columns = Spec()["components"]!["schemas"]!["InstrumentsManifest"]!["properties"]!["columns"]!["example"]!
            .AsArray().Select(v => v!.GetValue<string>()).ToList();

        Assert.Equal(17, columns.Count);
    }

    [Fact]
    public void IntradayInterval_enum_matches_the_pinned_specs_intraday_interval_enum()
    {
        var fromSpec = Spec()["paths"]!["/scrips/intraday"]!["post"]!["requestBody"]!["content"]!["application/json"]!["schema"]!
            ["properties"]!["interval"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).OrderBy(s => s).ToList();

        var fromCode = Enum.GetValues<IntradayInterval>()
            .Select(v => JsonSerializer.Serialize(v, High.OpenApi.Json.HighJsonOptions.Default).Trim('"'))
            .OrderBy(s => s)
            .ToList();

        Assert.Equal(fromSpec, fromCode);
    }

    [Fact]
    public void RiskOrderLeg_and_RiskOrderAction_enums_match_the_pinned_specs_PositionRiskOrderResult()
    {
        var properties = Spec()["components"]!["schemas"]!["PositionRiskOrderResult"]!["properties"]!;

        var legFromSpec = properties["leg"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).OrderBy(s => s).ToList();
        var legFromCode = Enum.GetValues<RiskOrderLeg>()
            .Select(v => JsonSerializer.Serialize(v, High.OpenApi.Json.HighJsonOptions.Default).Trim('"'))
            .OrderBy(s => s)
            .ToList();
        Assert.Equal(legFromSpec, legFromCode);

        var actionFromSpec = properties["action"]!["enum"]!.AsArray().Select(v => v!.GetValue<string>()).OrderBy(s => s).ToList();
        var actionFromCode = Enum.GetValues<RiskOrderAction>()
            .Select(v => JsonSerializer.Serialize(v, High.OpenApi.Json.HighJsonOptions.Default).Trim('"'))
            .OrderBy(s => s)
            .ToList();
        Assert.Equal(actionFromSpec, actionFromCode);
    }
}
