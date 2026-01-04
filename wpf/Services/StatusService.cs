namespace WPFLLM.Services;

public class StatusService : IStatusService
{
    private int _networkCallCount;
    private bool _isEncrypted;
    private string _currentStatus = "Ready";

    public bool IsOfflineMode { get; private set; } = true;
    public bool IsEncrypted => _isEncrypted;
    public int NetworkCallCount => _networkCallCount;
    public string CurrentStatus => _currentStatus;

    public event EventHandler? StatusChanged;

    public void IncrementNetworkCalls()
    {
        Interlocked.Increment(ref _networkCallCount);
        IsOfflineMode = false;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void SetStatus(string status)
    {
        _currentStatus = status;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    public void UpdateEncryptionStatus(bool isEncrypted)
    {
        _isEncrypted = isEncrypted;
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }
}
