using SharpHook;
using SharpHook.Native;
using Serilog;
using System.Runtime.InteropServices;

namespace ConsoleApp1
{
    internal class Program
    {
        private static bool isAltPressed = false;
        private static bool isCmdPressed = false;

        // Import Windows API functions for taskbar visibility
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        static void Main(string[] args)
        {
            // Default log level
            var logLevel = Serilog.Events.LogEventLevel.Information;

            // Check if a log level argument is provided
            foreach (var arg in args)
            {
                if (arg.StartsWith("--log-level="))
                {
                    var level = arg.Split("=")[1];
                    if (Enum.TryParse<Serilog.Events.LogEventLevel>(level, true, out var parsedLevel))
                    {
                        logLevel = parsedLevel;
                    }
                }
            }

            // Configure Serilog with dynamic log level
            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Is(logLevel)
                .WriteTo.Console()
                .CreateLogger();

            Log.Information("Application started.");

            try
            {
                if (args.Length == 0)
                {
                    Log.Warning("No arguments provided. Use '--taskbar' or '--suppress-keys'.");
                    return;
                }

                string command = args[0].ToLower();
                foreach (var arg in args)
                {
                    Log.Information($"- {arg}");
                }
                switch (command)
                {
                    case "--taskbar":
                        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                        {
                            if (args.Length > 1 && args[1] == "hide")
                                SetTaskbarVisibility(false);
                            else if (args.Length > 1 && args[1] == "show")
                                SetTaskbarVisibility(true);
                            else
                                Log.Warning("Invalid taskbar argument. Use 'hide' or 'show'.");
                        }
                        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                        {
                            if (args.Length > 1 && args[1] == "hide")
                                MacOSMenuBarControl.SetMenuBarAndDockVisibility(false);
                            else if (args.Length > 1 && args[1] == "show")
                                MacOSMenuBarControl.SetMenuBarAndDockVisibility(true);
                            else
                                Log.Warning("Invalid taskbar argument. Use 'hide' or 'show'.");
                        }
                        break;

                    case "--suppress-keys":
                        RunKeySuppressor();
                        break;

                    default:
                        Log.Warning("Unknown command. Use '--taskbar' or '--suppress-keys'.");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An exception occurred during hook initialization.");
            }
            finally
            {
                Log.Information("Application shutting down.");
                Log.CloseAndFlush();
            }
        }

        static void RunKeySuppressor()
        {
            using var hook = new SimpleGlobalHook();
            hook.KeyPressed += OnKeyPressed;
            hook.KeyReleased += OnKeyReleased;
            hook.Run();
            Log.Information("Keyboard hook initialized.");
        }

        /// <summary>
        /// Handles the KeyPressed event from the global hook.
        /// Suppresses specific key combinations based on platform.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">Event arguments containing key data.</param>
        private static void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            Log.Debug("Key pressed: {KeyCode}", e.Data.KeyCode);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // Track Alt key press
                if (e.Data.KeyCode == KeyCode.VcLeftAlt)
                {
                    isAltPressed = true;
                    Log.Information("Alt key pressed.");
                }

                // Suppress Alt+Tab combination
                if (isAltPressed && e.Data.KeyCode == KeyCode.VcTab)
                {
                    Log.Warning("Alt+Tab combination detected and suppressed.");
                    e.SuppressEvent = true;
                }

                if (isAltPressed && e.Data.KeyCode == KeyCode.VcSpace)
                {
                    Log.Warning("Alt+Space combination detected and suppressed.");
                    e.SuppressEvent = true;
                }

                // Suppress Meta (Windows) key
                if (e.Data.KeyCode == KeyCode.VcLeftMeta)
                {
                    Log.Warning("Meta (Windows) key detected and suppressed.");
                    e.SuppressEvent = true;
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                // macOS-specific key suppression
                if (e.Data.KeyCode == KeyCode.VcF3 || e.Data.KeyCode == KeyCode.VcUndefined)
                {
                    Log.Warning("Command key detected and suppressed on macOS.");
                    e.SuppressEvent = true;
                }
                if (e.Data.KeyCode == KeyCode.VcLeftMeta)
                {
                    isCmdPressed = true;
                    Log.Information("Cmd key pressed.");
                }
                if (isCmdPressed && e.Data.KeyCode == KeyCode.VcW)
                {
                    Log.Warning("Cmd+W combination detected and suppressed.");
                    e.SuppressEvent = true;
                }
            }
        }

        // New method to track when keys are released
        private static void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (e.Data.KeyCode == KeyCode.VcLeftAlt)
                {
                    isAltPressed = false;
                    Log.Information("Alt key released.");
                }
            }
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                if (e.Data.KeyCode == KeyCode.VcLeftMeta)
                {
                    isCmdPressed = false;
                    Log.Information("Cmd key released.");
                }
            }
        }

        /// <summary>
        /// Sets the visibility of the Windows taskbar.
        /// </summary>
        /// <param name="show">If true, shows the taskbar; otherwise, hides it.</param>
        private static void SetTaskbarVisibility(bool show)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Log.Warning("Taskbar visibility control is only applicable on Windows.");
                return;
            }

            try
            {
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);
                if (taskbarHandle == IntPtr.Zero)
                {
                    Log.Error("Failed to find the taskbar window.");
                    return;
                }

                ShowWindow(taskbarHandle, show ? SW_SHOW : SW_HIDE);
                Log.Information("Taskbar visibility set to: {Visibility}", show ? "Visible" : "Hidden");
            }
            catch (Exception ex)
            {
                Log.Error(ex, "An error occurred while setting the taskbar visibility.");
            }
        }
    }

    internal class MacOSMenuBarControl
    {
        // Import Objective-C runtime methods
        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_getClass")]
        private static extern IntPtr GetClass(string className);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "sel_registerName")]
        private static extern IntPtr RegisterSelector(string selectorName);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern void SendMessage_IntPtr(IntPtr receiver, IntPtr selector, uint value);

        [DllImport("/usr/lib/libobjc.dylib", EntryPoint = "objc_msgSend")]
        private static extern IntPtr SendMessage_IntPtrReturn(IntPtr receiver, IntPtr selector);

        private const uint NSApplicationPresentationHideDock = 0x2;
        private const uint NSApplicationPresentationHideMenuBar = 0x4;

        public static void SetMenuBarAndDockVisibility(bool hide)
        {
            IntPtr nsAppClass = GetClass("NSApplication");
            IntPtr sharedAppSelector = RegisterSelector("sharedApplication");
            IntPtr sharedApp = SendMessage_IntPtrReturn(nsAppClass, sharedAppSelector);

            IntPtr setOptionsSelector = RegisterSelector("setPresentationOptions:");
            uint options = hide ? (NSApplicationPresentationHideDock | NSApplicationPresentationHideMenuBar) : 0;

            SendMessage_IntPtr(sharedApp, setOptionsSelector, options);
        }
    }
}