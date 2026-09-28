// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Http;

/// <summary>Which credential an operation needs, per the spec's <c>security</c> block.</summary>
public enum AuthKind
{
    Bearer,
    ApiKey,
}
