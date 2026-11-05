namespace WinTerminalTools.Core;

/// <summary>
/// Um processo a executar: executável + argumentos já separados (nunca uma string de shell).
/// </summary>
public sealed record CommandSpec(string FileName, IReadOnlyList<string> Arguments)
{
    // Comandos internos do cmd.exe, que não existem como executável próprio.
    private static readonly HashSet<string> CmdBuiltins = new(StringComparer.OrdinalIgnoreCase) { "dir" };

    public static CommandSpec Create(string fileName, params string[] arguments) => new(fileName, arguments);

    /// <summary>
    /// Constrói a partir de um comando estático definido no XAML (ex.: "ipconfig /all").
    /// Nunca deve receber texto digitado pelo usuário.
    /// </summary>
    public static CommandSpec FromStaticCommand(string command)
    {
        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            throw new ArgumentException("Comando vazio.", nameof(command));

        if (CmdBuiltins.Contains(parts[0]))
            return new CommandSpec("cmd.exe", new[] { "/c" }.Concat(parts).ToArray());

        return new CommandSpec(parts[0], parts[1..]);
    }

    /// <summary>Texto exibido no console (apenas informativo).</summary>
    public string Display => Arguments.Count == 0
        ? FileName
        : $"{FileName} {string.Join(' ', Arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}";
}
