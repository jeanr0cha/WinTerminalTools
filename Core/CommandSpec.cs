namespace WinTerminalTools.Core;

/// <summary>
/// Um processo a executar: executável + argumentos já separados (nunca uma string de shell).
/// </summary>
public sealed record CommandSpec(string FileName, IReadOnlyList<string> Arguments)
{
    /// <summary>
    /// Pasta onde o processo roda. Usar isto em vez de passar um caminho digitado como argumento
    /// para o cmd.exe, que interpretaria caracteres como &amp; e |.
    /// </summary>
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// O programa escreve em UTF-8 mesmo com a saída redirecionada (ex.: winget). Os comandos clássicos
    /// do Windows usam a página de código OEM (850 no Brasil), que é o padrão.
    /// </summary>
    public bool Utf8Output { get; init; }

    public static CommandSpec Create(string fileName, params string[] arguments) => new(fileName, arguments);

    /// <summary>Texto exibido no console (apenas informativo).</summary>
    public string Display
    {
        get
        {
            var text = Arguments.Count == 0
                ? FileName
                : $"{FileName} {string.Join(' ', Arguments.Select(a => a.Contains(' ') ? $"\"{a}\"" : a))}";
            return WorkingDirectory == null ? text : $"{text}   (em {WorkingDirectory})";
        }
    }
}
