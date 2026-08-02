using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ServiceLib.Services;

namespace ServiceLib.Tests.Services;

public class MiaomiaoEndpointManifestServiceTests
{
    [Fact]
    public void TryValidateEnvelope_AcceptsSignedPayload()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = CreatePayload();
        var payloadNode = JsonNode.Parse(JsonUtils.Serialize(payload, false))!.AsObject();
        var downloadPageUrl = payloadNode[nameof(MiaomiaoEndpointManifestPayload.DownloadPageUrl)]!.GetValue<string>();
        Assert.True(payloadNode.Remove(nameof(MiaomiaoEndpointManifestPayload.DownloadPageUrl)));
        payloadNode["downloadPageUrl"] = downloadPageUrl;
        var payloadBytes = Encoding.UTF8.GetBytes(payloadNode.ToJsonString());

        var result = MiaomiaoEndpointManifestService.TryValidateEnvelope(
            CreateSignedEnvelope(signer, payloadBytes),
            signer.ExportSubjectPublicKeyInfoPem(),
            out var parsed,
            out var error);

        Assert.True(result, error);
        Assert.Equal(payload.Version, parsed?.Version);
        Assert.Equal(payload.ApiEndpoints, parsed?.ApiEndpoints);
        Assert.Equal(payload.DownloadPageUrl, parsed?.DownloadPageUrl);
    }

    [Fact]
    public void TryValidateEnvelope_RejectsTamperedPayload()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payloadBytes = Encoding.UTF8.GetBytes(JsonUtils.Serialize(CreatePayload(), false));
        var signature = signer.SignData(payloadBytes, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        payloadBytes[^2] ^= 1;
        var envelope = new MiaomiaoEndpointManifestEnvelope(
            "ECDSA_P256_SHA256",
            Convert.ToBase64String(payloadBytes),
            Convert.ToBase64String(signature));

        var result = MiaomiaoEndpointManifestService.TryValidateEnvelope(
            JsonUtils.Serialize(envelope, false),
            signer.ExportSubjectPublicKeyInfoPem(),
            out _,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryValidateEnvelope_RejectsSignedRemoteCommandField()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payloadNode = JsonNode.Parse(JsonUtils.Serialize(CreatePayload(), false))!.AsObject();
        payloadNode["command"] = "run-anything";
        var payloadBytes = Encoding.UTF8.GetBytes(payloadNode.ToJsonString());

        var result = MiaomiaoEndpointManifestService.TryValidateEnvelope(
            CreateSignedEnvelope(signer, payloadBytes),
            signer.ExportSubjectPublicKeyInfoPem(),
            out _,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryValidateEnvelope_RejectsPrivateNetworkApiEndpoint()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = CreatePayload() with { ApiEndpoints = ["https://127.0.0.1"] };
        var payloadBytes = Encoding.UTF8.GetBytes(JsonUtils.Serialize(payload, false));

        var result = MiaomiaoEndpointManifestService.TryValidateEnvelope(
            CreateSignedEnvelope(signer, payloadBytes),
            signer.ExportSubjectPublicKeyInfoPem(),
            out _,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void TryValidateEnvelope_RejectsMissingDownloadPageUrl()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payloadNode = JsonNode.Parse(JsonUtils.Serialize(CreatePayload(), false))!.AsObject();
        Assert.True(payloadNode.Remove(nameof(MiaomiaoEndpointManifestPayload.DownloadPageUrl)));
        var payloadBytes = Encoding.UTF8.GetBytes(payloadNode.ToJsonString());

        var result = MiaomiaoEndpointManifestService.TryValidateEnvelope(
            CreateSignedEnvelope(signer, payloadBytes),
            signer.ExportSubjectPublicKeyInfoPem(),
            out _,
            out _);

        Assert.False(result);
    }

    [Theory]
    [InlineData("http://download.example.com/download/index.html")]
    [InlineData("https://127.0.0.1/download/index.html")]
    [InlineData("https://downloads.local/download/index.html")]
    public void TryValidateEnvelope_RejectsNonPublicHttpsDownloadPage(string downloadPageUrl)
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = CreatePayload() with { DownloadPageUrl = downloadPageUrl };
        var payloadBytes = Encoding.UTF8.GetBytes(JsonUtils.Serialize(payload, false));

        var result = MiaomiaoEndpointManifestService.TryValidateEnvelope(
            CreateSignedEnvelope(signer, payloadBytes),
            signer.ExportSubjectPublicKeyInfoPem(),
            out _,
            out _);

        Assert.False(result);
    }

    [Fact]
    public void CachedValidation_PreservesExpiredLastKnownGoodManifest()
    {
        using var signer = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var payload = CreatePayload() with
        {
            IssuedAt = DateTimeOffset.UtcNow.AddDays(-30),
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(-1)
        };
        var envelope = CreateSignedEnvelope(
            signer,
            Encoding.UTF8.GetBytes(JsonUtils.Serialize(payload, false)));
        var publicKey = signer.ExportSubjectPublicKeyInfoPem();

        Assert.False(MiaomiaoEndpointManifestService.TryValidateEnvelope(
            envelope,
            publicKey,
            out _,
            out _));
        Assert.True(MiaomiaoEndpointManifestService.TryValidateCachedEnvelope(
            envelope,
            publicKey,
            out var cached,
            out var error), error);
        Assert.Equal(payload.Version, cached?.Version);
    }

    [Fact]
    public void VersionState_SeparatesAppliedAndAcknowledgedVersionsAndRejectsRestartRollback()
    {
        var legacyState = JsonUtils.Deserialize<MiaomiaoEndpointState>("{\"AcceptedVersion\":7}")!;
        var migratedState = MiaomiaoEndpointManifestService.NormalizeState(legacyState);

        Assert.Equal(7, migratedState.HighestAppliedVersion);
        Assert.Equal(7, migratedState.AcknowledgedNoticeVersion);

        var afterAutomaticApply = MiaomiaoEndpointManifestService.WithAppliedVersion(migratedState, 8);
        Assert.Equal(8, afterAutomaticApply.HighestAppliedVersion);
        Assert.Equal(7, afterAutomaticApply.AcknowledgedNoticeVersion);

        Assert.False(MiaomiaoEndpointManifestService.IsCandidateVersionAllowed(
            candidateVersion: 7,
            currentVersion: 0,
            highestAppliedVersion: afterAutomaticApply.HighestAppliedVersion));
        Assert.True(MiaomiaoEndpointManifestService.IsCandidateVersionAllowed(
            candidateVersion: 8,
            currentVersion: 0,
            highestAppliedVersion: afterAutomaticApply.HighestAppliedVersion));

        var afterAcknowledgement = MiaomiaoEndpointManifestService.WithAcknowledgedNoticeVersion(afterAutomaticApply, 8);
        Assert.Equal(8, afterAcknowledgement.HighestAppliedVersion);
        Assert.Equal(8, afterAcknowledgement.AcknowledgedNoticeVersion);
    }

    private static string CreateSignedEnvelope(ECDsa signer, byte[] payloadBytes)
    {
        var signature = signer.SignData(
            payloadBytes,
            HashAlgorithmName.SHA256,
            DSASignatureFormat.Rfc3279DerSequence);
        var envelope = new MiaomiaoEndpointManifestEnvelope(
            "ECDSA_P256_SHA256",
            Convert.ToBase64String(payloadBytes),
            Convert.ToBase64String(signature));
        return JsonUtils.Serialize(envelope, false);
    }

    private static MiaomiaoEndpointManifestPayload CreatePayload()
    {
        return new(
            1,
            7,
            DateTimeOffset.UtcNow.AddMinutes(-5),
            DateTimeOffset.UtcNow.AddDays(30),
            ["https://api.example.com"],
            "https://api.example.com/#/register",
            "https://download.example.com/download/index.html",
            ["https://cdn.example.com/manifest.json"],
            new("migration-7", "入口更新", "请切换到新入口", true));
    }
}
