using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using MemoMind.App.Commands;
using MemoMind.App.Models;
using MemoMind.App.Services;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace MemoMind.App.ViewModels;

public class CyberPlantViewModel : ViewModelBase, ISettingsAwareViewModel
{
    private readonly IChatService? chatService;
    private readonly ICustomPlantService customPlantService;
    private CyberPlant plant;
    private string inputText = string.Empty;
    private string statusMessage = string.Empty;
    private bool isAiMode;
    private bool isSending;
    private ImageSource? plantImageSource;
    private bool isCustomEditorOpen;
    private int? editingCustomPlantId;
    private string customEditName = string.Empty;
    private string customEditPersonality = string.Empty;
    private string customEditPrompt = string.Empty;
    private string customEditImagePath = string.Empty;
    private string? editingSystemPlantId;
    private bool isEditingSystemDeleted;
    private Dictionary<string, PlantProfileOverride> profileOverrides = new(StringComparer.OrdinalIgnoreCase);
    private const int CareIncreaseAmount = 3;
    private const double MinDailyDecayRate = 0.10;
    private const double MaxDailyDecayRate = 0.30;
    private const string CustomPlantPrefix = "custom:";
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoMind");
    private static readonly string PlantDataPath = Path.Combine(DataFolder, "cyber_plant.json");
    private static readonly string PlantProfileOverridesPath = Path.Combine(DataFolder, "plant_profiles.json");
    private static readonly string PlantImageLibraryPath = Path.Combine(DataFolder, "plant_images");
    private static readonly string OfficialImageLibraryPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "PlantImages");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CyberPlantViewModel()
    {
        chatService = App.Services.GetService<IChatService>();
        customPlantService = App.Services.GetRequiredService<ICustomPlantService>();
        plant = LoadPlant();
        EnsurePlantDefaults(plant);
        LoadProfileOverrides();

        PlantItems = new ObservableCollection<PlantListItem>();
        CustomPlants = new ObservableCollection<CustomPlantProfile>();
        Messages = new ObservableCollection<PlantMessage>(plant.Messages);

        BuildPlantItems();

        EnsurePlantStateStore();
        ApplyDailyDecayIfNeeded();
        EnsureDailyChatReset();
        UpdatePlantImageSource();

        var settings = App.Services.GetRequiredService<IAppSettingsStore>().LoadAsync().GetAwaiter().GetResult();
        ApplySettings(settings);

        SendCommand = new RelayCommand(_ => Send(), _ => !string.IsNullOrWhiteSpace(InputText) && !IsSending);
        WaterCommand = new RelayCommand(_ => Water());
        FertilizeCommand = new RelayCommand(_ => Fertilize());
        SunCommand = new RelayCommand(_ => Sunbathe());
        SelectPlantCommand = new RelayCommand(param => SelectPlant(param as PlantListItem));
        AddCustomPlantCommand = new RelayCommand(_ => CreateCustomPlant());
        SaveCustomPlantCommand = new RelayCommand(_ => SaveCustomPlant(), _ => IsCustomEditorOpen);
        DeleteCustomPlantCommand = new RelayCommand(_ => DeleteCustomPlant(), _ => EditingCustomPlantId.HasValue || !string.IsNullOrWhiteSpace(EditingSystemPlantId));
        ResetSystemPlantCommand = new RelayCommand(_ => ResetSystemPlant(), _ => !string.IsNullOrWhiteSpace(EditingSystemPlantId));
        OpenCustomEditorCommand = new RelayCommand(param => OpenCustomEditor(param as PlantListItem));

        _ = LoadCustomPlantsAsync();
        UpdateStatus();
    }

    public ObservableCollection<PlantListItem> PlantItems { get; }
    public ObservableCollection<CustomPlantProfile> CustomPlants { get; }
    public ObservableCollection<PlantMessage> Messages { get; }

    public string PlantType
    {
        get => plant.PlantType;
        set
        {
            plant.PlantType = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(IsCustomPlant));
            OnPropertyChanged(nameof(PlantEmoji));
            OnPropertyChanged(nameof(PlantPersonality));
            OnPropertyChanged(nameof(PlantSpecies));
            UpdatePlantImageSource();
        }
    }

    public string PlantName
    {
        get
        {
            if (IsCustomPlant)
            {
                return CurrentCustomProfile?.Name ?? plant.PlantName;
            }

            return GetOverride(PlantType)?.Name ?? plant.PlantName;
        }
        set
        {
            if (IsCustomPlant)
            {
                return;
            }

            plant.PlantName = value;
            OnPropertyChanged();
            SavePlant();
        }
    }

    public string PlantSpecies
    {
        get
        {
            if (IsCustomPlant)
            {
                return string.IsNullOrWhiteSpace(plant.CustomSpecies) ? "自定义植物" : plant.CustomSpecies;
            }

            return CurrentPreset?.Name ?? "植物";
        }
        set
        {
            if (!IsCustomPlant) return;
            plant.CustomSpecies = value;
            OnPropertyChanged();
        }
    }

    public string PlantEmoji => IsCustomPlant
        ? "🌱"
        : (CurrentPreset?.Emoji ?? "🌱");

    public string PlantPersonality => CyberPlantPresets.All
        .FirstOrDefault(p => p.Id == PlantType)?.Personality
        ?? GetOverride(PlantType)?.Personality
        ?? CurrentCustomProfile?.Personality
        ?? "独特";

    public int GrowthLevel
    {
        get => plant.GrowthLevel;
        set { plant.GrowthLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(GrowthDisplay)); }
    }

    public string GrowthDisplay => new string('⭐', Math.Clamp(GrowthLevel, 0, 10));

    public string Mood
    {
        get => plant.Mood;
        set { plant.Mood = value; OnPropertyChanged(); }
    }

    public DateTime LastWateredAt
    {
        get => plant.LastWateredAt;
        set { plant.LastWateredAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(LastWateredDisplay)); }
    }

    public DateTime LastFertilizedAt
    {
        get => plant.LastFertilizedAt;
        set { plant.LastFertilizedAt = value; OnPropertyChanged(); }
    }

    public DateTime LastSunbathedAt
    {
        get => plant.LastSunbathedAt;
        set { plant.LastSunbathedAt = value; OnPropertyChanged(); }
    }

    public string LastWateredDisplay => LastWateredAt == default
        ? "还没浇过水"
        : $"上次浇水：{LastWateredAt:MM-dd HH:mm}";

    public int WaterValue
    {
        get => plant.WaterValue;
        set
        {
            plant.WaterValue = value;
            OnPropertyChanged();
            NotifyCareStatusChanged();
        }
    }

    public int NutritionValue
    {
        get => plant.NutritionValue;
        set
        {
            plant.NutritionValue = value;
            OnPropertyChanged();
            NotifyCareStatusChanged();
        }
    }

    public int SunValue
    {
        get => plant.SunValue;
        set
        {
            plant.SunValue = value;
            OnPropertyChanged();
            NotifyCareStatusChanged();
        }
    }

    public int MaxWater
    {
        get => plant.MaxWater;
        set
        {
            plant.MaxWater = Math.Max(1, value);
            WaterValue = Math.Min(WaterValue, plant.MaxWater);
            NotifyCareStatusChanged();
            SavePlant();
        }
    }

    public int MaxNutrition
    {
        get => plant.MaxNutrition;
        set
        {
            plant.MaxNutrition = Math.Max(1, value);
            NutritionValue = Math.Min(NutritionValue, plant.MaxNutrition);
            NotifyCareStatusChanged();
            SavePlant();
        }
    }

    public int MaxSun
    {
        get => plant.MaxSun;
        set
        {
            plant.MaxSun = Math.Max(1, value);
            SunValue = Math.Min(SunValue, plant.MaxSun);
            NotifyCareStatusChanged();
            SavePlant();
        }
    }

    public int NeedWater
    {
        get => plant.NeedWater;
        set
        {
            plant.NeedWater = Math.Max(1, value);
            NotifyCareStatusChanged();
            SavePlant();
        }
    }

    public int NeedNutrition
    {
        get => plant.NeedNutrition;
        set
        {
            plant.NeedNutrition = Math.Max(1, value);
            NotifyCareStatusChanged();
            SavePlant();
        }
    }

    public int NeedSun
    {
        get => plant.NeedSun;
        set
        {
            plant.NeedSun = Math.Max(1, value);
            NotifyCareStatusChanged();
            SavePlant();
        }
    }

    public string WaterStatusDisplay => $"水分 {WaterValue}/{MaxWater} · 需求 {NeedWater}";

    public string NutritionStatusDisplay => $"营养 {NutritionValue}/{MaxNutrition} · 需求 {NeedNutrition}";

    public string SunStatusDisplay => $"阳光 {SunValue}/{MaxSun} · 需求 {NeedSun}";

    public string CustomEmoji
    {
        get => plant.CustomEmoji;
        set
        {
            plant.CustomEmoji = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlantEmoji));
            SavePlant();
        }
    }

    public string CustomSystemPrompt
    {
        get => plant.CustomSystemPrompt;
        set
        {
            plant.CustomSystemPrompt = value;
            OnPropertyChanged();
            SavePlant();
        }
    }

    public string CustomImagePath
    {
        get => plant.CustomImagePath;
        set
        {
            plant.CustomImagePath = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(PlantImageHint));
            UpdatePlantImageSource();
            SavePlant();
        }
    }

    public ImageSource? PlantImageSource
    {
        get => plantImageSource;
        private set
        {
            plantImageSource = value;
            OnPropertyChanged();
        }
    }

    public string PlantImageHint => string.IsNullOrWhiteSpace(CustomImagePath)
        ? "未选择图片"
        : Path.GetFileName(CustomImagePath);

    public bool IsCustomEditorOpen
    {
        get => isCustomEditorOpen;
        set
        {
            isCustomEditorOpen = value;
            OnPropertyChanged();
            (SaveCustomPlantCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public int? EditingCustomPlantId
    {
        get => editingCustomPlantId;
        private set
        {
            editingCustomPlantId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeleteButtonText));
            (DeleteCustomPlantCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string? EditingSystemPlantId
    {
        get => editingSystemPlantId;
        private set
        {
            editingSystemPlantId = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeleteButtonText));
            (DeleteCustomPlantCommand as RelayCommand)?.RaiseCanExecuteChanged();
            (ResetSystemPlantCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public bool IsEditingSystemDeleted
    {
        get => isEditingSystemDeleted;
        private set
        {
            isEditingSystemDeleted = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(DeleteButtonText));
            (DeleteCustomPlantCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string CustomEditName
    {
        get => customEditName;
        set
        {
            customEditName = value;
            OnPropertyChanged();
        }
    }

    public string CustomEditPersonality
    {
        get => customEditPersonality;
        set
        {
            customEditPersonality = value;
            OnPropertyChanged();
        }
    }

    public string CustomEditPrompt
    {
        get => customEditPrompt;
        set
        {
            customEditPrompt = value;
            OnPropertyChanged();
        }
    }

    public string CustomEditImagePath
    {
        get => customEditImagePath;
        set
        {
            customEditImagePath = value;
            OnPropertyChanged();
        }
    }

    public string DeleteButtonText =>
        !string.IsNullOrWhiteSpace(EditingSystemPlantId)
            ? (IsEditingSystemDeleted ? "恢复" : "隐藏")
            : "删除";

    public bool IsCustomPlant => PlantType.StartsWith(CustomPlantPrefix, StringComparison.OrdinalIgnoreCase);

    public bool IsAiMode
    {
        get => isAiMode;
        set { isAiMode = value; OnPropertyChanged(); }
    }

    public bool IsSending
    {
        get => isSending;
        set { isSending = value; OnPropertyChanged(); (SendCommand as RelayCommand)?.RaiseCanExecuteChanged(); }
    }

    public string InputText
    {
        get => inputText;
        set
        {
            inputText = value;
            OnPropertyChanged();
            (SendCommand as RelayCommand)?.RaiseCanExecuteChanged();
        }
    }

    public string StatusMessage
    {
        get => statusMessage;
        set { statusMessage = value; OnPropertyChanged(); }
    }

    public ICommand SendCommand { get; }
    public ICommand WaterCommand { get; }
    public ICommand FertilizeCommand { get; }
    public ICommand SunCommand { get; }
    public ICommand SelectPlantCommand { get; }
    public ICommand AddCustomPlantCommand { get; }
    public ICommand SaveCustomPlantCommand { get; }
    public ICommand DeleteCustomPlantCommand { get; }
    public ICommand ResetSystemPlantCommand { get; }
    public ICommand OpenCustomEditorCommand { get; }

    public void ApplySettings(UserSettings settings)
    {
        IsAiMode = settings.EnableAi && !string.IsNullOrWhiteSpace(settings.ApiKey);
        UpdateStatus();
    }

    public async Task ResetAllDataAsync()
    {
        try
        {
            var customProfiles = await customPlantService.GetAllAsync();
            foreach (var profile in customProfiles)
            {
                await customPlantService.DeleteAsync(profile.Id);
            }
        }
        catch
        {
            StatusMessage = "自定义植物清理失败，请确认数据库已迁移。";
        }

        try
        {
            if (File.Exists(PlantDataPath)) File.Delete(PlantDataPath);
            if (File.Exists(PlantProfileOverridesPath)) File.Delete(PlantProfileOverridesPath);
        }
        catch
        {
            // Ignore file cleanup errors
        }

        profileOverrides = new Dictionary<string, PlantProfileOverride>(StringComparer.OrdinalIgnoreCase);
        plant = LoadPlant();
        EnsurePlantDefaults(plant);
        InitializeNewPlantState(CurrentPreset);

        Messages.Clear();
        foreach (var msg in plant.Messages)
        {
            Messages.Add(msg);
        }

        await LoadCustomPlantsAsync();
        BuildPlantItems();
        UpdatePlantImageSource();
        UpdateStatus();
        SavePlant();

        StatusMessage = "赛博植物已恢复默认设定。";
    }

    private void SelectPlant(PlantListItem? item)
    {
        if (item is null) return;
        if (item.IsDeleted) return;

        var preset = item.IsCustom
            ? null
            : CyberPlantPresets.All.FirstOrDefault(p => p.Id == item.Id);

        if (!item.IsCustom && preset is null) return;

        IsCustomEditorOpen = false;
        PersistCurrentState();
        PlantType = item.Id;
        if (!TryRestorePlantState(item.Id))
        {
            InitializeNewPlantState(preset);
        }

        ApplyDailyDecayIfNeeded();
        EnsureDailyChatReset();
        UpdatePlantImageSource();
        UpdateMoodFromCare();

        SavePlant();
        UpdateStatus();
    }

    private async void Send()
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsSending) return;

        EnsureDailyChatReset();
        Messages.Add(new PlantMessage { Sender = "我", Content = text, Time = DateTime.Now });
        InputText = string.Empty;
        IsSending = true;

        ApplyDailyDecayIfNeeded();
        if (plant.IsCareLocked)
        {
            Messages.Add(new PlantMessage
            {
                Sender = PlantName,
                Content = "我现在太虚弱了，等水分、营养和阳光都恢复到需求值再和你聊。",
                Time = DateTime.Now
            });
            IsSending = false;
            SavePlant();
            return;
        }

        string reply;
        if (IsAiMode && chatService is not null)
        {
            var preset = CurrentPreset;
            var systemPrompt = BuildPlantSystemPrompt(preset);

            try
            {
                reply = await chatService.SendAsync(systemPrompt, text);
            }
            catch
            {
                reply = CyberPlantPresets.GetOfflinePlantReply(plant, text)
                    + "\n\n（AI 功能不可用，服务暂时无法连接）";
            }
        }
        else
        {
            await Task.Delay(300 + Random.Shared.Next(700));
            reply = CyberPlantPresets.GetOfflinePlantReply(plant, text)
                + "\n\n（AI 功能不可用，请在设置中配置并启用 AI）";
        }

        Messages.Add(new PlantMessage { Sender = PlantName, Content = reply, Time = DateTime.Now });
        plant.Messages = Messages.TakeLast(50).ToList();
        IsSending = false;
        SavePlant();
    }

    private void Water()
    {
        WaterValue = Math.Min(WaterValue + CareIncreaseAmount, MaxWater);
        LastWateredAt = DateTime.Now;

        Messages.Add(new PlantMessage
        {
            Sender = PlantName,
            Content = GrowthLevel switch
            {
                10 => "谢谢你一直以来的照顾，我已经长得很好啦！🌿✨",
                _ when GrowthLevel >= 7 => "咕嘟咕嘟～我感觉自己又强壮了不少！",
                _ when GrowthLevel >= 4 => "谢谢你浇水！我会努力长大的～",
                _ => "有水喝真好！我会好好长大的！"
            },
            Time = DateTime.Now
        });

        SavePlant();
        UpdateStatus();
    }

    private void Fertilize()
    {
        NutritionValue = Math.Min(NutritionValue + CareIncreaseAmount, MaxNutrition);
        LastFertilizedAt = DateTime.Now;

        Messages.Add(new PlantMessage
        {
            Sender = PlantName,
            Content = GrowthLevel switch
            {
                10 => "养分充足，我状态拉满！谢谢你的细心照料！",
                _ when GrowthLevel >= 7 => "营养补上了，我感觉更有劲啦！",
                _ when GrowthLevel >= 4 => "施肥真及时，我会慢慢变强的。",
                _ => "谢谢你的养分，我会努力长大的！"
            },
            Time = DateTime.Now
        });

        SavePlant();
        UpdateStatus();
    }

    private void Sunbathe()
    {
        SunValue = Math.Min(SunValue + CareIncreaseAmount, MaxSun);
        LastSunbathedAt = DateTime.Now;

        Messages.Add(new PlantMessage
        {
            Sender = PlantName,
            Content = GrowthLevel switch
            {
                10 => "阳光满满，我今天简直发光！",
                _ when GrowthLevel >= 7 => "晒得刚刚好，整株都精神了！",
                _ when GrowthLevel >= 4 => "谢谢你带我晒太阳，舒服～",
                _ => "阳光真好，我会好好吸收的！"
            },
            Time = DateTime.Now
        });

        SavePlant();
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        UpdateMoodFromCare();
        var careHint = BuildCareHint();

        StatusMessage = IsAiMode
            ? $"🌱 AI 模式已开启 — {PlantName}会用自己的性格和你聊天{careHint}"
            : $"🌱 离线模式 — {PlantName}仍然可以陪你聊天{careHint}";
    }

    private void EnsureDailyChatReset()
    {
        var today = DateTime.Today;
        if (plant.LastChatClearedAt.Date == today)
        {
            return;
        }

        Messages.Clear();
        plant.LastChatClearedAt = today;
        SavePlant();
    }

    private void ApplyDailyDecayIfNeeded()
    {
        var today = DateTime.Today;
        if (plant.LastCareDecayAt == default)
        {
            plant.LastCareDecayAt = today;
            return;
        }

        var days = (today - plant.LastCareDecayAt.Date).Days;
        if (days <= 0) return;

        var random = Random.Shared;
        for (var i = 0; i < days; i++)
        {
            WaterValue = ApplyDecay(WaterValue, random);
            NutritionValue = ApplyDecay(NutritionValue, random);
            SunValue = ApplyDecay(SunValue, random);
        }

        plant.LastCareDecayAt = today;
        NotifyCareStatusChanged();
    }

    private static int ApplyDecay(int value, Random random)
    {
        if (value <= 0) return 0;
        var rate = MinDailyDecayRate + random.NextDouble() * (MaxDailyDecayRate - MinDailyDecayRate);
        var loss = (int)Math.Ceiling(value * rate);
        return Math.Max(0, value - loss);
    }

    private static CyberPlant LoadPlant()
    {
        try
        {
            if (File.Exists(PlantDataPath))
            {
                var json = File.ReadAllText(PlantDataPath);
                return JsonSerializer.Deserialize<CyberPlant>(json) ?? new CyberPlant();
            }
        }
        catch { }

        return new CyberPlant
        {
            PlantType = "cactus",
            PlantName = "小仙人掌",
            GrowthLevel = 1,
            Mood = "开心",
            LastWateredAt = DateTime.Now
        };
    }

    private void SavePlant()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            PersistCurrentState();
            plant.Messages = Messages.TakeLast(50).ToList();
            var json = JsonSerializer.Serialize(plant, JsonOptions);
            File.WriteAllText(PlantDataPath, json);
        }
        catch { }
    }

    private void EnsurePlantStateStore()
    {
        if (plant.PlantStates.Count == 0)
        {
            PersistCurrentState();
            return;
        }

        if (plant.PlantStates.TryGetValue(plant.PlantType, out var state))
        {
            ApplyState(state);
        }
        else
        {
            PersistCurrentState();
        }
    }

    private void PersistCurrentState()
    {
        if (IsCustomPlant && CurrentCustomProfile is not null)
        {
            plant.PlantName = CurrentCustomProfile.Name;
        }

        plant.PlantStates[plant.PlantType] = BuildStateFromCurrent();
    }

    private bool TryRestorePlantState(string plantType)
    {
        if (!plant.PlantStates.TryGetValue(plantType, out var state)) return false;
        ApplyState(state);
        return true;
    }

    private PlantCareState BuildStateFromCurrent()
    {
        return new PlantCareState
        {
            PlantType = plant.PlantType,
            PlantName = plant.PlantName,
            CustomEmoji = plant.CustomEmoji,
            CustomSystemPrompt = plant.CustomSystemPrompt,
            CustomImagePath = plant.CustomImagePath,
            GrowthLevel = plant.GrowthLevel,
            Mood = plant.Mood,
            LastWateredAt = plant.LastWateredAt,
            LastFertilizedAt = plant.LastFertilizedAt,
            LastSunbathedAt = plant.LastSunbathedAt,
            LastCareDecayAt = plant.LastCareDecayAt,
            LastChatClearedAt = plant.LastChatClearedAt,
            WaterValue = plant.WaterValue,
            NutritionValue = plant.NutritionValue,
            SunValue = plant.SunValue,
            MaxWater = plant.MaxWater,
            MaxNutrition = plant.MaxNutrition,
            MaxSun = plant.MaxSun,
            NeedWater = plant.NeedWater,
            NeedNutrition = plant.NeedNutrition,
            NeedSun = plant.NeedSun,
            IsCareLocked = plant.IsCareLocked,
            Messages = Messages.ToList()
        };
    }

    private void ApplyState(PlantCareState state)
    {
        plant.PlantName = string.IsNullOrWhiteSpace(state.PlantName) ? plant.PlantName : state.PlantName;
        plant.CustomEmoji = state.CustomEmoji ?? string.Empty;
        plant.CustomSystemPrompt = state.CustomSystemPrompt ?? string.Empty;
        plant.CustomImagePath = state.CustomImagePath ?? string.Empty;
        plant.GrowthLevel = state.GrowthLevel;
        plant.Mood = string.IsNullOrWhiteSpace(state.Mood) ? plant.Mood : state.Mood;
        plant.LastWateredAt = state.LastWateredAt;
        plant.LastFertilizedAt = state.LastFertilizedAt;
        plant.LastSunbathedAt = state.LastSunbathedAt;
        plant.LastCareDecayAt = state.LastCareDecayAt == default ? DateTime.Today : state.LastCareDecayAt;
        plant.LastChatClearedAt = state.LastChatClearedAt == default ? DateTime.Today : state.LastChatClearedAt;
        plant.WaterValue = state.WaterValue;
        plant.NutritionValue = state.NutritionValue;
        plant.SunValue = state.SunValue;
        plant.MaxWater = Math.Max(1, state.MaxWater);
        plant.MaxNutrition = Math.Max(1, state.MaxNutrition);
        plant.MaxSun = Math.Max(1, state.MaxSun);
        plant.NeedWater = Math.Max(1, state.NeedWater);
        plant.NeedNutrition = Math.Max(1, state.NeedNutrition);
        plant.NeedSun = Math.Max(1, state.NeedSun);
        plant.IsCareLocked = state.IsCareLocked;
        plant.Messages = state.Messages ?? [];

        Messages.Clear();
        foreach (var msg in plant.Messages)
        {
            Messages.Add(msg);
        }

        OnPropertyChanged(nameof(PlantName));
        OnPropertyChanged(nameof(PlantEmoji));
        OnPropertyChanged(nameof(PlantPersonality));
        OnPropertyChanged(nameof(GrowthLevel));
        OnPropertyChanged(nameof(GrowthDisplay));
        OnPropertyChanged(nameof(Mood));
        OnPropertyChanged(nameof(LastWateredAt));
        OnPropertyChanged(nameof(LastWateredDisplay));
        OnPropertyChanged(nameof(WaterValue));
        OnPropertyChanged(nameof(NutritionValue));
        OnPropertyChanged(nameof(SunValue));
        OnPropertyChanged(nameof(MaxWater));
        OnPropertyChanged(nameof(MaxNutrition));
        OnPropertyChanged(nameof(MaxSun));
        OnPropertyChanged(nameof(NeedWater));
        OnPropertyChanged(nameof(NeedNutrition));
        OnPropertyChanged(nameof(NeedSun));
        OnPropertyChanged(nameof(CustomEmoji));
        OnPropertyChanged(nameof(CustomSystemPrompt));
        OnPropertyChanged(nameof(CustomImagePath));
        OnPropertyChanged(nameof(PlantImageHint));

        NotifyCareStatusChanged();
        UpdatePlantImageSource();
    }

    private void InitializeNewPlantState(CyberPlantType? preset)
    {
        var fallbackPreset = preset ?? CyberPlantPresets.All.First();

        if (!IsCustomPlant)
        {
            plant.PlantName = fallbackPreset.Name;
            plant.CustomEmoji = string.Empty;
            plant.CustomSystemPrompt = string.Empty;
            plant.CustomImagePath = string.Empty;
            ApplyPresetCareProfile(fallbackPreset);
        }
        else
        {
            plant.PlantName = string.IsNullOrWhiteSpace(plant.PlantName) ? "我的植物" : plant.PlantName;
            ApplyPresetCareProfile(fallbackPreset);
            InitializeCustomDefaults();
        }

        plant.LastWateredAt = DateTime.Now;
        plant.LastFertilizedAt = DateTime.Now;
        plant.LastSunbathedAt = DateTime.Now;
        plant.LastCareDecayAt = DateTime.Today;
        plant.IsCareLocked = false;

        Messages.Clear();
        plant.Messages = [];

        NotifyCareStatusChanged();
        UpdatePlantImageSource();
    }

    public bool TrySetCustomEditImageFromFile(string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath)) return false;

        var extension = Path.GetExtension(sourcePath).ToLowerInvariant();
        if (extension is not (".png" or ".jpg" or ".jpeg" or ".bmp")) return false;

        Directory.CreateDirectory(PlantImageLibraryPath);
        var fileName = $"{PlantType}_{DateTime.Now:yyyyMMddHHmmssfff}{extension}";
        var destPath = Path.Combine(PlantImageLibraryPath, fileName);
        File.Copy(sourcePath, destPath, true);

        CustomEditImagePath = Path.Combine("plant_images", fileName);
        return true;
    }

    private void UpdatePlantImageSource()
    {
        var resolvedPath = ResolvePlantImagePath();
        PlantImageSource = string.IsNullOrWhiteSpace(resolvedPath) ? null : LoadImage(resolvedPath);
    }

    private string? ResolvePlantImagePath()
    {
        if (IsCustomPlant)
        {
            return ResolveCustomImagePath();
        }

        var overrideProfile = GetOverride(PlantType);
        if (overrideProfile is not null && !string.IsNullOrWhiteSpace(overrideProfile.ImagePath))
        {
            return ResolveLocalImagePath(overrideProfile.ImagePath);
        }

        return FindPresetImagePath(PlantType);
    }

    private string? ResolveCustomImagePath()
    {
        var profile = CurrentCustomProfile;
        if (profile is null) return null;
        if (string.IsNullOrWhiteSpace(profile.ImagePath)) return null;
        return ResolveLocalImagePath(profile.ImagePath);
    }

    private static string? FindPresetImagePath(string plantType)
    {
        var candidates = new[]
        {
            Path.Combine(OfficialImageLibraryPath, $"{plantType}.png"),
            Path.Combine(OfficialImageLibraryPath, $"{plantType}.jpg"),
            Path.Combine(OfficialImageLibraryPath, $"{plantType}.jpeg")
        };

        foreach (var path in candidates)
        {
            if (File.Exists(path)) return path;
        }

        return null;
    }

    private static string ResolveLocalImagePath(string storedPath)
    {
        return Path.IsPathRooted(storedPath) ? storedPath : Path.Combine(DataFolder, storedPath);
    }

    private static ImageSource? LoadImage(string path)
    {
        try
        {
            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();
            return bitmap;
        }
        catch
        {
            return null;
        }
    }

    private async Task LoadCustomPlantsAsync()
    {
        try
        {
            var items = await customPlantService.GetAllAsync();
            CustomPlants.Clear();
            foreach (var item in items)
            {
                CustomPlants.Add(item);
            }
        }
        catch
        {
            StatusMessage = "自定义植物读取失败，请确认数据库已迁移。";
        }

        BuildPlantItems();

        OnPropertyChanged(nameof(PlantName));
        OnPropertyChanged(nameof(PlantPersonality));
        UpdatePlantImageSource();

        if (IsCustomPlant && CurrentCustomProfile is null)
        {
            var fallback = CyberPlantPresets.All.First();
            PlantType = fallback.Id;
            InitializeNewPlantState(fallback);
        }
    }

    private void BuildPlantItems()
    {
        PlantItems.Clear();

        foreach (var preset in CyberPlantPresets.All)
        {
            var overrideProfile = GetOverride(preset.Id);
            if (overrideProfile?.IsDeleted != true)
            {
                PlantItems.Add(new PlantListItem
                {
                    Id = preset.Id,
                    Name = string.IsNullOrWhiteSpace(overrideProfile?.Name) ? preset.Name : overrideProfile!.Name!,
                    Emoji = preset.Emoji,
                    Personality = string.IsNullOrWhiteSpace(overrideProfile?.Personality) ? preset.Personality : overrideProfile!.Personality!,
                    IsCustom = false,
                    IsDeleted = false
                });
            }
        }

        foreach (var profile in CustomPlants)
        {
            PlantItems.Add(new PlantListItem
            {
                Id = BuildCustomPlantTypeId(profile.Id),
                Name = profile.Name,
                Emoji = "🌱",
                Personality = profile.Personality,
                IsCustom = true,
                CustomId = profile.Id
            });
        }

        foreach (var preset in CyberPlantPresets.All)
        {
            var overrideProfile = GetOverride(preset.Id);
            if (overrideProfile?.IsDeleted == true)
            {
                PlantItems.Add(new PlantListItem
                {
                    Id = preset.Id,
                    Name = string.IsNullOrWhiteSpace(overrideProfile!.Name) ? preset.Name : overrideProfile!.Name!,
                    Emoji = preset.Emoji,
                    Personality = string.IsNullOrWhiteSpace(overrideProfile.Personality) ? preset.Personality : overrideProfile!.Personality!,
                    IsCustom = false,
                    IsDeleted = true
                });
            }
        }
    }

    private void OpenCustomEditor(PlantListItem? item)
    {
        if (item is null)
        {
            return;
        }

        EditingCustomPlantId = null;
        EditingSystemPlantId = null;
        IsEditingSystemDeleted = false;

        if (item.IsCustom)
        {
            var profile = CustomPlants.FirstOrDefault(x => x.Id == item.CustomId);
            if (profile is null)
            {
                return;
            }

            EditingCustomPlantId = profile.Id;
            CustomEditName = profile.Name;
            CustomEditPersonality = profile.Personality;
            CustomEditPrompt = profile.SystemPrompt;
            CustomEditImagePath = profile.ImagePath;
        }
        else
        {
            var preset = CyberPlantPresets.All.FirstOrDefault(p => p.Id == item.Id);
            if (preset is null)
            {
                return;
            }

            var overrideProfile = GetOverride(item.Id);
            EditingSystemPlantId = item.Id;
            IsEditingSystemDeleted = overrideProfile?.IsDeleted == true;
            CustomEditName = overrideProfile?.Name ?? preset.Name;
            CustomEditPersonality = overrideProfile?.Personality ?? preset.Personality;
            CustomEditPrompt = overrideProfile?.SystemPrompt ?? preset.SystemPrompt;
            CustomEditImagePath = overrideProfile?.ImagePath ?? string.Empty;
        }

        IsCustomEditorOpen = true;
    }

    private void CreateCustomPlant()
    {
        EditingCustomPlantId = null;
        EditingSystemPlantId = null;
        IsEditingSystemDeleted = false;

        if (IsCustomEditorOpen)
        {
            IsCustomEditorOpen = false;
            System.Windows.Application.Current.Dispatcher.BeginInvoke(
                System.Windows.Threading.DispatcherPriority.Background,
                () => ShowNewCustomPlantEditor());
        }
        else
        {
            ShowNewCustomPlantEditor();
        }
    }

    private void ShowNewCustomPlantEditor()
    {
        CustomEditName = "自定义植物";
        CustomEditPersonality = "温柔";
        CustomEditPrompt = "你是一株温柔的植物伙伴，介绍自己的习性和照料方式，偶尔借植物谈谈为人处世。回复简短，语气亲切。";
        CustomEditImagePath = string.Empty;
        IsCustomEditorOpen = true;
    }

    private async void SaveCustomPlant()
    {
        if (!IsCustomEditorOpen)
        {
            return;
        }

        var normalizedName = string.IsNullOrWhiteSpace(CustomEditName) ? "自定义植物" : CustomEditName.Trim();
        var normalizedPersonality = string.IsNullOrWhiteSpace(CustomEditPersonality) ? "温柔" : CustomEditPersonality.Trim();
        var normalizedPrompt = string.IsNullOrWhiteSpace(CustomEditPrompt)
            ? "你是一株温柔的植物伙伴，介绍自己的习性和照料方式，偶尔借植物谈谈为人处世。回复简短，语气亲切。"
            : CustomEditPrompt.Trim();

        try
        {
            if (EditingCustomPlantId.HasValue)
            {
                var profile = CustomPlants.FirstOrDefault(x => x.Id == EditingCustomPlantId.Value);
                if (profile is null) return;

                profile.Name = normalizedName;
                profile.Personality = normalizedPersonality;
                profile.SystemPrompt = normalizedPrompt;
                profile.ImageSourceType = "Local";
                profile.ImagePath = CustomEditImagePath;
                await customPlantService.UpdateAsync(profile);
            }
            else if (!string.IsNullOrWhiteSpace(EditingSystemPlantId))
            {
                var overrideProfile = GetOrCreateOverride(EditingSystemPlantId);
                overrideProfile.Name = normalizedName;
                overrideProfile.Personality = normalizedPersonality;
                overrideProfile.SystemPrompt = normalizedPrompt;
                overrideProfile.ImageSourceType = "Local";
                overrideProfile.ImagePath = CustomEditImagePath;
                overrideProfile.IsDeleted = false;
                SaveProfileOverrides();
                IsEditingSystemDeleted = false;
            }
            else
            {
                var profile = new CustomPlantProfile
                {
                    Name = normalizedName,
                    Personality = normalizedPersonality,
                    SystemPrompt = normalizedPrompt,
                    ImageSourceType = "Local",
                    ImagePath = CustomEditImagePath
                };

                await customPlantService.AddAsync(profile);
                EditingCustomPlantId = profile.Id;
            }

            await LoadCustomPlantsAsync();
            OnPropertyChanged(nameof(PlantName));
            OnPropertyChanged(nameof(PlantPersonality));
            UpdatePlantImageSource();
            IsCustomEditorOpen = false;
            OnPropertyChanged(nameof(IsEditingSystemDeleted));
            StatusMessage = "已保存植物设置。";
        }
        catch
        {
            StatusMessage = "自定义植物保存失败，请确认数据库已迁移。";
        }
    }

    private async void DeleteCustomPlant()
    {
        if (!EditingCustomPlantId.HasValue && string.IsNullOrWhiteSpace(EditingSystemPlantId))
        {
            return;
        }

        try
        {
            if (EditingCustomPlantId.HasValue)
            {
                var deletedId = EditingCustomPlantId.Value;
                await customPlantService.DeleteAsync(deletedId);
                EditingCustomPlantId = null;

                if (ParseCustomPlantId(PlantType) == deletedId)
                {
                    var fallback = CyberPlantPresets.All.First();
                    PlantType = fallback.Id;
                    InitializeNewPlantState(fallback);
                }

                EditingSystemPlantId = null;
                IsCustomEditorOpen = false;
                await LoadCustomPlantsAsync();
                StatusMessage = "已删除自定义植物。";
            }
            else if (!string.IsNullOrWhiteSpace(EditingSystemPlantId))
            {
                var overrideProfile = GetOrCreateOverride(EditingSystemPlantId);
                overrideProfile.IsDeleted = !overrideProfile.IsDeleted;
                SaveProfileOverrides();
                IsEditingSystemDeleted = overrideProfile.IsDeleted;

                if (overrideProfile.IsDeleted && string.Equals(PlantType, EditingSystemPlantId, StringComparison.OrdinalIgnoreCase))
                {
                    var fallback = CyberPlantPresets.All.First();
                    PlantType = fallback.Id;
                    InitializeNewPlantState(fallback);
                }

                await LoadCustomPlantsAsync();
                StatusMessage = overrideProfile.IsDeleted ? "已隐藏系统植物。" : "已恢复系统植物。";
            }
        }
        catch
        {
            StatusMessage = "自定义植物删除失败，请确认数据库已迁移。";
        }
    }

    private void ResetSystemPlant()
    {
        if (string.IsNullOrWhiteSpace(EditingSystemPlantId)) return;

        profileOverrides.Remove(EditingSystemPlantId);
        SaveProfileOverrides();

        var preset = CyberPlantPresets.All.FirstOrDefault(p => p.Id == EditingSystemPlantId);
        if (preset is not null)
        {
            CustomEditName = preset.Name;
            CustomEditPersonality = preset.Personality;
            CustomEditPrompt = preset.SystemPrompt;
            CustomEditImagePath = string.Empty;
        }

        IsEditingSystemDeleted = false;
        EditingSystemPlantId = null;
        IsCustomEditorOpen = false;
        BuildPlantItems();

        if (preset is not null && string.Equals(PlantType, preset.Id, StringComparison.OrdinalIgnoreCase))
        {
            InitializeNewPlantState(preset);
            OnPropertyChanged(nameof(PlantName));
            OnPropertyChanged(nameof(PlantPersonality));
            UpdatePlantImageSource();
        }

        UpdateStatus();
        StatusMessage = "已恢复初始设定。";
    }

    private void LoadProfileOverrides()
    {
        try
        {
            if (!File.Exists(PlantProfileOverridesPath))
            {
                profileOverrides = new Dictionary<string, PlantProfileOverride>(StringComparer.OrdinalIgnoreCase);
                return;
            }

            var json = File.ReadAllText(PlantProfileOverridesPath);
            var data = JsonSerializer.Deserialize<List<PlantProfileOverride>>(json) ?? [];
            profileOverrides = data
                .Where(x => !string.IsNullOrWhiteSpace(x.PlantId))
                .ToDictionary(x => x.PlantId, x => x, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            profileOverrides = new Dictionary<string, PlantProfileOverride>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private void SaveProfileOverrides()
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var data = profileOverrides.Values.ToList();
            var json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(PlantProfileOverridesPath, json);
        }
        catch
        {
        }
    }

    private PlantProfileOverride? GetOverride(string plantId)
    {
        if (profileOverrides.TryGetValue(plantId, out var value))
        {
            return value;
        }

        return null;
    }

    private PlantProfileOverride GetOrCreateOverride(string plantId)
    {
        if (profileOverrides.TryGetValue(plantId, out var value))
        {
            return value;
        }

        var created = new PlantProfileOverride { PlantId = plantId };
        profileOverrides[plantId] = created;
        return created;
    }

    private CyberPlantType? CurrentPreset => CyberPlantPresets.All.FirstOrDefault(p => p.Id == PlantType);

    private CustomPlantProfile? CurrentCustomProfile
    {
        get
        {
            if (!IsCustomPlant) return null;
            var id = ParseCustomPlantId(PlantType);
            return id is null ? null : CustomPlants.FirstOrDefault(x => x.Id == id.Value);
        }
    }

    private static string BuildCustomPlantTypeId(int id) => CustomPlantPrefix + id;

    private static int? ParseCustomPlantId(string plantType)
    {
        if (!plantType.StartsWith(CustomPlantPrefix, StringComparison.OrdinalIgnoreCase)) return null;
        var raw = plantType[CustomPlantPrefix.Length..];
        return int.TryParse(raw, out var id) ? id : null;
    }

    private void ApplyPresetCareProfile(CyberPlantType preset)
    {
        MaxWater = preset.MaxWater;
        MaxNutrition = preset.MaxNutrition;
        MaxSun = preset.MaxSun;
        NeedWater = preset.NeedWater;
        NeedNutrition = preset.NeedNutrition;
        NeedSun = preset.NeedSun;
        WaterValue = Math.Clamp(preset.DefaultWater, 0, MaxWater);
        NutritionValue = Math.Clamp(preset.DefaultNutrition, 0, MaxNutrition);
        SunValue = Math.Clamp(preset.DefaultSun, 0, MaxSun);
    }

    private void InitializeCustomDefaults()
    {
        if (string.IsNullOrWhiteSpace(plant.CustomSpecies))
        {
            plant.CustomSpecies = "我的植物";
            OnPropertyChanged(nameof(PlantSpecies));
        }

        if (string.IsNullOrWhiteSpace(plant.CustomEmoji))
        {
            plant.CustomEmoji = "🌱";
            OnPropertyChanged(nameof(PlantEmoji));
        }

        if (string.IsNullOrWhiteSpace(plant.CustomSystemPrompt))
        {
            plant.CustomSystemPrompt = "你是一株温柔的植物伙伴，介绍自己的习性和照料方式，偶尔借植物谈谈为人处世。回复简短，语气亲切。";
            OnPropertyChanged(nameof(CustomSystemPrompt));
        }

        if (string.IsNullOrWhiteSpace(plant.CustomImagePath))
        {
            plant.CustomImagePath = string.Empty;
            OnPropertyChanged(nameof(CustomImagePath));
            OnPropertyChanged(nameof(PlantImageHint));
        }
    }

    private void EnsurePlantDefaults(CyberPlant target)
    {
        var preset = CyberPlantPresets.All.FirstOrDefault(p => p.Id == target.PlantType)
            ?? CyberPlantPresets.All.First(p => p.Id == "cactus");

        if (target.MaxWater <= 0) target.MaxWater = preset.MaxWater;
        if (target.MaxNutrition <= 0) target.MaxNutrition = preset.MaxNutrition;
        if (target.MaxSun <= 0) target.MaxSun = preset.MaxSun;
        if (target.NeedWater <= 0) target.NeedWater = preset.NeedWater;
        if (target.NeedNutrition <= 0) target.NeedNutrition = preset.NeedNutrition;
        if (target.NeedSun <= 0) target.NeedSun = preset.NeedSun;
        if (target.WaterValue <= 0) target.WaterValue = preset.DefaultWater;
        if (target.NutritionValue <= 0) target.NutritionValue = preset.DefaultNutrition;
        if (target.SunValue <= 0) target.SunValue = preset.DefaultSun;
        if (target.LastCareDecayAt == default) target.LastCareDecayAt = DateTime.Today;
        if (target.LastChatClearedAt == default) target.LastChatClearedAt = DateTime.Today;

        if (!target.PlantType.StartsWith(CustomPlantPrefix, StringComparison.OrdinalIgnoreCase) && string.IsNullOrWhiteSpace(target.PlantName))
        {
            target.PlantName = preset.Name;
        }

        if (target.PlantType.StartsWith(CustomPlantPrefix, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(target.CustomSpecies)) target.CustomSpecies = "我的植物";
            if (string.IsNullOrWhiteSpace(target.CustomEmoji)) target.CustomEmoji = "🌱";
            if (string.IsNullOrWhiteSpace(target.CustomSystemPrompt))
            {
                target.CustomSystemPrompt = "你是一株温柔的植物伙伴，介绍自己的习性和照料方式，偶尔借植物谈谈为人处世。回复简短，语气亲切。";
            }
            if (string.IsNullOrWhiteSpace(target.CustomImagePath)) target.CustomImagePath = string.Empty;
        }
    }

    private void NotifyCareStatusChanged()
    {
        OnPropertyChanged(nameof(WaterStatusDisplay));
        OnPropertyChanged(nameof(NutritionStatusDisplay));
        OnPropertyChanged(nameof(SunStatusDisplay));
        UpdateCareLock();
        UpdateGrowthFromCare();
        UpdateStatus();
    }

    private void UpdateCareLock()
    {
        if (WaterValue == 0 && NutritionValue == 0 && SunValue == 0)
        {
            plant.IsCareLocked = true;
            return;
        }

        if (plant.IsCareLocked && WaterValue >= NeedWater && NutritionValue >= NeedNutrition && SunValue >= NeedSun)
        {
            plant.IsCareLocked = false;
        }
    }

    private void UpdateGrowthFromCare()
    {
        var avgRatio = (
            WaterValue / (double)MaxWater +
            NutritionValue / (double)MaxNutrition +
            SunValue / (double)MaxSun) / 3.0;

        var targetLevel = Math.Clamp((int)Math.Round(avgRatio * 10), 0, 10);
        if (GrowthLevel != targetLevel)
        {
            GrowthLevel = targetLevel;
        }
    }

    private void UpdateMoodFromCare()
    {
        var needsWater = WaterValue < NeedWater;
        var needsNutrition = NutritionValue < NeedNutrition;
        var needsSun = SunValue < NeedSun;

        if (needsWater || needsNutrition || needsSun)
        {
            if (needsWater) Mood = "有点渴了……";
            else if (needsNutrition) Mood = "有点虚弱……";
            else Mood = "有点没精神……";
            return;
        }

        var minRatio = new[]
        {
            WaterValue / (double)MaxWater,
            NutritionValue / (double)MaxNutrition,
            SunValue / (double)MaxSun
        }.Min();

        Mood = minRatio switch
        {
            >= 0.8 => "超级开心",
            >= 0.6 => "很开心",
            >= 0.4 => "还不错",
            _ => "还行"
        };
    }

    private string BuildCareHint()
    {
        var hints = new List<string>();
        if (WaterValue < NeedWater) hints.Add("需要浇水");
        if (NutritionValue < NeedNutrition) hints.Add("需要施肥");
        if (SunValue < NeedSun) hints.Add("需要晒太阳");

        if (hints.Count == 0) return string.Empty;
        return " （" + string.Join("、", hints) + "）";
    }

    private string BuildPlantSystemPrompt(CyberPlantType? preset)
    {
        var customPrompt = CurrentCustomProfile?.SystemPrompt;
        var overridePrompt = GetOverride(PlantType)?.SystemPrompt;
        var basePrompt = IsCustomPlant && !string.IsNullOrWhiteSpace(customPrompt)
            ? customPrompt.Trim()
            : !string.IsNullOrWhiteSpace(overridePrompt)
                ? overridePrompt!.Trim()
                : preset?.SystemPrompt ?? $"你是一株名叫{PlantName}的植物，用温暖的语气和主人聊天。";

        var identity = $"你的名字是{PlantName}。";
        var careStatus = $"当前状态：水分 {WaterValue}/{MaxWater}(需求 {NeedWater})，营养 {NutritionValue}/{MaxNutrition}(需求 {NeedNutrition})，阳光 {SunValue}/{MaxSun}(需求 {NeedSun})。";

        return basePrompt + "\n" + identity + "\n" + careStatus +
               "如果低于需求值，请提醒主人进行对应的照料。" +
               "回复通常控制在90字内，但允许小概率输出100到160字的长回复。";
    }
}
