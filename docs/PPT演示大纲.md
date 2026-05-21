# 心安·MemoMind — PPT 演示大纲

> **演示时长**：3~5 分钟（共约 14 页，每页讲解 15~25 秒）
> **演示主题**：面向大学生的 AI 心理辅助与智能事务管理桌面系统

---

## 第 1 页：封面

- **项目名称**：心安 · MemoMind
- **副标题**：面向大学生的 AI 心理辅助与智能事务管理桌面系统
- **技术栈标签**：C# · .NET 8 · WPF · MVVM · SQLite · AI
- **团队成员**：李颜铭 · 韩艺明 · 吕彤旭 · 章汇
- **日期**：2026 年 5 月

---

## 第 2 页：系统功能介绍

**一句话定位**：将"任务管理 + AI 聊天陪伴 + 文件工作区"整合到一个桌面应用中。

**核心功能**：
- 任务看板 — 待办事项的增删改查、优先级与状态管理
- AI 聊天 — 自然语言对话、任务自动提取、情绪识别
- 文件工作区 — 常用文件路径管理、一键打开
- 赛博植物 — 虚拟植物养成，任务完成促进植物成长
- 番茄钟 — 专注计时、闹钟提醒
- 设置中心 — 主题切换、API Key 管理、导航可配置

**解决的问题**：
- 大学生面临学业压力、时间管理困难、情绪波动
- 单一任务工具无法满足"事务 + 情绪"的复合需求
- 提供"能陪伴、能整理、能切换"的一体化桌面工具

---

## 第 3 页：项目架构总览

**三层架构图**（建议用 Mermaid 或框图展示）：

```
┌──────────────────────────────────────────────┐
│        MemoMind.App（表现层）                  │
│   WPF · MVVM · Views · ViewModels · 主题      │
├──────────────────────────────────────────────┤
│        MemoMind.Core（核心业务层）              │
│    实体模型 · 服务接口 · DTO · 通用工具        │
├──────────────────────────────────────────────┤
│     MemoMind.Infrastructure（基础设施层）       │
│   EF Core · SQLite · AI 调用 · 文件存储       │
└──────────────────────────────────────────────┘
```

**架构亮点**：
- 三层解耦：表现层不依赖数据库实现，核心层只定义接口
- MVVM 模式：View ↔ ViewModel ↔ Model 单向依赖
- 依赖注入：`Microsoft.Extensions.DependencyInjection` 统一管理服务生命周期

---

## 第 4 页：关键技术（上）— 必选项

| 技术类别 | 项目中的具体应用 |
|---------|-----------------|
| **面向对象** | MVVM 架构模式；`ViewModelBase` 基类继承；6 个服务接口 + 实现；`RelayCommand` 命令模式；依赖注入实现控制反转 |
| **WPF 框架** | XAML 声明式 UI；数据绑定（`{Binding}`）；`DataTemplate` 类型映射；`DynamicResource` 动态主题切换；6 个值转换器；`DropShadowEffect` 阴影特效 |
| **异常处理** | AI API 调用失败自动降级为离线模式；文件操作静默失败并提示状态；数据库操作全局 try/catch；`HttpRequestException` 捕获网络异常 |
| **数据库** | SQLite 本地数据库；EF Core 8.0.8 ORM；8 张数据表（Tasks、ChatMessages、Memories、FileWorkspaces、PomodoroSessions、CustomPlantProfiles、EmotionLogs、CalendarEvents）；自动迁移 `Database.Migrate()` |

---

## 第 5 页：关键技术（下）— 可选项

| 技术类别 | 项目中的具体应用 |
|---------|-----------------|
| **LINQ** | `OrderBy`/`ThenBy` 排序、`Where` 筛选、`Select` 投影、`FirstOrDefault` 查询、`ToList` 集合转换、`Sum` 统计聚合，贯穿所有 Service 层 |
| **文件操作** | `System.IO` 文件/目录增删改查；`FileSystemWatcher` 监听文件变化；JSON 文件持久化（settings.json、alarms.json、cyber_plant.json 等）；`OpenFileDialog`/`OpenFolderDialog` |
| **GUI 绘图** | 5 套自定义主题（Light/Dark/Forest/Ocean/Sunset）；圆形进度条 `Path` + `ProgressToArcConverter`；卡片阴影 `DropShadowEffect`；`BitmapImage` 植物图片加载 |
| **网络数据通信** | `HttpClient` 调用 OpenAI 兼容 API；支持 Function Calling（工具调用）；JSON Mode 结构化输出；7 种 AI 提供商预设（OpenAI/DeepSeek/Kimi/Qwen 等）；30 秒超时 |
| **多线程/异步** | `async`/`await` 全链路异步；`DispatcherTimer` 驱动番茄钟和任务倒计时；`Task.Run()` 后台文件枚举；`Dispatcher.Invoke` 线程安全 UI 更新；`IServiceScopeFactory` 管理作用域 |
| **多媒体** | `SoundService` 程序化生成 WAV 音频：正弦波合成 + ADSR 包络 + RIFF 文件头；`SoundPlayer` 播放；4 种内置音效（休息铃、工作铃、闹钟、倒计时结束） |

---

## 第 6 页：模块展示 — 主页

- **页面名称**：主页（Home）
- **截图占位**：[待插入主页截图]
- **核心功能**：
  - 欢迎语 + 日期显示
  - 功能模块卡片网格（任务看板、AI 聊天、文件工作区、赛博植物、番茄钟）
  - 点击"打开模块"跳转到对应页面
  - 卡片显示可在设置中自定义开关
- **技术要点**：`HomeViewModel` 读取 `AppPageCatalog` 中标记为 Home 可见的模块

---

## 第 7 页：模块展示 — 任务看板

- **页面名称**：任务看板（Task Board）
- **截图占位**：[待插入任务看板截图]
- **核心功能**：
  - 任务 CRUD（新增 / 编辑 / 删除）
  - 三列状态视图（待办 / 进行中 / 已完成）
  - 优先级标记、截止日期设置
  - 倒计时时钟（1 秒刷新）
- **技术要点**：`TaskBoardViewModel` + `TaskService`（EF Core）；`DispatcherTimer` 驱动实时倒计时

---

## 第 8 页：模块展示 — AI 聊天

- **页面名称**：AI 聊天（AI Chat）
- **截图占位**：[待插入 AI 聊天截图]
- **核心功能**：
  - 自然语言对话界面
  - 支持 Function Calling：自动创建/查询/更新/删除任务
  - 情绪识别 + 记忆提取 + 植物养护指令
  - 无 API Key 时自动降级为离线模式
- **技术要点**：`ChatService` + `AgentToolExecutor`；`HttpClient` 调用兼容 OpenAI 格式 API；7 种 AI 提供商可切换

---

## 第 9 页：模块展示 — 文件工作区

- **页面名称**：文件工作区（File Workspace）
- **截图占位**：[待插入文件工作区截图]
- **核心功能**：
  - 保存常用文件/文件夹路径
  - 一键打开文件或文件夹
  - 工作区分组管理
  - 最近访问记录 + 文件系统实时监听
- **技术要点**：`FileWorkspaceViewModel`；`FileSystemWatcher` 监听变化；`Directory.EnumerateFiles` 树形浏览

---

## 第 10 页：模块展示 — 赛博植物

- **页面名称**：赛博植物（Cyber Plant）
- **截图占位**：[待插入赛博植物截图]
- **核心功能**：
  - 虚拟植物养成（多种植物可选）
  - 完成任务可促进植物成长
  - 植物状态展示（健康值、成长阶段）
  - 与植物 AI 对话
- **技术要点**：`CyberPlantViewModel`；`BitmapImage` 加载植物图片；`cyber_plant.json` 持久化状态

---

## 第 11 页：模块展示 — 番茄钟

- **页面名称**：专注 & 闹钟（Pomodoro & Alarm）
- **截图占位**：[待插入番茄钟截图]
- **核心功能**：
  - 番茄钟专注计时（工作 / 休息循环）
  - 闹钟设定与提醒
  - 倒计时显示 + 圆形进度条
  - 到时间自动播放提示音
- **技术要点**：`PomodoroAlarmViewModel`；`DispatcherTimer` 驱动计时；`SoundService` WAV 合成播放；`ProgressToArcConverter` 弧形进度

---

## 第 12 页：模块展示 — 设置

- **页面名称**：设置（Settings）
- **截图占位**：[待插入设置页截图]
- **核心功能**：
  - AI 提供商选择 + API Key 配置（DPAPI 加密存储）
  - 5 套主题实时切换（浅色 / 深色 / 森林 / 海洋 / 日落）
  - 左侧导航栏和主页模块显示/隐藏控制
  - 音效开关、弹出窗口等偏好设置
- **技术要点**：`JsonAppSettingsStore`；`ApiKeyProtection`（DPAPI 加密）；`DynamicResource` 动态主题

---

## 第 13 页：人员贡献

| 成员 | 角色 | 主要贡献 |
|------|------|---------|
| **李颜铭** | 组长 | 整体架构设计；DI 容器搭建；联调推进；赛博植物养成系统；番茄钟计时与音效提醒 |
| **韩艺明** | 后端开发 | 任务看板完整 CRUD；任务状态/优先级管理；倒计时时钟；数据持久化与校验 |
| **吕彤旭** | AI 开发 | AI 对话交互；Prompt 与结构化解析；Function Calling 工具调用；情绪识别与记忆提取；离线降级逻辑 |
| **章汇** | 全栈 / 文档 | 文件工作区（路径管理 + 快速打开 + 实时监听）；设置页（主题/AI/导航/音效）；主页模块；测试与文档汇总 |

---

## 第 14 页：总结与展望

**项目成果**：
- 实现 7 大功能模块的完整桌面应用
- 三层 MVVM 架构，代码解耦、可扩展
- AI 可选启用，无 AI 时基础功能正常运行
- 本地优先，数据安全可控

**技术栈回顾**：
- C# + .NET 8 + WPF + MVVM + SQLite + EF Core + AI

**未来展望**：
- 数据可视化（情绪曲线、任务统计周报）
- 云端同步备份
- 更多 AI 模型与插件扩展

**致谢**：感谢老师指导与团队成员的努力！

---

## 附：演示时间分配建议

| 页码 | 内容 | 建议用时 |
|------|------|---------|
| 1 | 封面 | 5 秒 |
| 2 | 系统功能介绍 | 20 秒 |
| 3 | 项目架构总览 | 15 秒 |
| 4-5 | 关键技术 | 30 秒 |
| 6-12 | 七模块展示 | 每页 15 秒 × 7 ≈ 105 秒 |
| 13 | 人员贡献 | 15 秒 |
| 14 | 总结展望 | 10 秒 |
| **合计** | | **约 3 分 20 秒** |

> 建议：模块展示部分可控制语速，每页 15 秒点到即止；关键技术页可适当展开讲 1~2 个亮点如 AI Function Calling 或 WAV 音频合成。
