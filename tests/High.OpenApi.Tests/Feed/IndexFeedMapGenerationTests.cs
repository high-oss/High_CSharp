// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.Json;
using High.OpenApi.Feed;
using Xunit;

namespace High.OpenApi.Tests.Feed;

/// <summary>
/// Rebuilds the index-feed table from the vendored <c>spec/index-feed-map.json</c>
/// (a copy of the canonical <c>High/sdk/index-feed-map.json</c>) and asserts the
/// committed, generated <see cref="IndexFeedMap"/> still matches it exactly. A
/// new or changed index upstream then shows up as a failing test rather than a
/// silent gap between the source and the generated table.
///
/// The source is an object, not a flat array: <c>indices</c> (unambiguous rows)
/// and <c>ambiguous</c> (scrip keys the scrip master maps to more than one
/// index — one token shared by two NSE indices). Both are checked here.
/// </summary>
public class IndexFeedMapGenerationTests
{
    private sealed record SourceRow(string ScripKey, string FeedSegment, string FeedSymbol);
    private sealed record AmbiguousRow(string ScripKey, IReadOnlyList<string> Candidates);

    private static JsonDocument OpenSource()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "spec", "index-feed-map.json");
        return JsonDocument.Parse(File.ReadAllText(path));
    }

    private static IReadOnlyList<SourceRow> ReadIndices()
    {
        using var doc = OpenSource();
        var rows = new List<SourceRow>();
        foreach (var element in doc.RootElement.GetProperty("indices").EnumerateArray())
        {
            rows.Add(new SourceRow(
                element.GetProperty("scripKey").GetString()!,
                element.GetProperty("feedSegment").GetString()!,
                element.GetProperty("feedSymbol").GetString()!));
        }
        return rows;
    }

    private static IReadOnlyList<AmbiguousRow> ReadAmbiguous()
    {
        using var doc = OpenSource();
        var rows = new List<AmbiguousRow>();
        foreach (var element in doc.RootElement.GetProperty("ambiguous").EnumerateArray())
        {
            var candidates = element.GetProperty("candidates").EnumerateArray().Select(c => c.GetString()!).ToList();
            rows.Add(new AmbiguousRow(element.GetProperty("scripKey").GetString()!, candidates));
        }
        return rows;
    }

    [Fact]
    public void The_generated_table_has_exactly_the_unambiguous_rows_the_source_declares()
    {
        var source = ReadIndices();
        Assert.Equal(source.Count, IndexFeedMap.Table.Count);

        foreach (var row in source)
        {
            Assert.True(IndexFeedMap.Table.TryGetValue(row.ScripKey, out var entry),
                $"IndexFeedMap.Table is missing \"{row.ScripKey}\", which spec/index-feed-map.json declares.");
            Assert.Equal(row.FeedSegment, entry.Segment);
            Assert.Equal(row.FeedSymbol, entry.Symbol);
        }
    }

    [Fact]
    public void The_generated_table_has_no_rows_the_source_does_not_declare()
    {
        var sourceKeys = ReadIndices().Select(r => r.ScripKey).ToHashSet(StringComparer.Ordinal);
        var extra = IndexFeedMap.Table.Keys.Where(k => !sourceKeys.Contains(k)).ToList();
        Assert.True(extra.Count == 0, $"IndexFeedMap.Table has rows spec/index-feed-map.json does not declare: {string.Join(", ", extra)}");
    }

    [Fact]
    public void The_generated_ambiguous_set_matches_the_source_exactly()
    {
        var source = ReadAmbiguous();
        Assert.Equal(source.Count, IndexFeedMap.Ambiguous.Count);

        foreach (var row in source)
        {
            Assert.True(IndexFeedMap.Ambiguous.TryGetValue(row.ScripKey, out var candidates),
                $"IndexFeedMap.Ambiguous is missing \"{row.ScripKey}\".");
            Assert.Equal(row.Candidates.OrderBy(c => c, StringComparer.Ordinal), candidates.OrderBy(c => c, StringComparer.Ordinal));
        }
    }

    [Fact]
    public void No_scrip_key_appears_in_both_the_unambiguous_table_and_the_ambiguous_set()
    {
        var overlap = IndexFeedMap.Table.Keys.Intersect(IndexFeedMap.Ambiguous.Keys, StringComparer.Ordinal).ToList();
        Assert.True(overlap.Count == 0, $"appears in both: {string.Join(", ", overlap)}");
    }

    [Fact]
    public void The_source_has_81_unambiguous_indices_and_6_ambiguous_keys()
    {
        Assert.Equal(81, ReadIndices().Count);
        Assert.Equal(6, ReadAmbiguous().Count);
    }
}
