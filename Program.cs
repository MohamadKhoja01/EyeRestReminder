using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace EyeRestReminder
{
    internal static class Program
    {
        // ==================== Win32 API ====================
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        private const int SW_RESTORE = 9;

        // ==================== Main Entry Point ====================
        [STAThread]
        static void Main()
        {
            bool createdNew;

            // ==================== Single Instance Check ====================
            using (var mutex = new Mutex(true, "EyeRestReminderAppSingleton", out createdNew))
            {
                if (!createdNew)
                {
                    // Another instance is running, bring it to foreground
                    ActivateExistingInstance();
                    return; // Exit this new instance
                }

                // ==================== Application Initialization ====================
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
        }

        // ==================== Activate Existing Instance ====================
        private static void ActivateExistingInstance()
        {
            var currentProcess = System.Diagnostics.Process.GetCurrentProcess();
            var processes = System.Diagnostics.Process.GetProcessesByName(currentProcess.ProcessName);

            foreach (var process in processes)
            {
                if (process.Id != currentProcess.Id)
                {
                    IntPtr handle = process.MainWindowHandle;
                    if (handle != IntPtr.Zero)
                    {
                        ShowWindowAsync(handle, SW_RESTORE); // Restore if minimized
                        SetForegroundWindow(handle);         // Bring to front
                    }
                    break;
                }
            }
        }
    }
}

// EyeRestReminder
// Copyright (c) 2025 Mohamad Khoja
// All rights reserved.
