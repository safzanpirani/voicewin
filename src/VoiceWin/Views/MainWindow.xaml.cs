using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using VoiceWin.Services;

namespace VoiceWin.Views;

public partial class MainWindow : Window
{
    private readonly App _app;
    private readonly TrayIconService _trayIconService;
    private readonly StatusOverlayWindow _statusOverlay;
    private bool _isRecordingHotkey;
    private int _pendingHotkeyVirtualKey;
    private int _pendingHotkeyModifiers;

    public MainWindow()
    {
        InitializeComponent();
        _app = (App)Application.Current;
        _trayIconService = new TrayIconService();
        _statusOverlay = new StatusOverlayWindow();

        LoadSettings();
        SubscribeToEvents();

        TaskbarIcon.IconSource = _trayIconService.CreateIconWithStatus(TrayStatus.Ready);
    }

    private void LoadSettings()
    {
        var settings = _app.SettingsService.Settings;

        GroqApiKeyBox.Text = settings.GroqApiKey ?? "";
        DeepgramApiKeyBox.Text = settings.DeepgramApiKey ?? "";

        SelectComboItemByTag(ProviderCombo, settings.TranscriptionProvider);
        SelectComboItemByTag(HotkeyModeCombo, settings.HotkeyMode);
        SelectComboItemByTag(OverlayPositionCombo, settings.OverlayPosition);
        SelectComboItemByTag(LanguageCombo, settings.Language);

        AiEnhancementCheckBox.IsChecked = settings.AiEnhancementEnabled;
        AiEnhancementPromptBox.Text = settings.AiEnhancementPrompt;

        VadEnabledCheckBox.IsChecked = settings.VadEnabled;
        VadSilenceTimeoutBox.Text = settings.VadStreamingSilenceTimeoutSeconds.ToString();

        _pendingHotkeyVirtualKey = settings.HotkeyVirtualKey;
        _pendingHotkeyModifiers = settings.HotkeyModifiers;
        HotkeyDisplayBox.Text = GetHotkeyDisplayString(_pendingHotkeyModifiers, _pendingHotkeyVirtualKey);

        _statusOverlay.SetPosition(settings.OverlayPosition);
    }

    private void SelectComboItemByTag(ComboBox combo, string tag)
    {
        foreach (ComboBoxItem item in combo.Items)
        {
            if (item.Tag?.ToString() == tag)
            {
                combo.SelectedItem = item;
                return;
            }
        }
        combo.SelectedIndex = 0;
    }

    private string GetSelectedTag(ComboBox combo)
    {
        return (combo.SelectedItem as ComboBoxItem)?.Tag?.ToString() ?? "";
    }

    private void SubscribeToEvents()
    {
        _app.Orchestrator.RecordingStarted += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = "Recording...";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(239, 68, 68));
                TaskbarIcon.ToolTipText = "VoiceWin - Recording...";
                TaskbarIcon.IconSource = _trayIconService.CreateIconWithStatus(TrayStatus.Recording);
                
                bool isStreaming = _app.SettingsService.Settings.TranscriptionProvider == "deepgram-streaming";
                _statusOverlay.SetMode(isStreaming);
                _statusOverlay.UpdateStatus("Recording");
                _statusOverlay.StartAnimating();
            });
        };

        _app.Orchestrator.RecordingStopped += (s, e) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = "Processing...";
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(234, 179, 8));
                TaskbarIcon.IconSource = _trayIconService.CreateIconWithStatus(TrayStatus.Processing);
                
                _statusOverlay.StopAnimating();
                _statusOverlay.UpdateStatus("Processing");
            });
        };

        _app.Orchestrator.StatusChanged += (s, status) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                StatusText.Text = status;
                StatusIndicator.Fill = new SolidColorBrush(Color.FromRgb(34, 197, 94));
                TaskbarIcon.ToolTipText = $"VoiceWin - {status}";
                TaskbarIcon.IconSource = _trayIconService.CreateIconWithStatus(TrayStatus.Ready);
                
                _statusOverlay.UpdateStatus(status);
                if (IsTerminalStatus(status))
                {
                    _statusOverlay.HideOverlay();
                }
            });
        };

        _app.Orchestrator.TranscriptionCompleted += (s, result) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (result.Success)
                {
                    StatusText.Text = $"Done ({result.Duration.TotalMilliseconds:F0}ms)";
                }
            });
        };

        _app.Orchestrator.AudioLevelChanged += (s, level) =>
        {
            _statusOverlay.UpdateAudioLevel(level);
        };

        _app.Orchestrator.SpeechDetected += (s, isSpeaking) =>
        {
            Dispatcher.BeginInvoke(() =>
            {
                _statusOverlay.UpdateStatus(isSpeaking ? "Recording" : "Listening");
            });
        };
    }

    private static bool IsTerminalStatus(string status)
    {
        return status.Contains("Transcribed") || 
               status.Contains("Streamed") || 
               status.Contains("Error") ||
               status.Contains("too short") ||
               status.Contains("No API") ||
               status.Contains("No valid") ||
               status.Contains("No speech") ||
               status.Contains("Auto-stopped");
    }

    private void SaveSettings_Click(object sender, RoutedEventArgs e)
    {
        _app.SettingsService.UpdateSettings(settings =>
        {
            settings.GroqApiKey = GroqApiKeyBox.Text.Trim();
            settings.DeepgramApiKey = DeepgramApiKeyBox.Text.Trim();
            settings.TranscriptionProvider = GetSelectedTag(ProviderCombo);
            settings.HotkeyMode = GetSelectedTag(HotkeyModeCombo);
            settings.OverlayPosition = GetSelectedTag(OverlayPositionCombo);
            settings.Language = GetSelectedTag(LanguageCombo);
            settings.AiEnhancementEnabled = AiEnhancementCheckBox.IsChecked ?? false;
            settings.AiEnhancementPrompt = AiEnhancementPromptBox.Text;
            settings.VadEnabled = VadEnabledCheckBox.IsChecked ?? true;
            if (int.TryParse(VadSilenceTimeoutBox.Text, out int timeout))
            {
                settings.VadStreamingSilenceTimeoutSeconds = timeout;
            }
            settings.HotkeyVirtualKey = _pendingHotkeyVirtualKey;
            settings.HotkeyModifiers = _pendingHotkeyModifiers;
        });

        _app.Orchestrator.UpdateHotkeySettings();
        _statusOverlay.SetPosition(_app.SettingsService.Settings.OverlayPosition);

        StatusText.Text = "Settings saved!";
    }

    private void ShowWindow_Click(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void Exit_Click(object sender, RoutedEventArgs e)
    {
        Application.Current.Shutdown();
    }

    private void TaskbarIcon_TrayMouseDoubleClick(object sender, RoutedEventArgs e)
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);
        if (WindowState == WindowState.Minimized)
        {
            Hide();
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
    }

    private void RecordHotkey_Click(object sender, RoutedEventArgs e)
    {
        if (_isRecordingHotkey)
        {
            _isRecordingHotkey = false;
            RecordHotkeyButton.Content = "Record";
            HotkeyDisplayBox.Text = GetHotkeyDisplayString(_pendingHotkeyModifiers, _pendingHotkeyVirtualKey);
            return;
        }

        _isRecordingHotkey = true;
        RecordHotkeyButton.Content = "Cancel";
        HotkeyDisplayBox.Text = "Press key combo...";
        HotkeyDisplayBox.Focus();
    }

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!_isRecordingHotkey) return;

        e.Handled = true;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vkCode = KeyInterop.VirtualKeyFromKey(key);
        
        if (vkCode == 0) return;

        bool isModifier = IsModifierKey(vkCode);
        
        int modifiers = 0;
        if ((GetAsyncKeyState(0xA2) & 0x8000) != 0 || (GetAsyncKeyState(0xA3) & 0x8000) != 0) modifiers |= 1;
        if ((GetAsyncKeyState(0xA4) & 0x8000) != 0 || (GetAsyncKeyState(0xA5) & 0x8000) != 0) modifiers |= 2;
        if ((GetAsyncKeyState(0xA0) & 0x8000) != 0 || (GetAsyncKeyState(0xA1) & 0x8000) != 0) modifiers |= 4;
        if ((GetAsyncKeyState(0x5B) & 0x8000) != 0 || (GetAsyncKeyState(0x5C) & 0x8000) != 0) modifiers |= 8;

        if (isModifier)
        {
            HotkeyDisplayBox.Text = GetModifierString(modifiers) + "...";
            return;
        }

        _pendingHotkeyModifiers = modifiers;
        _pendingHotkeyVirtualKey = vkCode;
        HotkeyDisplayBox.Text = GetHotkeyDisplayString(modifiers, vkCode);
        _isRecordingHotkey = false;
        RecordHotkeyButton.Content = "Record";
    }

    protected override void OnPreviewKeyUp(KeyEventArgs e)
    {
        base.OnPreviewKeyUp(e);

        if (!_isRecordingHotkey) return;

        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        int vkCode = KeyInterop.VirtualKeyFromKey(key);
        
        if (IsModifierKey(vkCode) && _pendingHotkeyModifiers == 0)
        {
            _pendingHotkeyVirtualKey = vkCode;
            _pendingHotkeyModifiers = 0;
            HotkeyDisplayBox.Text = GetKeyName(vkCode);
            _isRecordingHotkey = false;
            RecordHotkeyButton.Content = "Record";
            e.Handled = true;
        }
    }

    private static bool IsModifierKey(int vkCode)
    {
        return vkCode is 160 or 161 or 162 or 163 or 164 or 165 or 91 or 92 or 16 or 17 or 18;
    }

    private static string GetModifierString(int modifiers)
    {
        var parts = new List<string>();
        if ((modifiers & 1) != 0) parts.Add("Ctrl");
        if ((modifiers & 2) != 0) parts.Add("Alt");
        if ((modifiers & 4) != 0) parts.Add("Shift");
        if ((modifiers & 8) != 0) parts.Add("Win");
        return parts.Count > 0 ? string.Join(" + ", parts) : "";
    }

    private static string GetHotkeyDisplayString(int modifiers, int virtualKey)
    {
        var modStr = GetModifierString(modifiers);
        var keyStr = GetKeyName(virtualKey);
        return string.IsNullOrEmpty(modStr) ? keyStr : $"{modStr} + {keyStr}";
    }

    private static string GetKeyName(int virtualKey)
    {
        return virtualKey switch
        {
            8 => "Backspace",
            9 => "Tab",
            13 => "Enter",
            16 => "Shift",
            17 => "Ctrl",
            18 => "Alt",
            19 => "Pause",
            20 => "Caps Lock",
            27 => "Escape",
            32 => "Space",
            33 => "Page Up",
            34 => "Page Down",
            35 => "End",
            36 => "Home",
            37 => "Left Arrow",
            38 => "Up Arrow",
            39 => "Right Arrow",
            40 => "Down Arrow",
            45 => "Insert",
            46 => "Delete",
            91 => "Left Win",
            92 => "Right Win",
            93 => "Menu",
            112 => "F1",
            113 => "F2",
            114 => "F3",
            115 => "F4",
            116 => "F5",
            117 => "F6",
            118 => "F7",
            119 => "F8",
            120 => "F9",
            121 => "F10",
            122 => "F11",
            123 => "F12",
            144 => "Num Lock",
            145 => "Scroll Lock",
            160 => "Left Shift",
            161 => "Right Shift",
            162 => "Left Ctrl",
            163 => "Right Ctrl",
            164 => "Left Alt",
            165 => "Right Alt",
            186 => ";",
            187 => "=",
            188 => ",",
            189 => "-",
            190 => ".",
            191 => "/",
            192 => "`",
            219 => "[",
            220 => "\\",
            221 => "]",
            222 => "'",
            _ when virtualKey >= 48 && virtualKey <= 57 => ((char)virtualKey).ToString(),
            _ when virtualKey >= 65 && virtualKey <= 90 => ((char)virtualKey).ToString(),
            _ when virtualKey >= 96 && virtualKey <= 105 => $"Numpad {virtualKey - 96}",
            _ => $"Key {virtualKey}"
        };
    }
}
