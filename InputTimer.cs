using System;
using System.Runtime.InteropServices;

public static class InputTimer
{
    [StructLayout(LayoutKind.Sequential)]
    private struct LASTINPUTINFO
    {
        public uint cbSize;
        public uint dwTime;
    }

    [DllImport("User32.dll")]
    private static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

    // ==================== Get User Idle Time ====================
    // Returns the number of seconds since the last user input (mouse/keyboard).
    public static int GetIdleTimeSeconds()
    {
        LASTINPUTINFO lastInputInfo = new LASTINPUTINFO();
        lastInputInfo.cbSize = (uint)Marshal.SizeOf(lastInputInfo);

        if (GetLastInputInfo(ref lastInputInfo))
        {
            int idleMilliseconds = Environment.TickCount - (int)lastInputInfo.dwTime;
            if (idleMilliseconds > 0)
            {
                return idleMilliseconds / 1000;
            }
        }
        return 0;
    }
}

// EyeRestReminder
// Copyright (c) 2025 Mohamad Khoja
// All rights reserved.
