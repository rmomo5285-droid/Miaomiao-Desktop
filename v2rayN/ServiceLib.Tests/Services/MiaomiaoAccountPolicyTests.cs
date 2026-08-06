using System.Text.Json;
using System.Text;
using ServiceLib.Models.Dto;
using ServiceLib.Models.Entities;
using ServiceLib.Services;
using Xunit;

namespace ServiceLib.Tests.Services;

public class MiaomiaoAccountPolicyTests
{
    [Fact]
    public void EncryptedSessionStore_RestoresAndClearsWithoutPlaintextToken()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"miaomiao-session-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var sessionPath = Path.Combine(directory, "session.bin");
            var keyPath = Path.Combine(directory, "session.key");
            var store = new MiaomiaoEncryptedSessionStore(sessionPath, keyPath);

            store.WriteToken("private-session-token");

            Assert.Equal("private-session-token", store.ReadToken());
            Assert.DoesNotContain(
                "private-session-token",
                Encoding.UTF8.GetString(File.ReadAllBytes(sessionPath)));
            Assert.Equal(32, File.ReadAllBytes(keyPath).Length);

            store.Clear();
            Assert.Null(store.ReadToken());
            Assert.False(File.Exists(sessionPath));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

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

    [Theory]
    [InlineData("month_price", "月付")]
    [InlineData("two_year_price", "两年付")]
    [InlineData("onetime_price", "一次性")]
    [InlineData("future_period", "future_period")]
    public void FormatOrderPeriod_UsesFriendlyLabels(string period, string expected)
    {
        Assert.Equal(expected, MiaomiaoDisplayPolicy.FormatOrderPeriod(period));
    }

    [Theory]
    [InlineData(79490, "¥794.90")]
    [InlineData(1500, "¥15.00")]
    [InlineData(0, "¥0.00")]
    public void FormatOrderAmount_ConvertsCentsToCurrency(double amount, string expected)
    {
        Assert.Equal(expected, MiaomiaoDisplayPolicy.FormatOrderAmount((decimal)amount));
    }

    [Fact]
    public void PricingPolicy_AppliesFixedCouponThenBalance()
    {
        var summary = MiaomiaoPricingPolicy.Calculate(
            10_000m,
            new MiaomiaoCoupon { Type = 1, Value = 1_500m },
            2_000m);

        Assert.Equal(10_000m, summary.OriginalAmount);
        Assert.Equal(1_500m, summary.DiscountAmount);
        Assert.Equal(2_000m, summary.BalanceAmount);
        Assert.Equal(6_500m, summary.PayableAmount);
    }

    [Fact]
    public void PricingPolicy_AppliesPercentageCouponAndCapsBalance()
    {
        var summary = MiaomiaoPricingPolicy.Calculate(
            8_000m,
            new MiaomiaoCoupon { Type = 2, Value = 25m },
            10_000m);

        Assert.Equal(2_000m, summary.DiscountAmount);
        Assert.Equal(6_000m, summary.BalanceAmount);
        Assert.Equal(0m, summary.PayableAmount);
    }

    [Fact]
    public void PricingPolicy_CapsOversizedCouponAtOriginalAmount()
    {
        var summary = MiaomiaoPricingPolicy.Calculate(
            1_000m,
            new MiaomiaoCoupon { Type = 1, Value = 9_999m },
            0m);

        Assert.Equal(1_000m, summary.DiscountAmount);
        Assert.Equal(0m, summary.PayableAmount);
    }

    [Theory]
    [InlineData(0, "待支付")]
    [InlineData(1, "处理中")]
    [InlineData(2, "已取消")]
    [InlineData(3, "已完成")]
    [InlineData(4, "已完成")]
    [InlineData(99, "未知")]
    public void FormatOrderStatus_UsesFriendlyLabels(int status, string expected)
    {
        Assert.Equal(expected, MiaomiaoDisplayPolicy.FormatOrderStatus(status));
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
    public void ResolveSubscriptionPlan_UsesPlanListWhenSubscriptionOnlyHasPlanId()
    {
        var subscription = new MiaomiaoSubscriptionInfo { PlanId = 16 };
        var plans = new[]
        {
            new MiaomiaoPlan { Id = 15, Name = "其他套餐" },
            new MiaomiaoPlan { Id = 16, Name = "尊享流量包" }
        };

        var resolved = MiaomiaoDisplayPolicy.ResolveSubscriptionPlan(subscription, plans);

        Assert.Equal("尊享流量包", resolved.Plan?.Name);
    }

    [Fact]
    public void ResolveSubscriptionPlan_PreservesEmbeddedPlanName()
    {
        var subscription = new MiaomiaoSubscriptionInfo
        {
            PlanId = 16,
            Plan = new MiaomiaoPlanSummary { Id = 16, Name = "接口套餐名" }
        };

        var resolved = MiaomiaoDisplayPolicy.ResolveSubscriptionPlan(
            subscription,
            new[] { new MiaomiaoPlan { Id = 16, Name = "列表套餐名" } });

        Assert.Equal("接口套餐名", resolved.Plan?.Name);
    }

    [Fact]
    public void TrafficPolicy_FormatsNormalByteTotalsAsGigabytes()
    {
        var subscription = new MiaomiaoSubscriptionInfo
        {
            TransferEnable = 10L * 1024 * 1024 * 1024,
            UploadBytes = 1L * 1024 * 1024 * 1024,
            DownloadBytes = 1L * 1024 * 1024 * 1024
        };

        Assert.Equal("8 GB", MiaomiaoTrafficPolicy.FormatRemaining(subscription));
    }

    [Fact]
    public void TrafficPolicy_FormatsLegacyPanelScaleAsGigabytes()
    {
        var subscription = new MiaomiaoSubscriptionInfo
        {
            TransferEnable = (long)(800.7m * 1024m * 1024m * 1024m * 1024m)
        };

        Assert.Equal("800.7 GB", MiaomiaoTrafficPolicy.FormatRemaining(subscription));
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
