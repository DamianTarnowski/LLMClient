using System;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;

namespace LLMClient.Services
{
    public interface IDeviceMemoryService
    {
        /// <summary>
        /// Gets total device RAM in MB
        /// </summary>
        long GetTotalRamMB();
        
        /// <summary>
        /// Gets available (free) RAM in MB
        /// </summary>
        long GetAvailableRamMB();
        
        /// <summary>
        /// Checks if device has enough RAM for the specified model
        /// </summary>
        bool HasEnoughRam(long requiredMB);
        
        /// <summary>
        /// Gets a warning message if RAM is insufficient, null if OK
        /// </summary>
        string? GetRamWarningMessage(long requiredMB, string modelName);
    }

    public class DeviceMemoryService : IDeviceMemoryService
    {
        private readonly ILogger<DeviceMemoryService> _logger;

        public DeviceMemoryService(ILogger<DeviceMemoryService> logger)
        {
            _logger = logger;
        }

        public long GetTotalRamMB()
        {
            try
            {
#if ANDROID
                return GetAndroidTotalRam();
#elif IOS || MACCATALYST
                return GetAppleTotalRam();
#elif WINDOWS
                return GetWindowsTotalRam();
#else
                return 0;
#endif
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get total RAM");
                return 0;
            }
        }

        public long GetAvailableRamMB()
        {
            try
            {
#if ANDROID
                return GetAndroidAvailableRam();
#elif IOS || MACCATALYST
                return GetAppleAvailableRam();
#elif WINDOWS
                return GetWindowsAvailableRam();
#else
                return 0;
#endif
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to get available RAM");
                return 0;
            }
        }

        public bool HasEnoughRam(long requiredMB)
        {
            var totalRam = GetTotalRamMB();
            if (totalRam == 0) return true; // Can't determine, allow download
            
            // Model needs requiredMB, but system also needs some RAM
            // Use 80% of total RAM as safe limit
            var safeLimit = (long)(totalRam * 0.8);
            return requiredMB <= safeLimit;
        }

        public string? GetRamWarningMessage(long requiredMB, string modelName)
        {
            var totalRam = GetTotalRamMB();
            var availableRam = GetAvailableRamMB();
            
            if (totalRam == 0)
            {
                return null; // Can't determine RAM, no warning
            }

            var totalGB = totalRam / 1024.0;
            var requiredGB = requiredMB / 1024.0;
            var availableGB = availableRam / 1024.0;

            // Critical: Required RAM > Total RAM
            if (requiredMB > totalRam)
            {
                return $"⚠️ OSTRZEŻENIE: Model '{modelName}' wymaga ~{requiredGB:F1} GB RAM, " +
                       $"ale Twoje urządzenie ma tylko {totalGB:F1} GB RAM.\n\n" +
                       $"Model prawdopodobnie NIE będzie działać poprawnie.\n\n" +
                       $"Czy mimo to chcesz pobrać model?";
            }

            // Warning: Required RAM > 80% of Total RAM
            var safeLimit = (long)(totalRam * 0.8);
            if (requiredMB > safeLimit)
            {
                return $"⚠️ Uwaga: Model '{modelName}' wymaga ~{requiredGB:F1} GB RAM.\n\n" +
                       $"Twoje urządzenie ma {totalGB:F1} GB RAM, co może być niewystarczające " +
                       $"dla stabilnej pracy.\n\n" +
                       $"Czy chcesz kontynuować pobieranie?";
            }

            // Info: Low available RAM right now
            if (availableRam > 0 && requiredMB > availableRam)
            {
                return $"ℹ️ Model '{modelName}' wymaga ~{requiredGB:F1} GB RAM.\n\n" +
                       $"Obecnie dostępne: {availableGB:F1} GB z {totalGB:F1} GB.\n" +
                       $"Zamknij inne aplikacje przed uruchomieniem modelu.\n\n" +
                       $"Czy chcesz pobrać model?";
            }

            return null; // No warning needed
        }

#if ANDROID
        private long GetAndroidTotalRam()
        {
            var activityManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.ActivityService) as Android.App.ActivityManager;
            if (activityManager != null)
            {
                var memInfo = new Android.App.ActivityManager.MemoryInfo();
                activityManager.GetMemoryInfo(memInfo);
                return memInfo.TotalMem / (1024 * 1024); // Convert bytes to MB
            }
            return 0;
        }

        private long GetAndroidAvailableRam()
        {
            var activityManager = Android.App.Application.Context.GetSystemService(Android.Content.Context.ActivityService) as Android.App.ActivityManager;
            if (activityManager != null)
            {
                var memInfo = new Android.App.ActivityManager.MemoryInfo();
                activityManager.GetMemoryInfo(memInfo);
                return memInfo.AvailMem / (1024 * 1024); // Convert bytes to MB
            }
            return 0;
        }
#endif

#if IOS || MACCATALYST
        private long GetAppleTotalRam()
        {
            // iOS doesn't expose total RAM directly, use ProcessInfo
            var totalMemory = (long)Foundation.NSProcessInfo.ProcessInfo.PhysicalMemory;
            return totalMemory / (1024 * 1024); // Convert bytes to MB
        }

        private long GetAppleAvailableRam()
        {
            // iOS doesn't provide available RAM easily
            // Return 0 to indicate unknown
            return 0;
        }
#endif

#if WINDOWS
        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORYSTATUSEX
        {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);

        private long GetWindowsTotalRam()
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)(memStatus.ullTotalPhys / (1024 * 1024)); // Convert bytes to MB
            }
            return 0;
        }

        private long GetWindowsAvailableRam()
        {
            var memStatus = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
            if (GlobalMemoryStatusEx(ref memStatus))
            {
                return (long)(memStatus.ullAvailPhys / (1024 * 1024)); // Convert bytes to MB
            }
            return 0;
        }
#endif
    }
}
