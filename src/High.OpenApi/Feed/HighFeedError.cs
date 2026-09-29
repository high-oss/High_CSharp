// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Feed;

/// <summary>
/// Every non-auth failure the datafeed client raises: an unrecognised or
/// unsupported scrip key, a subscription that would exceed a server-declared
/// limit, an unparsable wire value, or a control request the server refused.
/// Never carries a credential — safe to print in full.
/// </summary>
public class HighFeedError : Exception
{
    public HighFeedError(string message) : base(message)
    {
    }

    public HighFeedError(string message, Exception innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Named cases for a <c>"stat":"NotOk"</c> auth acknowledgement, so a caller
/// can act on the ones that matter without string-matching <see cref="HighFeedAuthError.ServerMessage"/>.
///
/// Only <see cref="MalformedRequest"/> (<c>stCode 11002</c>) is attested by the
/// vendor apiDoc. The gateway's exact code for a missing data plan is not yet
/// pinned down (see the datafeed socket plan's Phase 0), so
/// <see cref="MissingDataPlan"/> and <see cref="InvalidToken"/> are classified
/// from the acknowledgement's own message text; an ack that matches neither
/// pattern comes through as <see cref="Other"/> with <see cref="HighFeedAuthError.StCode"/>
/// and <see cref="HighFeedAuthError.ServerMessage"/> intact rather than being
/// flattened away.
/// </summary>
public enum FeedAuthFailureReason
{
    MissingDataPlan,
    InvalidToken,
    MalformedRequest,
    Other,
}

/// <summary>
/// The auth frame's <c>"stat":"NotOk"</c> acknowledgement, raised instead of a
/// bare socket close so the caller can tell a missing data plan from an
/// invalid token from a malformed request. Never retried and never followed
/// by a reconnect attempt — see <see cref="HighFeed"/>'s remarks.
/// </summary>
public sealed class HighFeedAuthError : HighFeedError
{
    /// <summary>The gateway's own status code from the <c>cn</c> acknowledgement (e.g. 11001, 11002).</summary>
    public int StCode { get; }

    /// <summary>The gateway's own message, verbatim.</summary>
    public string ServerMessage { get; }

    public FeedAuthFailureReason Reason { get; }

    public HighFeedAuthError(int stCode, string serverMessage, FeedAuthFailureReason reason)
        : base($"HIGH datafeed authentication failed ({stCode}): {serverMessage}")
    {
        StCode = stCode;
        ServerMessage = serverMessage;
        Reason = reason;
    }

    /// <summary>
    /// Best-effort classification from the ack's own message, pending a fixed
    /// stCode-to-reason mapping from the gateway (see the type's remarks).
    /// </summary>
    internal static FeedAuthFailureReason ClassifyReason(int stCode, string serverMessage)
    {
        if (stCode == 11002) return FeedAuthFailureReason.MalformedRequest;

        var lower = serverMessage.ToLowerInvariant();
        if (lower.Contains("plan") || lower.Contains("subscription")) return FeedAuthFailureReason.MissingDataPlan;
        if (lower.Contains("token") || lower.Contains("session") || lower.Contains("expired") || lower.Contains("invalid"))
        {
            return FeedAuthFailureReason.InvalidToken;
        }

        return FeedAuthFailureReason.Other;
    }
}
