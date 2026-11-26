using System.Text.RegularExpressions;

namespace WinTerminalTools.Core;

/// <summary>
/// Arruma a saída de um comando para o console: ignora linhas em branco no início e no fim e
/// junta várias linhas em branco seguidas numa só. Use uma instância por fluxo (stdout ou stderr).
/// </summary>
public sealed partial class OutputLineFilter
{
    private readonly Action<string> _write;
    private bool _started;
    private bool _pendingBlank;

    public OutputLineFilter(Action<string> write) => _write = write;

    // Animação do winget com a saída redirecionada: "-", "\", "|", "/" sozinhos e barras "███▒▒▒  1.2 MB / 3 MB".
    [GeneratedRegex(@"^\s*[-\\|/]\s*$")]
    private static partial Regex SpinnerRegex();

    [GeneratedRegex(@"^\s*[█▒]+\s+(\d+\s*%|[\d.,]+\s*[KMG]B\s*/\s*[\d.,]+\s*[KMG]B)\s*$")]
    private static partial Regex ProgressBarRegex();

    /// <summary>Linha que é só animação de progresso (não informa nada depois que o comando termina).</summary>
    public static bool IsProgressNoise(string line) => SpinnerRegex().IsMatch(line) || ProgressBarRegex().IsMatch(line);

    public void Push(string line)
    {
        if (IsProgressNoise(line))
            return;

        if (string.IsNullOrWhiteSpace(line))
        {
            // Só vira linha em branco de verdade se vier texto depois (assim o fim fica limpo).
            _pendingBlank = _started;
            return;
        }

        if (_pendingBlank)
        {
            _write("");
            _pendingBlank = false;
        }

        _started = true;
        _write(line);
    }
}
