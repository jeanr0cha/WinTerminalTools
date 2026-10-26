using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WinTerminalTools.Core;

public static class CommandExecutor
{
    public static async Task<(string output, string error, int exitCode)> ExecuteAsync(
        string command,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default)
    {
        Encoding encoding;
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            encoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            encoding = Encoding.UTF8;
        }

        var processStartInfo = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/c {command}",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                outputBuilder.AppendLine(e.Data);
                onOutputLine?.Invoke(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorBuilder.AppendLine(e.Data);
                onErrorLine?.Invoke(e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var reg = cancellationToken.Register(() =>
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                    // Processo pode já ter encerrado
                }
            });

            await process.WaitForExitAsync(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            errorBuilder.AppendLine("\n[Comando cancelado pelo usuário]");
            return (outputBuilder.ToString().TrimEnd(), errorBuilder.ToString().TrimEnd(), -1);
        }
        catch (Exception ex)
        {
            errorBuilder.AppendLine($"\nErro ao iniciar processo: {ex.Message}");
            return (outputBuilder.ToString().TrimEnd(), errorBuilder.ToString().TrimEnd(), -1);
        }

        return (outputBuilder.ToString().TrimEnd(), errorBuilder.ToString().TrimEnd(), process.ExitCode);
    }

    public static (string output, string error, int exitCode) Execute(string command, int timeoutMs = 120000)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        try
        {
            return ExecuteAsync(command, null, null, cts.Token).GetAwaiter().GetResult();
        }
        catch (OperationCanceledException)
        {
            return ("", $"O comando excedeu o tempo limite ({timeoutMs / 1000}s) e foi cancelado.", -1);
        }
    }
}
