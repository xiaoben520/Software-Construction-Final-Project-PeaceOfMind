# 心安·MemoMind

一个面向大学生的 Windows 桌面端应用，结合 AI 心理辅助与智能事务管理，帮助用户管理任务、AI 对话、文件工作区，并通过可配置导航与主页模块支持后续扩展。

## 技术栈

- C# / .NET 8
- WPF + MVVM
- SQLite + EF Core
- HttpClient 调用 AI API

## 解决方案结构

```plaintext
MemoMind.sln
├── src
│   ├── MemoMind.App
│   ├── MemoMind.Core
│   └── MemoMind.Infrastructure
├── tests
│   └── MemoMind.Tests
└── docs
```

## 功能方向

- 任务看板与优先级管理
- AI 聊天陪伴与任务提取
- 文件工作区管理
- 左侧导航与主页模块可配置
- 主题与本地设置保存

## 运行方式

### 使用 Visual Studio 2022

1. 打开 `MemoMind.sln`
2. 将 `MemoMind.App` 设为启动项目
3. 直接运行

### 使用 VS Code

1. 安装 .NET SDK 8.0
2. 安装 C# 扩展
3. 打开项目根目录
4. 直接按 `F5`，或在“运行和调试”中选择 `Launch MemoMind.App`
5. 如果需要手动编译，也可以在终端执行：

```bash
dotnet restore
 dotnet build
```

> 本仓库已包含 `.vscode/launch.json`、`.vscode/tasks.json` 和 `.vscode/extensions.json`，可直接用于 VS Code 调试。

> WPF 仅支持 Windows 环境运行与调试。

## 开发约定

- `MemoMind.App` 只放界面、页面逻辑和导航配置。
- `MemoMind.Core` 只放实体、接口和通用业务抽象。
- `MemoMind.Infrastructure` 负责数据库、AI、文件与外部集成。
- 所有 AI 输出尽量统一为 JSON，方便解析。

## 后续计划

- 完成任务看板页面
- 完成 AI 聊天页面
- 完成文件工作区与设置联动
- 继续增加可扩展页面示例
- 完成测试与演示文档
