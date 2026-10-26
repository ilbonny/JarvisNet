using System.Diagnostics;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using JarvisNet.Engine.Options;
using JarvisNet.Plugins.Sdk.Options;
using JarvisNet.UI.Models;
using Microsoft.Extensions.Options;

namespace JarvisNet.UI.Services;

public sealed class SystemStatusProvider : IDisposable
{
    private readonly OllamaOptions _ollama;
    private readonly IOptionsMonitor<JarvisLocaleOptions> _locale;
    private readonly object _netLock = new();
    private long _lastNetBytes;
    private DateTime _lastNetUtc = DateTime.UtcNow;
    private PerformanceCounter? _cpuCounter;
    private bool _cpuPrimed;

    public SystemStatusProvider(
        IOptions<OllamaOptions> ollama,
        IOptionsMonitor<JarvisLocaleOptions> locale)
    {
        _ollama = ollama.Value;
        _locale = locale;
        TryInitCpuCounter();
    }

    public SystemStatusResponse GetSnapshot()
    {
        var cpu = SampleCpuUsage();
        var memory = SampleMemoryUsage();
        var gpu = SampleGpuMemoryUsage();
        var networkKbps = SampleNetworkKbps();
        var power = SamplePower();

        var model = string.IsNullOrWhiteSpace(_ollama.ModelName) ? "—" : _ollama.ModelName.Trim();
        var language = string.IsNullOrWhiteSpace(_locale.CurrentValue.CultureName)
            ? "—"
            : _locale.CurrentValue.CultureName.Trim();

        return new SystemStatusResponse(
            AiModel: model,
            AiModelStatus: "Active",
            Language: language,
            LanguageStatus: "Ready",
            CpuUsagePercent: cpu,
            CpuStatus: cpu.HasValue ? "Monitoring" : "Unavailable",
            MemoryUsagePercent: memory,
            MemoryStatus: memory.HasValue ? "Monitoring" : "Unavailable",
            GpuMemoryUsagePercent: gpu,
            GpuMemoryStatus: gpu.HasValue ? "Monitoring" : "Unavailable",
            NetworkKbps: networkKbps,
            NetworkStatus: networkKbps > 0 ? "Active" : "Idle",
            PowerStatus: power.Status,
            PowerDetail: power.Detail);
    }

    public void Dispose()
    {
        _cpuCounter?.Dispose();
    }

    private void TryInitCpuCounter()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _ = _cpuCounter.NextValue();
            _cpuPrimed = false;
        }
        catch
        {
            _cpuCounter?.Dispose();
            _cpuCounter = null;
        }
    }

    private double? SampleCpuUsage()
    {
        if (OperatingSystem.IsWindows() && _cpuCounter is not null)
        {
            try
            {
                var value = _cpuCounter.NextValue();
                if (!_cpuPrimed)
                {
                    _cpuPrimed = true;
                    Thread.Sleep(80);
                    value = _cpuCounter.NextValue();
                }

                return Math.Clamp(Math.Round(value, 0), 0, 100);
            }
            catch
            {
                return null;
            }
        }

        if (OperatingSystem.IsLinux())
        {
            return TryReadLinuxCpuUsage();
        }

        return null;
    }

    private static double? TryReadLinuxCpuUsage()
    {
        try
        {
            var lines = File.ReadAllLines("/proc/stat");
            var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu ", StringComparison.Ordinal));
            if (cpuLine is null)
            {
                return null;
            }

            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return null;
            }

            long user = long.Parse(parts[1]);
            long nice = long.Parse(parts[2]);
            long system = long.Parse(parts[3]);
            long idle = long.Parse(parts[4]);
            long total1 = user + nice + system + idle;
            long active1 = user + nice + system;
            Thread.Sleep(100);
            lines = File.ReadAllLines("/proc/stat");
            cpuLine = lines.First(l => l.StartsWith("cpu ", StringComparison.Ordinal));
            parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            user = long.Parse(parts[1]);
            nice = long.Parse(parts[2]);
            system = long.Parse(parts[3]);
            idle = long.Parse(parts[4]);
            long total2 = user + nice + system + idle;
            long active2 = user + nice + system;
            var totalDelta = total2 - total1;
            if (totalDelta <= 0)
            {
                return null;
            }

            var usage = (active2 - active1) * 100.0 / totalDelta;
            return Math.Clamp(Math.Round(usage, 0), 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private static double? SampleMemoryUsage()
    {
        if (OperatingSystem.IsWindows())
        {
            return TryReadWindowsMemoryUsage();
        }

        if (OperatingSystem.IsLinux())
        {
            return TryReadLinuxMemoryUsage();
        }

        return null;
    }

    private static double? TryReadWindowsMemoryUsage()
    {
        try
        {
            var status = new MemoryStatusEx { DwLength = (uint)Marshal.SizeOf<MemoryStatusEx>() };
            if (!GlobalMemoryStatusEx(ref status))
            {
                return null;
            }

            var used = status.UllTotalPhys - status.UllAvailPhys;
            if (status.UllTotalPhys == 0)
            {
                return null;
            }

            return Math.Clamp(Math.Round(used * 100.0 / status.UllTotalPhys, 0), 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private static double? TryReadLinuxMemoryUsage()
    {
        try
        {
            long memTotal = 0;
            long memAvailable = 0;
            foreach (var line in File.ReadLines("/proc/meminfo"))
            {
                if (line.StartsWith("MemTotal:", StringComparison.Ordinal))
                {
                    memTotal = ParseMemInfoKb(line);
                }
                else if (line.StartsWith("MemAvailable:", StringComparison.Ordinal))
                {
                    memAvailable = ParseMemInfoKb(line);
                }
            }

            if (memTotal <= 0)
            {
                return null;
            }

            var used = memTotal - memAvailable;
            return Math.Clamp(Math.Round(used * 100.0 / memTotal, 0), 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private static long ParseMemInfoKb(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length >= 2 ? long.Parse(parts[1]) : 0;
    }

    private static double? SampleGpuMemoryUsage()
    {
        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        try
        {
            var category = new PerformanceCounterCategory("GPU Adapter Memory");
            var instanceNames = category.GetInstanceNames();
            if (instanceNames.Length == 0)
            {
                return null;
            }

            float usedSum = 0;
            float totalSum = 0;
            foreach (var instance in instanceNames)
            {
                using var dedicated = new PerformanceCounter("GPU Adapter Memory", "Dedicated Usage", instance, true);
                using var total = new PerformanceCounter("GPU Adapter Memory", "Total Committed", instance, true);
                usedSum += dedicated.NextValue();
                totalSum += Math.Max(total.NextValue(), 1);
            }

            if (totalSum <= 0)
            {
                return null;
            }

            return Math.Clamp(Math.Round(usedSum * 100.0 / totalSum, 0), 0, 100);
        }
        catch
        {
            return null;
        }
    }

    private double SampleNetworkKbps()
    {
        lock (_netLock)
        {
            try
            {
                var bytes = NetworkInterface.GetAllNetworkInterfaces()
                    .Where(n =>
                        n.OperationalStatus == OperationalStatus.Up
                        && n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                    .Sum(n => n.GetIPv4Statistics().BytesReceived + n.GetIPv4Statistics().BytesSent);

                var now = DateTime.UtcNow;
                var elapsed = (now - _lastNetUtc).TotalSeconds;
                if (_lastNetBytes > 0 && elapsed > 0.05)
                {
                    var delta = Math.Max(0, bytes - _lastNetBytes);
                    var kbps = delta / elapsed / 1024.0;
                    _lastNetBytes = bytes;
                    _lastNetUtc = now;
                    return Math.Round(kbps, 1);
                }

                _lastNetBytes = bytes;
                _lastNetUtc = now;
                return 0;
            }
            catch
            {
                return 0;
            }
        }
    }

    private static (string Status, string Detail) SamplePower()
    {
        try
        {
            if (OperatingSystem.IsWindows())
            {
                if (!GetSystemPowerStatus(out var power))
                {
                    return ("Unknown", "—");
                }

                if (power.ACLineStatus == 1)
                {
                    return ("AC Power", "Plugged in");
                }

                if (power.ACLineStatus == 0)
                {
                    return ("On Battery", FormatBatteryPercent(power.BatteryLifePercent));
                }

                return ("Unknown", "—");
            }

            if (OperatingSystem.IsLinux())
            {
                const string acPath = "/sys/class/power_supply/AC/online";
                if (File.Exists(acPath) && File.ReadAllText(acPath).Trim() == "1")
                {
                    return ("AC Power", "Plugged in");
                }

                return ("On Battery", "Battery");
            }
        }
        catch
        {
            // ignored
        }

        return ("Unknown", "—");
    }

    private static string FormatBatteryPercent(byte batteryLifePercent)
    {
        return batteryLifePercent is <= 100 ? $"{batteryLifePercent}%" : "Battery";
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    private struct MemoryStatusEx
    {
        public uint DwLength;
        public uint DwMemoryLoad;
        public ulong UllTotalPhys;
        public ulong UllAvailPhys;
        public ulong UllTotalPageFile;
        public ulong UllAvailPageFile;
        public ulong UllTotalVirtual;
        public ulong UllAvailVirtual;
        public ulong UllAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatusEx lpBuffer);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out SystemPowerStatus lpSystemPowerStatus);

    [StructLayout(LayoutKind.Sequential)]
    private struct SystemPowerStatus
    {
        public byte ACLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte Reserved1;
        public int BatteryLifeTime;
        public int BatteryFullLifeTime;
    }
}
