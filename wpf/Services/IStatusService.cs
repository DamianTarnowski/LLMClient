namespace WPFLLM.Services;

public interface IStatusService
{
    bool IsOfflineMode { get; }
    bool IsEncrypted { get; }
    int NetworkCallCount { get; }
    string CurrentStatus { get; }
    
    event EventHandler? StatusChanged;
    
    void IncrementNetworkCalls();
    void SetStatus(string status);
    void UpdateEncryptionStatus(bool isEncrypted);
}
