using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace LLMClient.Services
{
    public class DownloadSession
    {
        public string SessionId { get; set; } = Guid.NewGuid().ToString();
        public DateTime StartTime { get; set; } = DateTime.UtcNow;
        public DateTime? LastResumeTime { get; set; }
        public int ResumeCount { get; set; } = 0;
        public Dictionary<string, long> FileProgress { get; set; } = new();
        public bool IsCompleted { get; set; } = false;
        public string? LastError { get; set; }
        public long TotalBytesDownloaded { get; set; } = 0;
        public TimeSpan TotalDownloadTime { get; set; }
    }

    public enum DownloadFailureReason
    {
        NetworkError,
        InsufficientStorage,
        PermissionDenied,
        CorruptedFile,
        ServerError,
        UserCancelled,
        Unknown
    }

    public class LocalizedDownloadError
    {
        public DownloadFailureReason Reason { get; set; }
        public string TechnicalDetails { get; set; } = string.Empty;
        public Dictionary<string, string> LocalizedMessages { get; set; } = new();
        public Dictionary<string, string> LocalizedSolutions { get; set; } = new();
        public bool IsRetriable { get; set; } = true;
        public bool RequiresUserAction { get; set; } = false;
    }

    /// <summary>
    /// Smart download manager with automatic resume, localized errors, and storage management
    /// </summary>
    public class SmartDownloadManager : IDisposable
    {
        private readonly ILogger<SmartDownloadManager> _logger;
        private readonly ILocalizationService _localization;
        private readonly string _sessionPath;
        private DownloadSession _currentSession = new();
        private readonly Timer _autoResumeTimer;
        private const int AUTO_RESUME_CHECK_MINUTES = 2;
        private const int MAX_AUTO_RESUMES = 5;
        private const long MIN_FREE_SPACE_BYTES = 500_000_000; // 500MB safety margin

        public event Action<DownloadSession>? SessionUpdated;
        public event Action<LocalizedDownloadError>? LocalizedErrorOccurred;
        public event Action<double>? ProgressUpdated;

        public SmartDownloadManager(ILogger<SmartDownloadManager> logger, ILocalizationService localization)
        {
            _logger = logger;
            _localization = localization;
            _sessionPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), 
                "LLMClient", "download_session.json");

            // Load existing session
            Task.Run(LoadSessionAsync);

            // Auto-resume timer - checks every 2 minutes for incomplete downloads
            _autoResumeTimer = new Timer(CheckForAutoResume, null, 
                TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(AUTO_RESUME_CHECK_MINUTES));
        }

        private async void CheckForAutoResume(object? state)
        {
            try
            {
                if (_currentSession.IsCompleted || _currentSession.ResumeCount >= MAX_AUTO_RESUMES)
                    return;

                // Check if download was interrupted (last activity more than 5 minutes ago)
                var lastActivity = _currentSession.LastResumeTime ?? _currentSession.StartTime;
                if (DateTime.UtcNow - lastActivity > TimeSpan.FromMinutes(5))
                {
                    _logger.LogInformation($"Auto-resuming interrupted download (attempt {_currentSession.ResumeCount + 1})");
                    
                    // Trigger auto-resume (this would be called by the download service)
                    await AutoResumeDownloadAsync();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in auto-resume check");
            }
        }

        public async Task<bool> StartDownloadSessionAsync()
        {
            try
            {
                // Check storage space first
                var storageCheck = await CheckStorageSpaceAsync();
                if (!storageCheck.HasEnoughSpace)
                {
                    var error = CreateLocalizedError(DownloadFailureReason.InsufficientStorage, 
                        $"Available: {storageCheck.AvailableBytes / 1024 / 1024} MB, Required: {storageCheck.RequiredBytes / 1024 / 1024} MB");
                    LocalizedErrorOccurred?.Invoke(error);
                    return false;
                }

                _currentSession = new DownloadSession();
                await SaveSessionAsync();
                
                _logger.LogInformation($"Started new download session: {_currentSession.SessionId}");
                SessionUpdated?.Invoke(_currentSession);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start download session");
                var error = CreateLocalizedError(DownloadFailureReason.Unknown, ex.Message);
                LocalizedErrorOccurred?.Invoke(error);
                return false;
            }
        }

        public async Task<bool> AutoResumeDownloadAsync()
        {
            try
            {
                if (_currentSession.ResumeCount >= MAX_AUTO_RESUMES)
                {
                    _logger.LogWarning($"Max auto-resume attempts reached ({MAX_AUTO_RESUMES})");
                    var error = CreateLocalizedError(DownloadFailureReason.NetworkError, 
                        $"Maximum resume attempts ({MAX_AUTO_RESUMES}) reached");
                    error.RequiresUserAction = true;
                    error.IsRetriable = false;
                    LocalizedErrorOccurred?.Invoke(error);
                    return false;
                }

                _currentSession.ResumeCount++;
                _currentSession.LastResumeTime = DateTime.UtcNow;
                await SaveSessionAsync();

                _logger.LogInformation($"Auto-resuming download (attempt {_currentSession.ResumeCount})");
                SessionUpdated?.Invoke(_currentSession);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to auto-resume download");
                return false;
            }
        }

        public async Task UpdateProgressAsync(string fileName, long bytesDownloaded, double progressPercent)
        {
            try
            {
                _currentSession.FileProgress[fileName] = bytesDownloaded;
                _currentSession.TotalBytesDownloaded = _currentSession.FileProgress.Values.Sum();
                _currentSession.TotalDownloadTime = DateTime.UtcNow - _currentSession.StartTime;

                await SaveSessionAsync();
                ProgressUpdated?.Invoke(progressPercent);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error updating progress");
            }
        }

        public async Task CompleteSessionAsync()
        {
            try
            {
                _currentSession.IsCompleted = true;
                _currentSession.TotalDownloadTime = DateTime.UtcNow - _currentSession.StartTime;
                await SaveSessionAsync();

                _logger.LogInformation($"Download session completed in {_currentSession.TotalDownloadTime.TotalMinutes:F1} minutes");
                SessionUpdated?.Invoke(_currentSession);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error completing session");
            }
        }

        public async Task HandleDownloadErrorAsync(Exception exception, string? context = null)
        {
            try
            {
                var reason = ClassifyError(exception);
                var error = CreateLocalizedError(reason, exception.Message, context);
                
                _currentSession.LastError = exception.Message;
                await SaveSessionAsync();

                _logger.LogError(exception, $"Download error classified as {reason}: {context}");
                LocalizedErrorOccurred?.Invoke(error);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error handling download error");
            }
        }

        private DownloadFailureReason ClassifyError(Exception exception)
        {
            var message = exception.Message.ToLower();
            var type = exception.GetType().Name.ToLower();

            // Network-related errors
            if (message.Contains("timeout") || message.Contains("network") || 
                message.Contains("connection") || type.Contains("http"))
            {
                return DownloadFailureReason.NetworkError;
            }

            // Storage-related errors
            if (message.Contains("disk") || message.Contains("space") || 
                message.Contains("storage") || type.Contains("io"))
            {
                return DownloadFailureReason.InsufficientStorage;
            }

            // Permission errors
            if (message.Contains("access") || message.Contains("permission") || 
                message.Contains("denied") || message.Contains("unauthorized"))
            {
                return DownloadFailureReason.PermissionDenied;
            }

            // Server errors
            if (message.Contains("404") || message.Contains("500") || 
                message.Contains("server") || message.Contains("service"))
            {
                return DownloadFailureReason.ServerError;
            }

            // Corruption errors
            if (message.Contains("corrupt") || message.Contains("hash") || 
                message.Contains("checksum") || message.Contains("invalid"))
            {
                return DownloadFailureReason.CorruptedFile;
            }

            // Cancellation
            if (type.Contains("canceled") || type.Contains("cancelled") || 
                message.Contains("cancel"))
            {
                return DownloadFailureReason.UserCancelled;
            }

            return DownloadFailureReason.Unknown;
        }

        private LocalizedDownloadError CreateLocalizedError(DownloadFailureReason reason, string technicalDetails, string? context = null)
        {
            var error = new LocalizedDownloadError
            {
                Reason = reason,
                TechnicalDetails = technicalDetails
            };

            // Get current language
            var currentLang = _localization.CurrentCulture.ToLower();
            var langCode = currentLang.Split('-')[0]; // Get just language part (e.g., "pl" from "pl-PL")

            switch (reason)
            {
                case DownloadFailureReason.NetworkError:
                    error.LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = "Network connection problem occurred during download.",
                        ["pl"] = "Wystąpił problem z połączeniem sieciowym podczas pobierania.",
                        ["de"] = "Netzwerkverbindungsproblem während des Downloads aufgetreten.",
                        ["es"] = "Se produjo un problema de conexión de red durante la descarga.",
                        ["fr"] = "Problème de connexion réseau lors du téléchargement.",
                        ["it"] = "Si è verificato un problema di connessione di rete durante il download.",
                        ["ru"] = "Возникла проблема с сетевым подключением при загрузке.",
                        ["ja"] = "ダウンロード中にネットワーク接続の問題が発生しました。",
                        ["ko"] = "다운로드 중 네트워크 연결 문제가 발생했습니다.",
                        ["zh"] = "下载过程中出现网络连接问题。"
                    };
                    error.LocalizedSolutions = new Dictionary<string, string>
                    {
                        ["en"] = "Check your internet connection and try again. The download will resume automatically.",
                        ["pl"] = "Sprawdź połączenie internetowe i spróbuj ponownie. Pobieranie zostanie automatycznie wznowione.",
                        ["de"] = "Überprüfen Sie Ihre Internetverbindung und versuchen Sie es erneut. Der Download wird automatisch fortgesetzt.",
                        ["es"] = "Verifique su conexión a internet e intente nuevamente. La descarga se reanudará automáticamente.",
                        ["fr"] = "Vérifiez votre connexion internet et réessayez. Le téléchargement reprendra automatiquement.",
                        ["it"] = "Controlla la connessione internet e riprova. Il download riprenderà automaticamente.",
                        ["ru"] = "Проверьте подключение к интернету и попробуйте снова. Загрузка возобновится автоматически.",
                        ["ja"] = "インターネット接続を確認して再試行してください。ダウンロードは自動的に再開されます。",
                        ["ko"] = "인터넷 연결을 확인하고 다시 시도하세요. 다운로드가 자동으로 재개됩니다.",
                        ["zh"] = "请检查您的网络连接并重试。下载将自动恢复。"
                    };
                    error.IsRetriable = true;
                    break;

                case DownloadFailureReason.InsufficientStorage:
                    error.LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = "Not enough storage space available for the AI model (2.4 GB required).",
                        ["pl"] = "Brak wystarczającej ilości miejsca na dysku dla modelu AI (wymagane 2,4 GB).",
                        ["de"] = "Nicht genügend Speicherplatz für das KI-Modell verfügbar (2,4 GB erforderlich).",
                        ["es"] = "No hay suficiente espacio de almacenamiento para el modelo de IA (se requieren 2,4 GB).",
                        ["fr"] = "Espace de stockage insuffisant pour le modèle IA (2,4 Go requis).",
                        ["it"] = "Spazio di archiviazione insufficiente per il modello AI (richiesti 2,4 GB).",
                        ["ru"] = "Недостаточно места для хранения модели ИИ (требуется 2,4 ГБ).",
                        ["ja"] = "AIモデル用の十分なストレージ容量がありません（2.4 GB必要）。",
                        ["ko"] = "AI 모델을 위한 충분한 저장 공간이 없습니다 (2.4 GB 필요).",
                        ["zh"] = "存储空间不足，无法下载AI模型（需要2.4 GB）。"
                    };
                    error.LocalizedSolutions = new Dictionary<string, string>
                    {
                        ["en"] = "Free up storage space and try again. You can use cloud AI models instead.",
                        ["pl"] = "Zwolnij miejsce na dysku i spróbuj ponownie. Możesz używać modeli AI w chmurze.",
                        ["de"] = "Geben Sie Speicherplatz frei und versuchen Sie es erneut. Sie können stattdessen Cloud-KI-Modelle verwenden.",
                        ["es"] = "Libere espacio de almacenamiento e intente nuevamente. Puede usar modelos de IA en la nube.",
                        ["fr"] = "Libérez de l'espace de stockage et réessayez. Vous pouvez utiliser des modèles IA cloud à la place.",
                        ["it"] = "Libera spazio di archiviazione e riprova. Puoi usare modelli AI cloud invece.",
                        ["ru"] = "Освободите место и попробуйте снова. Вы можете использовать облачные модели ИИ.",
                        ["ja"] = "ストレージ容量を確保して再試行してください。代わりにクラウドAIモデルを使用できます。",
                        ["ko"] = "저장 공간을 확보하고 다시 시도하세요. 대신 클라우드 AI 모델을 사용할 수 있습니다.",
                        ["zh"] = "释放存储空间后重试。您可以改用云端AI模型。"
                    };
                    error.IsRetriable = true;
                    error.RequiresUserAction = true;
                    break;

                case DownloadFailureReason.PermissionDenied:
                    error.LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = "Permission denied. Cannot write to storage location.",
                        ["pl"] = "Odmowa dostępu. Nie można zapisać w lokalizacji przechowywania.",
                        ["de"] = "Zugriff verweigert. Kann nicht in Speicherort schreiben.",
                        ["es"] = "Permiso denegado. No se puede escribir en la ubicación de almacenamiento.",
                        ["fr"] = "Permission refusée. Impossible d'écrire dans l'emplacement de stockage.",
                        ["it"] = "Permesso negato. Impossibile scrivere nella posizione di archiviazione.",
                        ["ru"] = "Доступ запрещен. Невозможно записать в место хранения.",
                        ["ja"] = "アクセス許可が拒否されました。保存場所に書き込めません。",
                        ["ko"] = "권한이 거부되었습니다. 저장 위치에 쓸 수 없습니다.",
                        ["zh"] = "权限被拒绝。无法写入存储位置。"
                    };
                    error.LocalizedSolutions = new Dictionary<string, string>
                    {
                        ["en"] = "Restart the app or check app permissions in system settings.",
                        ["pl"] = "Uruchom ponownie aplikację lub sprawdź uprawnienia w ustawieniach systemu.",
                        ["de"] = "Starten Sie die App neu oder überprüfen Sie die App-Berechtigungen in den Systemeinstellungen.",
                        ["es"] = "Reinicie la aplicación o verifique los permisos en la configuración del sistema.",
                        ["fr"] = "Redémarrez l'application ou vérifiez les permissions dans les paramètres système.",
                        ["it"] = "Riavvia l'app o controlla i permessi nelle impostazioni di sistema.",
                        ["ru"] = "Перезапустите приложение или проверьте разрешения в системных настройках.",
                        ["ja"] = "アプリを再起動するか、システム設定でアプリの権限を確認してください。",
                        ["ko"] = "앱을 다시 시작하거나 시스템 설정에서 앱 권한을 확인하세요.",
                        ["zh"] = "重启应用或在系统设置中检查应用权限。"
                    };
                    error.RequiresUserAction = true;
                    break;

                case DownloadFailureReason.CorruptedFile:
                    error.LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = "Downloaded file is corrupted or incomplete.",
                        ["pl"] = "Pobrany plik jest uszkodzony lub niekompletny.",
                        ["de"] = "Die heruntergeladene Datei ist beschädigt oder unvollständig.",
                        ["es"] = "El archivo descargado está corrupto o incompleto.",
                        ["fr"] = "Le fichier téléchargé est corrompu ou incomplet.",
                        ["it"] = "Il file scaricato è corrotto o incompleto.",
                        ["ru"] = "Загруженный файл поврежден или неполный.",
                        ["ja"] = "ダウンロードしたファイルが破損しているか不完全です。",
                        ["ko"] = "다운로드한 파일이 손상되었거나 불완전합니다.",
                        ["zh"] = "下载的文件已损坏或不完整。"
                    };
                    error.LocalizedSolutions = new Dictionary<string, string>
                    {
                        ["en"] = "The download will restart automatically to fix the corrupted file.",
                        ["pl"] = "Pobieranie zostanie automatycznie wznowione aby naprawić uszkodzony plik.",
                        ["de"] = "Der Download wird automatisch neu gestartet, um die beschädigte Datei zu reparieren.",
                        ["es"] = "La descarga se reiniciará automáticamente para reparar el archivo corrupto.",
                        ["fr"] = "Le téléchargement redémarrera automatiquement pour réparer le fichier corrompu.",
                        ["it"] = "Il download ripartirà automaticamente per riparare il file corrotto.",
                        ["ru"] = "Загрузка автоматически перезапустится для восстановления поврежденного файла.",
                        ["ja"] = "破損したファイルを修復するため、ダウンロードが自動的に再開されます。",
                        ["ko"] = "손상된 파일을 수정하기 위해 다운로드가 자동으로 다시 시작됩니다.",
                        ["zh"] = "下载将自动重新开始以修复损坏的文件。"
                    };
                    error.IsRetriable = true;
                    break;

                case DownloadFailureReason.UserCancelled:
                    error.LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = "Download was cancelled by user.",
                        ["pl"] = "Pobieranie zostało anulowane przez użytkownika.",
                        ["de"] = "Download wurde vom Benutzer abgebrochen.",
                        ["es"] = "La descarga fue cancelada por el usuario.",
                        ["fr"] = "Le téléchargement a été annulé par l'utilisateur.",
                        ["it"] = "Il download è stato annullato dall'utente.",
                        ["ru"] = "Загрузка была отменена пользователем.",
                        ["ja"] = "ダウンロードはユーザーによってキャンセルされました。",
                        ["ko"] = "사용자가 다운로드를 취소했습니다.",
                        ["zh"] = "用户取消了下载。"
                    };
                    error.LocalizedSolutions = new Dictionary<string, string>
                    {
                        ["en"] = "You can resume the download anytime from settings.",
                        ["pl"] = "Możesz wznowić pobieranie w dowolnym momencie z ustawień.",
                        ["de"] = "Sie können den Download jederzeit aus den Einstellungen fortsetzen.",
                        ["es"] = "Puede reanudar la descarga en cualquier momento desde la configuración.",
                        ["fr"] = "Vous pouvez reprendre le téléchargement à tout moment depuis les paramètres.",
                        ["it"] = "Puoi riprendere il download in qualsiasi momento dalle impostazioni.",
                        ["ru"] = "Вы можете возобновить загрузку в любое время из настроек.",
                        ["ja"] = "設定からいつでもダウンロードを再開できます。",
                        ["ko"] = "설정에서 언제든지 다운로드를 재개할 수 있습니다.",
                        ["zh"] = "您可以随时从设置中恢复下载。"
                    };
                    error.IsRetriable = true;
                    error.RequiresUserAction = false;
                    break;

                default:
                    error.LocalizedMessages = new Dictionary<string, string>
                    {
                        ["en"] = "An unexpected error occurred during download.",
                        ["pl"] = "Wystąpił nieoczekiwany błąd podczas pobierania.",
                        ["de"] = "Ein unerwarteter Fehler ist während des Downloads aufgetreten.",
                        ["es"] = "Ocurrió un error inesperado durante la descarga.",
                        ["fr"] = "Une erreur inattendue s'est produite pendant le téléchargement.",
                        ["it"] = "Si è verificato un errore imprevisto durante il download.",
                        ["ru"] = "Произошла непредвиденная ошибка при загрузке.",
                        ["ja"] = "ダウンロード中に予期しないエラーが発生しました。",
                        ["ko"] = "다운로드 중 예기치 않은 오류가 발생했습니다.",
                        ["zh"] = "下载过程中发生意外错误。"
                    };
                    error.LocalizedSolutions = new Dictionary<string, string>
                    {
                        ["en"] = "Please try again later or contact support.",
                        ["pl"] = "Spróbuj ponownie później lub skontaktuj się z pomocą techniczną.",
                        ["de"] = "Bitte versuchen Sie es später erneut oder kontaktieren Sie den Support.",
                        ["es"] = "Inténtelo de nuevo más tarde o contacte al soporte.",
                        ["fr"] = "Veuillez réessayer plus tard ou contacter le support.",
                        ["it"] = "Riprova più tardi o contatta il supporto.",
                        ["ru"] = "Попробуйте позже или обратитесь в поддержку.",
                        ["ja"] = "後でもう一度試すか、サポートにお問い合わせください。",
                        ["ko"] = "나중에 다시 시도하거나 지원팀에 문의하세요.",
                        ["zh"] = "请稍后重试或联系支持。"
                    };
                    break;
            }

            return error;
        }

        public string GetLocalizedMessage(LocalizedDownloadError error)
        {
            var currentLang = _localization.CurrentCulture.ToLower();
            var langCode = currentLang.Split('-')[0];

            if (error.LocalizedMessages.TryGetValue(langCode, out var message))
                return message;
                
            if (error.LocalizedMessages.TryGetValue("en", out var englishMessage))
                return englishMessage;
                
            return "Download error occurred.";
        }

        public string GetLocalizedSolution(LocalizedDownloadError error)
        {
            var currentLang = _localization.CurrentCulture.ToLower();
            var langCode = currentLang.Split('-')[0];

            if (error.LocalizedSolutions.TryGetValue(langCode, out var solution))
                return solution;
                
            if (error.LocalizedSolutions.TryGetValue("en", out var englishSolution))
                return englishSolution;
                
            return "Please try again.";
        }

        private async Task<(bool HasEnoughSpace, long AvailableBytes, long RequiredBytes)> CheckStorageSpaceAsync()
        {
            try
            {
                var modelPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LLMClient", "Models");
                Directory.CreateDirectory(modelPath);

                var drive = new DriveInfo(Path.GetPathRoot(modelPath)!);
                var availableBytes = drive.AvailableFreeSpace;
                var requiredBytes = 2_400_000_000L + MIN_FREE_SPACE_BYTES; // Model size + safety margin

                return (availableBytes >= requiredBytes, availableBytes, requiredBytes);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking storage space");
                return (false, 0, 2_400_000_000L);
            }
        }

        private async Task LoadSessionAsync()
        {
            try
            {
                if (File.Exists(_sessionPath))
                {
                    var json = await File.ReadAllTextAsync(_sessionPath);
                    _currentSession = JsonSerializer.Deserialize<DownloadSession>(json) ?? new DownloadSession();
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load download session");
                _currentSession = new DownloadSession();
            }
        }

        private async Task SaveSessionAsync()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_sessionPath)!);
                var json = JsonSerializer.Serialize(_currentSession, new JsonSerializerOptions { WriteIndented = true });
                await File.WriteAllTextAsync(_sessionPath, json);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to save download session");
            }
        }

        public DownloadSession GetCurrentSession() => _currentSession;

        public void Dispose()
        {
            _autoResumeTimer?.Dispose();
        }
    }
}