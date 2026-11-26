namespace WinTerminalTools.Core;

/// <summary>Aba da interface onde a seção aparece.</summary>
public enum CommandTab
{
    Network,
    Apps,
    Files,
    System
}

/// <summary>Nível de risco: define a cor do botão e se é preciso confirmar antes de executar.</summary>
public enum CommandRisk
{
    /// <summary>Só lê informações; executa sem confirmação.</summary>
    Safe,
    /// <summary>Demora ou consome muitos recursos, mas não altera nada de forma perigosa.</summary>
    Caution,
    /// <summary>Altera o sistema, derruba a conexão ou desliga o PC.</summary>
    Dangerous
}

/// <summary>Card da interface que agrupa comandos de uma aba.</summary>
public sealed record CommandSection(string Id, CommandTab Tab, string Title, string? Subtitle = null);

/// <summary>
/// Um botão da interface: o que mostrar, o que executar e quais cuidados tomar antes.
/// Comandos sem entrada do usuário ficam todos no <see cref="CommandCatalog.Definitions"/>.
/// </summary>
public sealed record CommandDefinition
{
    public required string Id { get; init; }
    public required string SectionId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>Processos executados em sequência (normalmente um só).</summary>
    public required IReadOnlyList<CommandSpec> Steps { get; init; }

    public CommandRisk Risk { get; init; } = CommandRisk.Safe;
    public bool RequiresAdmin { get; init; }

    /// <summary>Texto do aviso exibido antes de executar. Obrigatório quando o risco não é Safe.</summary>
    public string? Confirmation { get; init; }
    public string? ConfirmationTitle { get; init; }

    /// <summary>Em sequências, continua para o próximo passo mesmo se um falhar.</summary>
    public bool ContinueOnFailure { get; init; }

    /// <summary>Palavras extras para a busca (além de título, descrição e comando).</summary>
    public string Keywords { get; init; } = "";

    /// <summary>Texto usado pelo filtro de busca.</summary>
    public string SearchText =>
        $"{Title} {Description} {Keywords} {string.Join(' ', Steps.Select(s => s.Display))}";
}
