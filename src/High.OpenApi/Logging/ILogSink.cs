// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

namespace High.OpenApi.Logging;

/// <summary>Anything that can receive the SDK's log lines. <see cref="ConsoleLogSink"/> satisfies it.</summary>
public interface ILogSink
{
    void Error(string message, object? detail = null);
    void Warn(string message, object? detail = null);
    void Info(string message, object? detail = null);
    void Debug(string message, object? detail = null);
}
