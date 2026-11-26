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
    private readonly CheckBox[] _appCheckBoxes;
    private readonly ConcurrentQueue<string> _pendingOutput = new();
    private readonly DispatcherTimer _flushTimer;

    public MainWindow()
    {
        InitializeComponent();

        _appCheckBoxes = new[]
        {
            AppChrome, AppBrave, AppFirefox, AppOperaGX,
            AppSpotify, AppDiscord, AppVLC, AppSteam, AppWhatsApp,
            AppVSCode, AppGit, App7Zip, AppNotepadPlusPlus, AppDocker
        };

        _flushTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(60)
        };
        _flushTimer.Tick += (_, _) => FlushOutput();

        _isAdmin = EhAdministrador();

        Title = $"WinTerminalTools v{AppInfo.Version}";
        TxtVersao.Text = $"v{AppInfo.Version}";

        ConstruirSecoesDoCatalogo();
        CarregarIconeSeguro();
        VerificarPermissaoAdministrador();
        ExibirMensagemBoasVindas();
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
        sb.AppendLine("• Todo comando pode ser interrompido a qualquer instante clicando em [Cancelar Execução].");
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
            ToolTip = string.Join("\n", definicao.Steps.Select(s => "$ " + s.Display))
        };
        botao.Click += BtnDefinicao_Click;
        return botao;
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

        await ExecutarSequenciaAsync(definicao.Steps, pararNaFalha: !definicao.ContinueOnFailure);
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
    private void MainTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PanelRede == null || PanelApps == null || PanelPastas == null || PanelSistema == null)
            return;

        PanelRede.Visibility = Visibility.Collapsed;
        PanelApps.Visibility = Visibility.Collapsed;
        PanelPastas.Visibility = Visibility.Collapsed;
        PanelSistema.Visibility = Visibility.Collapsed;

        if (MainTabControl.SelectedItem == TabRede)
            PanelRede.Visibility = Visibility.Visible;
        else if (MainTabControl.SelectedItem == TabApps)
            PanelApps.Visibility = Visibility.Visible;
        else if (MainTabControl.SelectedItem == TabPastas)
            PanelPastas.Visibility = Visibility.Visible;
        else if (MainTabControl.SelectedItem == TabSistema)
            PanelSistema.Visibility = Visibility.Visible;
    }

    // ================= FILTRO / BUSCA GLOBAL =================
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
        painel == PanelPastas ? TabPastas : TabSistema;

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

    // ================= WINGET: APP ESPECÍFICO, BUSCA E LOTE =================
    private async void BtnUpgradeEspecifico_Click(object sender, RoutedEventArgs e)
    {
        string appId = TxtAppIdUpgrade.Text.Trim();
        if (!InputValidator.IsValidWingetId(appId))
        {
            MessageBox.Show(
                "Digite um ID de aplicativo válido (ex: Git.Git, Google.Chrome). Use apenas letras, números, '.', '_', '+' e '-'.",
                "ID Inválido", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        await ExecutarAsync(CommandCatalog.WingetUpgrade(appId));
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
        foreach (var painel in Paineis)
            painel.IsEnabled = !ocupado;

        BtnPararComando.IsEnabled = ocupado;
    }

    // ================= MOTOR PRINCIPAL DE EXECUÇÃO ASSÍNCRONA =================
    private Task ExecutarAsync(CommandSpec comando) => ExecutarSequenciaAsync(new[] { comando }, pararNaFalha: true);

    private async Task ExecutarSequenciaAsync(IReadOnlyList<CommandSpec> comandos, bool pararNaFalha)
    {
        if (_isRunning)
            return;

        _currentCts = new CancellationTokenSource();
        var token = _currentCts.Token;

        SetOcupado(true);
        ConsoleStatusDot.Fill = new SolidColorBrush(Color.FromRgb(0, 163, 255)); // Ciano
        TxtConsoleStatus.Text = "Executando comando no terminal...";
        _flushTimer.Start();

        try
        {
            for (int i = 0; i < comandos.Count; i++)
            {
                var spec = comandos[i];
                TxtComandoAtual.Text = comandos.Count > 1
                    ? $"Executando ({i + 1}/{comandos.Count}): {spec.Display}"
                    : $"Executando: {spec.Display}";

                Escrever($"\n\n$ {spec.Display}\n");
                Escrever("────────────────────────────────────────────────────────────────────────\n");

                var (resultado, erro, codigoSaida) = await CommandExecutor.ExecuteAsync(
                    spec,
                    onOutputLine: linha => Escrever(linha + "\n"),
                    onErrorLine: linha => Escrever("[ERRO] " + linha + "\n"),
                    cancellationToken: token);

                if (token.IsCancellationRequested)
                {
                    Escrever("\n⏹ Comando interrompido pelo usuário.\n");
                    break;
                }

                if (codigoSaida != 0)
                {
                    Escrever($"\n⚠️ [Código de saída: {codigoSaida}]\n");
                    if (!_isAdmin && OutputAnalyzer.IndicatesElevationNeeded(resultado, erro))
                    {
                        Escrever("💡 DICA: Execute o WinTerminalTools como Administrador para que este comando tenha permissão total.\n");
                    }

                    if (pararNaFalha)
                        break;
                }
                else
                {
                    Escrever("✔ Comando finalizado com sucesso.\n");
                }
            }
        }
        catch (Exception ex)
        {
            Escrever($"\n❌ Falha na execução: {ex.Message}\n");
        }
        finally
        {
            _flushTimer.Stop();
            FlushOutput();

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
                MessageBox.Show("Saída do console copiada para a Área de Transferência com sucesso!", "Copiado", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Runtime.InteropServices.COMException)
            {
                MessageBox.Show("A Área de Transferência está em uso por outro programa. Tente novamente.", "Aviso", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
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
