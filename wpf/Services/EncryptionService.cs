using System.Runtime.Versioning;
using System.Security.Cryptography;
using System.Text;
using System.IO;

namespace WPFLLM.Services;

/// <summary>
/// Encryption service using DPAPI for key protection and AES-GCM for data encryption.
/// Data never leaves the device, encrypted at rest.
/// </summary>
[SupportedOSPlatform("windows")]
public class EncryptionService : IEncryptionService
{
    private const int KeySize = 256;
    private const int NonceSize = 12;
    private const int TagSize = 16;
    
    private byte[]? _masterKey;
    private bool _isEnabled;
    
    public bool IsEnabled => _isEnabled;

    public EncryptionService()
    {
        _isEnabled = false;
        InitializeMasterKey();
    }

    private void InitializeMasterKey()
    {
        try
        {
            var keyPath = GetKeyPath();
            
            if (File.Exists(keyPath))
            {
                // Load existing key protected by DPAPI
                var protectedKey = File.ReadAllBytes(keyPath);
                _masterKey = ProtectedData.Unprotect(protectedKey, null, DataProtectionScope.CurrentUser);
            }
            else
            {
                // Generate new key and protect with DPAPI
                _masterKey = RandomNumberGenerator.GetBytes(KeySize / 8);
                var protectedKey = ProtectedData.Protect(_masterKey, null, DataProtectionScope.CurrentUser);
                
                var dir = Path.GetDirectoryName(keyPath)!;
                Directory.CreateDirectory(dir);
                File.WriteAllBytes(keyPath, protectedKey);
                
                // Set file as hidden
                File.SetAttributes(keyPath, FileAttributes.Hidden);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to initialize encryption key: {ex.Message}");
            _masterKey = null;
        }
    }

    private static string GetKeyPath()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(appData, "WPFLLM", ".keys", "master.key");
    }

    public void SetEnabled(bool enabled)
    {
        _isEnabled = enabled && _masterKey != null;
    }

    public string Encrypt(string plainText)
    {
        if (!_isEnabled || _masterKey == null || string.IsNullOrEmpty(plainText))
            return plainText;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = EncryptBytes(plainBytes);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch
        {
            return plainText;
        }
    }

    public string Decrypt(string cipherText)
    {
        if (!_isEnabled || _masterKey == null || string.IsNullOrEmpty(cipherText))
            return cipherText;

        try
        {
            // Check if it looks like base64 encrypted data
            if (!IsBase64String(cipherText))
                return cipherText;
                
            var encryptedBytes = Convert.FromBase64String(cipherText);
            var plainBytes = DecryptBytes(encryptedBytes);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch
        {
            // If decryption fails, return as-is (might be unencrypted data)
            return cipherText;
        }
    }

    public byte[] EncryptBytes(byte[] plainData)
    {
        if (!_isEnabled || _masterKey == null || plainData.Length == 0)
            return plainData;

        var nonce = RandomNumberGenerator.GetBytes(NonceSize);
        var cipherText = new byte[plainData.Length];
        var tag = new byte[TagSize];

        using var aes = new AesGcm(_masterKey, TagSize);
        aes.Encrypt(nonce, plainData, cipherText, tag);

        // Format: [nonce (12)] + [tag (16)] + [ciphertext]
        var result = new byte[NonceSize + TagSize + cipherText.Length];
        Buffer.BlockCopy(nonce, 0, result, 0, NonceSize);
        Buffer.BlockCopy(tag, 0, result, NonceSize, TagSize);
        Buffer.BlockCopy(cipherText, 0, result, NonceSize + TagSize, cipherText.Length);

        return result;
    }

    public byte[] DecryptBytes(byte[] encryptedData)
    {
        if (!_isEnabled || _masterKey == null || encryptedData.Length < NonceSize + TagSize)
            return encryptedData;

        var nonce = new byte[NonceSize];
        var tag = new byte[TagSize];
        var cipherText = new byte[encryptedData.Length - NonceSize - TagSize];

        Buffer.BlockCopy(encryptedData, 0, nonce, 0, NonceSize);
        Buffer.BlockCopy(encryptedData, NonceSize, tag, 0, TagSize);
        Buffer.BlockCopy(encryptedData, NonceSize + TagSize, cipherText, 0, cipherText.Length);

        var plainText = new byte[cipherText.Length];

        using var aes = new AesGcm(_masterKey, TagSize);
        aes.Decrypt(nonce, cipherText, tag, plainText);

        return plainText;
    }

    private static bool IsBase64String(string s)
    {
        if (string.IsNullOrEmpty(s) || s.Length % 4 != 0)
            return false;
            
        try
        {
            // Quick check - encrypted data should be at least nonce + tag size
            var decoded = Convert.FromBase64String(s);
            return decoded.Length >= NonceSize + TagSize;
        }
        catch
        {
            return false;
        }
    }
}
