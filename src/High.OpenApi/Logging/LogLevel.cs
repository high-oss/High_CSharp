// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Logging;

/// <summary>
/// Log levels, least to most verbose. Setting a level prints that level and
/// everything more severe: <see cref="Warn"/> prints warnings and errors,
/// <see cref="Debug"/> prints everything.
/// </summary>
public enum LogLevel
{
    /// <summary>Nothing is printed. The default.</summary>
    Silent = 0,
    Error = 1,
    Warn = 2,
    Info = 3,
    Debug = 4,
}
