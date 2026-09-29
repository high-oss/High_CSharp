// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Feed;

/// <summary>
/// One delivery from <see cref="HighFeed.Events"/> — the unified
/// <c>IAsyncEnumerable</c> form. Every concrete case also carries the same
/// data through <see cref="HighFeed.OnQuote"/>/<see cref="HighFeed.OnDepth"/>/
/// <see cref="HighFeed.OnIndex"/> for callers who prefer events.
/// </summary>
public abstract record FeedEvent
{
    /// <summary>The key the caller subscribed with — never the feed's own token.</summary>
    public required string ScripKey { get; init; }

    /// <summary>
    /// The model's own property names that this particular tick changed.
    /// Ticks are deltas: most fields on the model are carried over from an
    /// earlier tick, and this is how a caller who wants to know what just
    /// moved can tell without diffing snapshots themselves.
    /// </summary>
    public required IReadOnlyList<string> ChangedFields { get; init; }
}

/// <summary>A quote (market-watch) tick, merged into a complete snapshot.</summary>
public sealed record QuoteEvent : FeedEvent
{
    public required Quote Quote { get; init; }
}

/// <summary>
/// A depth tick, merged into a complete book. See <see cref="FeedDepth.Source"/>
/// for whether this is a one-level top-of-book (split out of a market-watch
/// tick) or a five-level book from the dedicated depth feed.
/// </summary>
public sealed record DepthEvent : FeedEvent
{
    public required FeedDepth Depth { get; init; }
}

/// <summary>An index tick, merged into a complete snapshot.</summary>
public sealed record IndexEvent : FeedEvent
{
    public required IndexQuote Index { get; init; }
}
