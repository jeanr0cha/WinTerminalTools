using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;
using System.Windows.Threading;
using WinTerminalTools.Core;

namespace WinTerminalTools;

public partial class MainWindow : Window
{
    private const int MaxConsoleChars = 400_000;
    private const int KeepConsoleChars = 300_000;

    private CancellationTokenSource? _currentCts;
    private bool _isRunning;
    private readonly bool _isAdmin;
    private readonly List<CheckBox> _appCheckBoxes = new();
    private readonly UserSettingsStore _settingsStore = new(UserSettingsStore.DefaultPath);
    private readonly UserSettings _settings;
    private readonly ConcurrentQueue<string> _pendingOutput = new();
    private readonly DispatcherTimer _flushTimer;

    public MainWindow()
    {
        InitializeComponent();

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _flushTimer.Tick += (_, _) => FlushOutput();

        _isAdmin = EhAdministrador();
        _settings = _settingsStore.Load();

        Title = $"WinTerminalTools v{AppInfo.Version}";
        TxtVersao.Text = $"v{AppInfo.Version}";

        ConstruirSecoesDoCatalogo();
        CarregarListaApps(avisarErro: false);
        AtualizarFavoritos();
        AtualizarHistorico();
        AplicarQuebraDeLinhas();
        CarregarIconeSeguro();
        VerificarPermissaoAdministrador();
        ExibirMensagemBoasVindas();
        FlushOutput(); // avisos gerados durante a montagem da tela (ex.: apps.json com erro)
    }

    protected override void OnClosed(EventArgs e)
    {
        try { _currentCts?.Cancel(); } catch { }
        base.OnClosed(e);
    }

    private void CarregarIconeSeguro()
    {
        try
        {
            var iconUri = new Uri("pack://application:,,,/Resources/Icone.ico");
            this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(iconUri);
        }
        catch
        {
            try
            {
                string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "Icone.ico");
                if (File.Exists(iconPath))
                {
                    this.Icon = System.Windows.Media.Imaging.BitmapFrame.Create(new Uri(iconPath));
                }
            }
            catch { }
        }
    }

    private static bool EhAdministrador()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    private void VerificarPermissaoAdministrador()
    {
        if (_isAdmin)
        {
            TxtAdminStatus.Text = "Administrador";
            TxtAdminStatus.Foreground = new SolidColorBrush(Color.FromRgb(110, 231, 183));
            AdminBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(20, 51, 36));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            AdminStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129));
            BtnReiniciarAdmin.Visibility = Visibility.Collapsed;
        }
        else
        {
            TxtAdminStatus.Text = "Usuário Padrão";
            TxtAdminStatus.Foreground = new SolidColorBrush(Color.FromRgb(253, 230, 138));
            AdminBadgeBorder.Background = new SolidColorBrush(Color.FromRgb(55, 40, 15));
            AdminBadgeBorder.BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            AdminStatusDot.Fill = new SolidColorBrush(Color.FromRgb(245, 158, 11));
            BtnReiniciarAdmin.Visibility = Visibility.Visible;
        }
    }

    private void ExibirMensagemBoasVindas()
    {
        // Sem moldura desenhada: o console quebra linhas conforme a largura e a moldura se desalinhava.
        var sb = new StringBuilder();
        sb.AppendLine($"⚡ WinTerminalTools v{AppInfo.Version}");
        sb.AppendLine("Central de Linha de Comando, Rede e Winget");
        sb.AppendLine("────────────────────────────────────");
        sb.AppendLine();
        sb.AppendLine("• Escolha um comando nas categorias ao lado para executar em tempo real.");
        sb.AppendLine("• Use a aba 'Instalação Rápida & Winget' para verificar updates ou instalar apps.");
        sb.AppendLine("• Comandos com impacto no sistema exigem confirmação com aviso de risco.");
        sb.AppendLine("• Todo comando pode ser interrompido a qualquer instante clicando em [Cancelar Execução] ou com Esc.");
        sb.AppendLine("• Botão direito num comando: favoritar. Atalhos: Ctrl+F busca, Ctrl+L limpa, Ctrl+S salva.");
        sb.AppendLine();
        sb.AppendLine("────────────────────────────────────");
        TxtConsoleOutput.Text = sb.ToString();
    }

    // ================= CARDS GERADOS A PARTIR DO CATÁLOGO =================
    private void ConstruirSecoesDoCatalogo()
    {
        var containers = new Dictionary<CommandTab, Panel>
        {
            [CommandTab.Network] = NetworkSections,
            [CommandTab.Apps] = AppsSections,
            [CommandTab.Files] = FilesSections,
            [CommandTab.System] = SystemSections
        };

        foreach (var secao in CommandCatalog.Sections)
        {
            var conteudo = new StackPanel();
            conteudo.Children.Add(new TextBlock
            {
                Text = secao.Title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 0, 0, secao.Subtitle == null ? 12 : 4)
            });

            if (secao.Subtitle != null)
            {
                conteudo.Children.Add(new TextBlock
                {
                    Text = secao.Subtitle,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    Margin = new Thickness(0, 0, 0, 12)
                });
            }

            var grade = new UniformGrid { Columns = 2 };
            foreach (var definicao in CommandCatalog.Definitions.Where(d => d.SectionId == secao.Id))
                grade.Children.Add(CriarBotaoComando(definicao));
            conteudo.Children.Add(grade);

            containers[secao.Tab].Children.Add(new Border { Style = (Style)FindResource("CardStyle"), Child = conteudo });
        }
    }

    private Button CriarBotaoComando(CommandDefinition definicao)
    {
        var corTitulo = definicao.Risk switch
        {
            CommandRisk.Dangerous => "DangerTextBrush",
            CommandRisk.Caution => "WarningBrush",
            _ => "AccentBrush"
        };

        var descricao = definicao.RequiresAdmin ? $"{definicao.Description} · 🛡️ Admin" : definicao.Description;

        var conteudo = new StackPanel();
        conteudo.Children.Add(new TextBlock
        {
            Text = definicao.Title,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource(corTitulo),
            TextWrapping = TextWrapping.Wrap
        });
        conteudo.Children.Add(new TextBlock
        {
            Text = descricao,
            FontSize = 11,
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap
        });

        var botao = new Button
        {
            Tag = definicao,
            Content = conteudo,
            Margin = new Thickness(0, 0, 8, 8),
            Padding = new Thickness(12, 8, 12, 8),
            HorizontalContentAlignment = HorizontalAlignment.Left,
            ToolTip = string.Join("\n", definicao.Steps.Select(s => "$ " + s.Display)) +
                      "\n\nBotão direito: favoritar"
        };
        botao.Click += BtnDefinicao_Click;

        // O texto do menu é decidido na hora de abrir, porque o mesmo comando aparece em duas abas.
        var itemFavorito = new MenuItem();
        itemFavorito.Click += (_, _) => AlternarFavorito(definicao);
        botao.ContextMenu = new ContextMenu { Items = { itemFavorito } };
        botao.ContextMenuOpening += (_, _) => itemFavorito.Header = _settings.Favorites.Contains(definicao.Id)
            ? "☆ Remover dos Favoritos"
            : "⭐ Adicionar aos Favoritos";
        return botao;
    }

    // ================= FAVORITOS E HISTÓRICO =================
    private void AlternarFavorito(CommandDefinition definicao)
    {
        var agoraFavorito = _settings.ToggleFavorite(definicao.Id);
        SalvarPreferencias();
        AtualizarFavoritos();
        Escrever(agoraFavorito
            ? $"\n⭐ '{definicao.Title}' adicionado aos Favoritos.\n"
            : $"\n☆ '{definicao.Title}' removido dos Favoritos.\n");
        FlushOutput();
    }

    private void AtualizarFavoritos()
    {
        FavoritosGrid.Children.Clear();
        // Ignora ids que não existem mais no catálogo (ex.: comando removido numa versão nova).
        foreach (var definicao in _settings.Favorites
                     .Select(id => CommandCatalog.Definitions.FirstOrDefault(d => d.Id == id))
                     .OfType<CommandDefinition>())
        {
            FavoritosGrid.Children.Add(CriarBotaoComando(definicao));
        }

        TxtSemFavoritos.Visibility = FavoritosGrid.Children.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void RegistrarHistorico(HistoryEntry entrada)
    {
        _settings.AddHistory(entrada);
        SalvarPreferencias();
        AtualizarHistorico();
    }

    private void AtualizarHistorico()
    {
        HistoricoLista.Children.Clear();
        foreach (var entrada in _settings.History)
        {
            var (simbolo, cor) = entrada.Cancelled ? ("⏹", "TextMutedBrush")
                : entrada.ExitCode == 0 ? ("✔", "AccentSecondaryBrush")
                : ($"⚠ {entrada.ExitCode}", "WarningBrush");

            var linha = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            linha.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            linha.Children.Add(new TextBlock
            {
                Text = entrada.When.ToString("dd/MM HH:mm", CultureInfo.CurrentCulture),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextMutedBrush"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var texto = new TextBlock
            {
                Text = entrada.Display,
                FontSize = 12,
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = entrada.Display,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(texto, 1);
            linha.Children.Add(texto);

            var status = new TextBlock
            {
                Text = simbolo,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource(cor),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(status, 2);
            linha.Children.Add(status);

            // Só comandos do catálogo podem ser repetidos, e sempre passando pela confirmação deles.
            var definicao = entrada.DefinitionId == null
                ? null
                : CommandCatalog.Definitions.FirstOrDefault(d => d.Id == entrada.DefinitionId);
            if (definicao != null)
            {
                var repetir = new Button { Content = "▶ Repetir", Padding = new Thickness(8, 2, 8, 2), FontSize = 11 };
                repetir.Click += async (_, _) => await ExecutarDefinicaoAsync(definicao);
                Grid.SetColumn(repetir, 3);
                linha.Children.Add(repetir);
            }

            HistoricoLista.Children.Add(linha);
        }

        TxtSemHistorico.Visibility = _settings.History.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void BtnLimparHistorico_Click(object sender, RoutedEventArgs e)
    {
        _settings.History.Clear();
        SalvarPreferencias();
        AtualizarHistorico();
    }

    private void SalvarPreferencias()
    {
        if (!_settingsStore.Save(_settings))
            Escrever($"\n⚠️ Não foi possível salvar as preferências em {_settingsStore.FilePath}\n");
    }

    private async void BtnDefinicao_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: CommandDefinition definicao })
            await ExecutarDefinicaoAsync(definicao);
    }

    private async Task ExecutarDefinicaoAsync(CommandDefinition definicao)
    {
        if (definicao.RequiresAdmin && !PodeSeguirSemAdmin(definicao.Title))
            return;

        if (definicao.Confirmation != null)
        {
            var icone = definicao.Risk == CommandRisk.Dangerous ? MessageBoxImage.Warning : MessageBoxImage.Information;
            var resposta = MessageBox.Show(definicao.Confirmation, definicao.ConfirmationTitle ?? "Confirmação",
                MessageBoxButton.YesNo, icone);
            if (resposta != MessageBoxResult.Yes)
                return;
        }

        await ExecutarSequenciaAsync(definicao.Steps, pararNaFalha: !definicao.ContinueOnFailure, definicao);
    }

    /// <summary>
    /// Avisa antes de rodar um comando que precisa de Administrador. Retorna true se deve executar
    /// mesmo assim; false se a pessoa cancelou ou escolheu reabrir o app elevado.
    /// </summary>
    private bool PodeSeguirSemAdmin(string comando)
    {
        if (_isAdmin)
            return true;

        var resposta = MessageBox.Show(
            $"'{comando}' precisa de permissão de Administrador.\n\n" +
            "• Sim: reabrir o WinTerminalTools como Administrador\n" +
            "• Não: executar mesmo assim (provavelmente vai falhar)\n" +
            "• Cancelar: voltar",
            "Requer Administrador",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        if (resposta == MessageBoxResult.Yes)
            ReiniciarComoAdmin();

        return resposta == MessageBoxResult.No;
    }

    // ================= IMPEDIR ROLAGEM VERTICAL INVISÍVEL NOS CAMPOS DE TEXTO =================
    private void TextBox_BloquearScrollMouse(object sender, MouseWheelEventArgs e)
    {
        // Impede que a rodinha do mouse role o texto verticalmente para fora do campo visível
        e.Handled = true;
    }

    // ================= CONTROLE DE ABAS =================
    /// <summary>Todos os painéis, na ordem das abas.</summary>
    private ScrollViewer[] TodosOsPaineis => new[] { PanelRede, PanelApps, PanelPastas, PanelSistema, PanelFavoritos };

    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // Chamado durante o InitializeComponent, antes de os painéis existirem.
        if (PanelFavoritos == null)
            return;

        foreach (var painel in TodosOsPaineis)
            painel.Visibility = AbaDe(painel) == MainTabControl.SelectedItem ? Visibility.Visible : Visibility.Collapsed;
    }

    // ================= FILTRO / BUSCA GLOBAL =================
    /// <summary>Painéis em que a busca procura (Favoritos repete comandos das outras abas e fica de fora).</summary>
    private IEnumerable<ScrollViewer> Paineis => new[] { PanelRede, PanelApps, PanelPastas, PanelSistema };

    private void TxtBuscaGlobal_TextChanged(object sender, TextChangedEventArgs e)
    {
        string texto = TxtBuscaGlobal.Text;
        bool temTexto = !string.IsNullOrEmpty(texto);
        PlaceholderBusca.Visibility = temTexto ? Visibility.Collapsed : Visibility.Visible;
        BtnLimparBusca.Visibility = temTexto ? Visibility.Visible : Visibility.Collapsed;

        string filtro = texto.Trim();
        if (filtro.Length == 0)
        {
            AplicarFiltro(null);
            return;
        }

        // Se a aba aberta não tem nada que combine, vai para a primeira aba que tenha.
        var encontrados = AplicarFiltro(filtro);
        var painelAtual = Paineis.FirstOrDefault(p => AbaDe(p) == MainTabControl.SelectedItem);
        if (painelAtual == null || encontrados[painelAtual] == 0)
        {
            var primeiro = Paineis.FirstOrDefault(p => encontrados[p] > 0);
            if (primeiro != null)
                MainTabControl.SelectedItem = AbaDe(primeiro);
        }
    }

    private TabItem AbaDe(ScrollViewer painel) =>
        painel == PanelRede ? TabRede :
        painel == PanelApps ? TabApps :
        painel == PanelPastas ? TabPastas :
        painel == PanelSistema ? TabSistema : TabFavoritos;

    private void BtnLimparBusca_Click(object sender, RoutedEventArgs e)
    {
        TxtBuscaGlobal.Clear();
        TxtBuscaGlobal.Focus();
    }

    /// <summary>
    /// Escurece os botões e caixas de seleção que não combinam com o filtro e devolve quantos
    /// combinaram em cada aba. Ignora maiúsculas e acentos ("endereco" acha "Endereço").
    /// Usa a árvore lógica, que existe mesmo para abas ainda não renderizadas.
    /// </summary>
    private Dictionary<ScrollViewer, int> AplicarFiltro(string? filtro)
    {
        var encontrados = new Dictionary<ScrollViewer, int>();
        foreach (var painel in Paineis)
        {
            int total = 0;
            foreach (var controle in EnumerarAcionaveis(painel))
            {
                bool match = filtro == null || Contem(TextoDeBusca(controle), filtro);
                controle.Opacity = match ? 1.0 : 0.25;
                if (match)
                    total++;
            }
            encontrados[painel] = total;
        }
        return encontrados;
    }

    private static bool Contem(string texto, string filtro) =>
        CultureInfo.InvariantCulture.CompareInfo.IndexOf(texto, filtro,
            CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0;

    private static string TextoDeBusca(ButtonBase controle) => controle.Tag switch
    {
        CommandDefinition definicao => definicao.SearchText,
        string tag => $"{tag} {TextoDe(controle)}",
        _ => TextoDe(controle)
    };

    private static IEnumerable<ButtonBase> EnumerarAcionaveis(DependencyObject parent)
    {
        foreach (var child in LogicalTreeHelper.GetChildren(parent).OfType<DependencyObject>())
        {
            if (child is ButtonBase controle)
                yield return controle;

            foreach (var inner in EnumerarAcionaveis(child))
                yield return inner;
        }
    }

    private static string TextoDe(DependencyObject root)
    {
        var sb = new StringBuilder();
        void Visitar(DependencyObject node)
        {
            if (node is TextBlock tb)
                sb.Append(tb.Text).Append(' ');
            else if (node is ContentControl { Content: string s })
                sb.Append(s).Append(' ');

            foreach (var child in LogicalTreeHelper.GetChildren(node).OfType<DependencyObject>())
                Visitar(child);
        }
        Visitar(root);
        return sb.ToString();
    }

    // ================= REDE: PING E TRACERT =================
    private void ChipHost_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string host)
        {
            TxtHostRede.Text = host;
        }
    }

    private async void BtnPing_Click(object sender, RoutedEventArgs e)
    {
        var host = ObterHostValido("Ping");
        if (host != null)
            await ExecutarAsync(CommandCatalog.Ping(host));
    }

    private async void BtnTracert_Click(object sender, RoutedEventArgs e)
    {
        var host = ObterHostValido("Tracert");
        if (host != null)
            await ExecutarAsync(CommandCatalog.Tracert(host));
    }

    private async void BtnNslookup_Click(object sender, RoutedEventArgs e)
    {
        var host = ObterHostValido("DNS");
        if (host != null)
            await ExecutarAsync(CommandCatalog.Nslookup(host));
    }

    private async void BtnTestarPorta_Click(object sender, RoutedEventArgs e)
    {
        var host = ObterHostValido("porta");
        if (host == null)
            return;

        var porta = InputValidator.NormalizePort(TxtPortaRede.Text);
        if (porta == null)
        {
            MessageBox.Show("Digite uma porta entre 1 e 65535 (ex: 80, 443, 3389).",
                "Porta Inválida", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await ExecutarAsync(CommandCatalog.TestPort(host, porta.Value));
    }

    private string? ObterHostValido(string operacao)
    {
        var host = InputValidator.NormalizeHost(TxtHostRede.Text);
        if (host == null)
        {
            MessageBox.Show(
                $"Digite um host ou IP válido para o teste de {operacao} (ex: google.com, 8.8.8.8).",
                "Host Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        return host;
    }

    // ================= REDE: SENHA DO WI-FI =================
    private async void BtnSenhaWifi_Click(object sender, RoutedEventArgs e)
    {
        var perfil = InputValidator.NormalizeWifiProfile(TxtPerfilWifi.Text);
        if (perfil == null)
        {
            MessageBox.Show(
                "Digite o nome exato de uma rede Wi-Fi salva (sem aspas). Use \"Redes Wi-Fi Salvas\" para ver os nomes.",
                "Nome da Rede", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmacao = MessageBox.Show(
            $"⚠️ A senha da rede \"{perfil}\" vai aparecer em texto no console (linha \"Conteúdo da Chave\").\n\n" +
            "• Confira se ninguém está vendo sua tela.\n" +
            "• Cuidado ao copiar o console ou compartilhar prints.\n" +
            "• Use \"Limpar\" no console quando terminar.\n\n" +
            "Deseja mostrar a senha?",
            "Mostrar Senha do Wi-Fi", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacao == MessageBoxResult.Yes)
            await ExecutarAsync(CommandCatalog.WifiPassword(perfil));
    }

    // ================= WINGET: APP ESPECÍFICO, BUSCA E LOTE =================
    private string? ObterAppIdValido()
    {
        string appId = TxtAppIdUpgrade.Text.Trim();
        if (InputValidator.IsValidWingetId(appId))
            return appId;

        MessageBox.Show(
            "Digite um ID de aplicativo válido (ex: Git.Git, Google.Chrome). Use apenas letras, números, '.', '_', '+' e '-'.",
            "ID Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
        return null;
    }

    private async void BtnInstalarEspecifico_Click(object sender, RoutedEventArgs e)
    {
        var appId = ObterAppIdValido();
        if (appId != null)
            await ExecutarAsync(CommandCatalog.WingetInstall(appId));
    }

    private async void BtnUpgradeEspecifico_Click(object sender, RoutedEventArgs e)
    {
        var appId = ObterAppIdValido();
        if (appId != null)
            await ExecutarAsync(CommandCatalog.WingetUpgrade(appId));
    }

    private async void BtnDesinstalarEspecifico_Click(object sender, RoutedEventArgs e)
    {
        var appId = ObterAppIdValido();
        if (appId == null)
            return;

        var confirmacao = MessageBox.Show(
            $"⚠️ ATENÇÃO: Desinstalar \"{appId}\"\n\n" +
            "O programa será removido deste computador. Configurações e dados dele podem ser apagados.\n\n" +
            "Deseja desinstalar?",
            "Confirmar Desinstalação", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacao == MessageBoxResult.Yes)
            await ExecutarAsync(CommandCatalog.WingetUninstall(appId));
    }

    // ================= WINGET: BACKUP DA LISTA DE PROGRAMAS =================
    private async void BtnExportarApps_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salvar lista de programas",
            FileName = $"programas-{DateTime.Now:yyyy-MM-dd}.json",
            Filter = "Lista do winget (*.json)|*.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialogo.ShowDialog(this) == true)
            await ExecutarAsync(CommandCatalog.WingetExport(dialogo.FileName));
    }

    private async void BtnImportarApps_Click(object sender, RoutedEventArgs e)
    {
        var dialogo = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Escolha a lista de programas exportada",
            Filter = "Lista do winget (*.json)|*.json",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialogo.ShowDialog(this) != true)
            return;

        var confirmacao = MessageBox.Show(
            "⚠️ ATENÇÃO: Instalação em Lote a partir da Lista\n\n" +
            $"Todos os programas de \"{Path.GetFileName(dialogo.FileName)}\" que ainda não estão instalados serão baixados e instalados.\n\n" +
            "• Duração: pode levar bastante tempo, conforme a quantidade de programas.\n" +
            "• Programas indisponíveis no winget são pulados.\n\n" +
            "Deseja continuar?",
            "Importar Lista de Programas", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacao == MessageBoxResult.Yes)
            await ExecutarAsync(CommandCatalog.WingetImport(dialogo.FileName));
    }

    private async void BtnBuscarWinget_Click(object sender, RoutedEventArgs e)
    {
        var busca = InputValidator.NormalizeSearchTerm(TxtPesquisaWinget.Text);
        if (busca == null)
        {
            MessageBox.Show(
                "Digite o nome ou parte do nome do programa que deseja procurar no Winget (sem começar com '-').",
                "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await ExecutarAsync(CommandCatalog.WingetSearch(busca));
    }

    // ================= WINGET: INSTALAÇÃO EM LOTE =================
    /// <summary>
    /// Monta os grupos de caixas a partir da lista do usuário (apps.json) ou da padrão.
    /// Se o arquivo tiver erro, mantém o que já está na tela e avisa.
    /// </summary>
    private void CarregarListaApps(bool avisarErro)
    {
        IReadOnlyList<QuickInstallGroup> grupos;
        try
        {
            grupos = QuickInstallCatalog.Load(QuickInstallCatalog.UserFilePath);
        }
        catch (Exception ex) when (ex is FormatException or IOException or UnauthorizedAccessException)
        {
            if (avisarErro)
            {
                MessageBox.Show($"Não foi possível carregar a lista de apps:\n\n{ex.Message}\n\nArquivo: {QuickInstallCatalog.UserFilePath}",
                    "Lista de Apps", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // Na abertura do app, um arquivo com erro não pode deixar a aba vazia.
            grupos = QuickInstallCatalog.Defaults;
            Escrever($"\n⚠️ apps.json com erro ({ex.Message}). Usando a lista padrão.\n");
        }

        _appCheckBoxes.Clear();
        QuickInstallGrid.Children.Clear();
        QuickInstallGrid.Columns = Math.Min(3, grupos.Count);

        foreach (var grupo in grupos)
        {
            var conteudo = new StackPanel();
            conteudo.Children.Add(new TextBlock
            {
                Text = grupo.Title,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            foreach (var app in grupo.Apps)
            {
                var caixa = new CheckBox { Tag = app.Id, Content = app.Name, IsChecked = app.Checked, ToolTip = app.Id };
                _appCheckBoxes.Add(caixa);
                conteudo.Children.Add(caixa);
            }

            QuickInstallGrid.Children.Add(new Border
            {
                Background = (Brush)FindResource("InsetBgBrush"),
                BorderBrush = (Brush)FindResource("BorderDarkBrush"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 10, 10),
                Child = conteudo
            });
        }
    }

    private void BtnEditarListaApps_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            QuickInstallCatalog.EnsureUserFile(QuickInstallCatalog.UserFilePath);
            var psi = new ProcessStartInfo { FileName = "notepad.exe", UseShellExecute = true };
            psi.ArgumentList.Add(QuickInstallCatalog.UserFilePath);
            Process.Start(psi);
            Escrever($"\n✏️ Editando {QuickInstallCatalog.UserFilePath}\n   Salve o arquivo e clique em \"🔄 Recarregar\".\n");
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Não foi possível abrir a lista de apps: {ex.Message}\n");
        }

        FlushOutput();
    }

    private void BtnRecarregarListaApps_Click(object sender, RoutedEventArgs e)
    {
        CarregarListaApps(avisarErro: true);
        AplicarFiltro(string.IsNullOrWhiteSpace(TxtBuscaGlobal.Text) ? null : TxtBuscaGlobal.Text.Trim());
    }

    private void BtnMarcarTodos_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(true);
    }

    private void BtnDesmarcarTodos_Click(object sender, RoutedEventArgs e)
    {
        SetAllCheckBoxes(false);
    }

    private void SetAllCheckBoxes(bool isChecked)
    {
        foreach (var chk in _appCheckBoxes)
        {
            chk.IsChecked = isChecked;
        }
    }

    private async void BtnInstalarApps_Click(object sender, RoutedEventArgs e)
    {
        var selecionados = _appCheckBoxes
            .Where(c => c.IsChecked == true && c.Tag is string id && InputValidator.IsValidWingetId(id))
            .Select(c => (string)c.Tag)
            .ToList();

        if (selecionados.Count == 0)
        {
            MessageBox.Show("Nenhum aplicativo foi selecionado. Marque os aplicativos desejados nas caixas de seleção.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmacao = MessageBox.Show(
            $"Deseja instalar {selecionados.Count} aplicativo(s) selecionado(s) via Winget?\n\n" +
            "Os downloads e instalações serão executados em sequência com acompanhamento no console.",
            "Confirmar Instalação em Lote",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirmacao != MessageBoxResult.Yes)
            return;

        // Uma falha em um app não impede a instalação dos demais; o botão Cancelar interrompe o lote todo.
        await ExecutarSequenciaAsync(selecionados.Select(CommandCatalog.WingetInstall).ToList(), pararNaFalha: false);
    }

    // ================= PASTAS E ARQUIVOS =================
    private void BtnCriarPasta_Click(object sender, RoutedEventArgs e)
    {
        var caminho = InputValidator.NormalizeFolderPath(TxtCaminhoPasta.Text);
        if (caminho == null)
        {
            MessageBox.Show(
                "Digite um caminho absoluto e válido para a pasta (ex: C:\\MinhaNovaPasta).",
                "Caminho Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            if (Directory.Exists(caminho))
            {
                Escrever($"\nℹ A pasta já existe: {caminho}\n");
            }
            else
            {
                Directory.CreateDirectory(caminho);
                Escrever($"\n✔ Pasta criada: {caminho}\n");
            }
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Não foi possível criar a pasta: {ex.Message}\n");
        }

        FlushOutput();
    }

    private async void BtnDir_Click(object sender, RoutedEventArgs e)
    {
        var caminho = InputValidator.NormalizeFolderPath(TxtCaminhoPasta.Text);
        if (caminho == null || !Directory.Exists(caminho))
        {
            MessageBox.Show(
                "Digite o caminho completo de uma pasta que exista (ex: C:\\Users).",
                "Pasta não encontrada", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await ExecutarAsync(CommandCatalog.Dir(caminho));
    }

    private void BtnAbrirExplorer_Click(object sender, RoutedEventArgs e)
    {
        // Abre a pasta digitada (se existir); senão, a pasta do usuário.
        var digitado = InputValidator.NormalizeFolderPath(TxtCaminhoPasta.Text);
        string caminho = digitado != null && Directory.Exists(digitado)
            ? digitado
            : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        try
        {
            var psi = new ProcessStartInfo { FileName = "explorer.exe", UseShellExecute = true };
            psi.ArgumentList.Add(caminho);
            Process.Start(psi);
            Escrever($"\n✔ Windows Explorer aberto em: {caminho}\n");
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Erro ao abrir Explorer: {ex.Message}\n");
        }

        FlushOutput();
    }

    // ================= ENERGIA: DESLIGAR / REINICIAR PROGRAMADO =================
    private async void BtnDesligarProgramado_Click(object sender, RoutedEventArgs e) =>
        await AgendarEnergiaAsync("desligado", CommandCatalog.ShutdownIn);

    private async void BtnReiniciarProgramado_Click(object sender, RoutedEventArgs e) =>
        await AgendarEnergiaAsync("reiniciado", CommandCatalog.RestartIn);

    private async Task AgendarEnergiaAsync(string acao, Func<TimeSpan, CommandSpec> montar)
    {
        var minutos = InputValidator.NormalizeMinutes(TxtMinutosEnergia.Text);
        if (minutos == null)
        {
            MessageBox.Show("Digite um número de minutos entre 0 e 1440 (24 horas).",
                "Tempo Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var quando = minutos == 0
            ? "IMEDIATAMENTE (salve seus arquivos antes!)"
            : $"daqui a {minutos} minuto(s), às {DateTime.Now.AddMinutes(minutos.Value):HH:mm}";

        var confirmacao = MessageBox.Show(
            $"⚠️ ATENÇÃO: o computador será {acao} {quando}.\n\n" +
            "Programas abertos serão fechados e alterações não salvas serão perdidas.\n" +
            "Para desistir depois, use \"🛑 Cancelar Desligamento\".\n\n" +
            "Deseja agendar?",
            "Agendar Energia", MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirmacao == MessageBoxResult.Yes)
            await ExecutarAsync(montar(TimeSpan.FromMinutes(minutos.Value)));
    }

    // ================= CONSOLE (saída em lote, thread-safe) =================
    private void Escrever(string texto) => _pendingOutput.Enqueue(texto);

    private void FlushOutput()
    {
        if (_pendingOutput.IsEmpty)
            return;

        var sb = new StringBuilder();
        while (_pendingOutput.TryDequeue(out var parte))
            sb.Append(parte);

        TxtConsoleOutput.AppendText(sb.ToString());

        if (TxtConsoleOutput.Text.Length > MaxConsoleChars)
        {
            TxtConsoleOutput.Text = "[... saída antiga removida ...]\n" + TxtConsoleOutput.Text[^KeepConsoleChars..];
        }

        TxtConsoleOutput.ScrollToEnd();
    }

    private void SetOcupado(bool ocupado)
    {
        _isRunning = ocupado;
        // Enquanto algo executa, os painéis de comandos ficam desabilitados; só o botão Cancelar atua.
        foreach (var painel in TodosOsPaineis)
            painel.IsEnabled = !ocupado;

        BtnPararComando.IsEnabled = ocupado;
    }

    // ================= MOTOR PRINCIPAL DE EXECUÇÃO ASSÍNCRONA =================
    private const string FaixaConsole = "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━";

    private static string ResumoDaExecucao(TimeSpan duracao, bool cancelado, string? erroInesperado,
        int falhas, int executados, int ultimoCodigo, bool sequencia)
    {
        var tempo = duracao.TotalSeconds < 60
            ? $"{duracao.TotalSeconds.ToString("0.0", CultureInfo.CurrentCulture)} s"
            : $"{(int)duracao.TotalMinutes} min {duracao.Seconds} s";

        if (erroInesperado != null)
            return $"❌ Falha na execução: {erroInesperado}";
        if (cancelado)
            return $"⏹ Interrompido pelo usuário · {tempo}";
        if (falhas == 0)
            return $"✔ Concluído com sucesso · {tempo}";
        return sequencia
            ? $"⚠ {falhas} de {executados} passo(s) com erro · {tempo}"
            : $"⚠ Terminou com erro (código {CodigoLegivel(ultimoCodigo)}) · {tempo}";
    }

    /// <summary>Códigos HRESULT (ex.: -1978335212) ficam mais reconhecíveis em hexadecimal (0x8A150014).</summary>
    private static string CodigoLegivel(int codigo) =>
        codigo < -1000 ? $"0x{unchecked((uint)codigo):X8}" : codigo.ToString(CultureInfo.InvariantCulture);

    private Task ExecutarAsync(CommandSpec comando) => ExecutarSequenciaAsync(new[] { comando }, pararNaFalha: true);

    /// <param name="definicao">Comando do catálogo que originou a execução (permite repetir pelo histórico).</param>
    private async Task ExecutarSequenciaAsync(IReadOnlyList<CommandSpec> comandos, bool pararNaFalha, CommandDefinition? definicao = null)
    {
        if (_isRunning)
            return;

        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;
        int ultimoCodigo = 0;
        int falhas = 0;
        int executados = 0;
        string? erroInesperado = null;
        var cronometro = Stopwatch.StartNew();

        SetOcupado(true);
        ConsoleStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 163, 255)); // Ciano
        TxtConsoleStatus.Text = "Executando comando no terminal...";
        _flushTimer.Start();

        // Cada execução vira um bloco: faixa com título e hora, saída, e uma linha de resultado.
        var sequencia = comandos.Count > 1;
        var titulo = definicao?.Title ?? (sequencia ? $"{comandos.Count} comandos em sequência" : comandos[0].Display);
        Escrever($"\n\n{FaixaConsole}\n▶ {titulo}   ·   {DateTime.Now:HH:mm:ss}\n{FaixaConsole}\n");

        try
        {
            for (int i = 0; i < comandos.Count; i++)
            {
                var spec = comandos[i];
                TxtComandoAtual.Text = sequencia
                    ? $"Executando ({i + 1}/{comandos.Count}): {spec.Display}"
                    : $"Executando: {spec.Display}";

                if (sequencia)
                    Escrever($"\n── Passo {i + 1} de {comandos.Count} ──\n");
                // Sem título próprio, a faixa já mostra o comando; não repete.
                if (sequencia || definicao != null)
                    Escrever($"$ {spec.Display}\n");
                Escrever("\n");

                var saida = new OutputLineFilter(linha => Escrever(linha + "\n"));
                var erros = new OutputLineFilter(linha => Escrever(linha.Length == 0 ? "\n" : "[ERRO] " + linha + "\n"));

                var (resultado, erro, codigoSaida) = await CommandExecutor.ExecuteAsync(
                    spec,
                    onOutputLine: saida.Push,
                    onErrorLine: erros.Push,
                    cancellationToken: token);
                ultimoCodigo = codigoSaida;
                executados++;

                if (token.IsCancellationRequested)
                    break;

                if (codigoSaida != 0)
                {
                    falhas++;
                    if (sequencia)
                        Escrever($"\n⚠ Passo {i + 1} terminou com código {CodigoLegivel(codigoSaida)}.\n");
                    if (!_isAdmin && OutputAnalyzer.IndicatesElevationNeeded(resultado, erro))
                        Escrever("\n💡 Dica: execute o WinTerminalTools como Administrador para este comando ter permissão total.\n");
                    if (spec.FileName == "winget" && OutputAnalyzer.WingetExitHint(codigoSaida) is { } dicaWinget)
                        Escrever($"\n💡 {dicaWinget}\n");

                    if (pararNaFalha)
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            erroInesperado = ex.Message;
            ultimoCodigo = -1;
        }
        finally
        {
            Escrever($"\n{ResumoDaExecucao(cronometro.Elapsed, token.IsCancellationRequested, erroInesperado, falhas, executados, ultimoCodigo, sequencia)}\n");

            _flushTimer.Stop();
            FlushOutput();

            var rotulo = definicao?.Title ?? (comandos.Count > 1
                ? $"{comandos[0].Display} (+{comandos.Count - 1})"
                : comandos[0].Display);
            RegistrarHistorico(new HistoryEntry(DateTime.Now, rotulo, ultimoCodigo, token.IsCancellationRequested, definicao?.Id));

            _currentCts.Dispose();
            _currentCts = null;

            SetOcupado(false);
            TxtComandoAtual.Text = "Nenhum comando em execução.";
            TxtConsoleStatus.Text = "Console do Windows (Pronto)";
            ConsoleStatusDot.Fill = new SolidColorBrush(Color.FromRgb(16, 185, 129)); // Verde
        }
    }

    private void BtnPararComando_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _currentCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Execução já terminou.
        }
    }

    private void BtnLimparConsole_Click(object sender, RoutedEventArgs e)
    {
        while (_pendingOutput.TryDequeue(out _)) { }
        TxtConsoleOutput.Text = "Console limpo. Pronto para novos comandos.\n";
    }

    private void BtnCopiarConsole_Click(object sender, RoutedEventArgs e)
    {
        FlushOutput();
        if (!string.IsNullOrEmpty(TxtConsoleOutput.Text))
        {
            try
            {
                Clipboard.SetText(TxtConsoleOutput.Text);
                // Aviso discreto no rodapé do console, sem janela para fechar.
                if (!_isRunning)
                    TxtComandoAtual.Text = "📋 Saída copiada para a Área de Transferência.";
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                MessageBox.Show("A Área de Transferência está em uso por outro programa. Tente novamente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnQuebraLinhas_Click(object sender, RoutedEventArgs e)
    {
        _settings.ConsoleWrap = !_settings.ConsoleWrap;
        AplicarQuebraDeLinhas();
        SalvarPreferencias();
    }

    private void AplicarQuebraDeLinhas()
    {
        TxtConsoleOutput.TextWrapping = _settings.ConsoleWrap ? TextWrapping.Wrap : TextWrapping.NoWrap;
        TxtQuebraLinhas.Text = _settings.ConsoleWrap ? "↩ Quebrar: sim" : "↔ Quebrar: não";
    }

    private void BtnSalvarConsole_Click(object sender, RoutedEventArgs e) => SalvarConsole();

    private void SalvarConsole()
    {
        FlushOutput();
        var dialogo = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Salvar saída do console",
            FileName = $"WinTerminalTools-{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
            Filter = "Texto (*.txt)|*.txt",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialogo.ShowDialog(this) != true)
            return;

        try
        {
            // UTF-8 com BOM: o Bloco de Notas antigo abre com os acentos certos.
            File.WriteAllText(dialogo.FileName, TxtConsoleOutput.Text, new UTF8Encoding(true));
            if (!_isRunning)
                TxtComandoAtual.Text = $"💾 Saída salva em {dialogo.FileName}";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Não foi possível salvar o arquivo:\n\n{ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ================= ATALHOS DE TECLADO =================
    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        var ctrl = Keyboard.Modifiers == ModifierKeys.Control;

        if (ctrl && e.Key == Key.F)
        {
            TxtBuscaGlobal.Focus();
            TxtBuscaGlobal.SelectAll();
        }
        else if (e.Key == Key.Escape && _isRunning)
        {
            BtnPararComando_Click(sender, e);
        }
        else if (e.Key == Key.Escape && TxtBuscaGlobal.Text.Length > 0)
        {
            TxtBuscaGlobal.Clear();
        }
        else if (ctrl && e.Key == Key.L)
        {
            BtnLimparConsole_Click(sender, e);
        }
        else if (ctrl && e.Key == Key.S)
        {
            SalvarConsole();
        }
        else if (ctrl && e.Key is >= Key.D1 and <= Key.D5)
        {
            MainTabControl.SelectedIndex = e.Key - Key.D1;
        }
        else
        {
            return;
        }

        e.Handled = true;
    }

    private void BtnAbrirCmd_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o CMD: {ex.Message}", "Erro", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnReiniciarAdmin_Click(object sender, RoutedEventArgs e) => ReiniciarComoAdmin();

    private void ReiniciarComoAdmin()
    {
        try
        {
            var exeName = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exeName))
            {
                var psi = new ProcessStartInfo
                {
                    FileName = exeName,
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                Application.Current.Shutdown();
            }
        }
        catch
        {
            MessageBox.Show("A solicitação de elevação como Administrador foi recusada ou cancelada.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Não foi possível abrir o link: {ex.Message}", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        e.Handled = true;
    }
}
