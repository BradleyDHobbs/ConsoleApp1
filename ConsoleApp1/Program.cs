using SharpHook;
using SharpHook.Native;
using Serilog;
using System.Runtime.InteropServices;

namespace ConsoleApp1
{
    internal class Program
    {

        // Field to track Alt key state
        private static bool isAltPressed = false;

        // Import Windows API functions for taskbar visibility
        [DllImport("user32.dll")]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll")]
        private static extern int ShowWindow(IntPtr hWnd, int nCmdShow);

        private const int SW_HIDE = 0;
        private const int SW_SHOW = 5;

        static void Main(string[] args)
        {
            // Configure Serilog logging
            Log.Logger = new LoggerConfiguration()
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
                        if (args.Length > 1 && args[1] == "hide")
                            SetTaskbarVisibility(false);
                        else if (args.Length > 1 && args[1] == "show")
                            SetTaskbarVisibility(true);
                        else
                            Log.Warning("Invalid taskbar argument. Use 'hide' or 'show'.");
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
            hook.Run();
            Log.Information("Keyboard hook initialized.");
        }

        /// <summary>
        /// Handles the KeyPressed event from the global hook.
        /// Suppresses specific key combinations such as Alt+Tab and Meta (Windows) key.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">Event arguments containing key data.</param>
        private static void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
        {
            Log.Debug("Key pressed: {KeyCode}", e.Data.KeyCode);

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

            // Suppress Meta (Windows) key
            if (e.Data.KeyCode == KeyCode.VcLeftMeta)
            {
                Log.Warning("Meta (Windows) key detected and suppressed.");
                e.SuppressEvent = true;
            }
        }

        /// <summary>
        /// Sets the visibility of the Windows taskbar.
        /// </summary>
        /// <param name="show">If true, shows the taskbar; otherwise, hides it.</param>
        private static void SetTaskbarVisibility(bool show)
        {
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
}