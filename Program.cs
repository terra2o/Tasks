using Avalonia;
using System;
using System.Diagnostics;

namespace Tasks;

class Program
{
    public static Stopwatch StartupWatch = Stopwatch.StartNew();
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--test")
        {
            AppBuilder.Configure<App>()
                .UseStandardRuntimePlatformSubsystem()
                .UseWindowingSubsystem(() => { })
                .UseSkia()
                .SetupWithoutStarting();

            Tasks.Tests.EnvironmentTests.RunAllTests();
            return;
        }

        if (args.Length > 0 && args[0].StartsWith("--env="))
        {
            var env = args[0].Substring("--env=".Length);
            if (string.Equals(env, "windows10", StringComparison.OrdinalIgnoreCase))
                Tasks.Platform.WindowEnvironment.ForcedKind = Tasks.Platform.DesktopEnvironmentKind.Windows10;
            else if (string.Equals(env, "windows11", StringComparison.OrdinalIgnoreCase))
                Tasks.Platform.WindowEnvironment.ForcedKind = Tasks.Platform.DesktopEnvironmentKind.Windows11;
            else if (string.Equals(env, "macos", StringComparison.OrdinalIgnoreCase))
                Tasks.Platform.WindowEnvironment.ForcedKind = Tasks.Platform.DesktopEnvironmentKind.MacOS;
            else if (string.Equals(env, "gnome", StringComparison.OrdinalIgnoreCase))
                Tasks.Platform.WindowEnvironment.ForcedKind = Tasks.Platform.DesktopEnvironmentKind.Gnome;
        }

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
