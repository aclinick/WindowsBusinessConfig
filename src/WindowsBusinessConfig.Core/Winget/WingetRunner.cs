using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WindowsBusinessConfig.Core.Winget;

/// <summary>
/// Thin wrapper around <c>winget.exe configure</c>. Owns process
/// invocation and returns a structured result. Each method runs one
/// of the subcommands documented at
/// https://learn.microsoft.com/windows/package-manager/configuration/.
/// </summary>
public sealed class WingetRunner
{
    private readonly string wingetPath;

    public WingetRunner(string? wingetPath = null)
    {
        this.wingetPath = wingetPath ?? ResolveWinget()
            ?? throw new InvalidOperationException(
                "winget.exe not found. Install App Installer from the Microsoft Store.");
    }

    public Task<WingetResult> ValidateAsync(string configPath, CancellationToken ct = default)
        => RunAsync(new[] { "configure", "validate", "--file", configPath, "--accept-configuration-agreements", "--disable-interactivity", "--nowarn" }, ct);

    public Task<WingetResult> TestAsync(string configPath, CancellationToken ct = default)
        => RunAsync(new[] { "configure", "test", "--file", configPath, "--accept-configuration-agreements", "--disable-interactivity", "--nowarn" }, ct);

    public Task<WingetResult> ApplyAsync(string configPath, CancellationToken ct = default)
        => RunAsync(new[] { "configure", "--file", configPath, "--accept-configuration-agreements", "--disable-interactivity", "--nowarn" }, ct);

    public Task<WingetResult> VersionAsync(CancellationToken ct = default)
        => RunAsync(new[] { "--version" }, ct);

    private async Task<WingetResult> RunAsync(IReadOnlyList<string> args, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = wingetPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (var a in args)
        {
            psi.ArgumentList.Add(a);
        }

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException($"Failed to start {wingetPath}.");

        var stdout = new StringBuilder();
        var stderr = new StringBuilder();

        proc.OutputDataReceived += (_, e) => { if (e.Data is not null) stdout.AppendLine(e.Data); };
        proc.ErrorDataReceived  += (_, e) => { if (e.Data is not null) stderr.AppendLine(e.Data); };
        proc.BeginOutputReadLine();
        proc.BeginErrorReadLine();

        try
        {
            await proc.WaitForExitAsync(ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            if (!proc.HasExited)
            {
                try { proc.Kill(entireProcessTree: true); } catch { /* best effort */ }
                try { await proc.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false); } catch { /* best effort */ }
            }
            throw;
        }

        // WaitForExitAsync returns when the process exits, but the async
        // event pump for stdout/stderr can still be draining. WaitForExit()
        // with no timeout flushes the buffers synchronously and is the
        // documented way to guarantee all OutputDataReceived events have
        // fired before we read the StringBuilder.
        proc.WaitForExit();

        return new WingetResult(
            ExitCode: proc.ExitCode,
            StdOut:   stdout.ToString(),
            StdErr:   stderr.ToString());
    }

    public static string? ResolveWinget()
    {
        var fromPath = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator);
        if (fromPath is not null)
        {
            foreach (var dir in fromPath)
            {
                if (string.IsNullOrWhiteSpace(dir)) continue;
                try
                {
                    var candidate = Path.Combine(dir, "winget.exe");
                    if (File.Exists(candidate)) return candidate;
                }
                catch (ArgumentException)
                {
                    // PATH entry contains invalid characters; skip it.
                }
            }
        }

        var local = Environment.GetEnvironmentVariable("LOCALAPPDATA");
        if (local is not null)
        {
            var fallback = Path.Combine(local, "Microsoft", "WindowsApps", "winget.exe");
            if (File.Exists(fallback)) return fallback;
        }

        return null;
    }
}

public sealed record WingetResult(int ExitCode, string StdOut, string StdErr)
{
    public bool Success => ExitCode == 0;
}
