namespace ServiceLib.ViewModels;

public partial class MiaomiaoAccountViewModel : MyReactiveObject
{
    private const int MaxPaymentPollingAttempts = 120;
    private static readonly TimeSpan PaymentPollingInterval = TimeSpan.FromSeconds(5);
    private readonly MiaomiaoAccountService _accountService;
    private readonly SemaphoreSlim _paymentCheckGate = new(1, 1);
    private CancellationTokenSource? _paymentPollingCts;
    private string? _paymentPollingTradeNo;

    public BulkObservableCollection<MiaomiaoPlan> Plans { get; } = [];
    public BulkObservableCollection<MiaomiaoNotice> Notices { get; } = [];
    public BulkObservableCollection<MiaomiaoOrder> Orders { get; } = [];
    public BulkObservableCollection<MiaomiaoPaymentMethod> PaymentMethods { get; } = [];

    [Reactive]
    public partial string Email { get; set; } = string.Empty;

    [Reactive]
    public partial string Password { get; set; } = string.Empty;

    [Reactive]
    public partial bool IsLoggedIn { get; set; }

    [Reactive]
    public partial bool IsBusy { get; set; }

    [Reactive]
    public partial string? StatusMessage { get; set; }

    [Reactive]
    public partial string? ErrorMessage { get; set; }

    [Reactive]
    public partial string? RegistrationUrl { get; set; }

    [Reactive]
    public partial MiaomiaoUserInfo? CurrentUser { get; set; }

    [Reactive]
    public partial MiaomiaoSubscriptionInfo? CurrentSubscription { get; set; }

    [Reactive]
    public partial MiaomiaoPlan? SelectedPlan { get; set; }

    [Reactive]
    public partial string SelectedPeriod { get; set; } = "month_price";

    [Reactive]
    public partial string CouponCode { get; set; } = string.Empty;

    [Reactive]
    public partial MiaomiaoPaymentMethod? SelectedPaymentMethod { get; set; }

    [Reactive]
    public partial string? PendingTradeNo { get; set; }

    [Reactive]
    public partial string? PaymentUrl { get; set; }

    [Reactive]
    public partial MiaomiaoPaymentStatus? CurrentPaymentStatus { get; set; }

    public ReactiveCommand<RxVoid, RxVoid> LoginCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> LogoutCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> RefreshCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenRegistrationCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> PurchaseCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> PayCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> OpenPaymentCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CheckPaymentCmd { get; }
    public ReactiveCommand<RxVoid, RxVoid> CancelPaymentCmd { get; }

    public MiaomiaoAccountViewModel(MiaomiaoAccountService? accountService = null)
    {
        _config = AppManager.Instance.Config;
        _accountService = accountService ?? MiaomiaoAccountService.Instance;
        IsLoggedIn = _accountService.IsAuthenticated;
        RefreshRegistrationUrl();
        if (!IsLoggedIn)
        {
            _ = DetachManagedSubscriptionAsync();
        }

        var canLogin = this.WhenAnyValue(
            x => x.Email,
            x => x.Password,
            x => x.IsBusy,
            (email, password, busy) => !busy && email.IsNotEmpty() && password.IsNotEmpty());
        var canRefresh = this.WhenAnyValue(
            x => x.IsLoggedIn,
            x => x.IsBusy,
            (loggedIn, busy) => loggedIn && !busy);
        var canPurchase = this.WhenAnyValue(
            x => x.IsLoggedIn,
            x => x.IsBusy,
            x => x.SelectedPlan,
            x => x.SelectedPeriod,
            x => x.PendingTradeNo,
            (loggedIn, busy, plan, period, tradeNo) => loggedIn
                && !busy
                && plan != null
                && period.IsNotEmpty()
                && tradeNo.IsNullOrEmpty());
        var canPay = this.WhenAnyValue(
            x => x.IsLoggedIn,
            x => x.IsBusy,
            x => x.PendingTradeNo,
            x => x.SelectedPaymentMethod,
            x => x.CurrentPaymentStatus,
            (loggedIn, busy, tradeNo, method, status) => loggedIn
                && !busy
                && tradeNo.IsNotEmpty()
                && method != null
                && status?.State is null or MiaomiaoPaymentState.Pending);
        var canOpenPayment = this.WhenAnyValue(
            x => x.PaymentUrl,
            x => x.IsBusy,
            (url, busy) => !busy && url.IsNotEmpty());
        var canCheckPayment = this.WhenAnyValue(
            x => x.IsLoggedIn,
            x => x.IsBusy,
            x => x.PendingTradeNo,
            (loggedIn, busy, tradeNo) => loggedIn && !busy && tradeNo.IsNotEmpty());
        var canCancelPayment = this.WhenAnyValue(
            x => x.IsLoggedIn,
            x => x.IsBusy,
            x => x.PendingTradeNo,
            x => x.CurrentPaymentStatus,
            (loggedIn, busy, tradeNo, status) => loggedIn
                && !busy
                && tradeNo.IsNotEmpty()
                && status?.State == MiaomiaoPaymentState.Pending);

        LoginCmd = ReactiveCommand.CreateFromTask(LoginAsync, canLogin);
        LogoutCmd = ReactiveCommand.CreateFromTask(LogoutAsync);
        RefreshCmd = ReactiveCommand.CreateFromTask(RefreshAsync, canRefresh);
        OpenRegistrationCmd = ReactiveCommand.CreateFromTask(OpenRegistrationAsync);
        PurchaseCmd = ReactiveCommand.CreateFromTask(PurchaseAsync, canPurchase);
        PayCmd = ReactiveCommand.CreateFromTask(PayAsync, canPay);
        OpenPaymentCmd = ReactiveCommand.CreateFromTask(OpenPaymentAsync, canOpenPayment);
        CheckPaymentCmd = ReactiveCommand.CreateFromTask(CheckPaymentAsync, canCheckPayment);
        CancelPaymentCmd = ReactiveCommand.CreateFromTask(CancelPaymentAsync, canCancelPayment);

        foreach (var command in new[]
                 {
                     LoginCmd,
                     LogoutCmd,
                     RefreshCmd,
                     OpenRegistrationCmd,
                     PurchaseCmd,
                     PayCmd,
                     OpenPaymentCmd,
                     CheckPaymentCmd,
                     CancelPaymentCmd
                 })
        {
            command.ThrownExceptions.Subscribe(HandleUnexpectedCommandError);
        }
    }

    public async Task<bool> SyncManagedSubscriptionAsync(bool forceUpdate = false)
    {
        var url = CurrentSubscription?.SubscribeUrl?.Trim();
        if (!TryCreateHttpsUri(url, out _))
        {
            ErrorMessage = "托管订阅地址不可用或不是安全的 HTTPS 地址。";
            return false;
        }

        var subItems = await AppManager.Instance.SubItems() ?? [];
        var managed = subItems.FirstOrDefault(MiaomiaoManagedSubscriptionPolicy.IsManaged)
            ?? subItems.FirstOrDefault(item => string.Equals(item.Url, url, StringComparison.Ordinal));
        managed ??= new SubItem
        {
            Id = string.Empty,
            Remarks = MiaomiaoManagedSubscriptionPolicy.DisplayName,
            Url = url!,
            MoreUrl = string.Empty,
            UserAgent = string.Empty,
            Enabled = true
        };

        var urlChanged = !MiaomiaoManagedSubscriptionPolicy.MatchesSource(managed, url!);
        managed.Remarks = MiaomiaoManagedSubscriptionPolicy.DisplayName;
        managed.Url = url!;
        managed.MoreUrl = string.Empty;
        managed.Enabled = true;
        managed.UserAgent = $"Miaomiao/{Utils.GetVersion(false)}";
        managed.AutoUpdateInterval = MiaomiaoManagedSubscriptionPolicy.UpdateIntervalMinutes;
        MiaomiaoManagedSubscriptionPolicy.Attach(managed, url!);
        if (urlChanged)
        {
            managed.UpdateTime = 0;
            managed.NextAttemptTime = 0;
            managed.ConsecutiveFailures = 0;
        }

        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var updateIsDue = managed.UpdateTime <= 0
            || now - managed.UpdateTime >= MiaomiaoManagedSubscriptionPolicy.UpdateIntervalMinutes * 60L;
        var shouldUpdate = forceUpdate
            || urlChanged
            || (updateIsDue && managed.NextAttemptTime <= now);

        if (await ConfigHandler.AddSubItem(_config!, managed) != 0)
        {
            ErrorMessage = "保存托管订阅失败。";
            return false;
        }
        if (!shouldUpdate)
        {
            return true;
        }

        var updated = false;
        await SubscriptionHandler.UpdateProcess(
            _config!,
            managed.Id,
            blProxy: true,
            async (success, message) =>
            {
                updated |= success;
                StatusMessage = message;
                await Task.CompletedTask;
            });
        if (!updated)
        {
            var latest = await AppManager.Instance.GetSubItem(managed.Id) ?? managed;
            latest.ConsecutiveFailures++;
            var retryMinutes = latest.ConsecutiveFailures switch
            {
                1 => 15,
                2 => 60,
                _ => 360
            };
            latest.NextAttemptTime = now + retryMinutes * 60L;
            await ConfigHandler.AddSubItem(_config!, latest);
            StatusMessage = "订阅更新失败，已继续使用本地缓存节点。";
        }
        if (updated)
        {
            AppEvents.MiaomiaoManagedSubscriptionUpdated.Publish();
        }
        return updated;
    }

    public async Task OnWindowActivatedAsync()
    {
        if (!IsLoggedIn || IsBusy)
        {
            return;
        }

        try
        {
            if (CurrentUser == null)
            {
                await RunBusyAsync(() => RefreshAccountCoreAsync(forceSubscriptionUpdate: false));
                return;
            }
            if (PendingTradeNo.IsNullOrEmpty())
            {
                return;
            }
            var state = await CheckPaymentCoreAsync(CancellationToken.None);
            if (state == MiaomiaoPaymentState.Processing)
            {
                StartPaymentPolling();
            }
        }
        catch (Exception ex)
        {
            if (!_accountService.IsAuthenticated)
            {
                await DetachManagedSubscriptionAsync();
                ApplyLoggedOutState();
            }
            ErrorMessage = ex.Message;
            Logging.SaveLog("Miaomiao payment activation check", ex);
        }
    }

    private async Task LoginAsync()
    {
        await RunBusyAsync(async () =>
        {
            _accountService.Logout();
            IsLoggedIn = false;
            try
            {
                await _accountService.LoginAsync(Email, Password);
                IsLoggedIn = true;
                await RefreshAccountCoreAsync(forceSubscriptionUpdate: false);
            }
            finally
            {
                Password = string.Empty;
            }
        });
    }

    private async Task LogoutAsync()
    {
        await RunBusyAsync(async () =>
        {
            _accountService.Logout();
            await DetachManagedSubscriptionAsync();
            ApplyLoggedOutState();
            StatusMessage = "已退出登录，本地缓存节点仍可继续使用。";
        });
    }

    private async Task RefreshAsync()
    {
        await RunBusyAsync(() => RefreshAccountCoreAsync(forceSubscriptionUpdate: false));
    }

    private async Task RefreshAccountCoreAsync(bool forceSubscriptionUpdate)
    {
        var userTask = _accountService.GetUserInfoAsync();
        var subscriptionTask = _accountService.GetSubscriptionAsync();
        var plansTask = _accountService.GetPlansAsync();
        var noticesTask = _accountService.GetNoticesAsync();
        var ordersTask = _accountService.GetOrdersAsync();
        var paymentMethodsTask = _accountService.GetPaymentMethodsAsync();
        await Task.WhenAll(userTask, subscriptionTask, plansTask, noticesTask, ordersTask, paymentMethodsTask);

        CurrentUser = await userTask;
        CurrentSubscription = await subscriptionTask;
        ReplaceItems(Plans, (await plansTask).Where(plan => plan.Show));
        ReplaceItems(Notices, (await noticesTask).Where(notice => notice.Show));
        var orders = await ordersTask;
        ReplaceItems(Orders, orders);
        ReplaceItems(PaymentMethods, (await paymentMethodsTask).Where(method => method.Show && method.IsAvailable));
        RestoreRecoverableOrder(orders);
        SelectedPlan = Plans.FirstOrDefault();
        SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
        Email = CurrentUser.Email.IsNotEmpty() ? CurrentUser.Email : Email;
        IsLoggedIn = true;

        var subscriptionReady = await SyncManagedSubscriptionAsync(forceSubscriptionUpdate);
        StatusMessage = PendingTradeNo.IsNotEmpty()
            ? GetPendingOrderMessage(CurrentPaymentStatus?.State)
            : subscriptionReady
                ? "账户数据已更新。"
                : "账户数据已刷新，当前继续使用缓存节点。";
        if (CurrentPaymentStatus?.State == MiaomiaoPaymentState.Processing)
        {
            StartPaymentPolling();
        }
    }

    private async Task PurchaseAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (SelectedPlan == null)
            {
                throw new MiaomiaoApiException("请先选择套餐。");
            }
            if (SelectedPlan.GetPrice(SelectedPeriod) == null)
            {
                throw new MiaomiaoApiException("该套餐不支持所选周期。");
            }

            var coupon = CouponCode.Trim();
            PendingTradeNo = await _accountService.CreateOrderAsync(new(
                SelectedPlan.Id,
                SelectedPeriod,
                coupon.IsNotEmpty() ? coupon : null));
            PaymentUrl = null;
            CurrentPaymentStatus = new(MiaomiaoPaymentState.Pending, 0);

            ReplaceItems(PaymentMethods, (await _accountService.GetPaymentMethodsAsync())
                .Where(method => method.Show && method.IsAvailable));
            SelectedPaymentMethod = PaymentMethods.FirstOrDefault();
            StatusMessage = "订单已创建，请选择支付方式。";
        });
    }

    private async Task PayAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (PendingTradeNo.IsNullOrEmpty() || SelectedPaymentMethod == null)
            {
                throw new MiaomiaoApiException("订单或支付方式不可用。");
            }

            var result = await _accountService.CheckoutAsync(new(
                PendingTradeNo,
                SelectedPaymentMethod.Id));
            PaymentUrl = result.PaymentUrl;
            if (result.Completed)
            {
                CurrentPaymentStatus = new(MiaomiaoPaymentState.Completed, 3, result.Message);
                ClearPendingPaymentState();
                await RefreshAccountCoreAsync(forceSubscriptionUpdate: true);
                StatusMessage = result.Message ?? "订单已完成，订阅已更新。";
                return;
            }
            if (PaymentUrl.IsNotEmpty())
            {
                OpenHttpsUrl(PaymentUrl);
                StatusMessage = result.Message ?? "已在系统浏览器打开支付页面。";
                StartPaymentPolling();
                return;
            }
            throw new MiaomiaoApiException(result.Message ?? "支付响应未包含安全的 HTTPS 地址。");
        });
    }

    private async Task CheckPaymentAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (PendingTradeNo.IsNullOrEmpty())
            {
                throw new MiaomiaoApiException("当前没有待支付订单。");
            }
            await CheckPaymentCoreAsync(CancellationToken.None);
        });
    }

    private async Task CancelPaymentAsync()
    {
        await RunBusyAsync(async () =>
        {
            if (PendingTradeNo.IsNullOrEmpty()
                || CurrentPaymentStatus?.State != MiaomiaoPaymentState.Pending)
            {
                throw new MiaomiaoApiException("当前订单不能取消。处理中订单需要等待服务端确认。");
            }

            var tradeNo = PendingTradeNo;
            var result = await _accountService.CancelOrderAsync(tradeNo);
            ClearPendingPaymentState();
            await RefreshAccountCoreAsync(forceSubscriptionUpdate: false);
            StatusMessage = result.Message ?? "订单已取消。";
        });
    }

    private async Task<MiaomiaoPaymentState?> CheckPaymentCoreAsync(CancellationToken cancellationToken)
    {
        await _paymentCheckGate.WaitAsync(cancellationToken);
        try
        {
            var tradeNo = PendingTradeNo;
            if (tradeNo.IsNullOrEmpty())
            {
                return null;
            }

            var status = await _accountService.CheckOrderStatusAsync(tradeNo, cancellationToken);
            if (!string.Equals(PendingTradeNo, tradeNo, StringComparison.Ordinal))
            {
                return null;
            }

            CurrentPaymentStatus = status;
            switch (status.State)
            {
                case MiaomiaoPaymentState.Completed:
                    ClearPendingPaymentState();
                    await RefreshAccountCoreAsync(forceSubscriptionUpdate: true);
                    StatusMessage = status.Message ?? "订单已完成，订阅已更新。";
                    break;
                case MiaomiaoPaymentState.Canceled:
                case MiaomiaoPaymentState.Failed:
                    ClearPendingPaymentState();
                    await RefreshAccountCoreAsync(forceSubscriptionUpdate: false);
                    StatusMessage = status.Message ?? "订单已结束。";
                    break;
                default:
                    StatusMessage = status.Message ?? GetPendingOrderMessage(status.State);
                    break;
            }
            return status.State;
        }
        finally
        {
            _paymentCheckGate.Release();
        }
    }

    private void StartPaymentPolling()
    {
        var tradeNo = PendingTradeNo;
        if (tradeNo.IsNullOrEmpty())
        {
            return;
        }
        if (_paymentPollingCts is { IsCancellationRequested: false }
            && string.Equals(_paymentPollingTradeNo, tradeNo, StringComparison.Ordinal))
        {
            return;
        }

        StopPaymentPolling();
        _paymentPollingTradeNo = tradeNo;
        _paymentPollingCts = new CancellationTokenSource();
        _ = PollPaymentAsync(tradeNo, _paymentPollingCts.Token);
    }

    private async Task PollPaymentAsync(string tradeNo, CancellationToken cancellationToken)
    {
        try
        {
            for (var attempt = 0; attempt < MaxPaymentPollingAttempts; attempt++)
            {
                await Task.Delay(PaymentPollingInterval, cancellationToken);
                if (!IsLoggedIn || !string.Equals(PendingTradeNo, tradeNo, StringComparison.Ordinal))
                {
                    return;
                }

                MiaomiaoPaymentState? state;
                try
                {
                    state = await CheckPaymentCoreAsync(cancellationToken);
                }
                catch (MiaomiaoApiException ex) when (ex.IsTransient)
                {
                    continue;
                }
                if (state is MiaomiaoPaymentState.Completed
                    or MiaomiaoPaymentState.Canceled
                    or MiaomiaoPaymentState.Failed)
                {
                    return;
                }
            }

            if (string.Equals(PendingTradeNo, tradeNo, StringComparison.Ordinal))
            {
                StatusMessage = "自动查询已暂停，可手动查询订单状态。";
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            if (!_accountService.IsAuthenticated)
            {
                await DetachManagedSubscriptionAsync();
                ApplyLoggedOutState();
            }
            ErrorMessage = ex.Message;
            Logging.SaveLog("Miaomiao payment polling", ex);
        }
    }

    private void RestoreRecoverableOrder(IEnumerable<MiaomiaoOrder> orders)
    {
        var order = MiaomiaoOrderPolicy.FindRecoverableOrder(orders);
        if (order == null)
        {
            if (CurrentPaymentStatus?.State is MiaomiaoPaymentState.Completed
                or MiaomiaoPaymentState.Canceled
                or MiaomiaoPaymentState.Failed)
            {
                ClearPendingPaymentState();
            }
            return;
        }

        if (!string.Equals(PendingTradeNo, order.TradeNo, StringComparison.Ordinal))
        {
            PaymentUrl = null;
        }
        PendingTradeNo = order.TradeNo;
        CurrentPaymentStatus = new(
            MiaomiaoOrderPolicy.GetPaymentState(order.Status),
            order.Status);
    }

    private void ClearPendingPaymentState()
    {
        StopPaymentPolling();
        PendingTradeNo = null;
        PaymentUrl = null;
        CurrentPaymentStatus = null;
    }

    private void StopPaymentPolling()
    {
        _paymentPollingTradeNo = null;
        var polling = Interlocked.Exchange(ref _paymentPollingCts, null);
        if (polling == null)
        {
            return;
        }
        polling.Cancel();
        polling.Dispose();
    }

    private static string GetPendingOrderMessage(MiaomiaoPaymentState? state) => state switch
    {
        MiaomiaoPaymentState.Processing => "订单正在处理中，客户端会在限定时间内自动查询。",
        MiaomiaoPaymentState.Pending => "存在待支付订单，可继续支付或取消。",
        _ => "存在未完成订单，请查询最新状态。"
    };

    private Task OpenRegistrationAsync()
    {
        try
        {
            var uri = _accountService.GetRegistrationUri();
            RegistrationUrl = uri.AbsoluteUri;
            ProcUtils.ProcessStart(uri.AbsoluteUri);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return Task.CompletedTask;
    }

    private Task OpenPaymentAsync()
    {
        try
        {
            OpenHttpsUrl(PaymentUrl);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        return Task.CompletedTask;
    }

    private async Task RunBusyAsync(Func<Task> action)
    {
        if (IsBusy)
        {
            return;
        }
        IsBusy = true;
        ErrorMessage = null;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            if (!_accountService.IsAuthenticated)
            {
                await DetachManagedSubscriptionAsync();
                ApplyLoggedOutState();
            }
            ErrorMessage = ex.Message;
            Logging.SaveLog("Miaomiao account operation", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyLoggedOutState()
    {
        StopPaymentPolling();
        Password = string.Empty;
        IsLoggedIn = false;
        CurrentUser = null;
        CurrentSubscription = null;
        SelectedPlan = null;
        SelectedPaymentMethod = null;
        PendingTradeNo = null;
        PaymentUrl = null;
        CurrentPaymentStatus = null;
        Plans.Clear();
        Notices.Clear();
        Orders.Clear();
        PaymentMethods.Clear();
    }

    private async Task DetachManagedSubscriptionAsync()
    {
        try
        {
            var managed = (await AppManager.Instance.SubItems())
                .FirstOrDefault(MiaomiaoManagedSubscriptionPolicy.IsManaged);
            if (managed is null)
            {
                return;
            }

            MiaomiaoManagedSubscriptionPolicy.Detach(managed);
            await ConfigHandler.AddSubItem(_config!, managed);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Miaomiao managed subscription detach", ex);
        }
    }

    private void RefreshRegistrationUrl()
    {
        try
        {
            RegistrationUrl = _accountService.GetRegistrationUri().AbsoluteUri;
        }
        catch
        {
            RegistrationUrl = null;
        }
    }

    private void HandleUnexpectedCommandError(Exception exception)
    {
        ErrorMessage = exception.Message;
        Logging.SaveLog("Miaomiao account command", exception);
    }

    private static void OpenHttpsUrl(string? value)
    {
        if (!TryCreateHttpsUri(value, out var uri))
        {
            throw new MiaomiaoApiException("只能打开安全的 HTTPS 地址。");
        }
        ProcUtils.ProcessStart(uri!.AbsoluteUri);
    }

    private static bool TryCreateHttpsUri(string? value, out Uri? uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri)
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && uri.HostNameType != UriHostNameType.Unknown
            && uri.UserInfo.IsNullOrEmpty();
    }

    private static void ReplaceItems<T>(BulkObservableCollection<T> target, IEnumerable<T> items)
    {
        target.Clear();
        target.AddRange(items);
    }
}
