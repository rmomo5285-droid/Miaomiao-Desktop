using System.Security.Cryptography;

namespace ServiceLib.Services;

public interface IMiaomiaoSessionStore
{
    string? ReadToken();
    void WriteToken(string token);
    void Clear();
}

internal sealed class MiaomiaoEncryptedSessionStore : IMiaomiaoSessionStore
{
    private const byte FormatVersion = 1;
    private const int KeyLength = 32;
    private const int NonceLength = 12;
    private const int TagLength = 16;
    private const int MaxTokenBytes = 16 * 1024;
    private readonly string _sessionPath;
    private readonly string _keyPath;
    private readonly object _gate = new();

    public MiaomiaoEncryptedSessionStore()
        : this(
            Utils.GetConfigPath("miaomiao-session.bin"),
            Utils.GetConfigPath("miaomiao-session.key"))
    {
    }

    internal MiaomiaoEncryptedSessionStore(string sessionPath, string keyPath)
    {
        _sessionPath = sessionPath;
        _keyPath = keyPath;
    }

    public string? ReadToken()
    {
        lock (_gate)
        {
            try
            {
                if (!File.Exists(_sessionPath))
                {
                    return null;
                }

                var envelope = File.ReadAllBytes(_sessionPath);
                if (envelope.Length < 1 + NonceLength + TagLength + 1
                    || envelope.Length > 1 + NonceLength + TagLength + MaxTokenBytes
                    || envelope[0] != FormatVersion)
                {
                    ClearCore();
                    return null;
                }

                var key = ReadOrCreateKey();
                var plaintext = new byte[envelope.Length - 1 - NonceLength - TagLength];
                try
                {
                    using var aes = new AesGcm(key, TagLength);
                    aes.Decrypt(
                        envelope.AsSpan(1, NonceLength),
                        envelope.AsSpan(1 + NonceLength + TagLength),
                        envelope.AsSpan(1 + NonceLength, TagLength),
                        plaintext);
                    var token = Encoding.UTF8.GetString(plaintext).Trim();
                    return token.Length is > 0 and <= 8192 ? token : null;
                }
                finally
                {
                    CryptographicOperations.ZeroMemory(key);
                    CryptographicOperations.ZeroMemory(plaintext);
                    CryptographicOperations.ZeroMemory(envelope);
                }
            }
            catch (Exception ex)
            {
                Logging.SaveLog("Read Miaomiao encrypted session", ex);
                ClearCore();
                return null;
            }
        }
    }

    public void WriteToken(string token)
    {
        var plaintext = Encoding.UTF8.GetBytes(token);
        if (plaintext.Length is < 1 or > MaxTokenBytes)
        {
            CryptographicOperations.ZeroMemory(plaintext);
            throw new ArgumentOutOfRangeException(nameof(token));
        }

        lock (_gate)
        {
            var key = ReadOrCreateKey();
            var nonce = RandomNumberGenerator.GetBytes(NonceLength);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[TagLength];
            var envelope = new byte[1 + NonceLength + TagLength + ciphertext.Length];
            try
            {
                using var aes = new AesGcm(key, TagLength);
                aes.Encrypt(nonce, plaintext, ciphertext, tag);
                envelope[0] = FormatVersion;
                nonce.CopyTo(envelope, 1);
                tag.CopyTo(envelope, 1 + NonceLength);
                ciphertext.CopyTo(envelope, 1 + NonceLength + TagLength);
                AtomicWrite(_sessionPath, envelope);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(key);
                CryptographicOperations.ZeroMemory(nonce);
                CryptographicOperations.ZeroMemory(plaintext);
                CryptographicOperations.ZeroMemory(ciphertext);
                CryptographicOperations.ZeroMemory(tag);
                CryptographicOperations.ZeroMemory(envelope);
            }
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            ClearCore();
        }
    }

    private byte[] ReadOrCreateKey()
    {
        if (File.Exists(_keyPath))
        {
            var existing = File.ReadAllBytes(_keyPath);
            if (existing.Length == KeyLength)
            {
                return existing;
            }
            CryptographicOperations.ZeroMemory(existing);
            throw new CryptographicException("Miaomiao session key has an invalid length.");
        }

        var key = RandomNumberGenerator.GetBytes(KeyLength);
        try
        {
            AtomicWrite(_keyPath, key);
            return (byte[])key.Clone();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static void AtomicWrite(string path, byte[] content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllBytes(tempPath, content);
            RestrictToCurrentUser(tempPath);
            File.Move(tempPath, path, true);
            RestrictToCurrentUser(path);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    private static void RestrictToCurrentUser(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
    }

    private void ClearCore()
    {
        try
        {
            File.Delete(_sessionPath);
        }
        catch (Exception ex)
        {
            Logging.SaveLog("Clear Miaomiao encrypted session", ex);
        }
    }
}
