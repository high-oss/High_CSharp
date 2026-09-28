// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Logging;

/// <summary>Parsing helpers for <see cref="LogLevel"/>, e.g. from <c>HIGH_LOG_LEVEL</c>.</summary>
public static class LogLevels
{
    public const string ValidNames = "silent, error, warn, info, debug";

    /// <summary>Parses the lower-case name exactly as the option and the env var document it.</summary>
    public static bool TryParse(string value, out LogLevel level)
    {
        switch (value)
        {
            case "silent": level = LogLevel.Silent; return true;
            case "error": level = LogLevel.Error; return true;
            case "warn": level = LogLevel.Warn; return true;
            case "info": level = LogLevel.Info; return true;
            case "debug": level = LogLevel.Debug; return true;
            default: level = default; return false;
        }
    }
}
