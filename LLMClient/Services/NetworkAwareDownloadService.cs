using Microsoft.Extensions.Logging;
using System.Net.NetworkInformation;

namespace LLMClient.Services
{
    public class NetworkStatus
    {
        public bool IsConnected { get; set; }
        public bool IsWifi { get; set; }
        public bool IsMetered { get; set; }
        public string? ConnectionType { get; set; }
        public int SignalStrength { get; set; } // 0-100%
        public DateTime LastChecked { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Service that monitors network connectivity and automatically handles download resumption
    /// </summary>
    public class NetworkAwareDownloadService : IDisposable
    {
        private readonly ILogger<NetworkAwareDownloadService> _logger;
        private readonly SmartDownloadManager _downloadManager;
        private readonly ILocalModelService _localModelService;
        private readonly ILocalizationService _localization;
        private readonly Timer _networkCheckTimer;
        
        private NetworkStatus _currentNetworkStatus = new();
        private bool _wasConnectedBefore = true;
        private DateTime? _lastDisconnectTime;
        private CancellationTokenSource? _autoResumeToken;
        
        private const int NETWORK_CHECK_INTERVAL_SECONDS = 10;
        private const int RECONNECT_GRACE_PERIOD_SECONDS = 30;
        private const int MAX_AUTO_RESUME_ATTEMPTS = 10;
        private int _autoResumeAttempts = 0;

        public event Action<NetworkStatus>? NetworkStatusChanged;
        public event Action<string>? NetworkMessageReceived;

        public NetworkAwareDownloadService(
            ILogger<NetworkAwareDownloadService> logger,
            SmartDownloadManager downloadManager,
            ILocalModelService localModelService,
            ILocalizationService localization)
        {
            _logger = logger;
            _downloadManager = downloadManager;
            _localModelService = localModelService;
            _localization = localization;

            // Monitor network changes
            NetworkChange.NetworkAvailabilityChanged += OnNetworkAvailabilityChanged;
            NetworkChange.NetworkAddressChanged += OnNetworkAddressChanged;

            // Periodic network health check
            _networkCheckTimer = new Timer(CheckNetworkHealth, null, 
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(NETWORK_CHECK_INTERVAL_SECONDS));

            // Initial network check
            Task.Run(UpdateNetworkStatusAsync);
        }

        private async void OnNetworkAvailabilityChanged(object? sender, NetworkAvailabilityEventArgs e)
        {
            _logger.LogInformation($"Network availability changed: {e.IsAvailable}");
            await UpdateNetworkStatusAsync();
            
            if (e.IsAvailable && !_wasConnectedBefore)
            {
                await HandleNetworkReconnected();
            }
            else if (!e.IsAvailable && _wasConnectedBefore)
            {
                await HandleNetworkDisconnected();
            }
        }

        private async void OnNetworkAddressChanged(object? sender, EventArgs e)
        {
            _logger.LogDebug("Network address changed");
            await UpdateNetworkStatusAsync();
        }

        private async void CheckNetworkHealth(object? state)
        {
            try
            {
                await UpdateNetworkStatusAsync();
                
                // Auto-resume logic
                if (_currentNetworkStatus.IsConnected && 
                    _localModelService.State == LocalModelState.NotDownloaded &&
                    _autoResumeAttempts < MAX_AUTO_RESUME_ATTEMPTS)
                {
                    var session = _downloadManager.GetCurrentSession();
                    if (!session.IsCompleted && session.FileProgress.Any())
                    {
                        await AttemptAutoResumeAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in network health check");
            }
        }

        private async Task UpdateNetworkStatusAsync()
        {
            try
            {
                var oldStatus = _currentNetworkStatus;
                _currentNetworkStatus = await GetCurrentNetworkStatusAsync();
                
                if (HasNetworkStatusChanged(oldStatus, _currentNetworkStatus))
                {
                    _logger.LogInformation($"Network status changed: Connected={_currentNetworkStatus.IsConnected}, " +
                        $"Type={_currentNetworkStatus.ConnectionType}, WiFi={_currentNetworkStatus.IsWifi}");
                        
                    NetworkStatusChanged?.Invoke(_currentNetworkStatus);
                    
                    // Send user-friendly message
                    if (_currentNetworkStatus.IsConnected && !oldStatus.IsConnected)
                    {
                        await SendNetworkMessage("NetworkReconnected", "Połączenie sieciowe zostało przywrócone");
                    }
                    else if (!_currentNetworkStatus.IsConnected && oldStatus.IsConnected)
                    {
                        await SendNetworkMessage("NetworkDisconnected", "Utracono połączenie sieciowe");
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating network status");
            }
        }

        private async Task<NetworkStatus> GetCurrentNetworkStatusAsync()
        {
            var status = new NetworkStatus();
            
            try
            {
                // Check basic connectivity
                status.IsConnected = NetworkInterface.GetIsNetworkAvailable();
                
                if (status.IsConnected)
                {
                    // Get network interface details
                    var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(ni => ni.OperationalStatus == OperationalStatus.Up && 
                                   ni.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        .ToList();
                    
                    if (interfaces.Any())
                    {
                        var primaryInterface = interfaces.FirstOrDefault();
                        if (primaryInterface != null)
                        {
                            status.ConnectionType = primaryInterface.NetworkInterfaceType.ToString();
                            status.IsWifi = primaryInterface.NetworkInterfaceType == NetworkInterfaceType.Wireless80211;
                            
                            // Try to determine if connection is metered (simplified)
                            status.IsMetered = await IsConnectionMeteredAsync();
                            
                            // Estimate signal strength (simplified - would need platform-specific code)
                            status.SignalStrength = EstimateSignalStrength(primaryInterface);
                        }
                    }
                    
                    // Test actual internet connectivity
                    if (status.IsConnected)
                    {
                        status.IsConnected = await TestInternetConnectivityAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error getting detailed network status");
                status.IsConnected = NetworkInterface.GetIsNetworkAvailable();
            }
            
            return status;
        }

        private async Task<bool> TestInternetConnectivityAsync()
        {
            try
            {
                using var client = new HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                
                // Test connectivity to HuggingFace (where model is downloaded from)
                var response = await client.GetAsync("https://huggingface.co", 
                    HttpCompletionOption.ResponseHeadersRead);
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }

        private async Task<bool> IsConnectionMeteredAsync()
        {
            try
            {
                // Platform-specific logic would go here
                // For now, assume WiFi is not metered, cellular is metered
                return !_currentNetworkStatus.IsWifi;
            }
            catch
            {
                return false;
            }
        }

        private int EstimateSignalStrength(NetworkInterface networkInterface)
        {
            try
            {
                // Simplified signal strength estimation
                // Real implementation would use platform-specific APIs
                var stats = networkInterface.GetIPv4Statistics();
                if (stats.BytesReceived > 0 && stats.BytesSent > 0)
                {
                    return 85; // Good connection
                }
                return 60; // Moderate connection
            }
            catch
            {
                return 50; // Unknown
            }
        }

        private bool HasNetworkStatusChanged(NetworkStatus old, NetworkStatus current)
        {
            return old.IsConnected != current.IsConnected ||
                   old.IsWifi != current.IsWifi ||
                   old.ConnectionType != current.ConnectionType ||
                   Math.Abs(old.SignalStrength - current.SignalStrength) > 20;
        }

        private async Task HandleNetworkDisconnected()
        {
            try
            {
                _wasConnectedBefore = false;
                _lastDisconnectTime = DateTime.UtcNow;
                
                _logger.LogWarning("Network disconnected");
                await SendNetworkMessage("NetworkLost", "Utracono połączenie internetowe. Pobieranie zostanie wznowione po przywróceniu połączenia.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling network disconnection");
            }
        }

        private async Task HandleNetworkReconnected()
        {
            try
            {
                _wasConnectedBefore = true;
                var disconnectDuration = _lastDisconnectTime.HasValue 
                    ? DateTime.UtcNow - _lastDisconnectTime.Value
                    : TimeSpan.Zero;
                
                _logger.LogInformation($"Network reconnected after {disconnectDuration.TotalSeconds:F0} seconds");
                
                // Grace period before attempting auto-resume
                await Task.Delay(TimeSpan.FromSeconds(RECONNECT_GRACE_PERIOD_SECONDS));
                
                if (_currentNetworkStatus.IsConnected)
                {
                    await SendNetworkMessage("NetworkRestored", "Połączenie internetowe przywrócone. Sprawdzanie przerwanych pobierań...");
                    await AttemptAutoResumeAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling network reconnection");
            }
        }

        private async Task AttemptAutoResumeAsync()
        {
            try
            {
                if (_autoResumeToken != null)
                {
                    _logger.LogDebug("Auto-resume already in progress");
                    return;
                }

                _autoResumeToken = new CancellationTokenSource();
                _autoResumeAttempts++;
                
                _logger.LogInformation($"Attempting auto-resume #{_autoResumeAttempts}");
                
                // Check if there's an incomplete download
                var session = _downloadManager.GetCurrentSession();
                if (!session.IsCompleted && session.FileProgress.Any())
                {
                    var canResume = await _downloadManager.AutoResumeDownloadAsync();
                    if (canResume)
                    {
                        await SendNetworkMessage("DownloadResuming", "Automatycznie wznawianie przerwanych pobierań...");
                        
                        // Trigger actual download resume in the model service
                        // This would typically be handled by the UI or a coordinator service
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                // Small delay to let UI update
                                await Task.Delay(1000, _autoResumeToken.Token);
                                
                                if (!_autoResumeToken.Token.IsCancellationRequested)
                                {
                                    // This would trigger the actual download resume
                                    // The implementation depends on how your download UI is structured
                                    await SendNetworkMessage("DownloadResumed", "Pobieranie zostało automatycznie wznowione");
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                // Expected when cancelled
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error in download resume task");
                                await SendNetworkMessage("AutoResumeError", "Nie udało się automatycznie wznowić pobierania");
                            }
                        });
                    }
                    else
                    {
                        await SendNetworkMessage("AutoResumeFailed", "Nie można automatycznie wznowić pobierania. Spróbuj ręcznie.");
                    }
                }
                
                _autoResumeToken?.Dispose();
                _autoResumeToken = null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-resume attempt");
                _autoResumeToken?.Dispose();
                _autoResumeToken = null;
            }
        }

        private async Task SendNetworkMessage(string messageKey, string fallbackMessage)
        {
            try
            {
                // Try to get localized message
                var localizedMessage = _localization?.GetString(messageKey) ?? fallbackMessage;
                NetworkMessageReceived?.Invoke(localizedMessage);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, $"Error sending network message: {messageKey}");
                NetworkMessageReceived?.Invoke(fallbackMessage);
            }
        }

        public NetworkStatus GetCurrentNetworkStatus() => _currentNetworkStatus;

        public bool ShouldAllowDownload()
        {
            if (!_currentNetworkStatus.IsConnected)
                return false;

            // If connection is metered, ask user (this would be handled by UI)
            if (_currentNetworkStatus.IsMetered)
            {
                _logger.LogWarning("Connection is metered - download should require user confirmation");
                // Return true for now, but UI should show warning
            }

            return true;
        }

        public void CancelAutoResume()
        {
            _autoResumeToken?.Cancel();
            _autoResumeAttempts = 0;
            _logger.LogInformation("Auto-resume cancelled by user");
        }

        public void ResetAutoResumeCounter()
        {
            _autoResumeAttempts = 0;
            _logger.LogInformation("Auto-resume counter reset");
        }

        public void Dispose()
        {
            try
            {
                NetworkChange.NetworkAvailabilityChanged -= OnNetworkAvailabilityChanged;
                NetworkChange.NetworkAddressChanged -= OnNetworkAddressChanged;
                
                _networkCheckTimer?.Dispose();
                _autoResumeToken?.Cancel();
                _autoResumeToken?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error disposing NetworkAwareDownloadService");
            }
        }
    }
}