using System;
using Avalonia.Controls;
using Tasks.Platform;

namespace Tasks.Tests;

public static class EnvironmentTests
{
    public static void RunAllTests()
    {
        Console.WriteLine("[Tests] Running Cross-Platform CSD & Decoration Tests...");

        TestWindows10CornerRounding();
        TestWindows11CornerRounding();
        TestMacOSCornerRounding();
        TestGnomeCornerRounding();
        TestWindowsButtonLayout();
        TestMacOSButtonLayout();
        TestGnomeButtonLayoutCustom();
        TestSymbolicIconFraming();

        Console.WriteLine("[Tests] All 8 tests passed successfully!");
    }

    private static void AssertEqual<T>(T expected, T actual, string testName)
    {
        if (!object.Equals(expected, actual))
            throw new Exception($"Test '{testName}' failed: expected {expected}, got {actual}");
    }

    private static void TestWindows10CornerRounding()
    {
        WindowEnvironment.ForcedKind = DesktopEnvironmentKind.Windows10;
        var r = WindowEnvironment.GetTargetCornerRadius(WindowState.Normal);
        AssertEqual(0.0, r.TopLeft, "Windows 10 normal TopLeft");
        AssertEqual(0.0, r.BottomRight, "Windows 10 normal BottomRight");
        WindowEnvironment.ForcedKind = null;
    }

    private static void TestWindows11CornerRounding()
    {
        WindowEnvironment.ForcedKind = DesktopEnvironmentKind.Windows11;
        var r = WindowEnvironment.GetTargetCornerRadius(WindowState.Normal);
        AssertEqual(8.0, r.TopLeft, "Windows 11 normal TopLeft");
        var max = WindowEnvironment.GetTargetCornerRadius(WindowState.Maximized);
        AssertEqual(0.0, max.TopLeft, "Windows 11 maximized TopLeft");
        WindowEnvironment.ForcedKind = null;
    }

    private static void TestMacOSCornerRounding()
    {
        WindowEnvironment.ForcedKind = DesktopEnvironmentKind.MacOS;
        var r = WindowEnvironment.GetTargetCornerRadius(WindowState.Normal);
        AssertEqual(12.0, r.TopLeft, "macOS normal TopLeft");
        var max = WindowEnvironment.GetTargetCornerRadius(WindowState.Maximized);
        AssertEqual(0.0, max.TopLeft, "macOS maximized TopLeft");
        WindowEnvironment.ForcedKind = null;
    }

    private static void TestGnomeCornerRounding()
    {
        WindowEnvironment.ForcedKind = DesktopEnvironmentKind.Gnome;
        var r = WindowEnvironment.GetTargetCornerRadius(WindowState.Normal);
        AssertEqual(15.0, r.TopLeft, "GNOME normal TopLeft");
        var max = WindowEnvironment.GetTargetCornerRadius(WindowState.Maximized);
        AssertEqual(0.0, max.TopLeft, "GNOME maximized TopLeft");
        WindowEnvironment.ForcedKind = null;
    }

    private static void TestWindowsButtonLayout()
    {
        WindowEnvironment.ForcedKind = DesktopEnvironmentKind.Windows11;
        var layout = GnomeDesktopService.GetButtonLayout();
        AssertEqual(0, layout.LeftButtons.Count, "Windows LeftButtons count");
        AssertEqual(3, layout.RightButtons.Count, "Windows RightButtons count");
        AssertEqual(WindowButtonKind.Minimize, layout.RightButtons[0], "Windows first button");
        AssertEqual(WindowButtonKind.Maximize, layout.RightButtons[1], "Windows second button");
        AssertEqual(WindowButtonKind.Close, layout.RightButtons[2], "Windows third button");
        WindowEnvironment.ForcedKind = null;
    }

    private static void TestMacOSButtonLayout()
    {
        WindowEnvironment.ForcedKind = DesktopEnvironmentKind.MacOS;
        var layout = GnomeDesktopService.GetButtonLayout();
        AssertEqual(3, layout.LeftButtons.Count, "macOS LeftButtons count");
        AssertEqual(0, layout.RightButtons.Count, "macOS RightButtons count");
        AssertEqual(WindowButtonKind.Close, layout.LeftButtons[0], "macOS first button");
        AssertEqual(WindowButtonKind.Minimize, layout.LeftButtons[1], "macOS second button");
        AssertEqual(WindowButtonKind.Maximize, layout.LeftButtons[2], "macOS third button");
        WindowEnvironment.ForcedKind = null;
    }

    private static void TestGnomeButtonLayoutCustom()
    {
        var layout = GnomeDesktopService.ParseButtonLayout("close,minimize,maximize:appmenu");
        AssertEqual(3, layout.LeftButtons.Count, "GNOME custom LeftButtons count");
        AssertEqual(WindowButtonKind.Close, layout.LeftButtons[0], "GNOME custom first");
        AssertEqual(WindowButtonKind.Minimize, layout.LeftButtons[1], "GNOME custom second");
        AssertEqual(WindowButtonKind.Maximize, layout.LeftButtons[2], "GNOME custom third");
        AssertEqual(0, layout.RightButtons.Count, "GNOME custom RightButtons count");
    }

    private static void TestSymbolicIconFraming()
    {
        var geom = LinuxIconThemeResolver.GetMinimizeIcon("Adwaita");
        var bounds = geom.Bounds;
        AssertEqual(16.0, bounds.Width, "Minimize icon framed width");
        AssertEqual(16.0, bounds.Height, "Minimize icon framed height");
    }
}
