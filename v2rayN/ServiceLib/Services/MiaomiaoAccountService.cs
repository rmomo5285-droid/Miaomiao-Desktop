using System.Net.Http.Headers;
using System.Net.Mime;

namespace ServiceLib.Services;

public sealed class MiaomiaoApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public bool IsTransient { get; }

    public MiaomiaoApiException(
        string message,
        HttpStatusCode? statusCode = null,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        IsTransient = isTransient;
    }
}

public sealed class MiaomiaoAccountService
{
    private const int MaxResponseBytes = 2 * 1024 * 1024;
    private const int MaxRequestBytes = 64 * 1024;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(5);
    private static readonly Lazy<MiaomiaoAccountService> InstanceFactory = new(() => new());
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        MaxDepth = 32
    };

    private readonly MiaomiaoEndpointManifestService _endpointService;
    private string? _bearerToken;

    public static MiaomiaoAccountService Instance => InstanceFactory.Value;
    public bool IsAuthenticated => Volatile.Read(ref _bearerToken).IsNotEmpty();

    public MiaomiaoAccountService(MiaomiaoEndpointManifestService? endpointService = null)
    {
        _endpointService = endpointService ?? MiaomiaoEndpointManifestService.Instance;
    }

    public Uri GetRegistrationUri()
    {
        var value = _endpointService.GetCurrent().RegistrationUrl;
        return TryCreateHttpsUri(value, out var uri)
            ? uri!
            : throw new MiaomiaoApiException("注册地址暂时不可用。");
    }

    public async Task<MiaomiaoLoginResult> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        email = email.Trim();
        if (email.IsNullOrEmpty() || email.Length > 254 || password.IsNullOrEmpty() || password.Length > 1024)
        {
            throw new MiaomiaoApiException("邮箱或密码格式不正确。");
        }

        Logout();
        using var document = await SendAsync(
            HttpMethod.Post,
            "/api/v1/passport/auth/login",
            new MiaomiaoLoginRequest(email, password),
            authenticated: false,
            cancellationToken);
        EnsureApiSuccess(document.RootElement);

        var token = ExtractLoginToken(document.RootElement);
        if (token.IsNullOrEmpty() || token.Length > 8192)
        {
            throw new MiaomiaoApiException(GetMessage(document.RootElement) ?? "登录响应未包含有效凭证。");
        }

        Volatile.Write(ref _bearerToken, token);
        return new MiaomiaoLoginResult(email);
    }

    public void Logout()
    {
        Volatile.Write(ref _bearerToken, null);
    }

    public async Task<MiaomiaoUserInfo> GetUserInfoAsync(CancellationToken cancellationToken = default)
    {
        using var document = await SendAsync(HttpMethod.Get, "/api/v1/user/info", null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeData<MiaomiaoUserInfo>(document.RootElement)
            ?? throw new MiaomiaoApiException(GetMessage(document.RootElement) ?? "用户信息暂时不可用。");
    }

    public async Task<MiaomiaoSubscriptionInfo> GetSubscriptionAsync(CancellationToken cancellationToken = default)
    {
        using var document = await SendAsync(HttpMethod.Get, "/api/v1/user/getSubscribe", null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeData<MiaomiaoSubscriptionInfo>(document.RootElement)
            ?? throw new MiaomiaoApiException(GetMessage(document.RootElement) ?? "订阅信息暂时不可用。");
    }

    public async Task<IReadOnlyList<MiaomiaoPlan>> GetPlansAsync(CancellationToken cancellationToken = default)
    {
        using var document = await SendAsync(HttpMethod.Get, "/api/v1/user/plan/fetch", null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeListData<MiaomiaoPlan>(document.RootElement);
    }

    public async Task<IReadOnlyList<MiaomiaoNotice>> GetNoticesAsync(CancellationToken cancellationToken = default)
    {
        using var document = await SendAsync(HttpMethod.Get, "/api/v1/user/notice/fetch", null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeListData<MiaomiaoNotice>(document.RootElement);
    }

    public async Task<IReadOnlyList<MiaomiaoOrder>> GetOrdersAsync(CancellationToken cancellationToken = default)
    {
        using var document = await SendAsync(HttpMethod.Get, "/api/v1/user/order/fetch", null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeListData<MiaomiaoOrder>(document.RootElement);
    }

    public async Task<MiaomiaoOrder> GetOrderAsync(string tradeNo, CancellationToken cancellationToken = default)
    {
        ValidateTradeNo(tradeNo);
        var path = $"/api/v1/user/order/detail?trade_no={Uri.EscapeDataString(tradeNo)}";
        using var document = await SendAsync(HttpMethod.Get, path, null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeData<MiaomiaoOrder>(document.RootElement)
            ?? throw new MiaomiaoApiException(GetMessage(document.RootElement) ?? "订单信息暂时不可用。");
    }

    public async Task<string> CreateOrderAsync(
        MiaomiaoCreateOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.PlanId <= 0 || request.Period.IsNullOrEmpty() || request.Period.Length > 64)
        {
            throw new MiaomiaoApiException("订单参数不正确。");
        }

        using var document = await SendAsync(
            HttpMethod.Post,
            "/api/v1/user/order/save",
            request,
            true,
            cancellationToken,
            allowAutomaticReplay: false);
        EnsureApiSuccess(document.RootElement);
        var data = GetData(document.RootElement);
        var tradeNo = data.ValueKind switch
        {
            JsonValueKind.String => data.GetString(),
            JsonValueKind.Number => data.GetRawText(),
            JsonValueKind.Object when TryGetProperty(data, "trade_no", out var value) => GetStringValue(value),
            _ => null
        };
        if (tradeNo.IsNullOrEmpty() || tradeNo.Length > 128)
        {
            throw new MiaomiaoApiException(GetMessage(document.RootElement) ?? "订单响应未包含订单号。");
        }
        return tradeNo;
    }

    public async Task<MiaomiaoOperationResult> CancelOrderAsync(
        string tradeNo,
        CancellationToken cancellationToken = default)
    {
        ValidateTradeNo(tradeNo);
        using var document = await SendAsync(
            HttpMethod.Post,
            "/api/v1/user/order/cancel",
            new Dictionary<string, string> { ["trade_no"] = tradeNo },
            true,
            cancellationToken,
            allowAutomaticReplay: false);
        EnsureApiSuccess(document.RootElement);
        return new(true, GetMessage(document.RootElement));
    }

    public async Task<IReadOnlyList<MiaomiaoPaymentMethod>> GetPaymentMethodsAsync(
        CancellationToken cancellationToken = default)
    {
        using var document = await SendAsync(
            HttpMethod.Get,
            "/api/v1/user/order/getPaymentMethod",
            null,
            true,
            cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return DeserializeListData<MiaomiaoPaymentMethod>(document.RootElement);
    }

    public async Task<MiaomiaoCheckoutResult> CheckoutAsync(
        MiaomiaoCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateTradeNo(request.TradeNo);
        if (request.Method.IsNullOrEmpty() || request.Method.Length > 128)
        {
            throw new MiaomiaoApiException("支付方式不可用。");
        }

        using var document = await SendAsync(
            HttpMethod.Post,
            "/api/v1/user/order/checkout",
            request,
            true,
            cancellationToken,
            allowAutomaticReplay: false);
        EnsureApiSuccess(document.RootElement);
        return ParseCheckoutResult(document.RootElement);
    }

    public Task<MiaomiaoPaymentStatus> CheckOrderStatusAsync(
        string tradeNo,
        CancellationToken cancellationToken = default)
    {
        return GetPaymentStatusAsync(tradeNo, cancellationToken);
    }

    private async Task<MiaomiaoPaymentStatus> GetPaymentStatusAsync(
        string tradeNo,
        CancellationToken cancellationToken)
    {
        ValidateTradeNo(tradeNo);
        var path = $"/api/v1/user/order/check?trade_no={Uri.EscapeDataString(tradeNo)}";
        using var document = await SendAsync(HttpMethod.Get, path, null, true, cancellationToken);
        EnsureApiSuccess(document.RootElement);
        return ParsePaymentStatus(document.RootElement);
    }

    private async Task<JsonDocument> SendAsync(
        HttpMethod method,
        string path,
        object? requestBody,
        bool authenticated,
        CancellationToken cancellationToken,
        bool allowAutomaticReplay = true)
    {
        var token = authenticated ? Volatile.Read(ref _bearerToken) : null;
        if (authenticated && token.IsNullOrEmpty())
        {
            throw new MiaomiaoApiException("请先登录。", HttpStatusCode.Unauthorized);
        }

        var endpoints = GetApiEndpoints();
        if (endpoints.Count == 0)
        {
            throw new MiaomiaoApiException("当前没有可信的服务地址。");
        }

        byte[]? requestBytes = null;
        if (requestBody != null)
        {
            requestBytes = JsonSerializer.SerializeToUtf8Bytes(requestBody, JsonOptions);
            if (requestBytes.Length > MaxRequestBytes)
            {
                CryptographicOperations.ZeroMemory(requestBytes);
                throw new MiaomiaoApiException("请求内容过大。");
            }
        }

        try
        {
            var failures = new List<Exception>();
            if (!allowAutomaticReplay)
            {
                var route = await FindAuthenticatedRouteAsync(endpoints, token!, failures, cancellationToken);
                if (route == null)
                {
                    await _endpointService.RefreshAsync(cancellationToken);
                    var refreshedEndpoints = GetApiEndpoints();
                    var endpointSet = endpoints
                        .Select(endpoint => endpoint.AbsoluteUri)
                        .ToHashSet(StringComparer.OrdinalIgnoreCase);
                    var changedEndpoints = refreshedEndpoints
                        .Where(endpoint => !endpointSet.Contains(endpoint.AbsoluteUri))
                        .ToList();
                    route = await FindAuthenticatedRouteAsync(changedEndpoints, token!, failures, cancellationToken);
                }

                if (route == null)
                {
                    throw new MiaomiaoApiException(
                        "喵喵服务暂时不可用，请稍后重试。",
                        isTransient: true,
                        innerException: failures.Count > 0 ? new AggregateException(failures) : null);
                }

                // Submit mutations exactly once. An ambiguous timeout or 5xx response
                // must never create a second order or payment session automatically.
                return await SendSingleAsync(
                    route.Endpoint,
                    method,
                    path,
                    requestBytes,
                    token,
                    route.Proxy,
                    cancellationToken);
            }

            var result = await TryEndpointRoutesAsync(
                endpoints,
                method,
                path,
                requestBytes,
                token,
                failures,
                cancellationToken);
            if (result != null)
            {
                return result;
            }

            await _endpointService.RefreshAsync(cancellationToken);
            var refreshedEndpoints = GetApiEndpoints();
            var endpointSet = endpoints
                .Select(endpoint => endpoint.AbsoluteUri)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var changedEndpoints = refreshedEndpoints
                .Where(endpoint => !endpointSet.Contains(endpoint.AbsoluteUri))
                .ToList();
            if (changedEndpoints.Count > 0)
            {
                result = await TryEndpointRoutesAsync(
                    changedEndpoints,
                    method,
                    path,
                    requestBytes,
                    token,
                    failures,
                    cancellationToken);
                if (result != null)
                {
                    return result;
                }
            }

            throw new MiaomiaoApiException(
                "喵喵服务暂时不可用，请稍后重试。",
                isTransient: true,
                innerException: failures.Count > 0 ? new AggregateException(failures) : null);
        }
        finally
        {
            if (requestBytes != null)
            {
                CryptographicOperations.ZeroMemory(requestBytes);
            }
        }
    }

    private async Task<MiaomiaoEndpointRoute?> FindAuthenticatedRouteAsync(
        IReadOnlyList<Uri> endpoints,
        string token,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        if (endpoints.Count == 0)
        {
            return null;
        }

        var direct = await FindAuthenticatedRouteAsync(
            endpoints,
            token,
            proxy: null,
            failures,
            cancellationToken);
        if (direct != null)
        {
            return direct;
        }

        var localProxy = await TryGetLocalProxyAsync(cancellationToken);
        return localProxy == null
            ? null
            : await FindAuthenticatedRouteAsync(endpoints, token, localProxy, failures, cancellationToken);
    }

    private async Task<MiaomiaoEndpointRoute?> FindAuthenticatedRouteAsync(
        IReadOnlyList<Uri> endpoints,
        string token,
        IWebProxy? proxy,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var probe = await SendSingleAsync(
                    endpoint,
                    HttpMethod.Get,
                    "/api/v1/user/info",
                    requestBytes: null,
                    token,
                    proxy,
                    cancellationToken);
                EnsureApiSuccess(probe.RootElement);
                return new(endpoint, proxy);
            }
            catch (MiaomiaoApiException ex) when (ex.IsTransient)
            {
                failures.Add(ex);
            }
            catch (HttpRequestException ex)
            {
                failures.Add(ex);
            }
            catch (TimeoutException ex)
            {
                failures.Add(ex);
            }
        }

        return null;
    }

    private async Task<JsonDocument?> TryEndpointRoutesAsync(
        IReadOnlyList<Uri> endpoints,
        HttpMethod method,
        string path,
        byte[]? requestBytes,
        string? token,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        var directResult = await TryEndpointsAsync(
            endpoints,
            method,
            path,
            requestBytes,
            token,
            proxy: null,
            failures,
            cancellationToken);
        if (directResult != null)
        {
            return directResult;
        }

        var localProxy = await TryGetLocalProxyAsync(cancellationToken);
        if (localProxy == null)
        {
            return null;
        }
        return await TryEndpointsAsync(
            endpoints,
            method,
            path,
            requestBytes,
            token,
            localProxy,
            failures,
            cancellationToken);
    }

    private async Task<JsonDocument?> TryEndpointsAsync(
        IReadOnlyList<Uri> endpoints,
        HttpMethod method,
        string path,
        byte[]? requestBytes,
        string? token,
        IWebProxy? proxy,
        List<Exception> failures,
        CancellationToken cancellationToken)
    {
        foreach (var endpoint in endpoints)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return await SendSingleAsync(endpoint, method, path, requestBytes, token, proxy, cancellationToken);
            }
            catch (MiaomiaoApiException ex) when (ex.IsTransient)
            {
                failures.Add(ex);
            }
            catch (HttpRequestException ex)
            {
                failures.Add(ex);
            }
            catch (TimeoutException ex)
            {
                failures.Add(ex);
            }
        }
        return null;
    }

    private async Task<JsonDocument> SendSingleAsync(
        Uri endpoint,
        HttpMethod method,
        string path,
        byte[]? requestBytes,
        string? token,
        IWebProxy? proxy,
        CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.Deflate | DecompressionMethods.GZip,
            ConnectTimeout = ConnectTimeout,
            Proxy = proxy,
            UseCookies = false,
            UseProxy = proxy != null
        };
        using var client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        using var request = new HttpRequestMessage(method, BuildRequestUri(endpoint, path));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue(MediaTypeNames.Application.Json));
        request.Headers.UserAgent.TryParseAdd($"Miaomiao/{Utils.GetVersion(false)}");
        if (token.IsNotEmpty())
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
        if (requestBytes != null)
        {
            request.Content = new ByteArrayContent(requestBytes);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(MediaTypeNames.Application.Json)
            {
                CharSet = Encoding.UTF8.WebName
            };
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(RequestTimeout);
        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        }
        catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            throw new TimeoutException("服务请求超时。", ex);
        }

        using (response)
        {
            byte[] responseBytes;
            try
            {
                responseBytes = await ReadResponseBytesAsync(response.Content, timeout.Token);
            }
            catch (OperationCanceledException ex) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("服务响应超时。", ex);
            }
            if (!response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.Unauthorized && token.IsNotEmpty())
                {
                    Logout();
                }

                var message = TryGetMessage(responseBytes) ?? $"服务返回 HTTP {(int)response.StatusCode}。";
                throw new MiaomiaoApiException(
                    message,
                    response.StatusCode,
                    IsTransientStatus(response.StatusCode));
            }

            try
            {
                return JsonDocument.Parse(responseBytes, new JsonDocumentOptions { MaxDepth = 32 });
            }
            catch (JsonException ex)
            {
                throw new MiaomiaoApiException("服务返回了无效数据。", isTransient: true, innerException: ex);
            }
        }
    }

    private IReadOnlyList<Uri> GetApiEndpoints()
    {
        return _endpointService.GetCurrent().ApiEndpoints
            .Select(value => TryCreateHttpsUri(value, out var uri) ? uri : null)
            .Where(uri => uri != null && uri.Query.IsNullOrEmpty() && uri.Fragment.IsNullOrEmpty())
            .Cast<Uri>()
            .DistinctBy(uri => uri.AbsoluteUri, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static Uri BuildRequestUri(Uri endpoint, string path)
    {
        return new Uri($"{endpoint.AbsoluteUri.TrimEnd('/')}/{path.TrimStart('/')}", UriKind.Absolute);
    }

    private static async Task<IWebProxy?> TryGetLocalProxyAsync(CancellationToken cancellationToken)
    {
        // Both Xray and sing-box expose the primary v2rayN mixed inbound on this port.
        var port = AppManager.Instance.GetLocalPort(EInboundProtocol.socks);
        if (port is <= 0 or > 65535 || !await CanUseSocks5LoopbackAsync(port, cancellationToken))
        {
            return null;
        }
        return new WebProxy($"socks5://{Global.Loopback}:{port}");
    }

    private static async Task<bool> CanUseSocks5LoopbackAsync(int port, CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(800));
        try
        {
            using var client = new TcpClient(AddressFamily.InterNetwork);
            await client.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
            await using var stream = client.GetStream();
            await stream.WriteAsync(new byte[] { 0x05, 0x01, 0x00 }, timeout.Token);
            var response = new byte[2];
            var received = 0;
            while (received < response.Length)
            {
                var read = await stream.ReadAsync(response.AsMemory(received), timeout.Token);
                if (read == 0)
                {
                    return false;
                }
                received += read;
            }
            return response[0] == 0x05 && response[1] == 0x00;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
        catch (SocketException)
        {
            return false;
        }
        catch (IOException)
        {
            return false;
        }
    }

    private static async Task<byte[]> ReadResponseBytesAsync(HttpContent content, CancellationToken cancellationToken)
    {
        if (content.Headers.ContentLength > MaxResponseBytes)
        {
            throw new MiaomiaoApiException("服务响应过大。", isTransient: true);
        }

        await using var input = await content.ReadAsStreamAsync(cancellationToken);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            var read = await input.ReadAsync(buffer.AsMemory(), cancellationToken);
            if (read == 0)
            {
                break;
            }
            if (output.Length + read > MaxResponseBytes)
            {
                throw new MiaomiaoApiException("服务响应过大。", isTransient: true);
            }
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static T? DeserializeData<T>(JsonElement root)
    {
        var data = GetData(root);
        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return default;
        }
        return data.Deserialize<T>(JsonOptions);
    }

    private static IReadOnlyList<T> DeserializeListData<T>(JsonElement root)
    {
        var data = GetData(root);
        if (data.ValueKind == JsonValueKind.Object && TryGetProperty(data, "data", out var nested))
        {
            data = nested;
        }
        if (data.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return [];
        }
        if (data.ValueKind != JsonValueKind.Array)
        {
            throw new MiaomiaoApiException(GetMessage(root) ?? "服务返回的列表格式不正确。");
        }
        return data.Deserialize<List<T>>(JsonOptions) ?? [];
    }

    private static JsonElement GetData(JsonElement root)
    {
        return root.ValueKind == JsonValueKind.Object && TryGetProperty(root, "data", out var data)
            ? data
            : root;
    }

    private static void EnsureApiSuccess(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        if (TryGetProperty(root, "success", out var success)
            && (success.ValueKind == JsonValueKind.False
                || (TryGetInt32(success, out var successValue) && successValue == 0)))
        {
            throw new MiaomiaoApiException(GetMessage(root) ?? "服务请求失败。");
        }
        if (TryGetProperty(root, "status", out var status)
            && (status.ValueKind == JsonValueKind.False
                || (status.ValueKind == JsonValueKind.String
                    && status.GetString() is { } value
                    && (value.Equals("fail", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("failed", StringComparison.OrdinalIgnoreCase)
                        || value.Equals("error", StringComparison.OrdinalIgnoreCase)))))
        {
            throw new MiaomiaoApiException(GetMessage(root) ?? "服务请求失败。");
        }
    }

    private static MiaomiaoCheckoutResult ParseCheckoutResult(JsonElement root)
    {
        var container = root;
        var data = GetData(root);
        if (data.ValueKind == JsonValueKind.Object && TryGetProperty(data, "type", out _))
        {
            container = data;
            data = TryGetProperty(container, "data", out var nested) ? nested : default;
        }

        var type = TryGetProperty(container, "type", out var typeElement) && TryGetInt32(typeElement, out var typeValue)
            ? typeValue
            : 0;
        var completed = type == -1 || data.ValueKind == JsonValueKind.True;
        var paymentUrl = FindHttpsUrl(data);
        return new(type, completed, paymentUrl, GetMessage(root));
    }

    private static MiaomiaoPaymentStatus ParsePaymentStatus(JsonElement root)
    {
        var data = GetData(root);
        if (TryGetInt32(data, out var statusCode))
        {
            return new(MiaomiaoOrderPolicy.GetPaymentState(statusCode), statusCode, GetMessage(root));
        }
        if (data.ValueKind == JsonValueKind.Object)
        {
            if (TryGetProperty(data, "status", out var status) && TryGetInt32(status, out statusCode))
            {
                return new(MiaomiaoOrderPolicy.GetPaymentState(statusCode), statusCode, GetMessage(data) ?? GetMessage(root));
            }
            if (GetBoolean(data, "isSuccess", "is_success"))
            {
                return new(MiaomiaoPaymentState.Completed, null, GetMessage(data) ?? GetMessage(root));
            }
            if (GetBoolean(data, "isCanceled", "is_canceled"))
            {
                return new(MiaomiaoPaymentState.Canceled, null, GetMessage(data) ?? GetMessage(root));
            }
            if (GetBoolean(data, "isPending", "is_pending"))
            {
                return new(MiaomiaoPaymentState.Pending, null, GetMessage(data) ?? GetMessage(root));
            }
        }
        return new(MiaomiaoPaymentState.Unknown, null, GetMessage(root));
    }

    private static bool GetBoolean(JsonElement element, params string[] names)
    {
        foreach (var name in names)
        {
            if (!TryGetProperty(element, name, out var value))
            {
                continue;
            }
            if (value.ValueKind == JsonValueKind.True)
            {
                return true;
            }
            if (TryGetInt32(value, out var number))
            {
                return number != 0;
            }
        }
        return false;
    }

    private static string? FindHttpsUrl(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return TryCreateHttpsUri(element.GetString(), out var uri) ? uri!.AbsoluteUri : null;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (var name in new[] { "url", "payment_url", "checkout_url" })
        {
            if (TryGetProperty(element, name, out var value)
                && TryCreateHttpsUri(GetStringValue(value), out var uri))
            {
                return uri!.AbsoluteUri;
            }
        }
        return null;
    }

    private static string? GetMessage(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }
        foreach (var name in new[] { "message", "msg", "error" })
        {
            if (TryGetProperty(element, name, out var value) && value.ValueKind == JsonValueKind.String)
            {
                var message = value.GetString()?.Trim();
                return message is { Length: > 0 and <= 1024 } ? message : null;
            }
        }
        return null;
    }

    private static string? TryGetMessage(byte[] responseBytes)
    {
        try
        {
            using var document = JsonDocument.Parse(responseBytes, new JsonDocumentOptions { MaxDepth = 16 });
            return GetMessage(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool TryGetProperty(JsonElement element, string name, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    value = property.Value;
                    return true;
                }
            }
        }
        value = default;
        return false;
    }

    private static bool TryGetInt32(JsonElement value, out int result)
    {
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out result))
        {
            return true;
        }
        return value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out result);
    }

    private static string? GetStringValue(JsonElement value)
    {
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static bool TryCreateHttpsUri(string? value, out Uri? uri)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out uri)
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && uri.HostNameType != UriHostNameType.Unknown
            && uri.UserInfo.IsNullOrEmpty();
    }

    private static string? NormalizeToken(string? token)
    {
        token = token?.Trim();
        const string bearer = "Bearer ";
        return token?.StartsWith(bearer, StringComparison.OrdinalIgnoreCase) == true
            ? token[bearer.Length..].Trim()
            : token;
    }

    internal static string? ExtractLoginToken(JsonElement root)
    {
        var data = GetData(root);
        if (data.ValueKind == JsonValueKind.String)
        {
            return NormalizeToken(data.GetString());
        }
        if (data.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var loginData = data.Deserialize<MiaomiaoLoginResponseData>(JsonOptions);
        var token = loginData?.AuthData.IsNotEmpty() == true ? loginData.AuthData : loginData?.Token;
        return NormalizeToken(token);
    }

    private static bool IsTransientStatus(HttpStatusCode statusCode)
    {
        var numericStatus = (int)statusCode;
        return numericStatus is >= 300 and < 400
            || statusCode is HttpStatusCode.Forbidden
            or HttpStatusCode.NotFound
            or HttpStatusCode.RequestTimeout
            or HttpStatusCode.BadGateway
            or HttpStatusCode.ServiceUnavailable
            or HttpStatusCode.GatewayTimeout
            || numericStatus >= 500;
    }

    private sealed record MiaomiaoEndpointRoute(Uri Endpoint, IWebProxy? Proxy);

    private static void ValidateTradeNo(string tradeNo)
    {
        if (tradeNo.IsNullOrEmpty() || tradeNo.Length > 128)
        {
            throw new MiaomiaoApiException("订单号不正确。");
        }
    }
}
