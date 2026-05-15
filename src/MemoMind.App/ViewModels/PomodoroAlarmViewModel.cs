using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.App.Views;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class PomodoroAlarmViewModel : ViewModelBase, ISettingsAwareViewModel
{
    private readonly SoundService soundService;
    private readonly DispatcherTimer tickTimer;
    private readonly string alarmsFilePath;

    // ─── Settings ──────────────────────────
    private bool pomodoroSoundEnabled = true;
    private bool alarmSoundEnabled = true;
    private bool countdownSoundEnabled = true;
    private bool pomodoroPopupEnabled = true;
    private bool alarmPopupEnabled = true;
    private bool countdownPopupEnabled = true;
    private bool useCustomSound;
    private string customSoundPath = string.Empty;

    // ─── Pomodoro state ────────────────
    private int workMinutes = 25;
    private int breakMinutes = 5;
    private int cycleCount = 4;
    private int currentCycle = 1;
    private bool isWorkMode = true;
    private bool isRunning;
    private int remainingSeconds;
    private string modeText = "工作时间";
    private string remainingTimeDisplay = "25:00";
    private string startPauseText = "开始";

    // ─── Alarm state ───────────────────
    private string newAlarmName = string.Empty;
    private int newAlarmHour;
    private int newAlarmMinute;
    private AlarmRepeatMode newAlarmRepeatMode = AlarmRepeatMode.Once;
    private string newAlarmMessage = string.Empty;
    private bool newAlarmMon, newAlarmTue, newAlarmWed, newAlarmThu, newAlarmFri, newAlarmSat, newAlarmSun;

    // ─── Countdown state ───────────────
    private int countdownHours;
    private int countdownMinutes = 1;
    private int countdownSeconds;
    private int countdownRemaining;
    private bool isCountdownRunning;
    private string countdownDisplay = "01:00";

    public PomodoroAlarmViewModel()
        : this(new SoundService())
    {
    }

    public PomodoroAlarmViewModel(SoundService soundService)
    {
        this.soundService = soundService;

        var appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoMind");
        alarmsFilePath = Path.Combine(appData, "alarms.json");

        Alarms = new ObservableCollection<AlarmItem>();
        LoadAlarms();

        tickTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        tickTimer.Tick += OnTick;
        tickTimer.Start();

        remainingSeconds = workMinutes * 60;

        // ── Pomodoro commands ──
        StartPauseCommand = new RelayCommand(_ => TogglePomodoro());
        ResetCommand = new RelayCommand(_ => ResetPomodoro());
        SkipPhaseCommand = new RelayCommand(_ => SkipPhase());

        // ── Alarm commands ──
        AddAlarmCommand = new RelayCommand(_ => AddAlarm());
        DeleteAlarmCommand = new RelayCommand(p => { if (p is string id && id.Length > 0) DeleteAlarm(id); });

        // ── Countdown commands ──
        CountdownStartPauseCommand = new RelayCommand(_ => ToggleCountdown());
        CountdownResetCommand = new RelayCommand(_ => ResetCountdown());

        LoadSettings();
    }

    // ═══════════════════════════════════════
    //  Settings
    // ═══════════════════════════════════════

    public void ApplySettings(UserSettings settings)
    {
        pomodoroSoundEnabled = settings.PomodoroSoundEnabled;
        alarmSoundEnabled = settings.AlarmSoundEnabled;
        countdownSoundEnabled = settings.CountdownSoundEnabled;
        pomodoroPopupEnabled = settings.PomodoroPopupEnabled;
        alarmPopupEnabled = settings.AlarmPopupEnabled;
        countdownPopupEnabled = settings.CountdownPopupEnabled;
        useCustomSound = settings.UseCustomSound;
        customSoundPath = settings.CustomSoundPath ?? string.Empty;
    }

    private void LoadSettings()
    {
        try
        {
            var store = App.Services.GetRequiredService<IAppSettingsStore>();
            var settings = store.LoadAsync().GetAwaiter().GetResult();
            ApplySettings(settings);
        }
        catch
        {
            // Use defaults
        }
    }

    private void PlaySound(Action playSystemSound)
    {
        if (useCustomSound && !string.IsNullOrWhiteSpace(customSoundPath) && File.Exists(customSoundPath))
        {
            soundService.PlayCustomWav(customSoundPath);
        }
        else
        {
            playSystemSound();
        }
    }

    private void ShowPomodoroPopup(string title, string message)
    {
        if (!pomodoroPopupEnabled) return;
        Application.Current.Dispatcher.Invoke(() =>
        {
            var popup = new AlarmPopupWindow(title, message, DateTime.Now);
            popup.Show();
        });
    }

    // ═══════════════════════════════════════
    //  Pomodoro Properties
    // ═══════════════════════════════════════

    public int WorkMinutes
    {
        get => workMinutes;
        set
        {
            if (value < 1) value = 1;
            if (value > 120) value = 120;
            if (workMinutes == value) return;
            workMinutes = value;
            OnPropertyChanged();
            if (!isRunning && isWorkMode)
            {
                remainingSeconds = workMinutes * 60;
                UpdatePomodoroDisplay();
            }
        }
    }

    public int BreakMinutes
    {
        get => breakMinutes;
        set
        {
            if (value < 1) value = 1;
            if (value > 60) value = 60;
            if (breakMinutes == value) return;
            breakMinutes = value;
            OnPropertyChanged();
            if (!isRunning && !isWorkMode)
            {
                remainingSeconds = breakMinutes * 60;
                UpdatePomodoroDisplay();
            }
        }
    }

    public int CycleCount
    {
        get => cycleCount;
        set
        {
            if (value < 1) value = 1;
            if (value > 20) value = 20;
            if (cycleCount == value) return;
            cycleCount = value;
            OnPropertyChanged();
        }
    }

    public int CurrentCycle
    {
        get => currentCycle;
        set { currentCycle = value; OnPropertyChanged(); }
    }

    public bool IsWorkMode
    {
        get => isWorkMode;
        set { isWorkMode = value; OnPropertyChanged(); UpdateModeText(); }
    }

    public bool IsRunning
    {
        get => isRunning;
        set { isRunning = value; OnPropertyChanged(); UpdateStartPauseText(); }
    }

    public string RemainingTimeDisplay
    {
        get => remainingTimeDisplay;
        set { remainingTimeDisplay = value; OnPropertyChanged(); }
    }

    public string ModeText
    {
        get => modeText;
        set { modeText = value; OnPropertyChanged(); }
    }

    public string StartPauseText
    {
        get => startPauseText;
        set { startPauseText = value; OnPropertyChanged(); }
    }

    public ICommand StartPauseCommand { get; }
    public ICommand ResetCommand { get; }
    public ICommand SkipPhaseCommand { get; }

    // ═══════════════════════════════════════
    //  Alarm Properties
    // ═══════════════════════════════════════

    public ObservableCollection<AlarmItem> Alarms { get; }

    public string NewAlarmName
    {
        get => newAlarmName;
        set { newAlarmName = value ?? string.Empty; OnPropertyChanged(); }
    }

    public int NewAlarmHour
    {
        get => newAlarmHour;
        set
        {
            if (value < 0) value = 0;
            if (value > 23) value = 23;
            newAlarmHour = value;
            OnPropertyChanged();
        }
    }

    public int NewAlarmMinute
    {
        get => newAlarmMinute;
        set
        {
            if (value < 0) value = 0;
            if (value > 59) value = 59;
            newAlarmMinute = value;
            OnPropertyChanged();
        }
    }

    public string NewAlarmMessage
    {
        get => newAlarmMessage;
        set { newAlarmMessage = value ?? string.Empty; OnPropertyChanged(); }
    }

    public int NewAlarmRepeatIndex
    {
        get => (int)newAlarmRepeatMode;
        set
        {
            newAlarmRepeatMode = (AlarmRepeatMode)value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomDaysVisible));

            if (newAlarmRepeatMode == AlarmRepeatMode.Weekly)
            {
                var today = DateTime.Now.DayOfWeek;
                NewAlarmMon = today == DayOfWeek.Monday;
                NewAlarmTue = today == DayOfWeek.Tuesday;
                NewAlarmWed = today == DayOfWeek.Wednesday;
                NewAlarmThu = today == DayOfWeek.Thursday;
                NewAlarmFri = today == DayOfWeek.Friday;
                NewAlarmSat = today == DayOfWeek.Saturday;
                NewAlarmSun = today == DayOfWeek.Sunday;
            }
        }
    }

    public bool IsCustomDaysVisible => newAlarmRepeatMode is AlarmRepeatMode.CustomDays or AlarmRepeatMode.Weekly;

    public bool NewAlarmMon { get => newAlarmMon; set { newAlarmMon = value; OnPropertyChanged(); } }
    public bool NewAlarmTue { get => newAlarmTue; set { newAlarmTue = value; OnPropertyChanged(); } }
    public bool NewAlarmWed { get => newAlarmWed; set { newAlarmWed = value; OnPropertyChanged(); } }
    public bool NewAlarmThu { get => newAlarmThu; set { newAlarmThu = value; OnPropertyChanged(); } }
    public bool NewAlarmFri { get => newAlarmFri; set { newAlarmFri = value; OnPropertyChanged(); } }
    public bool NewAlarmSat { get => newAlarmSat; set { newAlarmSat = value; OnPropertyChanged(); } }
    public bool NewAlarmSun { get => newAlarmSun; set { newAlarmSun = value; OnPropertyChanged(); } }

    public ICommand AddAlarmCommand { get; }
    public ICommand DeleteAlarmCommand { get; }

    // ═══════════════════════════════════════
    //  Countdown Properties
    // ═══════════════════════════════════════

    public int CountdownHours
    {
        get => countdownHours;
        set
        {
            if (value < 0) value = 0;
            if (value > 99) value = 99;
            countdownHours = value;
            OnPropertyChanged();
            if (!isCountdownRunning) SyncCountdownTarget();
        }
    }

    public int CountdownMinutes
    {
        get => countdownMinutes;
        set
        {
            if (value < 0) value = 0;
            if (value > 59) value = 59;
            countdownMinutes = value;
            OnPropertyChanged();
            if (!isCountdownRunning) SyncCountdownTarget();
        }
    }

    public int CountdownSeconds
    {
        get => countdownSeconds;
        set
        {
            if (value < 0) value = 0;
            if (value > 59) value = 59;
            countdownSeconds = value;
            OnPropertyChanged();
            if (!isCountdownRunning) SyncCountdownTarget();
        }
    }

    public string CountdownDisplay
    {
        get => countdownDisplay;
        set { countdownDisplay = value; OnPropertyChanged(); }
    }

    public string CountdownStartPauseText
    {
        get => isCountdownRunning ? "暂停" : "开始";
    }

    public ICommand CountdownStartPauseCommand { get; }
    public ICommand CountdownResetCommand { get; }

    // ═══════════════════════════════════════
    //  Pomodoro Logic
    // ═══════════════════════════════════════

    private void TogglePomodoro()
    {
        isRunning = !isRunning;
        IsRunning = isRunning;
    }

    private void ResetPomodoro()
    {
        isRunning = false;
        IsRunning = false;
        isWorkMode = true;
        IsWorkMode = true;
        currentCycle = 1;
        CurrentCycle = currentCycle;
        remainingSeconds = workMinutes * 60;
        UpdatePomodoroDisplay();
    }

    private void SkipPhase()
    {
        SwitchPhase();
    }

    private void SwitchPhase()
    {
        if (isWorkMode)
        {
            isWorkMode = false;
            IsWorkMode = false;
            remainingSeconds = breakMinutes * 60;
            if (pomodoroSoundEnabled) PlaySound(soundService.PlayWorkToBreak);
            ShowPomodoroPopup("休息时间", $"第 {currentCycle}/{cycleCount} 轮工作结束，休息 {breakMinutes} 分钟吧！");
        }
        else
        {
            isWorkMode = true;
            IsWorkMode = true;

            if (currentCycle >= cycleCount)
            {
                isRunning = false;
                IsRunning = false;
                currentCycle = 1;
                CurrentCycle = currentCycle;
                remainingSeconds = workMinutes * 60;
                if (pomodoroSoundEnabled) PlaySound(soundService.PlayBreakToWork);
                ShowPomodoroPopup("全部完成", $"已完成全部 {cycleCount} 轮番茄钟，辛苦了！");
                UpdatePomodoroDisplay();
                return;
            }

            currentCycle++;
            CurrentCycle = currentCycle;
            remainingSeconds = workMinutes * 60;
            if (pomodoroSoundEnabled) PlaySound(soundService.PlayBreakToWork);
            ShowPomodoroPopup("工作时间", $"第 {currentCycle}/{cycleCount} 轮工作开始，专注 {workMinutes} 分钟！");
        }

        UpdatePomodoroDisplay();
    }

    private void UpdatePomodoroDisplay()
    {
        var mins = remainingSeconds / 60;
        var secs = remainingSeconds % 60;
        RemainingTimeDisplay = $"{mins:D2}:{secs:D2}";
        UpdateModeText();
    }

    private void UpdateModeText()
    {
        ModeText = isWorkMode
            ? $"工作时间 (第 {currentCycle}/{cycleCount} 轮)"
            : "休息时间";
    }

    private void UpdateStartPauseText()
    {
        StartPauseText = isRunning ? "暂停" : "开始";
    }

    // ═══════════════════════════════════════
    //  Alarm Logic
    // ═══════════════════════════════════════

    private void AddAlarm()
    {
        var alarm = new AlarmItem
        {
            Name = string.IsNullOrWhiteSpace(newAlarmName) ? "闹钟" : newAlarmName.Trim(),
            Hour = newAlarmHour,
            Minute = newAlarmMinute,
            RepeatMode = newAlarmRepeatMode,
            Message = newAlarmMessage.Trim(),
            Monday = newAlarmMon,
            Tuesday = newAlarmTue,
            Wednesday = newAlarmWed,
            Thursday = newAlarmThu,
            Friday = newAlarmFri,
            Saturday = newAlarmSat,
            Sunday = newAlarmSun
        };

        if (newAlarmRepeatMode == AlarmRepeatMode.Weekly)
        {
            var today = DateTime.Now.DayOfWeek;
            alarm.Monday = today == DayOfWeek.Monday;
            alarm.Tuesday = today == DayOfWeek.Tuesday;
            alarm.Wednesday = today == DayOfWeek.Wednesday;
            alarm.Thursday = today == DayOfWeek.Thursday;
            alarm.Friday = today == DayOfWeek.Friday;
            alarm.Saturday = today == DayOfWeek.Saturday;
            alarm.Sunday = today == DayOfWeek.Sunday;
        }

        Alarms.Add(alarm);
        SaveAlarms();

        // Reset form
        NewAlarmName = string.Empty;
        NewAlarmMessage = string.Empty;
        NewAlarmHour = 0;
        NewAlarmMinute = 0;
        NewAlarmRepeatIndex = 0;
        NewAlarmMon = NewAlarmTue = NewAlarmWed = NewAlarmThu = NewAlarmFri = NewAlarmSat = NewAlarmSun = false;
    }

    private void DeleteAlarm(string id)
    {
        var alarm = Alarms.FirstOrDefault(a => a.Id == id);
        if (alarm is not null)
        {
            Alarms.Remove(alarm);
            SaveAlarms();
        }
    }

    private void CheckAlarms(DateTime now)
    {
        foreach (var alarm in Alarms)
        {
            if (!alarm.IsEnabled) continue;

            if (!ShouldTriggerToday(alarm, now)) continue;

            if (now.Hour != alarm.Hour || now.Minute != alarm.Minute || now.Second != 0)
                continue;

            if (alarm.LastTriggered.Date == now.Date &&
                alarm.LastTriggered.Hour == now.Hour &&
                alarm.LastTriggered.Minute == now.Minute)
                continue;

            alarm.LastTriggered = now;
            SaveAlarms();

            TriggerAlarm(alarm);
        }
    }

    private static bool ShouldTriggerToday(AlarmItem alarm, DateTime now)
    {
        return alarm.RepeatMode switch
        {
            AlarmRepeatMode.Once => true,
            AlarmRepeatMode.Daily => true,
            AlarmRepeatMode.Weekly => now.DayOfWeek switch
            {
                DayOfWeek.Monday => alarm.Monday,
                DayOfWeek.Tuesday => alarm.Tuesday,
                DayOfWeek.Wednesday => alarm.Wednesday,
                DayOfWeek.Thursday => alarm.Thursday,
                DayOfWeek.Friday => alarm.Friday,
                DayOfWeek.Saturday => alarm.Saturday,
                DayOfWeek.Sunday => alarm.Sunday,
                _ => false
            },
            AlarmRepeatMode.CustomDays => now.DayOfWeek switch
            {
                DayOfWeek.Monday => alarm.Monday,
                DayOfWeek.Tuesday => alarm.Tuesday,
                DayOfWeek.Wednesday => alarm.Wednesday,
                DayOfWeek.Thursday => alarm.Thursday,
                DayOfWeek.Friday => alarm.Friday,
                DayOfWeek.Saturday => alarm.Saturday,
                DayOfWeek.Sunday => alarm.Sunday,
                _ => false
            },
            _ => false
        };
    }

    private void TriggerAlarm(AlarmItem alarm)
    {
        if (alarmSoundEnabled) PlaySound(soundService.PlayAlarm);

        if (alarmPopupEnabled)
        {
            var title = string.IsNullOrWhiteSpace(alarm.Name) ? "闹钟提醒" : alarm.Name;
            var message = string.IsNullOrWhiteSpace(alarm.Message)
                ? "时间到了！"
                : alarm.Message;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var popup = new AlarmPopupWindow(title, message, DateTime.Now);
                popup.Show();
            });
        }

        // Disable once-only alarms after triggering
        if (alarm.RepeatMode == AlarmRepeatMode.Once)
        {
            alarm.IsEnabled = false;
            SaveAlarms();
        }
    }

    // ═══════════════════════════════════════
    //  Countdown Logic
    // ═══════════════════════════════════════

    private void SyncCountdownTarget()
    {
        countdownRemaining = countdownHours * 3600 + countdownMinutes * 60 + countdownSeconds;
        UpdateCountdownDisplay();
    }

    private void ToggleCountdown()
    {
        if (countdownRemaining <= 0 && !isCountdownRunning)
        {
            SyncCountdownTarget();
        }

        isCountdownRunning = !isCountdownRunning;
        OnPropertyChanged(nameof(CountdownStartPauseText));
    }

    private void ResetCountdown()
    {
        isCountdownRunning = false;
        OnPropertyChanged(nameof(CountdownStartPauseText));
        SyncCountdownTarget();
    }

    private void UpdateCountdownDisplay()
    {
        var h = countdownRemaining / 3600;
        var m = (countdownRemaining % 3600) / 60;
        var s = countdownRemaining % 60;

        CountdownDisplay = h > 0
            ? $"{h}:{m:D2}:{s:D2}"
            : $"{m:D2}:{s:D2}";
    }

    // ═══════════════════════════════════════
    //  Tick Handler
    // ═══════════════════════════════════════

    private void OnTick(object? sender, EventArgs e)
    {
        var now = DateTime.Now;

        // Pomodoro countdown
        if (isRunning && remainingSeconds > 0)
        {
            remainingSeconds--;
            UpdatePomodoroDisplay();

            if (remainingSeconds <= 0)
            {
                SwitchPhase();
            }
        }

        // Alarm check
        CheckAlarms(now);

        // Countdown
        if (isCountdownRunning && countdownRemaining > 0)
        {
            countdownRemaining--;
            UpdateCountdownDisplay();

            if (countdownRemaining <= 0)
            {
                isCountdownRunning = false;
                OnPropertyChanged(nameof(CountdownStartPauseText));
                if (countdownSoundEnabled) PlaySound(soundService.PlayCountdownEnd);

                if (countdownPopupEnabled)
                {
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        var popup = new AlarmPopupWindow("倒计时结束", "倒计时已结束！", DateTime.Now);
                        popup.Show();
                    });
                }
            }
        }
    }

    // ═══════════════════════════════════════
    //  Alarm Persistence
    // ═══════════════════════════════════════

    private void LoadAlarms()
    {
        try
        {
            if (File.Exists(alarmsFilePath))
            {
                var json = File.ReadAllText(alarmsFilePath);
                var loaded = JsonSerializer.Deserialize<List<AlarmItem>>(json);
                if (loaded is not null)
                {
                    foreach (var alarm in loaded)
                        Alarms.Add(alarm);
                }
            }
        }
        catch
        {
            // Ignore load errors
        }
    }

    private void SaveAlarms()
    {
        try
        {
            var json = JsonSerializer.Serialize(Alarms.ToList(), new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(alarmsFilePath, json);
        }
        catch
        {
            // Ignore save errors
        }
    }
}
