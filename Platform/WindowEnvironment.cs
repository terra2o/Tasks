using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;

namespace Tasks.Platform;

public enum DesktopEnvironmentKind
{
    Windows10,
    Windows11,
    MacOS,
    Gnome,
    OtherLinux
}

public static class WindowEnvironment
{
    public static DesktopEnvironmentKind? ForcedKind { get; set; }

    private static readonly DesktopEnvironmentKind _detectedKind = DetectDesktopEnvironment();

    public static DesktopEnvironmentKind Current => ForcedKind ?? _detectedKind;

    public static bool IsGnomeOrLinux => Current == DesktopEnvironmentKind.Gnome || Current == DesktopEnvironmentKind.OtherLinux;
    public static bool IsWindows => Current == DesktopEnvironmentKind.Windows10 || Current == DesktopEnvironmentKind.Windows11;
    public static bool IsMacOS => Current == DesktopEnvironmentKind.MacOS;

    private static DesktopEnvironmentKind DetectDesktopEnvironment()
    {
        if (OperatingSystem.IsWindows())
        {
            // Windows 11 is build 22000 or greater
            if (Environment.OSVersion.Version.Build >= 22000)
                return DesktopEnvironmentKind.Windows11;
            return DesktopEnvironmentKind.Windows10;
        }

        if (OperatingSystem.IsMacOS())
        {
            return DesktopEnvironmentKind.MacOS;
        }

        if (OperatingSystem.IsLinux() || RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            var desktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP") ?? "";
            var session = Environment.GetEnvironmentVariable("DESKTOP_SESSION") ?? "";
            if (desktop.Contains("GNOME", StringComparison.OrdinalIgnoreCase) ||
                session.Contains("gnome", StringComparison.OrdinalIgnoreCase))
            {
                return DesktopEnvironmentKind.Gnome;
            }
            return DesktopEnvironmentKind.OtherLinux;
        }

        return DesktopEnvironmentKind.OtherLinux;
    }

    public static CornerRadius GetTargetCornerRadius(WindowState state)
    {
        if (state == WindowState.Maximized || state == WindowState.FullScreen)
            return new CornerRadius(0);

        return Current switch
        {
            DesktopEnvironmentKind.Windows10 => new CornerRadius(0),
            DesktopEnvironmentKind.Windows11 => new CornerRadius(8),
            DesktopEnvironmentKind.MacOS => new CornerRadius(12),
            DesktopEnvironmentKind.Gnome => new CornerRadius(15),
            _ => new CornerRadius(10)
        };
    }
}
