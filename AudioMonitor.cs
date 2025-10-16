using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;
using System;
using System.Diagnostics;

public class AudioMonitor : IMMNotificationClient
{
    private static MMDeviceEnumerator enumerator;      // Audio device enumerator
    private static MMDevice defaultDevice;             // Default output audio device
    public static float SoundThreshold { get; set; } = 0.001f; // Minimum peak value to consider sound active
    private static bool isCleaned = false;            // Flag to prevent multiple cleanup calls
    private static AudioMonitor audioMonitorInstance; // Singleton instance for callback registration

    // ==================== Initialize Audio Monitor ====================
    // Sets up the default audio device and registers for notifications
    public static void Initialize()
    {
        Cleanup(); // Ensure previous resources are released

        try
        {
            enumerator = new MMDeviceEnumerator();
            audioMonitorInstance = new AudioMonitor();
            enumerator.RegisterEndpointNotificationCallback(audioMonitorInstance);

            // Get the current default audio playback device
            defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            Debug.WriteLine($"AudioMonitor: Initialized -> {defaultDevice.FriendlyName}");
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioMonitor: Error initializing device -> {ex.Message}");
            defaultDevice = null;
        }

        isCleaned = false;
    }

    // ==================== Check If Sound is Playing ====================
    // Returns true if any session or device output is producing audio above the threshold
    public static bool IsSoundPlaying()
    {
        try
        {
            // Reinitialize if device is null or inactive
            if (defaultDevice == null || defaultDevice.State != DeviceState.Active)
            {
                Debug.WriteLine("AudioMonitor: Device invalid, reinitializing...");
                Initialize();
                if (defaultDevice == null || defaultDevice.State != DeviceState.Active)
                    return false;
            }

            bool sessionActive = false;
            var sessionManager = defaultDevice.AudioSessionManager;

            // Check all active audio sessions
            for (int i = 0; i < sessionManager.Sessions.Count; i++)
            {
                var session = sessionManager.Sessions[i];
                float peak = session.AudioMeterInformation.MasterPeakValue;
                if (peak > SoundThreshold)
                {
                    sessionActive = true;
                    break;
                }
            }

            // If no session is active, check the device peak itself
            if (!sessionActive)
            {
                float devicePeak = defaultDevice.AudioMeterInformation.MasterPeakValue;
                if (devicePeak > SoundThreshold)
                    sessionActive = true;
            }

            return sessionActive;
        }
        catch (System.Runtime.InteropServices.COMException comEx)
        {
            Debug.WriteLine($"AudioMonitor: COMException -> {comEx.Message}");
            Initialize(); // Reinitialize on COM errors
            return false;
        }
        catch (InvalidCastException icEx)
        {
            Debug.WriteLine($"AudioMonitor: InvalidCastException -> {icEx.Message}");
            Initialize(); // Reinitialize on cast errors
            return false;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioMonitor: Unexpected error -> {ex.Message}");
            defaultDevice = null;
            return false;
        }
    }

    // ==================== Cleanup Resources ====================
    // Releases device and enumerator resources and unregisters callbacks
    public static void Cleanup()
    {
        if (isCleaned) return;
        isCleaned = true;

        try
        {
            if (defaultDevice != null)
            {
                defaultDevice.Dispose();
                defaultDevice = null;
            }

            if (enumerator != null)
            {
                if (audioMonitorInstance != null)
                {
                    enumerator.UnregisterEndpointNotificationCallback(audioMonitorInstance);
                    audioMonitorInstance = null;
                }
                enumerator.Dispose();
                enumerator = null;
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"AudioMonitor: Cleanup error -> {ex.Message}");
        }
    }

    // ==================== IMMNotificationClient Implementation ====================
    // Called when the default audio device changes
    public void OnDefaultDeviceChanged(DataFlow dataFlow, Role deviceRole, string defaultDeviceId)
    {
        if (dataFlow == DataFlow.Render && deviceRole == Role.Multimedia)
        {
            Debug.WriteLine("AudioMonitor: Default device changed, reinitializing...");
            Initialize();
        }
    }

    // Unused IMMNotificationClient callbacks (required by interface)
    public void OnDeviceAdded(string pwstrDeviceId) { }
    public void OnDeviceRemoved(string deviceId) { }
    public void OnDeviceStateChanged(string deviceId, DeviceState newState) { }
    public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key) { }
}

// EyeRestReminder
// Copyright (c) 2025 Mohamad Khoja
// All rights reserved.
