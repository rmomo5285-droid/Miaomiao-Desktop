using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Text;

[assembly: InternalsVisibleTo("ServiceLib.Tests")]

namespace ServiceLib.Services;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MiaomiaoMigrationNotice(
    string Id,
    string Title,
    string Message,
    bool AutoApply = false,
    bool Required = false);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MiaomiaoEndpointManifestPayload(
    int Schema,
    long Version,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt,
    List<string> ApiEndpoints,
    string RegistrationUrl,
    string DownloadPageUrl,
    List<string> BootstrapMirrors,
    MiaomiaoMigrationNotice? MigrationNotice);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MiaomiaoEndpointManifestEnvelope(
    string Algorithm,
    string Payload,
    string Signature);

public sealed record MiaomiaoManifestRefreshResult(
    MiaomiaoEndpointManifestPayload Payload,
    bool Updated,
    bool ShouldPrompt,
    string? Error = null);

public sealed class MiaomiaoEndpointManifestService
{
    private const string Algorithm = "ECDSA_P256_SHA256";
    private const string CacheFileName = "miaomiao-endpoint-manifest.json";
    private const string StateFileName = "miaomiao-endpoint-state.json";
    private const int MaxEnvelopeLength = 256 * 1024;
    private const int MaxPayloadLength = 128 * 1024;
    private const int MaxSignatureLength = 512;
    private const int MaxUrlLength = 2048;
    private const int MaxEndpointCount = 8;
    private const int MaxMirrorCount = 8;

    public const string PublicKeyPem = """
        -----BEGIN PUBLIC KEY-----
        MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAEOyQ5cjr5sBj6ljunoleDGSupjtaz
        v1LzyUeGQ5NN2R0STXbcIN/fzgGpfG9EPDLIHiXKgkbG61VV+06cOV3Wdg==
        -----END PUBLIC KEY-----
        """;

    private static readonly string[] BuiltInMirrors =
    [
        "https://cdn.vpnmiao.com/json",
        "https://rmomo5285-droid.github.io/Miaomiao-Config/manifest.json",
        "https://cdn.jsdelivr.net/gh/rmomo5285-droid/Miaomiao-Config@gh-pages/manifest.json",
        "https://raw.githubusercontent.com/rmomo5285-droid/Miaomiao-Config/gh-pages/manifest.json",
    ];

    private static readonly MiaomiaoEndpointManifestPayload BuiltInPayload = new(
        Schema: 1,
        Version: 0,
        IssuedAt: DateTimeOffset.UnixEpoch,
        ExpiresAt: DateTimeOffset.MaxValue,
        ApiEndpoints: ["https://www.miaonetwork.com", "https://www.vpnmiao.com"],
        RegistrationUrl: "https://www.miaonetwork.com/#/register",
        DownloadPageUrl: "https://download.vpnmiao.com/download/index.html",
        BootstrapMirrors: [.. BuiltInMirrors],
        MigrationNotice: null);

    private static readonly Lazy<MiaomiaoEndpointManifestService> InstanceFactory = new(() => new());
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private readonly object _stateGate = new();
    private MiaomiaoEndpointManifestPayload? _cachedPayload;

    public static MiaomiaoEndpointManifestService Instance => InstanceFactory.Value;

    private MiaomiaoEndpointManifestService()
    {
    }

    public MiaomiaoEndpointManifestPayload GetCurrent()
    {
        var current = Volatile.Read(ref _cachedPayload);
        if (current != null)
        {
            return current;
        }

        lock (_stateGate)
        {
            if (_cachedPayload != null)
            {
                return _cachedPayload;
            }

            var state = LoadStateCore();
            var cached = LoadCachedEnvelope();
            if (cached == null || cached.Version < state.HighestAppliedVersion)
            {
                return _cachedPayload = BuiltInPayload;
            }

            if (cached.Version > state.HighestAppliedVersion)
            {
                try
                {
                    state = WithAppliedVersion(state, cached.Version);
                    SaveStateCore(state);
                }
                catch (Exception ex)
                {
                    // Never apply a new version unless its rollback floor was persisted first.
                    Logging.SaveLog("Persist Miaomiao endpoint manifest version", ex);
                    return _cachedPayload = BuiltInPayload;
                }
            }

            return _cachedPayload = cached;
        }
    }

    public async Task<MiaomiaoManifestRefreshResult> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var current = GetCurrent();
            var state = LoadState();
            var rollbackFloor = Math.Max(current.Version, state.HighestAppliedVersion);
            var mirrors = current.BootstrapMirrors
                .Concat(BuiltInMirrors)
                .Where(url => IsValidHttpsUrl(url, allowFragment: false))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var errors = new List<string>();
            foreach (var viaProxy in new[] { false, true })
            {
                foreach (var mirror in mirrors)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var content = await new DownloadService().TryDownloadString(
                            mirror,
                            viaProxy,
                            $"Miaomiao/{Utils.GetVersion(false)}");
                        if (content.IsNullOrEmpty() || content.Length > MaxEnvelopeLength)
                        {
                            errors.Add($"{mirror}: empty or oversized response");
                            continue;
                        }

                        if (!TryValidateEnvelope(content, PublicKeyPem, out var payload, out var error))
                        {
                            errors.Add($"{mirror}: {error}");
                            continue;
                        }

                        if (!IsCandidateVersionAllowed(payload!.Version, current.Version, rollbackFloor))
                        {
                            errors.Add($"{mirror}: manifest downgrade rejected");
                            continue;
                        }

                        // A version is immutable. A signing/publishing mistake must use a new version.
                        if (payload.Version == current.Version)
                        {
                            continue;
                        }

                        var updated = payload.Version > current.Version;
                        await SaveEnvelopeAsync(content, cancellationToken);
                        RecordAppliedVersion(payload.Version);
                        Volatile.Write(ref _cachedPayload, payload);
                        return new(payload, updated, ShouldPrompt(payload));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        errors.Add($"{mirror}: {ex.Message}");
                    }
                }
            }

            return new(current, false, ShouldPrompt(current), string.Join("; ", errors));
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public bool ShouldPrompt(MiaomiaoEndpointManifestPayload? payload = null)
    {
        payload ??= GetCurrent();
        return payload.MigrationNotice != null
            && payload.Version > LoadState().AcknowledgedNoticeVersion;
    }

    public Task AcknowledgeMigrationAsync(long version, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var appliedVersion = GetCurrent().Version;
        lock (_stateGate)
        {
            var state = LoadStateCore();
            if (version < 1 || version > Math.Max(appliedVersion, state.HighestAppliedVersion))
            {
                throw new ArgumentOutOfRangeException(nameof(version), "Only an applied manifest notice can be acknowledged.");
            }
            if (version <= state.AcknowledgedNoticeVersion)
            {
                return Task.CompletedTask;
            }

            SaveStateCore(WithAcknowledgedNoticeVersion(state, version));
        }
        return Task.CompletedTask;
    }

    public static bool TryValidateEnvelope(
        string content,
        string publicKeyPem,
        out MiaomiaoEndpointManifestPayload? payload,
        out string error)
    {
        return TryValidateEnvelope(content, publicKeyPem, allowExpired: false, out payload, out error);
    }

    internal static bool TryValidateCachedEnvelope(
        string content,
        string publicKeyPem,
        out MiaomiaoEndpointManifestPayload? payload,
        out string error)
    {
        return TryValidateEnvelope(content, publicKeyPem, allowExpired: true, out payload, out error);
    }

    private static bool TryValidateEnvelope(
        string content,
        string publicKeyPem,
        bool allowExpired,
        out MiaomiaoEndpointManifestPayload? payload,
        out string error)
    {
        payload = null;
        error = string.Empty;
        try
        {
            if (content.IsNullOrEmpty() || content.Length > MaxEnvelopeLength)
            {
                error = "empty or oversized manifest envelope";
                return false;
            }

            var envelope = JsonUtils.Deserialize<MiaomiaoEndpointManifestEnvelope>(content);
            if (envelope == null
                || envelope.Algorithm != Algorithm
                || envelope.Payload.IsNullOrEmpty()
                || envelope.Payload.Length > MaxEnvelopeLength
                || envelope.Signature.IsNullOrEmpty()
                || envelope.Signature.Length > 1024)
            {
                error = "unsupported manifest algorithm";
                return false;
            }

            var payloadBytes = Convert.FromBase64String(envelope.Payload);
            var signatureBytes = Convert.FromBase64String(envelope.Signature);
            if (payloadBytes.Length > MaxPayloadLength || signatureBytes.Length > MaxSignatureLength)
            {
                error = "oversized manifest payload or signature";
                return false;
            }
            using var verifier = ECDsa.Create();
            verifier.ImportFromPem(publicKeyPem);
            if (verifier.KeySize != 256)
            {
                error = "manifest key is not P-256";
                return false;
            }
            if (!verifier.VerifyData(
                    payloadBytes,
                    signatureBytes,
                    HashAlgorithmName.SHA256,
                    DSASignatureFormat.Rfc3279DerSequence))
            {
                error = "invalid manifest signature";
                return false;
            }

            payload = JsonUtils.Deserialize<MiaomiaoEndpointManifestPayload>(Encoding.UTF8.GetString(payloadBytes));
            if (!ValidatePayload(payload, allowExpired, out error))
            {
                payload = null;
                return false;
            }
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool ValidatePayload(
        MiaomiaoEndpointManifestPayload? payload,
        bool allowExpired,
        out string error)
    {
        if (payload == null || payload.Schema != 1 || payload.Version < 1)
        {
            error = "unsupported manifest schema or version";
            return false;
        }
        if (payload.IssuedAt > DateTimeOffset.UtcNow.AddDays(1)
            || payload.ExpiresAt <= payload.IssuedAt
            || (!allowExpired && payload.ExpiresAt <= DateTimeOffset.UtcNow))
        {
            error = "manifest is not currently valid";
            return false;
        }
        if (payload.ApiEndpoints is not { Count: > 0 and <= MaxEndpointCount }
            || payload.ApiEndpoints.Any(url => !IsValidApiEndpoint(url))
            || payload.ApiEndpoints.Distinct(StringComparer.OrdinalIgnoreCase).Count() != payload.ApiEndpoints.Count)
        {
            error = "manifest contains an invalid API endpoint";
            return false;
        }
        if (!IsValidHttpsUrl(payload.RegistrationUrl)
            || !IsValidHttpsUrl(payload.DownloadPageUrl)
            || payload.BootstrapMirrors is not { Count: > 0 and <= MaxMirrorCount }
            || payload.BootstrapMirrors.Any(url => !IsValidHttpsUrl(url, allowFragment: false))
            || payload.BootstrapMirrors.Distinct(StringComparer.OrdinalIgnoreCase).Count() != payload.BootstrapMirrors.Count)
        {
            error = "manifest contains an invalid registration URL, download page URL, or mirror";
            return false;
        }
        if (payload.MigrationNotice is { } notice
            && (!notice.AutoApply
                || notice.Id.IsNullOrEmpty()
                || notice.Id.Length > 128
                || notice.Title.IsNullOrEmpty()
                || notice.Title.Length > 200
                || notice.Message.IsNullOrEmpty()
                || notice.Message.Length > 4000))
        {
            error = "manifest contains an invalid migration notice";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static bool IsValidApiEndpoint(string? value)
    {
        return IsValidHttpsUrl(value, allowFragment: false)
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && string.IsNullOrEmpty(uri.Query)
            && (uri.AbsolutePath.IsNullOrEmpty() || uri.AbsolutePath == "/");
    }

    private static bool IsValidHttpsUrl(string? value, bool allowFragment = true)
    {
        return value is { Length: > 0 and <= MaxUrlLength }
            && Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            && uri.HostNameType == UriHostNameType.Dns
            && uri.Host.Contains('.')
            && !uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrEmpty(uri.UserInfo)
            && (allowFragment || string.IsNullOrEmpty(uri.Fragment));
    }

    private MiaomiaoEndpointManifestPayload? LoadCachedEnvelope()
    {
        try
        {
            var path = Utils.GetConfigPath(CacheFileName);
            if (!File.Exists(path))
            {
                return null;
            }
            var content = File.ReadAllText(path);
            return TryValidateCachedEnvelope(content, PublicKeyPem, out var payload, out _)
                ? payload
                : null;
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Load Miaomiao endpoint manifest", ex);
            return null;
        }
    }

    private static async Task SaveEnvelopeAsync(string content, CancellationToken cancellationToken)
    {
        var path = Utils.GetConfigPath(CacheFileName);
        var tempPath = $"{path}.tmp";
        await File.WriteAllTextAsync(tempPath, content, Encoding.UTF8, cancellationToken);
        File.Move(tempPath, path, true);
    }

    private MiaomiaoEndpointState LoadState()
    {
        lock (_stateGate)
        {
            return LoadStateCore();
        }
    }

    private static MiaomiaoEndpointState LoadStateCore()
    {
        try
        {
            var path = Utils.GetConfigPath(StateFileName);
            var state = File.Exists(path)
                ? JsonUtils.Deserialize<MiaomiaoEndpointState>(File.ReadAllText(path)) ?? new()
                : new();
            return NormalizeState(state);
        }
        catch
        {
            return new();
        }
    }

    private void RecordAppliedVersion(long version)
    {
        lock (_stateGate)
        {
            var state = LoadStateCore();
            SaveStateCore(WithAppliedVersion(state, version));
        }
    }

    private static void SaveStateCore(MiaomiaoEndpointState state)
    {
        var path = Utils.GetConfigPath(StateFileName);
        var tempPath = $"{path}.tmp";
        File.WriteAllText(tempPath, JsonUtils.Serialize(NormalizeState(state)), Encoding.UTF8);
        File.Move(tempPath, path, true);
    }

    internal static bool IsCandidateVersionAllowed(long candidateVersion, long currentVersion, long highestAppliedVersion)
    {
        return candidateVersion >= Math.Max(currentVersion, highestAppliedVersion);
    }

    internal static MiaomiaoEndpointState NormalizeState(MiaomiaoEndpointState state)
    {
        var acknowledged = Math.Max(0, Math.Max(state.AcknowledgedNoticeVersion, state.AcceptedVersion));
        var applied = Math.Max(acknowledged, Math.Max(0, state.HighestAppliedVersion));
        return new(applied, acknowledged, 0);
    }

    internal static MiaomiaoEndpointState WithAppliedVersion(MiaomiaoEndpointState state, long version)
    {
        state = NormalizeState(state);
        return state with { HighestAppliedVersion = Math.Max(state.HighestAppliedVersion, version) };
    }

    internal static MiaomiaoEndpointState WithAcknowledgedNoticeVersion(MiaomiaoEndpointState state, long version)
    {
        state = NormalizeState(state);
        return state with
        {
            AcknowledgedNoticeVersion = Math.Min(
                state.HighestAppliedVersion,
                Math.Max(state.AcknowledgedNoticeVersion, version))
        };
    }
}

internal sealed record MiaomiaoEndpointState(
    long HighestAppliedVersion = 0,
    long AcknowledgedNoticeVersion = 0,
    long AcceptedVersion = 0);
