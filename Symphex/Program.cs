using System;
using Avalonia;
using Avalonia.Win32;
using Velopack;

namespace Symphex
{
    internal sealed class Program
    {
        // Initialization code. Don't use any Avalonia, third-party APIs or any
        // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
        // yet and stuff might break.
        [STAThread]
        public static void Main(string[] args)
        {
            // Initialize Velopack - MUST be first thing in Main()
            VelopackApp.Build().Run();
            
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        // Avalonia configuration, don't remove; also used by visual designer.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace()
                .With(new Win32PlatformOptions
                {
                    RenderingMode = new[] 
                    { 
                        Win32RenderingMode.Wgl,
                        Win32RenderingMode.Software 
                    }
                });
    }
}
