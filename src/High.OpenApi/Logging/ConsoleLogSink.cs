// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Logging;

/// <summary>Default sink: errors and warnings to stderr, info and debug to stdout.</summary>
public sealed class ConsoleLogSink : ILogSink
{
    public static readonly ConsoleLogSink Instance = new();

    public void Error(string message, object? detail = null) => Write(Console.Error, message, detail);
    public void Warn(string message, object? detail = null) => Write(Console.Error, message, detail);
    public void Info(string message, object? detail = null) => Write(Console.Out, message, detail);
    public void Debug(string message, object? detail = null) => Write(Console.Out, message, detail);

    private static void Write(TextWriter writer, string message, object? detail)
    {
        if (detail is null) writer.WriteLine(message);
        else writer.WriteLine($"{message} {detail}");
    }
}
