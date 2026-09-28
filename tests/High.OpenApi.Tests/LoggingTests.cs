// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Text.RegularExpressions;
using High.OpenApi.Logging;
using Xunit;

namespace High.OpenApi.Tests;

file sealed class CapturingSink : ILogSink
{
    public List<(string Level, string Message, object? Detail)> Lines { get; } = new();
    public void Error(string message, object? detail = null) => Lines.Add(("error", message, detail));
    public void Warn(string message, object? detail = null) => Lines.Add(("warn", message, detail));
    public void Info(string message, object? detail = null) => Lines.Add(("info", message, detail));
    public void Debug(string message, object? detail = null) => Lines.Add(("debug", message, detail));
}

public class LoggingTests
{
    [Fact]
    public void Silent_prints_nothing()
    {
        var sink = new CapturingSink();
        var logger = new Logger(LogLevel.Silent, sink);
        logger.Error("boom");
        logger.Warn("careful");
        logger.Info("hello");
        logger.Debug("detail");
        Assert.Empty(sink.Lines);
    }

    [Fact]
    public void Each_level_prints_itself_and_everything_more_severe()
    {
        var sink = new CapturingSink();
        var logger = new Logger(LogLevel.Warn, sink);
        logger.Error("e");
        logger.Warn("w");
        logger.Info("i");
        logger.Debug("d");
        Assert.Equal(new[] { "error", "warn" }, sink.Lines.Select(l => l.Level));
    }

    [Fact]
    public void Lines_carry_an_ISO8601_timestamp_and_the_padded_level()
    {
        var sink = new CapturingSink();
        var logger = new Logger(LogLevel.Debug, sink);
        logger.Debug("hello");

        var message = sink.Lines.Single().Message;
        Assert.Matches(@"^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{3}Z DEBUG hello$", message);
    }

    [Fact]
    public void Enabled_reports_whether_a_level_would_print()
    {
        var logger = new Logger(LogLevel.Warn);
        Assert.True(logger.Enabled(LogLevel.Error));
        Assert.True(logger.Enabled(LogLevel.Warn));
        Assert.False(logger.Enabled(LogLevel.Info));
        Assert.False(logger.Enabled(LogLevel.Debug));
    }
}

public class RedactionTests
{
    [Theory]
    [InlineData("https://api.example.com/v1/auth/generate-access-token?clientId=C1&tOtp=123456")]
    [InlineData("https://api.example.com/v1/x?apiKey=SECRET")]
    [InlineData("https://api.example.com/v1/x?accessToken=SECRET")]
    [InlineData("https://api.example.com/v1/x?tokenId=SECRET")]
    [InlineData("https://api.example.com/v1/x?stepToken=SECRET")]
    public void RedactUrl_masks_every_sensitive_query_parameter(string url)
    {
        var redacted = Redaction.RedactUrl(url);
        Assert.DoesNotContain("SECRET", redacted);
        Assert.DoesNotContain("123456", redacted);
    }

    [Fact]
    public void RedactUrl_leaves_a_URL_with_no_sensitive_params_unchanged()
    {
        const string url = "https://api.example.com/v1/orders/list?page=2";
        Assert.Equal(url, Redaction.RedactUrl(url));
    }

    [Fact]
    public void RedactUrl_returns_an_unparseable_string_as_is_rather_than_throwing()
    {
        const string notAUrl = "not a url at all";
        Assert.Equal(notAUrl, Redaction.RedactUrl(notAUrl));
    }

    [Fact]
    public void RedactBody_masks_sensitive_keys_at_any_nesting_depth()
    {
        var body = new { totp = "123456", nested = new { password = "hunter2", safe = "visible" } };
        var redacted = Redaction.RedactBody(body)!.ToString()!;
        Assert.DoesNotContain("123456", redacted);
        Assert.DoesNotContain("hunter2", redacted);
        Assert.Contains("visible", redacted);
        Assert.Contains("REDACTED", redacted);
    }

    [Fact]
    public void RedactBody_replaces_an_oversized_payload_with_a_note()
    {
        var big = new { data = new string('x', 5000) };
        var redacted = Redaction.RedactBody(big)?.ToString();
        Assert.Contains("truncated", redacted);
    }
}
