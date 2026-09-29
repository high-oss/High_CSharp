// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using System.Runtime.CompilerServices;
using System.Text;

namespace High.OpenApi.Csv;

/// <summary>
/// A minimal RFC 4180 reader: quoted fields may contain commas, embedded
/// newlines and doubled-quote escapes (<c>""</c>). The instrument list's
/// <c>name</c> column routinely contains commas, so a plain <c>Split(',')</c>
/// would silently shift every field after it — this reads character by
/// character instead of trusting the delimiter alone.
/// </summary>
internal static class CsvReader
{
    private const int BufferSize = 16 * 1024;

    /// <summary>
    /// Reads rows from <paramref name="reader"/> as they arrive, never
    /// buffering more than one read chunk plus the row in progress — safe for
    /// a multi-megabyte file streamed from an HTTP response.
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<string>> ReadRowsAsync(
        TextReader reader,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var splitter = new CsvLineSplitter();
        var buffer = new char[BufferSize];

        int read;
        while ((read = await reader.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
        {
            for (var i = 0; i < read; i++)
            {
                var row = splitter.Feed(buffer[i]);
                if (row is not null) yield return row;
            }
        }

        var last = splitter.FeedEnd();
        if (last is not null) yield return last;
    }
}

/// <summary>
/// The character-at-a-time state machine behind <see cref="CsvReader"/>. Kept
/// separate so the quote/escape state persists correctly across read-buffer
/// boundaries — a quoted field, or the escaped-quote lookahead inside one,
/// can straddle two chunks of a streamed response.
/// </summary>
internal sealed class CsvLineSplitter
{
    private readonly StringBuilder _field = new();
    private readonly List<string> _row = new();
    private bool _inQuotes;
    private bool _quotePending;
    private bool _rowStarted;

    /// <summary>Feeds one character. Returns the completed row once a line ends, otherwise <c>null</c>.</summary>
    public IReadOnlyList<string>? Feed(char ch)
    {
        if (_quotePending)
        {
            _quotePending = false;
            if (ch == '"')
            {
                // A doubled quote inside a quoted field is a literal quote.
                _field.Append('"');
                _rowStarted = true;
                return null;
            }
            // The quote just closed the field; fall through and handle ch as
            // an ordinary (unquoted) character.
            _inQuotes = false;
        }

        if (_inQuotes)
        {
            if (ch == '"') _quotePending = true;
            else _field.Append(ch);
            _rowStarted = true;
            return null;
        }

        switch (ch)
        {
            case '"':
                _inQuotes = true;
                _rowStarted = true;
                return null;
            case ',':
                _row.Add(_field.ToString());
                _field.Clear();
                _rowStarted = true;
                return null;
            case '\r':
                return null;
            case '\n':
                return CompleteRow();
            default:
                _field.Append(ch);
                _rowStarted = true;
                return null;
        }
    }

    /// <summary>Call once the input is exhausted: flushes a final row with no trailing newline. Returns <c>null</c> if nothing was pending.</summary>
    public IReadOnlyList<string>? FeedEnd()
    {
        if (!_rowStarted && _field.Length == 0 && _row.Count == 0) return null;
        return CompleteRow();
    }

    private IReadOnlyList<string> CompleteRow()
    {
        _row.Add(_field.ToString());
        var result = _row.ToArray();
        _row.Clear();
        _field.Clear();
        _rowStarted = false;
        return result;
    }
}
