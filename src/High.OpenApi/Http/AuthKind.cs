// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Http;

/// <summary>Which credential an operation needs, per the spec's <c>security</c> block.</summary>
public enum AuthKind
{
    Bearer,
    ApiKey,

    /// <summary>
    /// No credential at all — <c>security: []</c> in the spec. Used by
    /// <c>instruments</c>: the manifest and the CSVs it points at are public,
    /// and attaching a bearer token to a third-party CDN would leak it.
    /// </summary>
    None,
}
