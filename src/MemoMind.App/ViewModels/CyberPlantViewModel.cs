using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Windows.Input;
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
    private CyberPlant plant;
    private string inputText = string.Empty;
    private string statusMessage = string.Empty;
    private bool isAiMode;
    private bool isSending;
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoMind");
    private static readonly string PlantDataPath = Path.Combine(DataFolder, "cyber_plant.json");
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    public CyberPlantViewModel()
    {
        chatService = App.Services.GetService<IChatService>();
        plant = LoadPlant();

        PlantTypes = new ObservableCollection<CyberPlantType>(CyberPlantPresets.All);
        Messages = new ObservableCollection<PlantMessage>(plant.Messages);

        var settings = App.Services.GetRequiredService<IAppSettingsStore>().LoadAsync().GetAwaiter().GetResult();
        ApplySettings(settings);

        SendCommand = new RelayCommand(_ => Send(), _ => !string.IsNullOrWhiteSpace(InputText) && !IsSending);
        WaterCommand = new RelayCommand(_ => Water());
        SelectPlantCommand = new RelayCommand(param => SelectPlant(param?.ToString()));

        UpdateStatus();
    }

    public ObservableCollection<CyberPlantType> PlantTypes { get; }
    public ObservableCollection<PlantMessage> Messages { get; }

    public string PlantType
    {
        get => plant.PlantType;
        set { plant.PlantType = value; OnPropertyChanged(); OnPropertyChanged(nameof(IsCustomPlant)); }
    }

    public string PlantName
    {
        get => plant.PlantName;
        set { plant.PlantName = value; OnPropertyChanged(); }
    }

    public string PlantEmoji => CyberPlantPresets.All
        .FirstOrDefault(p => p.Id == PlantType)?.Emoji ?? "🌱";

    public string PlantPersonality => CyberPlantPresets.All
        .FirstOrDefault(p => p.Id == PlantType)?.Personality ?? "独特";

    public int GrowthLevel
    {
        get => plant.GrowthLevel;
        set { plant.GrowthLevel = value; OnPropertyChanged(); OnPropertyChanged(nameof(GrowthDisplay)); }
    }

    public string GrowthDisplay => new string('⭐', Math.Clamp(GrowthLevel, 1, 10));

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

    public string LastWateredDisplay => LastWateredAt == default
        ? "还没浇过水"
        : $"上次浇水：{LastWateredAt:MM-dd HH:mm}";

    public bool IsCustomPlant => PlantType == "custom";

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
    public ICommand SelectPlantCommand { get; }

    public void ApplySettings(UserSettings settings)
    {
        IsAiMode = settings.EnableAi && !string.IsNullOrWhiteSpace(settings.ApiKey);
        UpdateStatus();
    }

    private void SelectPlant(string? plantTypeId)
    {
        if (string.IsNullOrWhiteSpace(plantTypeId)) return;

        var preset = CyberPlantPresets.All.FirstOrDefault(p => p.Id == plantTypeId);
        if (preset is null) return;

        PlantType = preset.Id;
        if (preset.Id != "custom")
        {
            PlantName = preset.Name;
        }
        else
        {
            PlantName = "我的植物";
        }

        SavePlant();
        UpdateStatus();
        Messages.Clear();
        Messages.Add(new PlantMessage
        {
            Sender = PlantName,
            Content = preset.Id != "custom"
                ? $"你好！我是一株{PlantName}，{PlantPersonality}。有什么想聊的吗？"
                : $"你好！我是{PlantName}，以后请多关照！"
        });
    }

    private async void Send()
    {
        var text = InputText.Trim();
        if (string.IsNullOrWhiteSpace(text) || IsSending) return;

        Messages.Add(new PlantMessage { Sender = "我", Content = text, Time = DateTime.Now });
        InputText = string.Empty;
        IsSending = true;

        string reply;
        if (IsAiMode && chatService is not null)
        {
            var preset = CyberPlantPresets.All.FirstOrDefault(p => p.Id == PlantType);
            var systemPrompt = preset?.SystemPrompt ?? $"你是一株名叫{PlantName}的植物，用温暖的语气和主人聊天。回复简短，不超过60字。";

            try
            {
                reply = await chatService.SendAsync(systemPrompt, text);
            }
            catch
            {
                reply = CyberPlantPresets.GetOfflinePlantReply(PlantType, text);
            }
        }
        else
        {
            await Task.Delay(300 + Random.Shared.Next(700));
            reply = CyberPlantPresets.GetOfflinePlantReply(PlantType, text);
        }

        Messages.Add(new PlantMessage { Sender = PlantName, Content = reply, Time = DateTime.Now });
        plant.Messages = Messages.TakeLast(50).ToList();
        IsSending = false;
        SavePlant();
    }

    private void Water()
    {
        GrowthLevel = Math.Min(GrowthLevel + 1, 10);
        LastWateredAt = DateTime.Now;
        Mood = GrowthLevel switch
        {
            >= 8 => "超级开心",
            >= 5 => "很开心",
            >= 3 => "还不错",
            _ => "还行"
        };

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

    private void UpdateStatus()
    {
        if (LastWateredAt != default && (DateTime.Now - LastWateredAt).TotalHours > 48)
        {
            Mood = "有点渴了……";
        }

        StatusMessage = IsAiMode
            ? $"🌱 AI 模式已开启 — {PlantName}会用自己的性格和你聊天"
            : $"🌱 离线模式 — {PlantName}仍然可以陪你聊天";
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
            plant.Messages = Messages.TakeLast(50).ToList();
            var json = JsonSerializer.Serialize(plant, JsonOptions);
            File.WriteAllText(PlantDataPath, json);
        }
        catch { }
    }
}
