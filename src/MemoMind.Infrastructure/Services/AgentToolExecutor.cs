using System.Text.Json;
using MemoMind.Core.Interfaces;
using MemoMind.Core.Models;

namespace MemoMind.Infrastructure.Services;

/// <summary>
/// AI Agent 工具执行器——将 AI 的 function call 路由到实际的本地操作。
///
/// 支持 11 个工具，分三大类：
/// - 任务管理：create_task / list_tasks / update_task / delete_task
/// - 赛博植物：care_plant / check_plant_status / switch_plant / list_plants
/// - 计时闹钟：start_pomodoro / start_countdown / set_alarm
///
/// 植物状态持久化在本地 JSON 文件（%LocalAppData%/MemoMind/cyber_plant.json），
/// 计时器命令通过写入 timer_command.json 文件传递给番茄钟 ViewModel。
///
/// 参数解析使用大小写不敏感的 JSON 属性名，适应不同 AI 模型的输出格式差异。
/// </summary>
public class AgentToolExecutor : IAgentToolExecutor
{
    private readonly ITaskService taskService;
    private readonly ICustomPlantService customPlantService;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>数据存储根目录：%LocalAppData%/MemoMind</summary>
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MemoMind");
    private static readonly string PlantDataPath = Path.Combine(DataFolder, "cyber_plant.json");
    private static readonly string PlantOverridesPath = Path.Combine(DataFolder, "plant_profiles.json");

    /// <summary>系统预设植物类型 → 中文名映射</summary>
    private static readonly Dictionary<string, string> SystemPlantNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cactus"] = "仙人掌",
        ["sunflower"] = "向日葵",
        ["mint"] = "薄荷",
        ["fern"] = "蕨类",
        ["bamboo"] = "竹子",
    };

    /// <summary>系统预设植物类型 → emoji 映射</summary>
    private static readonly Dictionary<string, string> SystemPlantEmojis = new(StringComparer.OrdinalIgnoreCase)
    {
        ["cactus"] = "🌵",
        ["sunflower"] = "🌻",
        ["mint"] = "🌿",
        ["fern"] = "🍃",
        ["bamboo"] = "🎋",
    };

    /// <summary>自定义植物的类型前缀，如 "custom:3" 表示 Id=3 的自定义植物</summary>
    private const string CustomPlantPrefix = "custom:";

    /// <summary>每次照料操作增加的数值（浇水/施肥/晒太阳统一 +3）</summary>
    private const int CareIncreaseAmount = 3;

    private record PlantOverrideDto(string PlantId, string? Name, string? Personality, string? SystemPrompt, bool IsDeleted);

    private static readonly string TimerCommandPath = Path.Combine(DataFolder, "timer_command.json");

    /// <summary>
    /// 计时器命令的 JSON 序列化模型。
    /// 写入 timer_command.json 后由番茄钟 ViewModel 定时轮询读取并执行。
    /// </summary>
    private class TimerCommand
    {
        public string Action { get; set; } = "";
        public int WorkMinutes { get; set; }
        public int BreakMinutes { get; set; }
        public int Cycles { get; set; }
        public int Hours { get; set; }
        public int Minutes { get; set; }
        public int Seconds { get; set; }
        public string? Name { get; set; }
        public int Hour { get; set; }
        public int Minute { get; set; }
        public string? Message { get; set; }
        public string? RepeatMode { get; set; }
    }

    public AgentToolExecutor(ITaskService taskService, ICustomPlantService customPlantService)
    {
        this.taskService = taskService;
        this.customPlantService = customPlantService;
    }

    /// <summary>
    /// 工具执行路由入口。
    /// 根据 functionName 分发到对应的操作方法。
    /// </summary>
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
            "start_pomodoro" => await StartPomodoroAsync(argumentsJson),
            "start_countdown" => await StartCountdownAsync(argumentsJson),
            "set_alarm" => await SetAlarmAsync(argumentsJson),
            _ => $"未知操作：{functionName}"
        };
    }

    // ===================== 任务工具 =====================

    /// <summary>
    /// 创建新任务。
    /// 从 JSON 参数中提取 title/description/due_date 等，生成 TaskItem 并持久化。
    /// 支持中文日期格式的自动解析（如 "2026-06-01 15:00"）。
    /// </summary>
    private async Task<string> CreateTaskAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var title = GetStringProperty(root, "title") ?? "未命名任务";
            var description = GetStringProperty(root, "description") ?? "";
            var isUrgent = GetBoolOrStringProperty(root, "is_urgent") ?? false;

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
                SourceType = "Agent"        // 标记来源为 AI Agent，区别于手动创建
            };

            await taskService.AddAsync(task);

            // 构建友好的确认消息
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

    /// <summary>
    /// 列出所有任务，以易读的中文格式返回。
    /// 状态映射：Todo→待办 / Doing→进行中 / Done→已完成
    /// </summary>
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

    /// <summary>
    /// 更新已有任务。
    ///
    /// 匹配策略：通过 title 模糊匹配（双向 contains），找到第一个匹配的任务。
    /// 仅更新 JSON 中提供了的字段（部分更新），没有提供的字段保持不变。
    /// 支持中英文状态值（"待办"/"todo"、"进行中"/"doing"、"已完成"/"done"）。
    /// </summary>
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
            // 模糊匹配：任务标题包含关键词 或 关键词包含任务标题
            var target = tasks.FirstOrDefault(t =>
                t.Title.Contains(title, StringComparison.OrdinalIgnoreCase) ||
                title.Contains(t.Title, StringComparison.OrdinalIgnoreCase));

            if (target is null)
            {
                var allTitles = string.Join("、", tasks.Select(t => t.Title));
                return $"未找到标题包含「{title}」的任务。当前任务：{allTitles}。";
            }

            var newTitle = GetStringProperty(root, "new_title");
            var newDescription = GetStringProperty(root, "description");
            var newStatus = GetStringProperty(root, "status");
            var isUrgent = GetBoolOrStringProperty(root, "is_urgent");
            var startDateStr = GetStringProperty(root, "start_date");
            var dueDateStr = GetStringProperty(root, "due_date");
            var estimatedHours = GetIntProperty(root, "estimated_hours");
            var estimatedMinutes = GetIntProperty(root, "estimated_minutes");

            LogDiagnostic("UpdateTaskAsync", $"args={argsJson}, target.Title={target.Title}, target.IsUrgent={target.IsUrgent}, isUrgent parsed={isUrgent?.ToString() ?? "null"}, isUrgent kind={GetJsonKind(root, "is_urgent")}");

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
                // 标准化状态值：支持中文和英文输入
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
                        target.CompletedAt = DateTime.Now;   // 自动记录完成时间
                    }
                    changes.Add($"状态→{newStatus}");
                }
            }

            if (isUrgent.HasValue && isUrgent.Value != target.IsUrgent)
            {
                target.IsUrgent = isUrgent.Value;
                changes.Add(isUrgent.Value ? "标记为紧急" : "取消紧急标记");
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

            LogDiagnostic("UpdateTaskAsync", $"About to save: target.IsUrgent={target.IsUrgent}, changes={string.Join(",", changes)}");
            await taskService.UpdateAsync(target);
            LogDiagnostic("UpdateTaskAsync", $"Save completed. Verifying: target.IsUrgent={target.IsUrgent}");
            return $"任务「{target.Title}」已更新：{string.Join("，", changes)}。";
        }
        catch (Exception ex)
        {
            LogDiagnostic("UpdateTaskAsync", $"EXCEPTION: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}");
            return $"更新任务失败：{ex.Message}";
        }
    }

    /// <summary>
    /// 删除任务。通过 title 模糊匹配定位目标任务。
    /// </summary>
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

    // ===================== 植物工具 =====================

    /// <summary>
    /// 照料植物：浇水/施肥/晒太阳。
    ///
    /// 逻辑：
    /// 1. 如果指定了 plant_type 且与当前不同，则先切换植物
    /// 2. 应用每日衰减（跨天未照料时自动扣减）
    /// 3. 执行照料操作（数值 +CareIncreaseAmount，上限为 Max）
    /// 4. 更新成长等级和心情
    /// 5. 持久化到本地 JSON 文件
    /// </summary>
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

            // 解析目标植物类型
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

            // 如果照料的不是当前植物，切换过去
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

            // 执行照料动作，回复根据成长等级变化
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

    /// <summary>
    /// 查看植物状态报告。
    /// 包含水分/营养/阳光/成长等级/心情/照料建议/上次照料时间。
    /// 支持指定 plant_type 查看非当前植物的状态。
    /// </summary>
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
                    // 从保存的状态中恢复
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

    /// <summary>
    /// 切换当前植物。将当前植物状态持久化到 PlantStates 字典，然后加载目标植物。
    /// 如果目标植物从未被照料过，使用该植物类型的预设默认值初始化。
    /// </summary>
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

            // 保存当前植物状态再切换
            PersistCurrentState(plant);

            // 加载目标植物（优先从历史状态恢复，否则初始化）
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

    /// <summary>列出所有可用植物（系统预设 + 自定义），标注当前选中的植物。</summary>
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

                // 跳过已删除的植物
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

    // ===================== 植物辅助方法 =====================

    /// <summary>
    /// 从本地 JSON 文件加载植物数据。
    /// 如果文件不存在或损坏，创建一个默认的仙人掌。
    /// </summary>
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

        // 首次启动：创建默认植物（仙人掌，中等状态）
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

    /// <summary>将植物数据序列化写入本地 JSON 文件</summary>
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

    /// <summary>加载植物配置文件覆盖（自定义名称、人设等）</summary>
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

    /// <summary>
    /// 获取植物的显示名称。
    /// 优先级：自定义名称 > 配置文件覆盖 > 系统预设名称。
    /// </summary>
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

    /// <summary>
    /// 解析植物类型提示词为实际的植物类型 ID。
    /// 支持：中文名、英文 ID、部分匹配。
    /// </summary>
    private string? ResolvePlantType(string hint)
    {
        // 精确匹配类型 ID
        if (SystemPlantNames.ContainsKey(hint))
            return hint;

        // 精确匹配中文名
        foreach (var (typeId, name) in SystemPlantNames)
        {
            if (string.Equals(name, hint, StringComparison.OrdinalIgnoreCase))
                return typeId;
        }

        // 部分匹配类型 ID
        foreach (var typeId in SystemPlantNames.Keys)
        {
            if (typeId.Contains(hint, StringComparison.OrdinalIgnoreCase))
                return typeId;
        }

        // 部分匹配中文名
        foreach (var (typeId, name) in SystemPlantNames)
        {
            if (name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                hint.Contains(name, StringComparison.OrdinalIgnoreCase))
                return typeId;
        }

        // 配置文件覆盖名称匹配
        var overrides = LoadProfileOverrides();
        foreach (var (typeId, ov) in overrides)
        {
            if (!string.IsNullOrWhiteSpace(ov.Name) &&
                (ov.Name.Contains(hint, StringComparison.OrdinalIgnoreCase) ||
                 hint.Contains(ov.Name, StringComparison.OrdinalIgnoreCase)))
                return typeId;
        }

        // 自定义植物名称匹配
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

    /// <summary>
    /// 每日衰减机制：如果跨天未照料，对水分/营养/阳光各做一次随机衰减。
    /// 衰减率：10%~30% 随机，天数叠加（跨 N 天则衰减 N 次）。
    /// </summary>
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

    /// <summary>单次衰减计算：随机扣除当前值的 10%~30%</summary>
    private static int ApplyDecay(int value, Random random)
    {
        if (value <= 0) return 0;
        const double minRate = 0.10;
        const double maxRate = 0.30;
        var rate = minRate + random.NextDouble() * (maxRate - minRate);
        var loss = (int)Math.Ceiling(value * rate);
        return Math.Max(0, value - loss);
    }

    /// <summary>
    /// 更新植物的成长等级和心情。
    ///
    /// 成长等级 = (水分比率 + 营养比率 + 阳光比率) / 3 * 10，0~10 级
    /// 心情：基于最低属性比率分 5 档——超级开心/很开心/还不错/还行/缺啥说啥
    /// 枯萎锁定：三项全归零时 IsCareLocked=true，需至少恢复到需求值以上才能解锁
    /// </summary>
    private static void UpdateGrowthAndMood(CyberPlant plant)
    {
        // 枯萎锁定检测
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

        // 成长等级
        var avgRatio = (
            plant.WaterValue / (double)plant.MaxWater +
            plant.NutritionValue / (double)plant.MaxNutrition +
            plant.SunValue / (double)plant.MaxSun) / 3.0;
        plant.GrowthLevel = Math.Clamp((int)Math.Round(avgRatio * 10), 0, 10);

        // 心情判定
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

    /// <summary>将当前植物状态保存到 PlantStates 字典，用于切换植物时保留状态</summary>
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

    /// <summary>尝试从 PlantStates 字典恢复之前保存的植物状态</summary>
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

    /// <summary>为新植物设置初始默认值。不同植物类型有不同的 Max 和 Need 值。</summary>
    private static void InitializePlantState(CyberPlant plant, string plantType)
    {
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
        // 初始值设为最大值的 50%
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
        // 二次赋值确保状态完全覆盖
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

    /// <summary>
    /// 每种植物类型的预设属性值。
    /// 不同植物有不同的特征：蕨类水分需求高(9)、竹子营养需求高(7)、向日葵阳光需求高(9)等。
    /// </summary>
    private static (int MaxWater, int MaxNutrition, int MaxSun, int NeedWater, int NeedNutrition, int NeedSun)
        GetPresetDefaults(string plantType)
    {
        return plantType switch
        {
            "cactus" => (14, 10, 14, 5, 4, 6),       // 仙人掌：耐旱，需求低
            "sunflower" => (14, 12, 18, 7, 6, 9),      // 向日葵：喜阳，阳光需求高
            "mint" => (16, 12, 12, 8, 6, 6),            // 薄荷：喜水，水分需求高
            "fern" => (18, 12, 10, 9, 6, 5),            // 蕨类：湿润环境，水分需求最高
            "bamboo" => (18, 14, 12, 9, 7, 6),          // 竹子：营养需求最高
            _ => (12, 12, 12, 6, 6, 6)
        };
    }

    // ===================== 计时器工具 =====================

    /// <summary>
    /// 启动番茄钟：将命令写入 timer_command.json，由番茄钟 ViewModel 轮询读取。
    /// 如果 AI 未指定参数，则使用番茄钟页面的当前设置。
    /// </summary>
    private static async Task<string> StartPomodoroAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var workMinutes = GetIntProperty(root, "work_minutes");
            var breakMinutes = GetIntProperty(root, "break_minutes");
            var cycles = GetIntProperty(root, "cycles");

            var command = new TimerCommand
            {
                Action = "start_pomodoro",
                WorkMinutes = workMinutes > 0 ? workMinutes : 0,
                BreakMinutes = breakMinutes > 0 ? breakMinutes : 0,
                Cycles = cycles > 0 ? cycles : 0
            };

            WriteTimerCommand(command);

            var parts = new List<string>();
            if (workMinutes > 0) parts.Add($"工作 {workMinutes} 分钟");
            if (breakMinutes > 0) parts.Add($"休息 {breakMinutes} 分钟");
            if (cycles > 0) parts.Add($"共 {cycles} 轮");
            var details = parts.Count > 0 ? "（" + string.Join("，", parts) + "）" : "（使用当前设置）";

            return $"番茄钟已启动{details}。你可以在番茄钟页面查看进度。";
        }
        catch (Exception ex)
        {
            return $"启动番茄钟失败：{ex.Message}";
        }
    }

    /// <summary>启动倒计时：支持小时+分钟+秒的组合</summary>
    private static async Task<string> StartCountdownAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var hours = GetIntProperty(root, "hours");
            var minutes = GetIntProperty(root, "minutes");
            var seconds = GetIntProperty(root, "seconds");

            if (hours <= 0 && minutes <= 0 && seconds <= 0)
            {
                return "请至少指定一个大于0的时间。例如：10分钟、1小时30分钟、45秒。";
            }

            var command = new TimerCommand
            {
                Action = "start_countdown",
                Hours = hours,
                Minutes = minutes,
                Seconds = seconds
            };

            WriteTimerCommand(command);

            var timeParts = new List<string>();
            if (hours > 0) timeParts.Add($"{hours}小时");
            if (minutes > 0) timeParts.Add($"{minutes}分钟");
            if (seconds > 0) timeParts.Add($"{seconds}秒");
            return $"倒计时 {string.Join("", timeParts)} 已启动。你可以在番茄钟页面查看进度。";
        }
        catch (Exception ex)
        {
            return $"启动倒计时失败：{ex.Message}";
        }
    }

    /// <summary>设置闹钟：支持单次/每天/每周重复</summary>
    private static async Task<string> SetAlarmAsync(string argsJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(argsJson);
            var root = doc.RootElement;

            var name = GetStringProperty(root, "name") ?? "闹钟";
            var hour = GetIntProperty(root, "hour");
            var minute = GetIntProperty(root, "minute");
            var message = GetStringProperty(root, "message") ?? "";
            var repeatMode = GetStringProperty(root, "repeat_mode")?.ToLowerInvariant();

            if (hour < 0 || hour > 23 || minute < 0 || minute > 59)
            {
                return "小时应在 0-23 之间，分钟应在 0-59 之间。";
            }

            var command = new TimerCommand
            {
                Action = "set_alarm",
                Name = name,
                Hour = hour,
                Minute = minute,
                Message = message,
                RepeatMode = repeatMode
            };

            WriteTimerCommand(command);

            var repeatText = repeatMode switch
            {
                "daily" => "，每天重复",
                "weekly" => "，每周重复",
                _ => ""
            };
            var messageText = string.IsNullOrWhiteSpace(message) ? "" : $"，备注「{message}」";
            return $"闹钟「{name}」已设置：{hour:D2}:{minute:D2}{repeatText}{messageText}。";
        }
        catch (Exception ex)
        {
            return $"设置闹钟失败：{ex.Message}";
        }
    }

    /// <summary>将计时器命令写入 JSON 文件。番茄钟 ViewModel 通过文件轮询来接收 AI 发起的计时请求。</summary>
    private static void WriteTimerCommand(TimerCommand command)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var json = JsonSerializer.Serialize(command, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(TimerCommandPath, json);
        }
        catch
        {
            // 静默失败——ViewModel 端有文件不存在的兜底处理
        }
    }

    // ===================== Function Calling 工具定义 =====================

    /// <summary>
    /// 构建发送给 AI 的 tools 数组，包含 11 个可用工具的函数定义。
    ///
    /// 每个工具定义包含：
    /// - Name: 函数唯一标识
    /// - Description: 函数用途 + 触发条件（指导 AI 何时调用）
    /// - Parameters: JSON Schema 格式的参数定义（type + properties + required）
    ///
    /// 注意：Description 的质量直接影响 AI 的工具调用准确率，
    /// 因此包含了中文触发词提示（如 "当用户说'创建任务'时调用"）。
    /// </summary>
    public static IReadOnlyList<ToolDefinition> GetAvailableTools()
    {
        return new[]
        {
            // ---- 任务工具 ----
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
                            is_urgent = new { type = "boolean", description = "是否紧急" },
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

            // ---- 植物工具 ----
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
            },

            // ---- 计时器工具 ----
            new ToolDefinition
            {
                Type = "function",
                Function = new FunctionDefinition
                {
                    Name = "start_pomodoro",
                    Description = "启动一个番茄钟。当用户说'开始番茄钟'、'帮我开始一个25分钟的番茄钟'、'开始专注'、'启动番茄'等时调用。从用户的话中提取工作时长、休息时长、轮数。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            work_minutes = new { type = "integer", description = "每轮工作时长（分钟），如 25、30。如果用户没有指定则不传，使用当前设置。" },
                            break_minutes = new { type = "integer", description = "每轮休息时长（分钟），如 5、10。如果用户没有指定则不传。" },
                            cycles = new { type = "integer", description = "循环轮数，如 4。如果用户没有指定则不传。" }
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
                    Name = "start_countdown",
                    Description = "启动一个倒计时。当用户说'帮我倒计时'、'倒计时10分钟'、'计时半小时'、'帮我计个时'等时调用。从用户的话中提取小时、分钟、秒数。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            hours = new { type = "integer", description = "小时数，如 1。如果用户没有指定则不传，默认0。" },
                            minutes = new { type = "integer", description = "分钟数，如 10、30。如果用户没有指定则不传，默认0。" },
                            seconds = new { type = "integer", description = "秒数，如 45。如果用户没有指定则不传，默认0。" }
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
                    Name = "set_alarm",
                    Description = "设置一个闹钟。当用户说'设个闹钟'、'帮我设一个下午3点的闹钟'、'提醒我'、'定个闹铃'等时调用。从用户的话中提取闹钟名称、时间（小时、分钟）、备注信息、重复方式。",
                    Parameters = new
                    {
                        type = "object",
                        properties = new
                        {
                            name = new { type = "string", description = "闹钟名称，如'上课提醒'、'休息闹钟'。如果用户没有提供则默认为'闹钟'。" },
                            hour = new { type = "integer", description = "小时（0-23），如 15 表示下午3点。从用户的话中提取。" },
                            minute = new { type = "integer", description = "分钟（0-59），如 30。如果用户没有指定则默认为0。" },
                            message = new { type = "string", description = "闹钟备注信息，如果用户没有提供则不传。" },
                            repeat_mode = new { type = "string", description = "重复方式：once（一次）、daily（每天）、weekly（每周）。如果用户没有指定则默认为once。" }
                        },
                        required = new[] { "hour", "minute" }
                    }
                }
            }
        };
    }

    // ===================== JSON 解析辅助方法 =====================

    /// <summary>安全获取字符串属性，不存在或类型不匹配时返回 null</summary>
    private static string? GetStringProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }
        return null;
    }

    /// <summary>安全获取布尔属性</summary>
    private static bool GetBoolProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            return prop.GetBoolean();
        }
        return false;
    }

    /// <summary>
    /// 安全获取布尔值，同时支持 JSON 布尔和字符串形式。
    /// 不同 AI 模型可能将 boolean 参数序列化为不同格式（true 或 "true"）。
    /// </summary>
    private static bool? GetBoolOrStringProperty(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var prop))
            return null;

        if (prop.ValueKind is JsonValueKind.True or JsonValueKind.False)
            return prop.GetBoolean();

        if (prop.ValueKind == JsonValueKind.String)
        {
            var s = prop.GetString();
            if (bool.TryParse(s, out var result))
                return result;
        }

        return null;
    }

    /// <summary>安全获取整数属性，不存在或非数字时返回 0</summary>
    private static int GetIntProperty(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop) && prop.ValueKind == JsonValueKind.Number)
        {
            if (prop.TryGetInt32(out var value))
                return value;
        }
        return 0;
    }

    /// <summary>获取属性的 JSON 值类型（用于诊断日志）</summary>
    private static string GetJsonKind(JsonElement element, string name)
    {
        if (element.TryGetProperty(name, out var prop))
            return prop.ValueKind.ToString();
        return "missing";
    }

    // ===================== 诊断日志 =====================

    private static readonly string LogPath = Path.Combine(DataFolder, "agent_diag.log");

    /// <summary>写入诊断日志到 agent_diag.log，用于排查 AI 工具调用问题</summary>
    private static void LogDiagnostic(string method, string message)
    {
        try
        {
            Directory.CreateDirectory(DataFolder);
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{method}] {message}\n";
            File.AppendAllText(LogPath, line);
        }
        catch { }
    }
}
