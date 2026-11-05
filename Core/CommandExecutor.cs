using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace WinTerminalTools.Core;

public static class CommandExecutor
{
    private static readonly Lazy<Encoding> OemEncoding = new(ResolveOemEncoding);

    private static Encoding ResolveOemEncoding()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.UTF8;
        }
    }

    /// <summary>
    /// Executa o comando diretamente (sem passar por cmd /c), com cada argumento
    /// enviado via ArgumentList, o que elimina injeção por metacaracteres do shell.
    /// </summary>
    public static async Task<(string output, string error, int exitCode)> ExecuteAsync(
        CommandSpec spec,
        Action<string>? onOutputLine = null,
        Action<string>? onErrorLine = null,
        CancellationToken cancellationToken = default)
    {
        var encoding = OemEncoding.Value;

        var processStartInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = encoding,
            StandardErrorEncoding = encoding,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (var arg in spec.Arguments)
        {
            processStartInfo.ArgumentList.Add(arg);
        }

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
}
