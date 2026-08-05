namespace ServiceLib.Models.Dto;

public sealed record MiaomiaoLoginRequest(
    [property: JsonPropertyName("email")] string Email,
    [property: JsonPropertyName("password")] string Password);

public sealed record MiaomiaoLoginResult(string Email);

public sealed record MiaomiaoCreateOrderRequest(
    [property: JsonPropertyName("plan_id")] int PlanId,
    [property: JsonPropertyName("period")] string Period,
    [property: JsonPropertyName("coupon_code")] string? CouponCode = null);

public sealed record MiaomiaoCheckoutRequest(
    [property: JsonPropertyName("trade_no")] string TradeNo,
    [property: JsonPropertyName("method")] string Method);

public sealed record MiaomiaoOperationResult(bool Success, string? Message = null);

public sealed record MiaomiaoCheckoutResult(
    int Type,
    bool Completed,
    string? PaymentUrl,
    string? Message = null);

public enum MiaomiaoPaymentState
{
    Unknown,
    Pending,
    Processing,
    Canceled,
    Completed,
    Failed
}

public sealed record MiaomiaoPaymentStatus(
    MiaomiaoPaymentState State,
    int? StatusCode = null,
    string? Message = null);

public sealed record MiaomiaoUserInfo
{
    [JsonPropertyName("email")]
    public string Email { get; init; } = string.Empty;

    [JsonPropertyName("transfer_enable")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? TransferEnable { get; init; }

    [JsonPropertyName("device_limit")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? DeviceLimit { get; init; }

    [JsonPropertyName("expired_at")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? ExpiredAt { get; init; }

    [JsonPropertyName("balance")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? Balance { get; init; }

    [JsonPropertyName("plan_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? PlanId { get; init; }

    [JsonPropertyName("banned")]
    [JsonConverter(typeof(MiaomiaoFlexibleBooleanConverter))]
    public bool Banned { get; init; }

    [JsonPropertyName("u")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? UploadBytes { get; init; }

    [JsonPropertyName("d")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? DownloadBytes { get; init; }

    [JsonPropertyName("uuid")]
    public string? Uuid { get; init; }
}

public sealed record MiaomiaoSubscriptionInfo
{
    [JsonPropertyName("subscribe_url")]
    public string? SubscribeUrl { get; init; }

    [JsonPropertyName("expired_at")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? ExpiredAt { get; init; }

    [JsonPropertyName("transfer_enable")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? TransferEnable { get; init; }

    [JsonPropertyName("device_limit")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? DeviceLimit { get; init; }

    [JsonPropertyName("u")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? UploadBytes { get; init; }

    [JsonPropertyName("d")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? DownloadBytes { get; init; }

    [JsonPropertyName("plan_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? PlanId { get; init; }

    [JsonPropertyName("plan")]
    public MiaomiaoPlanSummary? Plan { get; init; }
}

public sealed record MiaomiaoPlanSummary
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Id { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }
}

public sealed record MiaomiaoPlan
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int Id { get; init; }

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string? Content { get; init; }

    [JsonPropertyName("transfer_enable")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? TransferEnable { get; init; }

    [JsonPropertyName("speed_limit")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? SpeedLimit { get; init; }

    [JsonPropertyName("device_limit")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? DeviceLimit { get; init; }

    [JsonPropertyName("show")]
    [JsonConverter(typeof(MiaomiaoFlexibleBooleanConverter))]
    public bool Show { get; init; } = true;

    [JsonPropertyName("renew")]
    [JsonConverter(typeof(MiaomiaoFlexibleBooleanConverter))]
    public bool Renew { get; init; }

    [JsonPropertyName("month_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? MonthPrice { get; init; }

    [JsonPropertyName("quarter_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? QuarterPrice { get; init; }

    [JsonPropertyName("half_year_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? HalfYearPrice { get; init; }

    [JsonPropertyName("year_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? YearPrice { get; init; }

    [JsonPropertyName("two_year_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? TwoYearPrice { get; init; }

    [JsonPropertyName("three_year_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? ThreeYearPrice { get; init; }

    [JsonPropertyName("onetime_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? OneTimePrice { get; init; }

    [JsonPropertyName("reset_price")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? ResetPrice { get; init; }

    public decimal? GetPrice(string period) => period switch
    {
        "month_price" => MonthPrice,
        "quarter_price" => QuarterPrice,
        "half_year_price" => HalfYearPrice,
        "year_price" => YearPrice,
        "two_year_price" => TwoYearPrice,
        "three_year_price" => ThreeYearPrice,
        "onetime_price" => OneTimePrice,
        "reset_price" => ResetPrice,
        _ => null
    };
}

public sealed record MiaomiaoNotice
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Id { get; init; }

    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("content")]
    public string Content { get; init; } = string.Empty;

    [JsonPropertyName("show")]
    [JsonConverter(typeof(MiaomiaoFlexibleBooleanConverter))]
    public bool Show { get; init; } = true;

    [JsonPropertyName("img_url")]
    public string? ImageUrl { get; init; }

    [JsonPropertyName("created_at")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? CreatedAt { get; init; }
}

public sealed record MiaomiaoOrder
{
    [JsonPropertyName("id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Id { get; init; }

    [JsonPropertyName("trade_no")]
    public string TradeNo { get; init; } = string.Empty;

    [JsonPropertyName("plan_id")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? PlanId { get; init; }

    [JsonPropertyName("period")]
    public string? Period { get; init; }

    [JsonPropertyName("total_amount")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? TotalAmount { get; init; }

    [JsonPropertyName("status")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public int? Status { get; init; }

    [JsonPropertyName("created_at")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public long? CreatedAt { get; init; }

    [JsonPropertyName("plan")]
    public MiaomiaoPlanSummary? Plan { get; init; }
}

public static class MiaomiaoDisplayPolicy
{
    public static string FormatOrderPeriod(string? period) => period switch
    {
        "month_price" => "月付",
        "quarter_price" => "季付",
        "half_year_price" => "半年付",
        "year_price" => "年付",
        "two_year_price" => "两年付",
        "three_year_price" => "三年付",
        "onetime_price" => "一次性",
        "reset_price" => "流量重置",
        null or "" => "-",
        _ => period
    };

    public static string FormatOrderAmount(decimal? amountInCents)
    {
        return amountInCents is { } amount ? $"¥{amount / 100m:0.00}" : "-";
    }

    public static string FormatOrderStatus(int? statusCode) => statusCode switch
    {
        0 => "待支付",
        1 => "处理中",
        2 => "已取消",
        3 or 4 => "已完成",
        _ => "未知"
    };
}

internal static class MiaomiaoOrderPolicy
{
    internal static MiaomiaoPaymentState GetPaymentState(int? statusCode) => statusCode switch
    {
        0 => MiaomiaoPaymentState.Pending,
        1 => MiaomiaoPaymentState.Processing,
        2 => MiaomiaoPaymentState.Canceled,
        3 or 4 => MiaomiaoPaymentState.Completed,
        _ => MiaomiaoPaymentState.Unknown
    };

    internal static MiaomiaoOrder? FindRecoverableOrder(IEnumerable<MiaomiaoOrder> orders)
    {
        return orders
            .Where(order => order.TradeNo.IsNotEmpty() && order.Status is 0 or 1)
            .OrderByDescending(order => order.CreatedAt ?? long.MinValue)
            .ThenByDescending(order => order.Id ?? int.MinValue)
            .FirstOrDefault();
    }
}

public sealed record MiaomiaoPaymentMethod
{
    [JsonPropertyName("id")]
    [JsonConverter(typeof(MiaomiaoFlexibleStringConverter))]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("payment")]
    public string? Payment { get; init; }

    [JsonPropertyName("icon")]
    public string? Icon { get; init; }

    [JsonPropertyName("show")]
    [JsonConverter(typeof(MiaomiaoFlexibleBooleanConverter))]
    public bool Show { get; init; } = true;

    [JsonPropertyName("is_available")]
    [JsonConverter(typeof(MiaomiaoFlexibleBooleanConverter))]
    public bool IsAvailable { get; init; } = true;

    [JsonPropertyName("handling_fee_fixed")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? HandlingFeeFixed { get; init; }

    [JsonPropertyName("handling_fee_percent")]
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public decimal? HandlingFeePercent { get; init; }
}

internal sealed record MiaomiaoLoginResponseData
{
    [JsonPropertyName("auth_data")]
    public string? AuthData { get; init; }

    [JsonPropertyName("token")]
    public string? Token { get; init; }
}

internal sealed class MiaomiaoFlexibleStringConverter : JsonConverter<string?>
{
    public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.String => reader.GetString(),
            JsonTokenType.Number when reader.TryGetInt64(out var number) =>
                number.ToString(System.Globalization.CultureInfo.InvariantCulture),
            JsonTokenType.Null => null,
            _ => throw new JsonException("Expected a string or number.")
        };
    }

    public override void Write(Utf8JsonWriter writer, string? value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value);
    }
}

internal sealed class MiaomiaoFlexibleBooleanConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.Number => reader.TryGetInt32(out var number) && number != 0,
            JsonTokenType.String => ParseString(reader.GetString()),
            JsonTokenType.Null => false,
            _ => throw new JsonException("Expected a boolean-compatible value.")
        };
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteBooleanValue(value);
    }

    private static bool ParseString(string? value)
    {
        return bool.TryParse(value, out var boolean)
            ? boolean
            : int.TryParse(value, out var number) && number != 0;
    }
}
