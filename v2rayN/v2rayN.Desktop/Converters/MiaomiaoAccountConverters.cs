using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using Avalonia.Data.Converters;

namespace v2rayN.Desktop.Converters;

public sealed class MiaomiaoPlanPriceConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MiaomiaoPlan plan)
        {
            return "-";
        }

        var price = new (decimal? Value, string Suffix)[]
        {
            (plan.MonthPrice, "/ 月"),
            (plan.QuarterPrice, "/ 季"),
            (plan.HalfYearPrice, "/ 半年"),
            (plan.YearPrice, "/ 年"),
            (plan.TwoYearPrice, "/ 两年"),
            (plan.ThreeYearPrice, "/ 三年"),
            (plan.OneTimePrice, "/ 一次性")
        }.FirstOrDefault(item => item.Value is > 0);

        return price.Value is { } amount ? $"¥{amount / 100m:0.##} {price.Suffix}" : "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class MiaomiaoDateConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not long timestamp || timestamp <= 0)
        {
            return "-";
        }

        try
        {
            return DateTimeOffset.FromUnixTimeSeconds(timestamp).ToLocalTime().ToString("yyyy-MM-dd", culture);
        }
        catch (ArgumentOutOfRangeException)
        {
            return "-";
        }
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class MiaomiaoBytesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        return value is long bytes && bytes >= 0
            ? MiaomiaoTrafficPolicy.FormatGigabytes(MiaomiaoTrafficPolicy.ToGigabytes(bytes))
            : "-";
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class MiaomiaoRemainingBytesConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not MiaomiaoSubscriptionInfo subscription || subscription.TransferEnable is not { } total)
        {
            return "-";
        }

        return MiaomiaoTrafficPolicy.FormatRemaining(subscription);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}

public sealed class MiaomiaoOrderPeriodConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MiaomiaoDisplayPolicy.FormatOrderPeriod(value as string);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MiaomiaoOrderAmountConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MiaomiaoDisplayPolicy.FormatOrderAmount(value as decimal?);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MiaomiaoOrderStatusConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => MiaomiaoDisplayPolicy.FormatOrderStatus(value as int?);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MiaomiaoProxyStateTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int state && state == (int)ESysProxyType.ForcedChange ? "已连接\n点击断开" : "开启连接";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed class MiaomiaoSpeedLimitConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is int speed && speed > 0 ? $"限速 {speed} Mbps" : "不限速";

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public sealed partial class MiaomiaoNoticeTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not string html || string.IsNullOrWhiteSpace(html))
        {
            return string.Empty;
        }

        var text = ScriptAndStyleRegex().Replace(html, string.Empty);
        text = BreakRegex().Replace(text, Environment.NewLine);
        text = TagRegex().Replace(text, string.Empty);
        text = WebUtility.HtmlDecode(text);
        return WhitespaceRegex().Replace(text, " ").Trim();
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }

    [GeneratedRegex("<(script|style)\\b[^>]*>.*?</\\1>", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ScriptAndStyleRegex();

    [GeneratedRegex("<(br\\s*/?|/p|/div|/li)>", RegexOptions.IgnoreCase)]
    private static partial Regex BreakRegex();

    [GeneratedRegex("<[^>]+>", RegexOptions.Singleline)]
    private static partial Regex TagRegex();

    [GeneratedRegex("[ \\t\\f\\v]+")]
    private static partial Regex WhitespaceRegex();
}
