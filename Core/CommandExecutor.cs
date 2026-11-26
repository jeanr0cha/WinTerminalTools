using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
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
        var encoding = spec.Utf8Output ? new UTF8Encoding(false) : OemEncoding.Value;

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

        if (spec.WorkingDirectory != null)
        {
            processStartInfo.WorkingDirectory = spec.WorkingDirectory;
        }

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
            // Sem isto a tela mostraria só "código -1", sem dizer o motivo.
            var message = ex is Win32Exception { NativeErrorCode: ErrorFileNotFound }
                ? $"Erro ao iniciar processo: o programa '{spec.FileName}' não foi encontrado neste computador."
                : $"Erro ao iniciar processo: {ex.Message}";
            errorBuilder.AppendLine(message);
            onErrorLine?.Invoke(message);

            var hint = MissingProgramHint(spec.FileName);
            if (hint != null && ex is Win32Exception)
            {
                errorBuilder.AppendLine(hint);
                onErrorLine?.Invoke(hint);
            }

            return (outputBuilder.ToString().TrimEnd(), errorBuilder.ToString().TrimEnd(), -1);
        }

        return (outputBuilder.ToString().TrimEnd(), errorBuilder.ToString().TrimEnd(), process.ExitCode);
    }

    private const int ErrorFileNotFound = 2;

    /// <summary>Como instalar programas que não vêm em todo Windows.</summary>
    public static string? MissingProgramHint(string fileName) =>
        Path.GetFileNameWithoutExtension(fileName).ToLowerInvariant() switch
        {
            "winget" => "Dica: o winget vem no 'Instalador de Aplicativo' (App Installer). Instale ou atualize pela " +
                        "Microsoft Store: https://apps.microsoft.com/detail/9NBLGGH4NNS1",
            _ => null
        };
}
