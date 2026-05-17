using System.Text.Json;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;

namespace MemoMind.Infrastructure.Services;

public class AgentToolExecutor : IAgentToolExecutor
{
    private readonly ITaskService taskService;
    private readonly ICustomPlantService customPlantService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoMind");
    private static readonly string PlantDataPath = Path.Combine(DataFolder, "cyber_plant.json");
    private static readonly string PlantOverridesPath = Path.Combine(DataFolder, "plant_profiles.json");

    private static readonly Dictionary<string, string> SystemPlantNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cactus"] = "仙人掌",
        ["sunflower"] = "向日葵",
        ["mint"] = "薄荷",
        ["fern"] = "蕨类",
        ["bamboo"] = "竹子",
    };

    private static readonly Dictionary<string, string> SystemPlantEmojis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cactus"] = "🌵",
        ["sunflower"] = "🌻",
        ["mint"] = "🌿",
        ["fern"] = "🍃",
        ["bamboo"] = "🎋",
    };

    private const string CustomPlantPrefix = "custom:";
    private const int CareIncreaseAmount = 3;

    private record PlantOverrideDto(string PlantId, string? Name, string? Personality, string? SystemPrompt, bool IsDeleted);

    public AgentToolExecutor(ITaskService taskService, ICustomPlantService customPlantService)
    {
        this.taskService = taskService;
        this.customPlantService = customPlantService;
    }

    public async Task<string> ExecuteToolAsync(string functionName, string argumentsJson)
    {
        return functionName switch
        {
            "create_task" => await CreateTaskAsync(argumentsJson),
            "list_tasks" => await ListTasksAsync(),
            "update_task" => await UpdateTaskAsync(argumentsJson),
            "delete_task" => await DeleteTaskAsync(argumentsJson),
            "care_plant" => await CarePlantAsync(argumentsJson),
            "check_plant_status" => await CheckPlantStatusAsync(argumentsJson),
            "switch_plant" => await SwitchPlantAsync(argumentsJson),
            "list_plants" => await ListPlantsAsync(),
            _ => $"未知操作：{functionName}"
        };
    }

    // ===================== Task Tools =====================

    private async Task<string> CreateTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title") ?? "未命名任务";
            var description = GetStringProperty(root, "description") ?? "";
            var isUrgent = GetBoolProperty(root, "is_urgent");

            DateTime? startDate = null;
            if (root.TryGetProperty("start_date", out var sd) && sd.ValueKind == JsonValueKind.String)
            {
                var ss = sd.GetString();
                if (!string.IsNullOrWhiteSpace(ss) && DateTime.TryParse(ss, out var parsedStart))
                {
                    startDate = parsedStart;
                }
            }

            DateTime? dueDate = null;
            if (root.TryGetProperty("due_date", out var dd) && dd.ValueKind == JsonValueKind.String)
            {
                var ds = dd.GetString();
                if (!string.IsNullOrWhiteSpace(ds))
                {
                    if (DateTime.TryParse(ds, out var parsed))
                    {
                        dueDate = parsed;
                    }
                }
            }

            var estimatedHours = GetIntProperty(root, "estimated_hours");
            var estimatedMinutes = GetIntProperty(root, "estimated_minutes");

            var task = new TaskItem
            {
                Title = title,
                Description = description,
                IsUrgent = isUrgent,
                StartDate = startDate,
                DueDate = dueDate,
                EstimatedHours = estimatedHours > 0 ? estimatedHours : 0,
                EstimatedMinutes = estimatedMinutes > 0 ? estimatedMinutes : 0,
                Status = "Todo",
                CreatedAt = DateTime.Now,
                SourceType = "Agent"
            };

            await taskService.AddAsync(task);

            var parts = new List<string>();
            if (isUrgent) parts.Add("紧急");
            if (startDate.HasValue) parts.Add($"开始：{startDate:yyyy-MM-dd HH:mm}");
            if (dueDate.HasValue) parts.Add($"截止：{dueDate:yyyy-MM-dd HH:mm}");
            if (estimatedHours > 0 || estimatedMinutes > 0) parts.Add($"预计 {estimatedHours}h{estimatedMinutes:D2}m");

            var extraInfo = parts.Count > 0 ? "（" + string.Join("，", parts) + "）" : "";
            return $"任务「{title}」已创建成功{extraInfo}。";
        }
        catch (Exception ex)
        {
            return $"创建任务失败：{ex.Message}";
        }
    }

    private async Task<string> ListTasksAsync()
    {
        try
        {
            var tasks = await taskService.GetAllAsync();
            if (tasks.Count == 0)
            {
                return "当前没有任何任务。";
            }

            var lines = new List<string> { $"当前共有 {tasks.Count} 个任务：" };
            foreach (var t in tasks)
            {
                var status = t.Status switch
                {
                    "Todo" => "待办",
                    "Doing" => "进行中",
                    "Done" => "已完成",
                    _ => t.Status
                };
                var start = t.StartDate.HasValue ? $" 开始：{t.StartDate:MM-dd HH:mm}" : "";
                var due = t.DueDate.HasValue ? $" 截止：{t.DueDate:MM-dd HH:mm}" : "";
                var urgent = t.IsUrgent ? " ⚠紧急" : "";
                var estTime = (t.EstimatedHours > 0 || t.EstimatedMinutes > 0) ? $" 预计 {t.EstimatedHours}h{t.EstimatedMinutes:D2}m" : "";
                lines.Add($"  [{status}] {t.Title}{start}{due}{estTime}{urgent}");
            }

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return $"获取任务列表失败：{ex.Message}";
        }
    }

    private async Task<string> UpdateTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                return "请指定要更新的任务标题。";
            }

            var tasks = await taskService.GetAllAsync();
            var target = tasks.FirstOrDefault(t =>
                t.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
                title.Contains(t.Title, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return $"未找到标题包含「{title}」的任务。请先用 list_tasks 查看所有任务。";
            }

            var newTitle = GetStringProperty(root, "new_title");
            var newDescription = GetStringProperty(root, "description");
            var newStatus = GetStringProperty(root, "status");
            var isUrgentStr = GetStringProperty(root, "is_urgent");
            var startDateStr = GetStringProperty(root, "start_date");
            var dueDateStr = GetStringProperty(root, "due_date");
            var estimatedHours = GetIntProperty(root, "estimated_hours");
            var estimatedMinutes = GetIntProperty(root, "estimated_minutes");

            var changes = new List<string>();

            if (!string.IsNullOrWhiteSpace(newTitle) && newTitle != target.Title)
            {
                target.Title = newTitle;
                changes.Add($"标题→「{newTitle}」");
            }

            if (!string.IsNullOrWhiteSpace(newDescription) && newDescription != target.Description)
            {
                target.Description = newDescription;
                changes.Add("描述已更新");
            }

            if (!string.IsNullOrWhiteSpace(newStatus))
            {
                var normalized = newStatus switch
                {
                    "todo" or "待办" => "Todo",
                    "doing" or "进行中" => "Doing",
                    "done" or "已完成" => "Done",
                    _ => null
                };
                if (normalized is not null && normalized != target.Status)
                {
                    target.Status = normalized;
                    if (normalized == "Done")
                    {
                        target.CompletedAt = DateTime.Now;
                    }
                    changes.Add($"状态→{newStatus}");
                }
            }

            if (!string.IsNullOrWhiteSpace(isUrgentStr) && bool.TryParse(isUrgentStr, out var urgent))
            {
                if (urgent != target.IsUrgent)
                {
                    target.IsUrgent = urgent;
                    changes.Add(urgent ? "标记为紧急" : "取消紧急标记");
                }
            }

            if (!string.IsNullOrWhiteSpace(startDateStr) && DateTime.TryParse(startDateStr, out var newStart))
            {
                if (target.StartDate != newStart)
                {
                    target.StartDate = newStart;
                    changes.Add($"开始时间→{newStart:yyyy-MM-dd HH:mm}");
                }
            }

            if (!string.IsNullOrWhiteSpace(dueDateStr) && DateTime.TryParse(dueDateStr, out var newDue))
            {
                if (target.DueDate != newDue)
                {
                    target.DueDate = newDue;
                    changes.Add($"截止时间→{newDue:yyyy-MM-dd HH:mm}");
                }
            }

            if (estimatedHours > 0 || estimatedMinutes >= 0)
            {
                var oldHours = target.EstimatedHours;
                var oldMinutes = target.EstimatedMinutes;
                if (estimatedHours != oldHours || estimatedMinutes != oldMinutes)
                {
                    if (estimatedHours > 0) target.EstimatedHours = estimatedHours;
                    if (estimatedMinutes > 0) target.EstimatedMinutes = estimatedMinutes;
                    changes.Add($"预计时长→{target.EstimatedHours}h{target.EstimatedMinutes:D2}m");
                }
            }

            if (changes.Count == 0)
            {
                return $"任务「{target.Title}」没有需要更新的内容。";
            }

            await taskService.UpdateAsync(target);
            return $"任务「{target.Title}」已更新：{string.Join("，", changes)}。";
        }
        catch (Exception ex)
        {
            return $"更新任务失败：{ex.Message}";
        }
    }

    private async Task<string> DeleteTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                return "请指定要删除的任务标题。";
            }

            var tasks = await taskService.GetAllAsync();
            var target = tasks.FirstOrDefault(t =>
                t.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
                title.Contains(t.Title, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                return $"未找到标题包含「{title}」的任务。";
            }

            await taskService.DeleteAsync(target.Id);
            return $"任务「{target.Title}」已删除。";
        }
        catch (Exception ex)
        {
            return $"删除任务失败：{ex.Message}";
        }
    }

    // ===================== Plant Tools =====================

    private async Task<string> CarePlantAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var action = GetStringProperty(root, "action")?.ToLowerInvariant();
            if (action is not ("water" or "fertilize" or "sunbathe"))
            {
                return "请指定照料动作：water（浇水）、fertilize（施肥）或 sunbathe（晒太阳）。";
            }

            var plantTypeHint = GetStringProperty(root, "plant_type");

            var plant = LoadPlant();

            // Resolve target plant type
            string targetType;
            if (!string.IsNullOrWhiteSpace(plantTypeHint))
            {
                var resolved = ResolvePlantType(plantTypeHint);
                if (resolved is null)
                {
                    return $"未找到名为「{plantTypeHint}」的植物。可用的植物有：{string.Join("、", SystemPlantNames.Values)}，以及你的自定义植物。";
                }
                targetType = resolved;
            }
            else
            {
                targetType = plant.PlantType;
            }

            // If caring for a different plant than current, switch to it first
            if (!string.Equals(targetType, plant.PlantType, StringComparison.OrdinalIgnoreCase))
            {
                PersistCurrentState(plant);
                if (!TryRestorePlantState(plant, targetType))
                {
                    InitializePlantState(plant, targetType);
                }
                plant.PlantType = targetType;
            }

            ApplyDailyDecayIfNeeded(plant);

            var displayName = GetPlantDisplayName(targetType);

            string message;
            switch (action)
            {
                case "water":
                    plant.WaterValue = Math.Min(plant.WaterValue + CareIncreaseAmount, plant.MaxWater);
                    plant.LastWateredAt = DateTime.Now;
                    message = plant.GrowthLevel switch
                    {
                        10 => $"咕嘟咕嘟～{displayName}已经长得很好啦！谢谢你一直以来的照顾。🌿✨",
                        >= 7 => $"咕嘟咕嘟～{displayName}感觉自己又强壮了不少！",
                        >= 4 => $"谢谢你给{displayName}浇水！它会努力长大的～",
                        _ => $"{displayName}喝到水了，真舒服！会好好长大的！"
                    };
                    break;
                case "fertilize":
                    plant.NutritionValue = Math.Min(plant.NutritionValue + CareIncreaseAmount, plant.MaxNutrition);
                    plant.LastFertilizedAt = DateTime.Now;
                    message = plant.GrowthLevel switch
                    {
                        10 => $"养分充足，{displayName}状态拉满！谢谢你的细心照料！",
                        >= 7 => $"营养补上了，{displayName}感觉更有劲啦！",
                        >= 4 => $"施肥真及时，{displayName}会慢慢变强的。",
                        _ => $"谢谢你给{displayName}施肥，它会努力长大的！"
                    };
                    break;
                case "sunbathe":
                    plant.SunValue = Math.Min(plant.SunValue + CareIncreaseAmount, plant.MaxSun);
                    plant.LastSunbathedAt = DateTime.Now;
                    message = plant.GrowthLevel switch
                    {
                        10 => $"阳光满满，{displayName}今天简直在发光！",
                        >= 7 => $"晒得刚刚好，{displayName}整株都精神了！",
                        >= 4 => $"谢谢你带{displayName}晒太阳，舒服～",
                        _ => $"阳光真好，{displayName}会好好吸收的！"
                    };
                    break;
                default:
                    return $"未知的照料动作：{action}";
            }

            UpdateGrowthAndMood(plant);

            var statusReport = $"当前状态：水分 {plant.WaterValue}/{plant.MaxWater}，营养 {plant.NutritionValue}/{plant.MaxNutrition}，阳光 {plant.SunValue}/{plant.MaxSun}，成长 ⭐×{plant.GrowthLevel}，心情「{plant.Mood}」";

            PersistCurrentState(plant);
            SavePlant(plant);

            return $"{message}\n{statusReport}";
        }
        catch (Exception ex)
        {
            return $"照料植物失败：{ex.Message}";
        }
    }

    private async Task<string> CheckPlantStatusAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var plantTypeHint = GetStringProperty(root, "plant_type");

            var plant = LoadPlant();

            ApplyDailyDecayIfNeeded(plant);
            UpdateGrowthAndMood(plant);

            string targetType;
            CyberPlant? targetPlant;

            if (!string.IsNullOrWhiteSpace(plantTypeHint))
            {
                var resolved = ResolvePlantType(plantTypeHint);
                if (resolved is null)
                {
                    return $"未找到名为「{plantTypeHint}」的植物。";
                }

                if (string.Equals(resolved, plant.PlantType, StringComparison.OrdinalIgnoreCase))
                {
                    targetType = resolved;
                    targetPlant = plant;
                }
                else if (plant.PlantStates.TryGetValue(resolved, out var state))
                {
                    targetType = resolved;
                    targetPlant = StateToPlant(state, resolved);
                }
                else
                {
                    targetType = resolved;
                    targetPlant = CreateDefaultPlant(resolved);
                }
            }
            else
            {
                targetType = plant.PlantType;
                targetPlant = plant;
            }

            var displayName = GetPlantDisplayName(targetType);
            var isCurrent = string.Equals(targetType, plant.PlantType, StringComparison.OrdinalIgnoreCase);

            var needs = new List<string>();
            if (targetPlant.WaterValue < targetPlant.NeedWater) needs.Add("需要浇水");
            if (targetPlant.NutritionValue < targetPlant.NeedNutrition) needs.Add("需要施肥");
            if (targetPlant.SunValue < targetPlant.NeedSun) needs.Add("需要晒太阳");
            var needsStr = needs.Count > 0 ? string.Join("、", needs) : "一切良好";
            var careLocked = targetPlant.IsCareLocked ? " ⚠植物已枯萎，需要紧急照料！" : "";
            var currentTag = isCurrent ? "【当前】" : "";

            var lines = new List<string>
            {
                $"{currentTag}{displayName} 的状态报告：",
                $"  水分：{targetPlant.WaterValue}/{targetPlant.MaxWater}（需求 {targetPlant.NeedWater}）",
                $"  营养：{targetPlant.NutritionValue}/{targetPlant.MaxNutrition}（需求 {targetPlant.NeedNutrition}）",
                $"  阳光：{targetPlant.SunValue}/{targetPlant.MaxSun}（需求 {targetPlant.NeedSun}）",
                $"  成长等级：{'⭐' * Math.Clamp(targetPlant.GrowthLevel, 0, 10)}（{targetPlant.GrowthLevel}/10）",
                $"  心情：{targetPlant.Mood}",
                $"  照料建议：{needsStr}{careLocked}"
            };

            if (targetPlant.LastWateredAt != default)
                lines.Add($"  上次浇水：{targetPlant.LastWateredAt:MM-dd HH:mm}");
            if (targetPlant.LastFertilizedAt != default)
                lines.Add($"  上次施肥：{targetPlant.LastFertilizedAt:MM-dd HH:mm}");
            if (targetPlant.LastSunbathedAt != default)
                lines.Add($"  上次晒太阳：{targetPlant.LastSunbathedAt:MM-dd HH:mm}");

            return string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return $"查看植物状态失败：{ex.Message}";
        }
    }

    private async Task<string> SwitchPlantAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var plantTypeHint = GetStringProperty(root, "plant_type");
            if (string.IsNullOrWhiteSpace(plantTypeHint))
            {
                return "请指定要切换到的植物名称或类型。例如：'切换到仙人掌' 或 '切换到cactus'。";
            }

            var resolved = ResolvePlantType(plantTypeHint);
            if (resolved is null)
            {
                var available = await GetAllAvailablePlantNames();
                return $"未找到名为「{plantTypeHint}」的植物。可用的植物：{string.Join("、", available)}";
            }

            var plant = LoadPlant();

            if (string.Equals(resolved, plant.PlantType, StringComparison.OrdinalIgnoreCase))
            {
                var currentName = GetPlantDisplayName(plant.PlantType);
                return $"当前已经是{currentName}了，无需切换。";
            }

            // Persist current state before switching
            PersistCurrentState(plant);

            // Switch to target
            if (!TryRestorePlantState(plant, resolved))
            {
                InitializePlantState(plant, resolved);
            }
            plant.PlantType = resolved;

            ApplyDailyDecayIfNeeded(plant);
            UpdateGrowthAndMood(plant);

            PersistCurrentState(plant);
            SavePlant(plant);

            var newName = GetPlantDisplayName(resolved);
            return $"已切换到{newName}。当前状态：水分 {plant.WaterValue}/{plant.MaxWater}，营养 {plant.NutritionValue}/{plant.MaxNutrition}，阳光 {plant.SunValue}/{plant.MaxSun}，成长 ⭐×{plant.GrowthLevel}，心情「{plant.Mood}」。";
        }
        catch (Exception ex)
        {
            return $"切换植物失败：{ex.Message}";
        }
    }

    private async Task<string> ListPlantsAsync()
    {
        try
        {
            var plant = LoadPlant();
            var currentType = plant.PlantType;

            var customPlants = await customPlantService.GetAllAsync();
            var overrides = LoadProfileOverrides();

            var lines = new List<string>();

            foreach (var (typeId, name) in SystemPlantNames)
            {
                var displayName = name;
                var emoji = SystemPlantEmojis.GetValueOrDefault(typeId, "🌱");

                if (overrides.TryGetValue(typeId, out var ov) && ov.IsDeleted)
                    continue;

                if (overrides.TryGetValue(typeId, out var o) && !string.IsNullOrWhiteSpace(o.Name))
                    displayName = o.Name;

                var marker = string.Equals(typeId, currentType, StringComparison.OrdinalIgnoreCase) ? " ← 当前" : "";
                lines.Add($"  {emoji} {displayName}（{typeId}）{marker}");
            }

            foreach (var cp in customPlants)
            {
                var typeId = CustomPlantPrefix + cp.Id;
                var marker = string.Equals(typeId, currentType, StringComparison.OrdinalIgnoreCase) ? " ← 当前" : "";
                lines.Add($"  🌱 {cp.Name}（自定义·{cp.Personality}）{marker}");
            }

            var header = $"你共有 {lines.Count} 株植物，当前选中的是 {GetPlantDisplayName(currentType)}：";

            return header + "\n" + string.Join("\n", lines);
        }
        catch (Exception ex)
        {
            return $"获取植物列表失败：{ex.Message}";
        }
    }

    // ===================== Plant Helpers =====================

    private static CyberPlant LoadPlant()
    {
        try
        {
            if (File.Exists(PlantDataPath))
            {
                var json = File.ReadAllText(PlantDataPath);
                var existing = JsonSerializer.Deserialize<CyberPlant>(json, JsonOptions);
                if (existing is not null) return existing;
            }
        }
        catch { }

        // No plant file exists yet — create a default plant
        var plant = new CyberPlant
        {
            PlantType = "cactus",
            PlantName = "小仙人掌",
            GrowthLevel = 5,
            Mood = "还不错",
            LastWateredAt = DateTime.Now,
            LastFertilizedAt = DateTime.Now,
            LastSunbathedAt = DateTime.Now,
            LastCareDecayAt = DateTime.Today,
            LastChatClearedAt = DateTime.Today,
            WaterValue = 7,
            NutritionValue = 5,
            SunValue = 7,
            MaxWater = 14,
            MaxNutrition = 10,
            MaxSun = 14,
            NeedWater = 5,
            NeedNutrition = 4,
            NeedSun = 6,
            IsCareLocked = false,
            CreatedAt = DateTime.Now,
            Messages = [],
            PlantStates = new Dictionary<string, PlantCareState>()
        };
        SavePlant(plant);
        return plant;
    }

    private static void SavePlant(CyberPlant plant)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var json = JsonSerializer.Serialize(plant, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(PlantDataPath, json);
        }
        catch { }
    }

    private static Dictionary<string, PlantOverrideDto> LoadProfileOverrides()
    {
        try
        {
            if (!File.Exists(PlantOverridesPath))
                return new(StringComparer.OrdinalIgnoreCase);

            var json = File.ReadAllText(PlantOverridesPath);
            var data = JsonSerializer.Deserialize<List<PlantOverrideDto>>(json, JsonOptions) ?? [];
            return data
                .Where(x => !string.IsNullOrWhiteSpace(x.PlantId))
                .ToDictionary(x => x.PlantId, x => x, StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private string GetPlantDisplayName(string plantType)
    {
        if (plantType.StartsWith(CustomPlantPrefix, StringComparison.OrdinalIgnoreCase))
        {
            var raw = plantType[CustomPlantPrefix.Length..];
            if (int.TryParse(raw, out var id))
            {
                var custom = customPlantService.GetAllAsync().GetAwaiter().GetResult()
                    .FirstOrDefault(x => x.Id == id);
                if (custom is not null) return custom.Name;
            }
            return "自定义植物";
        }

        var overrides = LoadProfileOverrides();
        if (overrides.TryGetValue(plantType, out var ov) && !string.IsNullOrWhiteSpace(ov.Name))
            return ov.Name;

        return SystemPlantNames.GetValueOrDefault(plantType, plantType);
    }

    private string? ResolvePlantType(string hint)
    {
        // Try exact match on type ID
        if (SystemPlantNames.ContainsKey(hint))
            return hint;

        // Try exact match on Chinese name
        foreach (var (typeId, name) in SystemPlantNames)
        {
            if (string.Equals(name, hint, StringComparison.OrdinalIgnoreCase))
                return typeId;
        }

        // Try partial match on type ID
        foreach (var typeId in SystemPlantNames.Keys)
        {
            if (typeId.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return typeId;
        }

        // Try partial match on Chinese name
        foreach (var (typeId, name) in SystemPlantNames)
        {
            if (name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                hint.Contains(name, StringComparison.OrdinalIgnoreCase))
                return typeId;
        }

        // Check profile overrides for custom names
        var overrides = LoadProfileOverrides();
        foreach (var (typeId, ov) in overrides)
        {
            if (!string.IsNullOrWhiteSpace(ov.Name) &&
                (ov.Name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                 hint.Contains(ov.Name, StringComparison.OrdinalIgnoreCase)))
                return typeId;
        }

        // Check custom plants
        var customPlants = customPlantService.GetAllAsync().GetAwaiter().GetResult();
        foreach (var cp in customPlants)
        {
            if (cp.Name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                hint.Contains(cp.Name, StringComparison.OrdinalIgnoreCase))
                return CustomPlantPrefix + cp.Id;
        }

        return null;
    }

    private async Task<List<string>> GetAllAvailablePlantNames()
    {
        var names = new List<string>();
        var overrides = LoadProfileOverrides();

        foreach (var (typeId, name) in SystemPlantNames)
        {
            if (overrides.TryGetValue(typeId, out var ov) && ov.IsDeleted)
                continue;
            names.Add(overrides.TryGetValue(typeId, out var o) && !string.IsNullOrWhiteSpace(o.Name)
                ? o.Name
                : name);
        }

        var customPlants = await customPlantService.GetAllAsync();
        foreach (var cp in customPlants)
        {
            names.Add(cp.Name);
        }

        return names;
    }

    private static void ApplyDailyDecayIfNeeded(CyberPlant plant)
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
            plant.WaterValue = ApplyDecay(plant.WaterValue, random);
            plant.NutritionValue = ApplyDecay(plant.NutritionValue, random);
            plant.SunValue = ApplyDecay(plant.SunValue, random);
        }

        plant.LastCareDecayAt = today;
    }

    private static int ApplyDecay(int value, Random random)
    {
        if (value <= 0) return 0;
        const double minRate = 0.10;
        const double maxRate = 0.30;
        var rate = minRate + random.NextDouble() * (maxRate - minRate);
        var loss = (int)Math.Ceiling(value * rate);
        return Math.Max(0, value - loss);
    }

    private static void UpdateGrowthAndMood(CyberPlant plant)
    {
        // Update care lock
        if (plant.WaterValue == 0 && plant.NutritionValue == 0 && plant.SunValue == 0)
        {
            plant.IsCareLocked = true;
        }
        else if (plant.IsCareLocked &&
                 plant.WaterValue >= plant.NeedWater &&
                 plant.NutritionValue >= plant.NeedNutrition &&
                 plant.SunValue >= plant.NeedSun)
        {
            plant.IsCareLocked = false;
        }

        // Update growth
        var avgRatio = (
            plant.WaterValue / (double)plant.MaxWater +
            plant.NutritionValue / (double)plant.MaxNutrition +
            plant.SunValue / (double)plant.MaxSun) / 3.0;
        plant.GrowthLevel = Math.Clamp((int)Math.Round(avgRatio * 10), 0, 10);

        // Update mood
        var needsWater = plant.WaterValue < plant.NeedWater;
        var needsNutrition = plant.NutritionValue < plant.NeedNutrition;
        var needsSun = plant.SunValue < plant.NeedSun;

        if (needsWater || needsNutrition || needsSun)
        {
            if (needsWater) plant.Mood = "有点渴了……";
            else if (needsNutrition) plant.Mood = "有点虚弱……";
            else plant.Mood = "有点没精神……";
        }
        else
        {
            var minRatio = new[]
            {
                plant.WaterValue / (double)plant.MaxWater,
                plant.NutritionValue / (double)plant.MaxNutrition,
                plant.SunValue / (double)plant.MaxSun
            }.Min();

            plant.Mood = minRatio switch
            {
                >= 0.8 => "超级开心",
                >= 0.6 => "很开心",
                >= 0.4 => "还不错",
                _ => "还行"
            };
        }
    }

    private static void PersistCurrentState(CyberPlant plant)
    {
        plant.PlantStates[plant.PlantType] = new PlantCareState
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
            Messages = plant.Messages
        };
    }

    private static bool TryRestorePlantState(CyberPlant plant, string plantType)
    {
        if (!plant.PlantStates.TryGetValue(plantType, out var state)) return false;

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

        return true;
    }

    private static void InitializePlantState(CyberPlant plant, string plantType)
    {
        // Use sensible defaults based on plant type
        var defaults = SystemPlantNames.ContainsKey(plantType)
            ? GetPresetDefaults(plantType)
            : (MaxWater: 12, MaxNutrition: 12, MaxSun: 12, NeedWater: 6, NeedNutrition: 6, NeedSun: 6);

        plant.PlantName = SystemPlantNames.GetValueOrDefault(plantType, "我的植物");
        plant.CustomEmoji = string.Empty;
        plant.CustomSystemPrompt = string.Empty;
        plant.CustomImagePath = string.Empty;
        plant.MaxWater = defaults.MaxWater;
        plant.MaxNutrition = defaults.MaxNutrition;
        plant.MaxSun = defaults.MaxSun;
        plant.NeedWater = defaults.NeedWater;
        plant.NeedNutrition = defaults.NeedNutrition;
        plant.NeedSun = defaults.NeedSun;
        plant.WaterValue = (int)(defaults.MaxWater * 0.5);
        plant.NutritionValue = (int)(defaults.MaxNutrition * 0.5);
        plant.SunValue = (int)(defaults.MaxSun * 0.5);
        plant.GrowthLevel = 5;
        plant.Mood = "还不错";
        plant.LastWateredAt = DateTime.Now;
        plant.LastFertilizedAt = DateTime.Now;
        plant.LastSunbathedAt = DateTime.Now;
        plant.LastCareDecayAt = DateTime.Today;
        plant.LastChatClearedAt = DateTime.Today;
        plant.IsCareLocked = false;
        plant.Messages = [];
    }

    private static CyberPlant CreateDefaultPlant(string plantType)
    {
        var plant = new CyberPlant { PlantType = plantType };
        InitializePlantState(plant, plantType);
        return plant;
    }

    private static CyberPlant StateToPlant(PlantCareState state, string plantType)
    {
        var plant = new CyberPlant { PlantType = plantType };
        TryRestorePlantState(plant, plantType);
        // Re-apply from state
        plant.PlantName = string.IsNullOrWhiteSpace(state.PlantName) ? plant.PlantName : state.PlantName;
        plant.GrowthLevel = state.GrowthLevel;
        plant.Mood = string.IsNullOrWhiteSpace(state.Mood) ? plant.Mood : state.Mood;
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
        return plant;
    }

    private static (int MaxWater, int MaxNutrition, int MaxSun, int NeedWater, int NeedNutrition, int NeedSun)
        GetPresetDefaults(string plantType)
    {
        return plantType switch
        {
            "cactus" => (14, 10, 14, 5, 4, 6),
            "sunflower" => (14, 12, 18, 7, 6, 9),
            "mint" => (16, 12, 12, 8, 6, 6),
            "fern" => (18, 12, 10, 9, 6, 5),
            "bamboo" => (18, 14, 12, 9, 7, 6),
            _ => (12, 12, 12, 6, 6, 6)
        };
    }

    // ===================== Tool Definitions =====================

    public static IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        return new[]
        {
            // ---- Task tools ----
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "create_task",
                    Description = "创建一个新任务。当用户说'帮我创建任务'、'添加任务'、'新建任务'、'记一下'等时调用。需要从用户的话中提取任务的标题、描述、开始日期、截止日期、预计时长、是否紧急等信息。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "任务标题（必填）" },
                            description = new { type = "string", description = "任务描述/内容，如果用户没有提供则留空" },
                            start_date = new { type = "string", description = "开始日期，格式 yyyy-MM-dd HH:mm，如果用户没有提供则不传" },
                            due_date = new { type = "string", description = "截止日期，格式 yyyy-MM-dd HH:mm，如果用户没有提供则不传" },
                            estimated_hours = new { type = "integer", description = "预计小时数，如果用户没有提供则不传" },
                            estimated_minutes = new { type = "integer", description = "预计分钟数，如果用户没有提供则不传" },
                            is_urgent = new { type = "boolean", description = "是否紧急，默认false" }
                        },
                        required = new[] { "title" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "list_tasks",
                    Description = "查看所有任务的列表。当用户说'查看任务'、'有哪些任务'、'任务列表'、'所有任务'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new { },
                        required = Array.Empty<string>()
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "update_task",
                    Description = "更新一个已有任务的状态或内容。当用户说'完成任务'、'标记为已完成'、'修改任务'、'更新任务'、'把XX改成'等时调用。需要提供原任务标题来找到任务。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "要更新的原任务标题或标题关键词（必填）" },
                            new_title = new { type = "string", description = "新标题，如果不改则不传" },
                            description = new { type = "string", description = "新描述/内容，如果不改则不传" },
                            status = new { type = "string", description = "新状态：todo/待办, doing/进行中, done/已完成" },
                            is_urgent = new { type = "string", description = "是否紧急：true/false" },
                            start_date = new { type = "string", description = "新的开始日期，格式 yyyy-MM-dd HH:mm，如果不改则不传" },
                            due_date = new { type = "string", description = "新的截止日期，格式 yyyy-MM-dd HH:mm，如果不改则不传" },
                            estimated_hours = new { type = "integer", description = "预计小时数，如果不改则不传" },
                            estimated_minutes = new { type = "integer", description = "预计分钟数，如果不改则不传" }
                        },
                        required = new[] { "title" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "delete_task",
                    Description = "删除一个任务。当用户说'删除任务'、'移除任务'、'取消任务'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            title = new { type = "string", description = "要删除的任务标题（或标题关键词）" }
                        },
                        required = new[] { "title" }
                    }
                }
            },

            // ---- Plant tools ----
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "care_plant",
                    Description = "照料赛博植物：浇水、施肥或晒太阳。当用户说'帮我浇一下水'、'给植物施肥'、'让植物晒太阳'、'照顾一下植物'、'浇花'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            action = new { type = "string", description = "照料动作：water（浇水）、fertilize（施肥）、sunbathe（晒太阳）" },
                            plant_type = new { type = "string", description = "要照料的植物名称或类型，如'仙人掌'、'cactus'、'向日葵'等。如果不指定则照料当前选中的植物。" }
                        },
                        required = new[] { "action" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "check_plant_status",
                    Description = "查看赛博植物的当前状态，包括水分、营养、阳光、成长等级、心情。当用户说'我的植物怎么样了'、'植物状态'、'看看植物'、'仙人掌还好吗'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            plant_type = new { type = "string", description = "要查看的植物名称或类型，如'仙人掌'、'cactus'等。如果不指定则查看当前选中的植物。" }
                        },
                        required = Array.Empty<string>()
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "switch_plant",
                    Description = "切换到另一株赛博植物。当用户说'切换到仙人掌'、'换一棵植物'、'我想到向日葵那边去'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            plant_type = new { type = "string", description = "要切换到的植物名称或类型，如'仙人掌'、'cactus'、'向日葵'、'sunflower'等" }
                        },
                        required = new[] { "plant_type" }
                    }
                }
            },
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "list_plants",
                    Description = "列出所有可用的赛博植物（系统预设 + 自定义植物），标注当前选中的是哪一株。当用户说'我有哪些植物'、'列出植物'、'植物列表'、'看看有什么植物'等时调用。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new { },
                        required = Array.Empty<string>()
                    }
                }
            }
        };
    }

    // ===================== JSON Helpers =====================

    private static string? GetStringProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    private static bool GetBoolProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return prop.GetBoolean();
        }
        return false;
    }

    private static int GetIntProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetInt32(out var value))
                return value;
        }
        return 0;
    }
}
