using System.Text.Json;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using ServiceLib.Services;
using Xunit;

namespace ServiceLib.Tests.Services;

public class MiaomiaoAccountPolicyTests
{
    [Theory]
    [InlineData(0, MiaomiaoPaymentState.Pending)]
    [InlineData(1, MiaomiaoPaymentState.Processing)]
    [InlineData(2, MiaomiaoPaymentState.Canceled)]
    [InlineData(3, MiaomiaoPaymentState.Completed)]
    [InlineData(4, MiaomiaoPaymentState.Completed)]
    [InlineData(99, MiaomiaoPaymentState.Unknown)]
    public void GetPaymentState_UsesXboardOrderStates(int status, MiaomiaoPaymentState expected)
    {
        Assert.Equal(expected, MiaomiaoOrderPolicy.GetPaymentState(status));
    }

    [Fact]
    public void FindRecoverableOrder_SelectsNewestPendingOrProcessingOrder()
    {
        var orders = new[]
        {
            new MiaomiaoOrder { Id = 10, TradeNo = "completed", Status = 3, CreatedAt = 300 },
            new MiaomiaoOrder { Id = 11, TradeNo = "pending-old", Status = 0, CreatedAt = 100 },
            new MiaomiaoOrder { Id = 12, TradeNo = "processing-new", Status = 1, CreatedAt = 200 },
            new MiaomiaoOrder { Id = 13, TradeNo = "canceled", Status = 2, CreatedAt = 400 }
        };

        var result = MiaomiaoOrderPolicy.FindRecoverableOrder(orders);

        Assert.Equal("processing-new", result?.TradeNo);
    }

    [Fact]
    public void ExtractLoginToken_NormalizesCurrentXboardBearerResponse()
    {
        using var document = JsonDocument.Parse("""
            {"data":{"auth_data":"Bearer current-token"}}
            """);

        var token = MiaomiaoAccountService.ExtractLoginToken(document.RootElement);

        Assert.Equal("current-token", token);
    }

    [Fact]
    public void ExtractLoginToken_AcceptsLegacyDirectStringResponse()
    {
        using var document = JsonDocument.Parse("""
            {"data":"legacy-token"}
            """);

        var token = MiaomiaoAccountService.ExtractLoginToken(document.RootElement);

        Assert.Equal("legacy-token", token);
    }

    [Fact]
    public void SubscriptionInfo_AcceptsStringEncodedNumericLimits()
    {
        var subscription = JsonSerializer.Deserialize<MiaomiaoSubscriptionInfo>("""
            {"device_limit":"3","transfer_enable":"10737418240"}
            """);

        Assert.NotNull(subscription);
        Assert.Equal(3, subscription.DeviceLimit);
        Assert.Equal(10_737_418_240L, subscription.TransferEnable);
    }

    [Fact]
    public void ManagedSubscriptionPolicy_RecognizesOnlyTheProtectedMarker()
    {
        Assert.True(MiaomiaoManagedSubscriptionPolicy.IsManaged(new SubItem
        {
            Memo = MiaomiaoManagedSubscriptionPolicy.Marker
        }));
        var managed = new SubItem();
        MiaomiaoManagedSubscriptionPolicy.Attach(managed, "https://example.com/sub?token=one");
        Assert.True(MiaomiaoManagedSubscriptionPolicy.IsManaged(managed));
        Assert.True(MiaomiaoManagedSubscriptionPolicy.MatchesSource(
            managed,
            "https://example.com/sub?token=one"));
        Assert.False(MiaomiaoManagedSubscriptionPolicy.MatchesSource(
            managed,
            "https://example.com/sub?token=two"));
        Assert.False(MiaomiaoManagedSubscriptionPolicy.IsManaged(new SubItem
        {
            Memo = "user-subscription"
        }));
        Assert.False(MiaomiaoManagedSubscriptionPolicy.IsManaged(null));
    }

    [Fact]
    public void ManagedSubscriptionPolicy_DetachClearsCredentialButKeepsIdentity()
    {
        var item = new SubItem
        {
            Id = "managed-id",
            Url = "https://example.com/subscription?token=secret",
            MoreUrl = "https://backup.example.com/subscription?token=secret",
            Enabled = true,
            AutoUpdateInterval = MiaomiaoManagedSubscriptionPolicy.UpdateIntervalMinutes,
            NextAttemptTime = 123,
            ConsecutiveFailures = 2
        };
        MiaomiaoManagedSubscriptionPolicy.Attach(item, item.Url);
        var marker = item.Memo;

        MiaomiaoManagedSubscriptionPolicy.Detach(item);

        Assert.Equal("managed-id", item.Id);
        Assert.Equal(marker, item.Memo);
        Assert.True(MiaomiaoManagedSubscriptionPolicy.MatchesSource(
            item,
            "https://example.com/subscription?token=secret"));
        Assert.Empty(item.Url);
        Assert.Empty(item.MoreUrl);
        Assert.False(item.Enabled);
        Assert.Equal(0, item.AutoUpdateInterval);
        Assert.Equal(0, item.NextAttemptTime);
        Assert.Equal(0, item.ConsecutiveFailures);
    }
}
