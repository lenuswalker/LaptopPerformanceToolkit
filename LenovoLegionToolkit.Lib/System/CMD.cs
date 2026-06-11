using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LenovoLegionToolkit.Lib.Utils;

namespace LenovoLegionToolkit.Lib.System;

public static class CMD
{
    public static async Task<(int, string)> RunAsync(string file, string arguments, bool createNoWindow = true, bool waitForExit = true, Dictionary<string, string?>? environment = null, CancellationToken token = default)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Running... [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]");

        var cmd = new Process();
        cmd.StartInfo.UseShellExecute = false;
        cmd.StartInfo.CreateNoWindow = createNoWindow;
        cmd.StartInfo.RedirectStandardOutput = createNoWindow;
        cmd.StartInfo.RedirectStandardError = createNoWindow;
        cmd.StartInfo.WindowStyle = createNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        cmd.StartInfo.FileName = file;
        if (!string.IsNullOrWhiteSpace(arguments))
            cmd.StartInfo.Arguments = arguments;

        if (environment is not null)
        {
            foreach (var (key, value) in environment)
                cmd.StartInfo.Environment[key] = value;
        }

        cmd.Start();

        if (!waitForExit)
        {
            if (Log.Instance.IsTraceEnabled)
                Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, environment=[{(environment is null ? string.Empty : string.Join(",", environment))}]");

            return (-1, string.Empty);
        }

        await cmd.WaitForExitAsync(token).ConfigureAwait(false);

        var exitCode = cmd.ExitCode;
        var output = createNoWindow ? await cmd.StandardOutput.ReadToEndAsync(token).ConfigureAwait(false) : string.Empty;

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Ran [file={file}, argument={arguments}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, exitCode={exitCode} output={output}]");

        return (exitCode, output);
    }

    public static async Task<(int, string)> RunAsync(string file, string arguments, bool useShellExecute, bool createNoWindow = true, bool waitForExit = true, CancellationToken token = default)
    {
        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Running... [file={file}, argument={arguments}, useShellExecute={useShellExecute}, createNoWindow={createNoWindow}, waitForExit={waitForExit}]");

        // Output can only be captured without shell execute; the stream must be
        // drained while waiting or a chatty child process blocks on a full pipe.
        var redirectOutput = !useShellExecute && waitForExit;

        using var cmd = new Process();
        cmd.StartInfo.UseShellExecute = useShellExecute;
        cmd.StartInfo.CreateNoWindow = createNoWindow;
        cmd.StartInfo.RedirectStandardOutput = redirectOutput;
        cmd.StartInfo.WindowStyle = createNoWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        cmd.StartInfo.FileName = file;
        if (!string.IsNullOrWhiteSpace(arguments))
            cmd.StartInfo.Arguments = arguments;

        cmd.Start();

        if (!waitForExit)
            return (-1, string.Empty);

        var outputTask = redirectOutput ? cmd.StandardOutput.ReadToEndAsync(token) : null;
        await cmd.WaitForExitAsync(token).ConfigureAwait(false);

        var exitCode = cmd.ExitCode;
        var output = outputTask is null ? string.Empty : await outputTask.ConfigureAwait(false);

        if (Log.Instance.IsTraceEnabled)
            Log.Instance.Trace($"Ran [file={file}, argument={arguments}, useShellExecute={useShellExecute}, createNoWindow={createNoWindow}, waitForExit={waitForExit}, exitCode={exitCode} output={output}]");

        return (exitCode, output);
    }
}
