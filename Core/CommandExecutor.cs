using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;

namespace WinTerminalTools.Core;

public static class CommandExecutor
{
    private static readonly Lazy<Encoding> OemEncoding = new(ResolveOemEncoding);
    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    /// <summary>Quanto esperar a saída terminar de chegar depois que o processo fecha (ou é cancelado).</summary>
    private static readonly TimeSpan DrainTimeout = TimeSpan.FromSeconds(5);

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
    /// Converte os bytes de uma linha em texto. Cada programa do Windows escreve numa codificação:
    /// ipconfig/systeminfo usam a página OEM (850 no Brasil) e netsh/winget/PowerShell podem usar UTF-8.
    /// Uma linha que é UTF-8 válido com acentos quase nunca é texto OEM por acaso, então: UTF-8 se for
    /// válido, senão OEM.
    /// </summary>
    public static string DecodeLine(byte[] bytes, Encoding oem)
    {
        if (Array.TrueForAll(bytes, b => b < 0x80))
            return Encoding.ASCII.GetString(bytes);

        try
        {
            return StrictUtf8.GetString(bytes).TrimStart('﻿');
        }
        catch (DecoderFallbackException)
        {
            return oem.GetString(bytes);
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
        var processStartInfo = new ProcessStartInfo
        {
            FileName = spec.FileName,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
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

        Func<byte[], string> decode = spec.Utf8Output
            ? bytes => new UTF8Encoding(false).GetString(bytes).TrimStart('﻿')
            : bytes => DecodeLine(bytes, OemEncoding.Value);

        using var process = new Process { StartInfo = processStartInfo };
        var outputBuilder = new StringBuilder();
        var errorBuilder = new StringBuilder();
        Task leituras = Task.CompletedTask;

        try
        {
            process.Start();

            leituras = Task.WhenAll(
                ReadLinesAsync(process.StandardOutput.BaseStream, decode, line =>
                {
                    outputBuilder.AppendLine(line);
                    onOutputLine?.Invoke(line);
                }),
                ReadLinesAsync(process.StandardError.BaseStream, decode, line =>
                {
                    errorBuilder.AppendLine(line);
                    onErrorLine?.Invoke(line);
                }));

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
            // Um programa filho que herdou a saída pode segurá-la aberta; não espera para sempre.
            await Task.WhenAny(leituras, Task.Delay(DrainTimeout));
        }
        catch (OperationCanceledException)
        {
            await Task.WhenAny(leituras, Task.Delay(DrainTimeout));
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

    /// <summary>
    /// Lê o fluxo em bytes e entrega linha a linha. Quebra em \n, \r\n e também em \r sozinho
    /// (usado por barras de progresso), como o BeginOutputReadLine fazia.
    /// </summary>
    private static async Task ReadLinesAsync(Stream stream, Func<byte[], string> decode, Action<string> onLine)
    {
        var buffer = new byte[4096];
        var line = new MemoryStream();
        var lastWasCr = false;

        void Emit()
        {
            onLine(decode(line.ToArray()));
            line.SetLength(0);
        }

        try
        {
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    var b = buffer[i];
                    if (b == (byte)'\n')
                    {
                        if (!lastWasCr)
                            Emit();
                        lastWasCr = false;
                    }
                    else if (b == (byte)'\r')
                    {
                        Emit();
                        lastWasCr = true;
                    }
                    else
                    {
                        line.WriteByte(b);
                        lastWasCr = false;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            // Saída fechada à força (processo cancelado).
        }

        if (line.Length > 0)
            Emit();
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
