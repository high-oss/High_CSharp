// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Logging;

/// <summary>
/// A logger that drops anything below its configured level. Every line carries
/// an ISO-8601 timestamp and the level, padded so lines align.
/// </summary>
public sealed class Logger
{
    private readonly LogLevel _level;
    private readonly ILogSink _sink;

    public Logger(LogLevel level, ILogSink? sink = null)
    {
        _level = level;
        _sink = sink ?? ConsoleLogSink.Instance;
    }

    /// <summary>True when this level would print. Guard expensive detail with it.</summary>
    public bool Enabled(LogLevel level) => level != LogLevel.Silent && level <= _level;

    public void Error(string message, object? detail = null)
    {
        if (Enabled(LogLevel.Error)) _sink.Error(Format(LogLevel.Error, message), detail);
    }

    public void Warn(string message, object? detail = null)
    {
        if (Enabled(LogLevel.Warn)) _sink.Warn(Format(LogLevel.Warn, message), detail);
    }

    public void Info(string message, object? detail = null)
    {
        if (Enabled(LogLevel.Info)) _sink.Info(Format(LogLevel.Info, message), detail);
    }

    public void Debug(string message, object? detail = null)
    {
        if (Enabled(LogLevel.Debug)) _sink.Debug(Format(LogLevel.Debug, message), detail);
    }

    /// <summary>`2026-09-28T17:05:12.345Z DEBUG message` — sortable, and aligned in a terminal.</summary>
    private static string Format(LogLevel level, string message)
    {
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", System.Globalization.CultureInfo.InvariantCulture);
        var label = level.ToString().ToUpperInvariant().PadRight(5);
        return $"{timestamp} {label} {message}";
    }
}
