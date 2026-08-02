namespace ServiceLib.Services;

public static class MiaomiaoManagedSubscriptionPolicy
{
    public const string Marker = "miaomiao:managed-subscription";
    public const string DisplayName = "喵喵托管订阅";
    public const int UpdateIntervalMinutes = 48 * 60;

    public static bool IsManaged(SubItem? item)
    {
        return string.Equals(item?.Memo, Marker, StringComparison.Ordinal)
            || item?.Memo?.StartsWith($"{Marker}:", StringComparison.Ordinal) == true;
    }

    public static bool MatchesSource(SubItem? item, string url)
    {
        if (!IsManaged(item) || url.IsNullOrEmpty())
        {
            return false;
        }

        return string.Equals(item!.Memo, CreateMarker(url), StringComparison.Ordinal);
    }

    public static void Attach(SubItem item, string url)
    {
        ArgumentNullException.ThrowIfNull(item);
        if (url.IsNullOrEmpty())
        {
            throw new ArgumentException("Managed subscription URL must not be empty.", nameof(url));
        }

        item.Memo = CreateMarker(url);
    }

    public static void Detach(SubItem item)
    {
        if (!IsManaged(item))
        {
            return;
        }

        item.Url = string.Empty;
        item.MoreUrl = string.Empty;
        item.Enabled = false;
        item.AutoUpdateInterval = 0;
        item.NextAttemptTime = 0;
        item.ConsecutiveFailures = 0;
    }

    private static string CreateMarker(string url)
    {
        var sourceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url.Trim())))
            .ToLowerInvariant();
        return $"{Marker}:{sourceHash}";
    }
}
