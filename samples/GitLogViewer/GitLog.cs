using System.Diagnostics;
using Sharp.Tui.Runtime;

namespace GitLogViewer;

// The first Cmd in the whole codebase that does real async work — an external process — rather
// than Cmd.None/Cmd.Quit or a Task.Delay. Everything here is ordinary async I/O; nothing about
// Cmd<TMsg> had to change to support it.
internal static class GitLog
{
    // Unit Separator, not '|': a commit subject can legitimately contain '|', but essentially
    // never a raw 0x1F control byte.
    private const char FieldSeparator = '\x1f';
    private const int MaxCommits = 500;

    public static Cmd<Msg> Load(string repositoryPath) => async ct =>
    {
        Process? process = null;

        try
        {
            var startInfo = new ProcessStartInfo("git")
            {
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            startInfo.ArgumentList.Add("-C");
            startInfo.ArgumentList.Add(repositoryPath);
            startInfo.ArgumentList.Add("log");
            startInfo.ArgumentList.Add($"--pretty=format:%h{FieldSeparator}%an{FieldSeparator}%ad{FieldSeparator}%s");
            startInfo.ArgumentList.Add("--date=short");
            startInfo.ArgumentList.Add("-n");
            startInfo.ArgumentList.Add(MaxCommits.ToString());

            process = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start git.");

            // Best-effort: if the app quits while git is still running, don't leave it behind —
            // same "clean shutdown" reasoning as everywhere else raw mode/the terminal gets
            // restored on exit.
            using var killOnCancel = ct.Register(static state =>
            {
                var p = (Process)state!;
                try
                {
                    if (!p.HasExited)
                        p.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Already exited, or couldn't be killed — nothing more to do here.
                }
            }, process);

            var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
            var stderrTask = process.StandardError.ReadToEndAsync(ct);
            await process.WaitForExitAsync(ct).ConfigureAwait(false);
            var stdout = await stdoutTask.ConfigureAwait(false);
            var stderr = await stderrTask.ConfigureAwait(false);

            if (process.ExitCode != 0)
            {
                var message = string.IsNullOrWhiteSpace(stderr)
                    ? $"git exited with code {process.ExitCode}."
                    : stderr.Trim();
                return new LoadFailed(message);
            }

            return new CommitsLoaded(ParseCommits(stdout));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw; // a normal shutdown, not a load failure — let RuntimeLoop's own handling see it
        }
        catch (Exception ex)
        {
            // Covers git missing from PATH (Win32Exception/FileNotFoundException, depending on
            // platform) as well as anything else going wrong starting or reading the process.
            return new LoadFailed($"Could not run git: {ex.Message}");
        }
        finally
        {
            process?.Dispose();
        }
    };

    private static IReadOnlyList<Commit> ParseCommits(string stdout)
    {
        var commits = new List<Commit>();

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            var fields = line.Split(FieldSeparator);
            if (fields.Length != 4)
                continue; // a malformed line is skipped rather than crashing the whole load

            commits.Add(new Commit(fields[0], fields[1], fields[2], fields[3]));
        }

        return commits;
    }
}
