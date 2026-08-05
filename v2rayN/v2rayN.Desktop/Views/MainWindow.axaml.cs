using Avalonia.Controls.Notifications;
using DialogHostAvalonia;
using ServiceLib.Services;
using v2rayN.Desktop.Base;
using v2rayN.Desktop.Common;
using v2rayN.Desktop.Manager;

namespace v2rayN.Desktop.Views;

public partial class MainWindow : WindowBase<MainWindowViewModel>
{
    private static Config _config;
    private readonly SingleReplaceableDisposable _layoutBindingsDisposable = new();
    private readonly WindowNotificationManager? _manager;
    private readonly SemaphoreSlim _miaomiaoPromptGate = new(1, 1);
    private readonly SemaphoreSlim _miaomiaoNoticeGate = new(1, 1);
    private readonly HashSet<long> _shownMiaomiaoMigrationVersions = [];
    private readonly HashSet<long> _shownMiaomiaoDesktopUpdateBuilds = [];
    private readonly HashSet<string> _shownMiaomiaoNotices = [];
    private BackupAndRestoreView? _backupAndRestoreView;
    private bool _isApplyingRouteSelection;
    private bool _blCloseByUser = false;
    private bool _isWindowOpened;

    public MainWindow()
    {
        InitializeComponent();

        _config = AppManager.Instance.Config;
        _manager = new WindowNotificationManager(TopLevel.GetTopLevel(this)) { MaxItems = 3, Position = NotificationPosition.TopRight };

        KeyDown += MainWindow_KeyDown;
        Opened += MainWindow_Opened;
        Activated += MainWindow_Activated;
        menuBackupAndRestore.Click += MenuBackupAndRestore_Click;
        menuClose.Click += MenuClose_Click;
        btnBackup.Click += MenuBackupAndRestore_Click;

        conTheme.Content ??= new ThemeSettingView();
        contentAccount.Content ??= new MiaomiaoAccountView();
        var accountView = (MiaomiaoAccountView)contentAccount.Content;
        homeAccountPanel.DataContext = accountView.ViewModel;
        txtVersion.Text = Utils.GetVersion();

        this.WhenActivated(disposables =>
        {
            //servers
            this.BindCommand(ViewModel, vm => vm.AddVmessServerCmd, v => v.menuAddVmessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddVlessServerCmd, v => v.menuAddVlessServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddShadowsocksServerCmd, v => v.menuAddShadowsocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddSocksServerCmd, v => v.menuAddSocksServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHttpServerCmd, v => v.menuAddHttpServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTrojanServerCmd, v => v.menuAddTrojanServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddHysteria2ServerCmd, v => v.menuAddHysteria2Server).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddTuicServerCmd, v => v.menuAddTuicServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddWireguardServerCmd, v => v.menuAddWireguardServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddAnytlsServerCmd, v => v.menuAddAnytlsServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddNaiveServerCmd, v => v.menuAddNaiveServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddCustomServerCmd, v => v.menuAddCustomServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddPolicyGroupServerCmd, v => v.menuAddPolicyGroupServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddProxyChainServerCmd, v => v.menuAddProxyChainServer).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaClipboardCmd, v => v.menuAddServerViaClipboard).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaScanCmd, v => v.menuAddServerViaScan).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.AddServerViaImageCmd, v => v.menuAddServerViaImage).DisposeWith(disposables);

            //sub
            this.BindCommand(ViewModel, vm => vm.SubSettingCmd, v => v.menuSubSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.menuSubUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateViaProxyCmd, v => v.menuSubUpdateViaProxy).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateCmd, v => v.menuSubGroupUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubGroupUpdateViaProxyCmd, v => v.menuSubGroupUpdateViaProxy).DisposeWith(disposables);

            //setting
            this.BindCommand(ViewModel, vm => vm.OptionSettingCmd, v => v.menuOptionSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OptionSettingCmd, v => v.btnOptions).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingSettingCmd, v => v.menuRoutingSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RoutingSettingCmd, v => v.btnRouting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DNSSettingCmd, v => v.menuDNSSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.DNSSettingCmd, v => v.btnDns).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.FullConfigTemplateCmd, v => v.menuFullConfigTemplate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.GlobalHotkeySettingCmd, v => v.menuGlobalHotkeySetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RebootAsAdminCmd, v => v.menuRebootAsAdmin).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.ClearServerStatisticsCmd, v => v.menuClearServerStatistics).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenTheFileLocationCmd, v => v.menuOpenTheFileLocation).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenTheFileLocationCmd, v => v.btnOpenFolder).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.CheckCoreUpdateCmd, v => v.btnCoreUpdate).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.OpenDownloadPageCmd, v => v.btnDownload).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubSettingCmd, v => v.btnSubscriptionSetting).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.btnQuickRefresh).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.SubUpdateCmd, v => v.btnHomeRefresh).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetDefaultCmd, v => v.menuRegionalPresetsDefault).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetRussiaCmd, v => v.menuRegionalPresetsRussia).DisposeWith(disposables);
            this.BindCommand(ViewModel, vm => vm.RegionalPresetIranCmd, v => v.menuRegionalPresetsIran).DisposeWith(disposables);

            this.BindCommand(ViewModel, vm => vm.ReloadCmd, v => v.menuReload).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.BlReloadEnabled, v => v.menuReload.IsEnabled).DisposeWith(disposables);
            this.OneWayBind(ViewModel, vm => vm.StatusBarViewModel, v => v.contentStatusBarView.Content).DisposeWith(disposables);

            _layoutBindingsDisposable.DisposeWith(disposables);

            this.WhenAnyValue(v => v.ViewModel.MainGirdOrientation)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .Subscribe(UpdateLayout)
                .DisposeWith(disposables);

            ViewModel.ReadTextFromClipboardInteraction.RegisterHandler(async interaction =>
            {
                var result = await AvaUtils.GetClipboardData(this);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel.ScanScreenInteraction.RegisterHandler(async interaction =>
            {
                ShowHideWindow(false);
                await Task.Delay(200);
                var result = QRCodeAvaloniaUtils.CaptureScreen();
                ShowHideWindow(true);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel.BrowseImageFileInteraction.RegisterHandler(async interaction =>
            {
                var result = await UI.OpenFileDialog(null);
                interaction.SetOutput(result);
            }).DisposeWith(disposables);

            ViewModel.ShowHideWindowInteraction.RegisterHandler(interaction =>
            {
                ShowHideWindow(interaction.Input);
                interaction.SetOutput(RxVoid.Default);
            }).DisposeWith(disposables);

            AppEvents.SendSnackMsgRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(async content => await DelegateSnackMsg(content))
              .DisposeWith(disposables);

            AppEvents.MiaomiaoManifestUpdated
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(_manifestResult => _ = ShowMiaomiaoPromptsAsync())
              .DisposeWith(disposables);

            accountView.ViewModel!.NoticesUpdated
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(notices => _ = ShowMiaomiaoAccountNoticesAsync(notices))
              .DisposeWith(disposables);

            AppEvents.AppExitRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(_ => StorageUI())
              .DisposeWith(disposables);

            AppEvents.ShutdownRequested
              .AsObservable()
              .ObserveOn(RxSchedulers.MainThreadScheduler)
              .Subscribe(Shutdown)
              .DisposeWith(disposables);
        });

        if (Utils.IsWindows())
        {
            Title = $"喵喵 {Utils.GetVersion()} - {(Utils.IsAdministrator() ? ResUI.RunAsAdmin : ResUI.NotRunAsAdmin)}";

            if (!Design.IsDesignMode)
            {
                ThreadPool.RegisterWaitForSingleObject(Program.ProgramStarted, OnProgramStarted, null, -1, false);
                HotkeyManager.Instance.Init(_config, OnHotkeyHandler);
            }
        }
        else
        {
            Title = $"喵喵 {Utils.GetVersion()}";
            menuAddServerViaScan.IsVisible = false;
        }

        if (_config.UiItem.AutoHideStartup && Utils.IsWindows())
        {
            WindowState = WindowState.Minimized;
        }

    }

    #region Event

    private async void MainWindow_Opened(object? sender, EventArgs e)
    {
        _isWindowOpened = true;
        Opened -= MainWindow_Opened;
        await ShowMiaomiaoPromptsAsync();
        if (contentAccount.Content is MiaomiaoAccountView { ViewModel: { } accountViewModel })
        {
            await accountViewModel.OnWindowActivatedAsync();
            await ShowMiaomiaoAccountNoticesAsync(accountViewModel.Notices.ToList());
        }
    }

    private async void MainWindow_Activated(object? sender, EventArgs e)
    {
        if (contentAccount.Content is MiaomiaoAccountView { ViewModel: { } accountViewModel })
        {
            await accountViewModel.OnWindowActivatedAsync();
        }
    }

    private async Task ShowMiaomiaoPromptsAsync()
    {
        if (!_isWindowOpened)
        {
            return;
        }

        await _miaomiaoPromptGate.WaitAsync();
        try
        {
            var endpointService = MiaomiaoEndpointManifestService.Instance;
            var payload = endpointService.GetCurrent();
            if (payload.MigrationNotice is { } notice
                && endpointService.ShouldPromptMigration(payload)
                && _shownMiaomiaoMigrationVersions.Add(payload.Version))
            {
                ShowHideWindow(true);
                var primaryHost = new Uri(payload.ApiEndpoints[0]).Host;
                var importance = notice.Required ? "重要通知\n\n" : string.Empty;
                var message = $"{importance}{notice.Title}\n\n{notice.Message}\n\n服务入口已由签名配置自动更新为：{primaryHost}\n本地节点和订阅缓存不会被清除。";
                if (await UI.ShowYesNo(message) == ButtonResult.Yes)
                {
                    await endpointService.AcknowledgeMigrationAsync(payload.Version);
                }
            }

            var update = endpointService.GetAvailableDesktopUpdate(payload);
            if (update == null
                || !endpointService.ShouldPromptDesktopUpdate(payload)
                || !_shownMiaomiaoDesktopUpdateBuilds.Add(update.Build))
            {
                return;
            }

            ShowHideWindow(true);
            var updateImportance = update.Required == true ? "必须更新\n\n" : string.Empty;
            var updateMessage = $"{updateImportance}{update.Title}\n\n{update.Message}\n\n当前版本：{Utils.GetVersionInfo()}\n最新版本：{update.Version}\n\n点击确定前往官方下载页。";
            var updateNow = await UI.ShowYesNo(updateMessage) == ButtonResult.Yes;
            if (update.Required != true)
            {
                await endpointService.AcknowledgeDesktopUpdateAsync(update.Build);
            }
            if (updateNow)
            {
                var downloadUrl = Uri.TryCreate(update.DownloadUrl, UriKind.Absolute, out var updateUri)
                    && updateUri.Scheme == Uri.UriSchemeHttps
                    ? updateUri.AbsoluteUri
                    : endpointService.GetDownloadUri().AbsoluteUri;
                ProcUtils.ProcessStart(downloadUrl);
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Show Miaomiao manifest prompts", ex);
        }
        finally
        {
            _miaomiaoPromptGate.Release();
        }
    }

    private async Task ShowMiaomiaoAccountNoticesAsync(IReadOnlyList<MiaomiaoNotice> notices)
    {
        if (!_isWindowOpened || notices.Count == 0)
        {
            return;
        }

        await _miaomiaoNoticeGate.WaitAsync();
        try
        {
            foreach (var notice in notices)
            {
                var identity = notice.Id is { } id
                    ? $"id:{id}"
                    : $"content:{notice.Title}:{notice.CreatedAt}:{notice.Content.GetHashCode(StringComparison.Ordinal)}";
                if (!_shownMiaomiaoNotices.Add(identity))
                {
                    continue;
                }

                ShowHideWindow(true);
                await DialogHost.Show(new MiaomiaoNoticeDialog(notice));
            }
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Show Miaomiao account notices", ex);
        }
        finally
        {
            _miaomiaoNoticeGate.Release();
        }
    }

    private void OnProgramStarted(object state, bool timeout)
    {
        Dispatcher.UIThread.Post(() =>
                ShowHideWindow(true),
            DispatcherPriority.Default);
    }

    private async Task DelegateSnackMsg(string content)
    {
        _manager?.Show(new Avalonia.Controls.Notifications.Notification(null, content, NotificationType.Information));
        await Task.CompletedTask;
    }

    private void OnHotkeyHandler(EGlobalHotkey e)
    {
        switch (e)
        {
            case EGlobalHotkey.ShowForm:
                Dispatcher.UIThread.Post(() => ShowHideWindow(null));
                break;

            case EGlobalHotkey.SystemProxyClear:
            case EGlobalHotkey.SystemProxySet:
            case EGlobalHotkey.SystemProxyUnchanged:
            case EGlobalHotkey.SystemProxyPac:
                AppEvents.SysProxyChangeRequested.Publish((ESysProxyType)((int)e - 1));
                break;
        }
    }

    protected override async void OnClosing(WindowClosingEventArgs e)
    {
        if (_blCloseByUser)
        {
            return;
        }

        Logging.SaveLog("OnClosing -> " + e.CloseReason.ToString());

        switch (e.CloseReason)
        {
            case WindowCloseReason.OwnerWindowClosing or WindowCloseReason.WindowClosing:
                e.Cancel = true;
                ShowHideWindow(false);
                break;

            case WindowCloseReason.ApplicationShutdown or WindowCloseReason.OSShutdown:
                await AppManager.Instance.AppExitAsync(false);
                break;
        }

        base.OnClosing(e);
    }

    private async void MainWindow_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is KeyModifiers.Control or KeyModifiers.Meta)
        {
            switch (e.Key)
            {
                case Key.V:
                    await AddServerViaClipboardAsync();
                    break;

                case Key.S:
                    await ScanScreenTaskAsync();
                    break;
            }
        }
        else
        {
            if (e.Key == Key.F5)
            {
                ViewModel?.Reload();
            }
        }
    }

    public async Task AddServerViaClipboardAsync()
    {
        var clipboardData = await AvaUtils.GetClipboardData(this);
        if (clipboardData.IsNotEmpty() && ViewModel != null)
        {
            await ViewModel.AddServerViaClipboardAsync(clipboardData);
        }
    }

    public async Task ScanScreenTaskAsync()
    {
        ShowHideWindow(false);

        await Task.Delay(200);

        var bytes = QRCodeAvaloniaUtils.CaptureScreen();
        if (bytes != null && ViewModel != null)
        {
            await ViewModel.ScanScreenResult(bytes);
        }

        ShowHideWindow(true);
    }

    private void MenuBackupAndRestore_Click(object? sender, RoutedEventArgs e)
    {
        _backupAndRestoreView ??= new BackupAndRestoreView();
        _backupAndRestoreView.ViewModel = ViewModel?.BackupAndRestoreViewModel;
        DialogHost.Show(_backupAndRestoreView);
    }

    private async void MenuClose_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (await UI.ShowYesNo(ResUI.menuExitTips) != ButtonResult.Yes)
            {
                return;
            }

            _blCloseByUser = true;
            StorageUI();

            await AppManager.Instance.AppExitAsync(true);
        }
        catch
        {
            // Ignore
        }
    }

    private void Shutdown(bool obj)
    {
        if (obj is bool b && _blCloseByUser == false)
        {
            _blCloseByUser = b;
        }
        StorageUI();
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            HotkeyManager.Instance.Dispose();
            desktop.Shutdown();
        }
    }

    #endregion Event

    #region UI

    private void NavHome_Click(object? sender, RoutedEventArgs e)
    {
        SetActivePage(pageHome, navHome);
    }

    private void NavRoutes_Click(object? sender, RoutedEventArgs e)
    {
        SetActivePage(pageConnection, navRoutes);
    }

    private void NavAccount_Click(object? sender, RoutedEventArgs e)
    {
        ShowAccountSection(0, "账户", navAccount);
    }

    private void NavPlans_Click(object? sender, RoutedEventArgs e)
    {
        ShowAccountSection(1, "套餐", navPlans);
    }

    private void NavOrders_Click(object? sender, RoutedEventArgs e)
    {
        ShowAccountSection(2, "订单", navOrders);
    }

    private void NavTools_Click(object? sender, RoutedEventArgs e)
    {
        SetActivePage(pageTools, navTools);
    }

    private void OpenPlans_Click(object? sender, RoutedEventArgs e)
    {
        ShowAccountSection(1, "套餐", navPlans);
    }

    private void OpenAccount_Click(object? sender, RoutedEventArgs e)
    {
        ShowAccountSection(0, "账户", navAccount);
    }

    private void OpenAllRoutes_Click(object? sender, RoutedEventArgs e)
    {
        SetActivePage(pageConnection, navRoutes);
    }

    private async void MiaomiaoRoutes_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isApplyingRouteSelection
            || sender is not ListBox { SelectedItem: ServiceLib.Models.Dto.ProfileItemModel selected }
            || selected.IndexId.IsNullOrEmpty()
            || ViewModel is null)
        {
            return;
        }

        ViewModel.ProfilesViewModel.SelectedProfile = selected;
        ViewModel.ProfilesViewModel.SelectedProfiles = [selected];
        if (_config.IndexId == selected.IndexId)
        {
            return;
        }

        try
        {
            _isApplyingRouteSelection = true;
            await ViewModel.ProfilesViewModel.SetDefaultServer(selected.IndexId);
        }
        finally
        {
            _isApplyingRouteSelection = false;
        }
    }

    private void ConnectionToggle_Click(object? sender, RoutedEventArgs e)
    {
        var statusBar = ViewModel.StatusBarViewModel;
        statusBar.SystemProxySelected = statusBar.SystemProxySelected == (int)ESysProxyType.ForcedChange
            ? (int)ESysProxyType.ForcedClear
            : (int)ESysProxyType.ForcedChange;
    }

    private void ShowAccountSection(int index, string title, Button activeButton)
    {
        txtAccountPageTitle.Text = title;
        if (contentAccount.Content is MiaomiaoAccountView accountView)
        {
            accountView.SelectSection(index);
        }
        SetActivePage(pageAccount, activeButton);
    }

    private void SetActivePage(Control activePage, Button activeButton)
    {
        pageHome.IsVisible = ReferenceEquals(activePage, pageHome);
        pageConnection.IsVisible = ReferenceEquals(activePage, pageConnection);
        pageAccount.IsVisible = ReferenceEquals(activePage, pageAccount);
        pageTools.IsVisible = ReferenceEquals(activePage, pageTools);

        navHome.Classes.Set("Active", ReferenceEquals(activeButton, navHome));
        navRoutes.Classes.Set("Active", ReferenceEquals(activeButton, navRoutes));
        navAccount.Classes.Set("Active", ReferenceEquals(activeButton, navAccount));
        navPlans.Classes.Set("Active", ReferenceEquals(activeButton, navPlans));
        navOrders.Classes.Set("Active", ReferenceEquals(activeButton, navOrders));
        navTools.Classes.Set("Active", ReferenceEquals(activeButton, navTools));
    }

    public void ShowHideWindow(bool? blShow)
    {
        var bl = blShow ??
                    (Utils.IsLinux() || Utils.IsMacOS()
                    ? (!AppManager.Instance.ShowInTaskbar ^ (WindowState == WindowState.Minimized))
                    : !AppManager.Instance.ShowInTaskbar);
        if (bl)
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }
            Activate();
            Focus();
        }
        else
        {
            if (Utils.IsLinux() && _config.UiItem.Hide2TrayWhenClose == false)
            {
                WindowState = WindowState.Minimized;
                return;
            }

            foreach (var ownedWindow in OwnedWindows)
            {
                ownedWindow.Close();
            }
            Hide();
        }

        AppManager.Instance.ShowInTaskbar = bl;
    }

    protected override void OnLoaded(object? sender, RoutedEventArgs e)
    {
        base.OnLoaded(sender, e);
        if (_config.UiItem.AutoHideStartup)
        {
            ShowHideWindow(false);
        }
        RestoreUI();
    }

    private void RestoreUI()
    {
        if (_config.UiItem.MainGirdHeight1 > 0 && _config.UiItem.MainGirdHeight2 > 0)
        {
            if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
            {
                gridMain.ColumnDefinitions[0].Width = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain.ColumnDefinitions[2].Width = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
            else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
            {
                gridMain1.RowDefinitions[0].Height = new GridLength(_config.UiItem.MainGirdHeight1, GridUnitType.Star);
                gridMain1.RowDefinitions[2].Height = new GridLength(_config.UiItem.MainGirdHeight2, GridUnitType.Star);
            }
        }
    }

    private void StorageUI()
    {
        ConfigHandler.SaveWindowSizeItem(_config, GetType().Name, Width, Height);

        if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Horizontal)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain.ColumnDefinitions[0].ActualWidth, gridMain.ColumnDefinitions[2].ActualWidth);
        }
        else if (_config.UiItem.MainGirdOrientation == EGirdOrientation.Vertical)
        {
            ConfigHandler.SaveMainGirdHeight(_config, gridMain1.RowDefinitions[0].ActualHeight, gridMain1.RowDefinitions[2].ActualHeight);
        }
    }

    private void UpdateLayout(EGirdOrientation orientation)
    {
        var currentLayoutDisposables = new MultipleDisposable();
        _layoutBindingsDisposable.Create(currentLayoutDisposables);

        gridMain.IsVisible = orientation == EGirdOrientation.Horizontal;
        gridMain1.IsVisible = orientation == EGirdOrientation.Vertical;
        gridMain2.IsVisible = orientation == EGirdOrientation.Tab;

        switch (orientation)
        {
            case EGirdOrientation.Horizontal:
                this.OneWayBind(ViewModel, vm => vm.ProfilesViewModel, v => v.tabProfiles.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.MsgViewModel, v => v.tabMsgView.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ClashProxiesViewModel, v => v.tabClashProxies.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ClashConnectionsViewModel, v => v.tabClashConnections.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView.IsVisible).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies.IsVisible).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections.IsVisible).DisposeWith(currentLayoutDisposables);
                this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain.SelectedIndex).DisposeWith(currentLayoutDisposables);
                break;

            case EGirdOrientation.Vertical:
                this.OneWayBind(ViewModel, vm => vm.ProfilesViewModel, v => v.tabProfiles1.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.MsgViewModel, v => v.tabMsgView1.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ClashProxiesViewModel, v => v.tabClashProxies1.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ClashConnectionsViewModel, v => v.tabClashConnections1.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabMsgView1.IsVisible).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies1.IsVisible).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections1.IsVisible).DisposeWith(currentLayoutDisposables);
                this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain1.SelectedIndex).DisposeWith(currentLayoutDisposables);
                break;

            case EGirdOrientation.Tab:
            default:
                this.OneWayBind(ViewModel, vm => vm.ProfilesViewModel, v => v.tabProfiles2.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.MsgViewModel, v => v.tabMsgView2.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ClashProxiesViewModel, v => v.tabClashProxies2.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ClashConnectionsViewModel, v => v.tabClashConnections2.Content).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashProxies2.IsVisible).DisposeWith(currentLayoutDisposables);
                this.OneWayBind(ViewModel, vm => vm.ShowClashUI, v => v.tabClashConnections2.IsVisible).DisposeWith(currentLayoutDisposables);
                this.Bind(ViewModel, vm => vm.TabMainSelectedIndex, v => v.tabMain2.SelectedIndex).DisposeWith(currentLayoutDisposables);
                break;
        }

        RestoreUI();
    }

    #endregion UI
}
