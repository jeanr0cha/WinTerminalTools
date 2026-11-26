namespace WinTerminalTools.Core;

public static class OutputAnalyzer
{
    private static readonly string[] ElevationMarkers =
    {
        "requer elevação",
        "requires elevation",
        "elevação",
        "elevated",
        "administrador",
        "administrator",
        "acesso negado",
        "access is denied",
        "0x80070005"
    };

    /// <summary>
    /// Indica se a saída de um comando que falhou sugere falta de privilégio de administrador.
    /// Usa frases específicas (não palavras genéricas como "requer").
    /// </summary>
    public static bool IndicatesElevationNeeded(string? output, string? error)
    {
        var text = $"{output} {error}";
        return ElevationMarkers.Any(m => text.Contains(m, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Explica em português os códigos de saída mais comuns do winget (HRESULTs 0x8A15xxxx),
    /// que aparecem como números negativos enormes. Null quando o código não é conhecido.
    /// </summary>
    public static string? WingetExitHint(int exitCode) => unchecked((uint)exitCode) switch
    {
        0x8A150014 => "Pacote não encontrado no winget. O ID pode ter mudado: use a busca no repositório e copie o ID da coluna \"Id\".",
        0x8A150011 => "Mais de um pacote combina com o que foi digitado. Use o ID exato (coluna \"Id\" da busca).",
        0x8A15002B => "Nenhuma atualização disponível para este programa (já está na versão mais recente).",
        0x8A150061 => "Este programa já está instalado. Para trocar de versão, use \"Atualizar\".",
        _ => null
    };
}
