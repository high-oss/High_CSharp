// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Feed;

/// <summary>
/// Which of the feed's three channels a subscription targets. Internal only:
/// the public surface is the dedicated per-kind methods on <see cref="HighFeed"/>
/// (<c>SubscribeQuotesAsync</c>, <c>SubscribeDepthAsync</c>, <c>SubscribeIndicesAsync</c>,
/// and their <c>Unsubscribe</c>/<c>Snapshot</c> counterparts) rather than one
/// method taking a kind — a caller says what they mean, and an index key can
/// never be accepted where only a quote or depth subscription belongs.
/// </summary>
internal enum FeedKind
{
    Quote,
    Depth,
    Index,
}

internal static class FeedKindExtensions
{
    /// <summary>The wire type prefix for this channel: <c>mw</c> market watch, <c>dp</c> depth, <c>if</c> indices.</summary>
    public static string WirePrefix(this FeedKind kind) => kind switch
    {
        FeedKind.Quote => "mw",
        FeedKind.Depth => "dp",
        FeedKind.Index => "if",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    /// <summary>A fixed channel number per kind. Channel management (pause/resume/lite) is not part of this SDK's public surface.</summary>
    public static int ChannelNumber(this FeedKind kind) => kind switch
    {
        FeedKind.Quote => 1,
        FeedKind.Depth => 2,
        FeedKind.Index => 3,
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };
}
