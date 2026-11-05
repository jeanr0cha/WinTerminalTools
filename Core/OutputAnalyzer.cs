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
}
