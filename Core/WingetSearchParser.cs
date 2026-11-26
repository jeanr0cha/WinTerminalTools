namespace WinTerminalTools.Core;

/// <summary>Um pacote encontrado pelo "winget search".</summary>
public sealed record WingetPackage(string Name, string Id, string Version, string Source)
{
    /// <summary>
    /// IDs cortados pelo winget (terminam em "…") ou com caracteres estranhos não podem ser instalados
    /// com segurança pelo ID exato.
    /// </summary>
    public bool CanInstall => InputValidator.IsValidWingetId(Id);
}

/// <summary>
/// Lê a tabela do "winget search". As colunas são achadas pela posição dos títulos (vale para qualquer
/// idioma) e medidas em largura de tela: caracteres chineses/japoneses/coreanos ocupam duas colunas.
/// </summary>
public static class WingetSearchParser
{
    public static IReadOnlyList<WingetPackage> Parse(string output)
    {
        var lines = output.Replace("\r", "").Split('\n');
        var separator = Array.FindIndex(lines, l => l.Trim().Length >= 10 && l.Trim().All(c => c == '-'));
        if (separator <= 0)
            return Array.Empty<WingetPackage>();

        var header = lines[separator - 1];
        var starts = new List<int>();
        for (int i = 0; i < header.Length; i++)
        {
            if (header[i] != ' ' && (i == 0 || header[i - 1] == ' '))
                starts.Add(i);
        }

        // Name, Id, Version, [Match], Source
        if (starts.Count < 4)
            return Array.Empty<WingetPackage>();

        var packages = new List<WingetPackage>();
        foreach (var line in lines.Skip(separator + 1))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var fields = SplitByCells(line, starts);
            var package = new WingetPackage(fields[0], fields[1], fields[2], fields[^1]);
            if (package.Id.Length > 0)
                packages.Add(package);
        }

        return packages;
    }

    /// <summary>Corta a linha nas colunas, contando a largura de tela de cada caractere.</summary>
    private static string[] SplitByCells(string line, IReadOnlyList<int> starts)
    {
        var fields = new string[starts.Count];
        var current = new System.Text.StringBuilder();
        int column = 0;
        int cell = 0;

        foreach (var ch in line)
        {
            while (column + 1 < starts.Count && cell >= starts[column + 1])
            {
                fields[column++] = current.ToString().Trim();
                current.Clear();
            }

            current.Append(ch);
            cell += CellWidth(ch);
        }

        fields[column] = current.ToString().Trim();
        for (int i = column + 1; i < fields.Length; i++)
            fields[i] = "";

        return fields;
    }

    /// <summary>Largura de tela: 2 para caracteres "largos" do leste asiático, 1 para o resto.</summary>
    public static int CellWidth(char ch) => ch switch
    {
        >= 'ᄀ' and <= 'ᅟ' => 2, // Hangul Jamo
        >= '⺀' and <= '꓏' => 2, // CJK, Hiragana, Katakana, etc.
        >= '가' and <= '힣' => 2, // Hangul
        >= '豈' and <= '﫿' => 2, // CJK compatibilidade
        >= '︰' and <= '﹏' => 2,
        >= '＀' and <= '｠' => 2, // largura total
        >= '￠' and <= '￦' => 2,
        _ => 1
    };
}
