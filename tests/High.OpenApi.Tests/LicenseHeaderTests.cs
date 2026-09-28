// Copyright (c) 2026 Truestock
// SPDX-License-Identifier: MIT

using Xunit;

namespace High.OpenApi.Tests;

public class LicenseHeaderTests
{
    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && dir.GetFiles("*.sln").Length == 0) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("Could not locate the repo root (no .sln found above the test output directory).");
    }

    private static IEnumerable<string> SourceFiles(string root, string relativeDir, string pattern)
    {
        var dir = Path.Combine(root, relativeDir);
        if (!Directory.Exists(dir)) yield break;

        foreach (var file in Directory.EnumerateFiles(dir, pattern, SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            {
                continue;
            }
            yield return file;
        }
    }

    [Fact]
    public void Every_hand_written_source_file_carries_the_SPDX_header()
    {
        var root = RepoRoot();
        var files = new[] { "src", "tests", "examples" }
            .SelectMany(d => SourceFiles(root, d, "*.cs").Concat(SourceFiles(root, d, "*.csproj")))
            .ToList();

        Assert.True(files.Count > 0, "expected to find hand-written source files");

        var missing = files
            .Where(file =>
            {
                var head = File.ReadAllText(file);
                head = head.Length > 200 ? head[..200] : head;
                return !head.Contains("SPDX-License-Identifier: MIT") || !head.Contains("Copyright (c) 2026 Truestock");
            })
            .ToList();

        Assert.True(missing.Count == 0, $"missing licence header:\n{string.Join('\n', missing)}");
    }

    [Fact]
    public void The_LICENSE_file_is_MIT_and_names_the_copyright_holder()
    {
        var licence = File.ReadAllText(Path.Combine(RepoRoot(), "LICENSE"));
        Assert.Contains("MIT License", licence);
        Assert.Contains("Copyright (c) 2026 Truestock", licence);
        Assert.Contains("WITHOUT WARRANTY OF ANY KIND", licence);
    }

    [Fact]
    public void The_csproj_declares_MIT()
    {
        var csproj = File.ReadAllText(Path.Combine(RepoRoot(), "src", "High.OpenApi", "High.OpenApi.csproj"));
        Assert.Contains("<PackageLicenseExpression>MIT</PackageLicenseExpression>", csproj);
    }
}
