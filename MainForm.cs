using Microsoft.Win32;
using NAudio.Wave;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Resources;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using IWshRuntimeLibrary; // 🔹 مضافة: لإنشاء اختصار في مجلد Startup

namespace EyeRestReminder
{
    public partial class MainForm : Form
    {
        // ==================== Timers & Counters ====================
        private System.Windows.Forms.Timer activityCheckTimer;
        private int accumulatedActiveTimeSeconds = 0;
        private int remainingTime;
        private const int GracePeriodSeconds = 10;
        private int gracePeriodCounter = 0;
        private bool isTimerStoppedForRest = false;

        // ==================== Reminder Interval ====================
        private int reminderIntervalMinutes = 20;

        // ==================== UI Components ====================
        private NotifyIcon restNotifyIcon;
        private ContextMenuStrip notifyMenu;
        private Label countdownLabel;
        private Label statusLabel;
        private Label activityTypeLabel;
        private Button resumeButton;

        // ==================== Localization ====================
        private string currentLang = "en";
        private Dictionary<string, string> strings;

        // ==================== Auto Start ====================
        private bool autoStartEnabled = false;

        // ==================== Constructor ====================
        public MainForm()
        {
            InitializeComponent();

            // Restore previous settings
            currentLang = Properties.Settings.Default.LastLanguage ?? "en";
            reminderIntervalMinutes = Properties.Settings.Default.LastReminderInterval > 0
                ? Properties.Settings.Default.LastReminderInterval
                : 20;

            // Load language
            SetLanguage(currentLang);

            // Setup window
            this.Icon = new Icon("AppIcon.ico");
            this.Text = strings["AppTitle"];
            this.Size = new Size(420, 250);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.WhiteSmoke;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Font = new Font("Segoe UI", 10);

            // Setup system tray icon
            restNotifyIcon = new NotifyIcon
            {
                Icon = new Icon("AppIcon.ico"),
                Text = strings["AppTitle"],
                Visible = true
            };
            restNotifyIcon.MouseDoubleClick += RestNotifyIcon_MouseDoubleClick;

            // Setup timers
            AudioMonitor.Initialize();
            remainingTime = reminderIntervalMinutes * 60;
            autoStartEnabled = IsAutoStartEnabled();

            // Initialize UI components
            SetupApplicationComponents();
            InitializeReminderIntervalMenu();

            this.FormClosing += Form1_FormClosing;

            this.ShowInTaskbar = false;
            this.WindowState = FormWindowState.Minimized;
            this.Hide();
        }

        // ==================== Load RESX File ====================
        private Dictionary<string, string> LoadResxFile(string lang)
        {
            string fileName = $"Strings.{lang}.resx";
            string path = Path.Combine(Application.StartupPath, fileName);

            if (!System.IO.File.Exists(path))
                path = Path.Combine(Application.StartupPath, "Strings.resx");

            var dict = new Dictionary<string, string>();
            using (var reader = new ResXResourceReader(path))
            {
                foreach (DictionaryEntry entry in reader)
                {
                    dict[entry.Key.ToString()] = entry.Value?.ToString() ?? "";
                }
            }
            return dict;
        }

        // ==================== Set Language ====================
        private void SetLanguage(string lang)
        {
            currentLang = lang;
            strings = LoadResxFile(lang);

            this.RightToLeft = (lang == "ar") ? RightToLeft.Yes : RightToLeft.No;
            this.RightToLeftLayout = (lang == "ar");
        }

        // ==================== Setup UI Components ====================
        private void SetupApplicationComponents()
        {
            this.Controls.Clear();

            countdownLabel = new Label
            {
                Font = new Font("Consolas", 32, FontStyle.Bold),
                ForeColor = Color.RoyalBlue,
                Dock = DockStyle.Top,
                Height = 80,
                TextAlign = ContentAlignment.MiddleCenter,
                Text = $"{reminderIntervalMinutes}:00"
            };

            statusLabel = new Label
            {
                Text = strings["AppReady"],
                Font = new Font("Segoe UI", 12),
                Dock = DockStyle.Top,
                Height = 40,
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.DarkSlateGray
            };

            activityTypeLabel = new Label
            {
                Text = "⏸️ " + strings["IdleText"],
                Font = new Font("Segoe UI", 11, FontStyle.Italic),
                ForeColor = Color.Gray,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter
            };

            resumeButton = new Button
            {
                Text = strings["ResumeText"],
                Dock = DockStyle.Bottom,
                Height = 45,
                Visible = false,
                BackColor = Color.LightSteelBlue,
                FlatStyle = FlatStyle.Flat
            };
            resumeButton.FlatAppearance.BorderSize = 0;
            resumeButton.Click += ResumeMenuItem_Click;

            this.Controls.Add(resumeButton);
            this.Controls.Add(activityTypeLabel);
            this.Controls.Add(statusLabel);
            this.Controls.Add(countdownLabel);

            SetupNotifyMenu();

            // Activity timer
            if (activityCheckTimer == null)
            {
                activityCheckTimer = new System.Windows.Forms.Timer();
                activityCheckTimer.Interval = 1000;
                activityCheckTimer.Tick += ActivityCheckTimer_Tick;
                activityCheckTimer.Start();
            }
        }

        // ==================== Setup Notification Menu ====================
        private void SetupNotifyMenu()
        {
            notifyMenu = new ContextMenuStrip();

            var showItem = new ToolStripMenuItem(strings["ShowStatus"]);
            showItem.Click += ShowMenuItem_Click;

            var langMenu = new ToolStripMenuItem("🌐 " + strings["Language"]);
            var langEN = new ToolStripMenuItem("English");
            var langAR = new ToolStripMenuItem("العربية");
            var langTR = new ToolStripMenuItem("Türkçe");
            langEN.Click += (s, e) => ChangeLanguage("en");
            langAR.Click += (s, e) => ChangeLanguage("ar");
            langTR.Click += (s, e) => ChangeLanguage("tr");
            langMenu.DropDownItems.AddRange(new ToolStripItem[] { langEN, langAR, langTR });

            var autoStartItem = new ToolStripMenuItem(strings["AutoStart"]) { Checked = autoStartEnabled };
            autoStartItem.Click += (s, e) =>
            {
                autoStartEnabled = !autoStartEnabled;
                autoStartItem.Checked = autoStartEnabled;
                ToggleAutoStart(autoStartEnabled);
            };

            var exitItem = new ToolStripMenuItem(strings["Exit"]);
            exitItem.Click += ExitMenuItem_Click;

            notifyMenu.Items.Add(showItem);
            notifyMenu.Items.Add(new ToolStripSeparator());
            notifyMenu.Items.Add(langMenu);
            notifyMenu.Items.Add(autoStartItem);
            notifyMenu.Items.Add(new ToolStripSeparator());
            notifyMenu.Items.Add(exitItem);

            restNotifyIcon.ContextMenuStrip = notifyMenu;
        }

        // ==================== Reminder Interval Menu ====================
        private void InitializeReminderIntervalMenu()
        {
            string menuName = "⏱️ " + strings["ReminderIntervalMenu"];
            var reminderMenu = new ToolStripMenuItem(menuName);

            var interval20 = new ToolStripMenuItem(strings["Reminder20Text"]) { Checked = reminderIntervalMinutes == 20 };
            var interval30 = new ToolStripMenuItem(strings["Reminder30Text"]) { Checked = reminderIntervalMinutes == 30 };
            var interval40 = new ToolStripMenuItem(strings["Reminder40Text"]) { Checked = reminderIntervalMinutes == 40 };
            var interval50 = new ToolStripMenuItem(strings["Reminder50Text"]) { Checked = reminderIntervalMinutes == 50 };

            interval20.Click += (s, e) => SetReminderInterval(20, interval20, interval30, interval40, interval50);
            interval30.Click += (s, e) => SetReminderInterval(30, interval20, interval30, interval40, interval50);
            interval40.Click += (s, e) => SetReminderInterval(40, interval20, interval30, interval40, interval50);
            interval50.Click += (s, e) => SetReminderInterval(50, interval20, interval30, interval40, interval50);

            reminderMenu.DropDownItems.AddRange(new ToolStripItem[]
            {
                interval20, interval30, interval40, interval50
            });

            restNotifyIcon.ContextMenuStrip.Items.Insert(3, reminderMenu);
        }

        private void SetReminderInterval(int minutes, params ToolStripMenuItem[] items)
        {
            foreach (var item in items) item.Checked = false;
            var selected = items.FirstOrDefault(i => i.Text.StartsWith(minutes.ToString()));
            if (selected != null) selected.Checked = true;

            reminderIntervalMinutes = minutes;
            accumulatedActiveTimeSeconds = 0;
            remainingTime = minutes * 60;

            Properties.Settings.Default.LastReminderInterval = minutes;
            Properties.Settings.Default.Save();

            string msg = string.Format(strings["ReminderIntervalSetMsg"], minutes);
            restNotifyIcon.ShowBalloonTip(1000, strings["AppTitle"], msg, ToolTipIcon.Info);
        }

        private void ChangeLanguage(string lang)
        {
            SetLanguage(lang);
            statusLabel.Text = strings["AppReady"];
            resumeButton.Text = strings["ResumeText"];
            this.Text = strings["AppTitle"];
            restNotifyIcon.Text = strings["AppTitle"];

            SetupNotifyMenu();
            InitializeReminderIntervalMenu();

            Properties.Settings.Default.LastLanguage = lang;
            Properties.Settings.Default.Save();
        }

        // ==================== Auto Start (Startup Folder) ====================
        private void ToggleAutoStart(bool enable)
        {
            try
            {
                string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                string shortcutPath = Path.Combine(startupFolder, "EyeRestReminder.lnk");
                string exePath = Application.ExecutablePath;

                if (enable)
                {
                    var shell = new WshShell();
                    var shortcut = (IWshShortcut)shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = exePath;
                    shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                    shortcut.Description = "EyeRestReminder AutoStart";
                    shortcut.Save();
                }
                else
                {
                    if (System.IO.File.Exists(shortcutPath))
                        System.IO.File.Delete(shortcutPath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error toggling auto-start: " + ex.Message);
            }
        }

        private bool IsAutoStartEnabled()
        {
            string startupFolder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
            string shortcutPath = Path.Combine(startupFolder, "EyeRestReminder.lnk");
            return System.IO.File.Exists(shortcutPath);
        }

        // ==================== Activity Timer Logic ====================
        private void ActivityCheckTimer_Tick(object sender, EventArgs e)
        {
            if (isTimerStoppedForRest) return;

            int idleTime = InputTimer.GetIdleTimeSeconds();
            bool isInputActive = idleTime < 5;
            bool isAudioActive = AudioMonitor.IsSoundPlaying();

            string activityType = "⏸️ " + strings["IdleText"];
            if (isInputActive) activityType = "🖱️ " + strings["MouseKeyboard"];
            else if (isAudioActive) activityType = "🎵 " + strings["AudioPlaying"];

            activityTypeLabel.Text = activityType;

            if (isInputActive || isAudioActive)
            {
                accumulatedActiveTimeSeconds++;
                gracePeriodCounter = 0;
                remainingTime = reminderIntervalMinutes * 60 - accumulatedActiveTimeSeconds;

                if (remainingTime < 0) remainingTime = 0;
                TimeSpan t = TimeSpan.FromSeconds(remainingTime);
                countdownLabel.Text = $"{t.Minutes:D2}:{t.Seconds:D2}";
                restNotifyIcon.Text = $"{strings["AppTitle"]} - {t.Minutes:D2}:{t.Seconds:D2}";

                if (accumulatedActiveTimeSeconds >= reminderIntervalMinutes * 60)
                    Show202020Reminder();
            }
            else if (gracePeriodCounter < GracePeriodSeconds)
            {
                gracePeriodCounter++;
            }
        }

        // ==================== Reminder Popup ====================
        private async void Show202020Reminder()
        {
            activityCheckTimer.Stop();
            isTimerStoppedForRest = true;

            string soundPath = Path.Combine(Application.StartupPath, "ReminderSound.mp3");

            ShowForm();
            resumeButton.Visible = true;
            countdownLabel.Text = "--:--";
            statusLabel.Text = strings["ReminderMessage"];
            restNotifyIcon.Text = strings["ReminderTitle"];

            _ = System.Threading.Tasks.Task.Run(() => PlayMp3Sound(soundPath));
            await System.Threading.Tasks.Task.Delay(200);
        }

        private void PlayMp3Sound(string path)
        {
            try
            {
                if (!System.IO.File.Exists(path)) return;

                using (var audioFile = new AudioFileReader(path))
                using (var outputDevice = new WaveOutEvent())
                {
                    outputDevice.Init(audioFile);
                    outputDevice.Play();
                    while (outputDevice.PlaybackState == PlaybackState.Playing)
                        System.Threading.Thread.Sleep(100);
                }
            }
            catch { }
        }

        private void ResumeCounting()
        {
            accumulatedActiveTimeSeconds = 0;
            remainingTime = reminderIntervalMinutes * 60;
            isTimerStoppedForRest = false;
            activityCheckTimer.Start();
            resumeButton.Visible = false;
            statusLabel.Text = strings["AppReady"];
            countdownLabel.Text = $"{reminderIntervalMinutes}:00";
            restNotifyIcon.Text = strings["AppTitle"];
        }

        private void ResumeMenuItem_Click(object sender, EventArgs e) => ResumeCounting();
        private void RestNotifyIcon_MouseDoubleClick(object sender, MouseEventArgs e) => ShowForm();
        private void ShowMenuItem_Click(object sender, EventArgs e) => ShowForm();

        private void ShowForm()
        {
            this.Show();
            this.WindowState = FormWindowState.Normal;
            this.ShowInTaskbar = true;
            this.Activate();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing)
            {
                this.Hide();
                this.ShowInTaskbar = false;
                e.Cancel = true;
            }
        }

        private void ExitMenuItem_Click(object sender, EventArgs e)
        {
            activityCheckTimer.Dispose();
            restNotifyIcon.Visible = false;
            AudioMonitor.Cleanup();
            Application.Exit();
        }
    }
}

// EyeRestReminder
// Copyright (c) 2025 Mohamad Khoja
// All rights reserved.
