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
        Assert.Equal("a3a0c14a9ea57a3295a6329f83c56fcf0ee45db7", lockFile["commit"]!.GetValue<string>());
    }
}
