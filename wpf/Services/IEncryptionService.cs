namespace WPFLLM.Services;

public interface IEncryptionService
{
    bool IsEnabled { get; }
    
    string Encrypt(string plainText);
    string Decrypt(string cipherText);
    
    byte[] EncryptBytes(byte[] plainData);
    byte[] DecryptBytes(byte[] encryptedData);
    
    void SetEnabled(bool enabled);
}
