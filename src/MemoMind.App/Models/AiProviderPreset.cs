using System.Collections.Generic;

namespace MemoMind.App.Models;

/// <summary>
/// AI 提供商的预设配置，包含名称、API 地址和可用模型列表。
/// 用于设置页面快速切换 AI 服务商，用户也可选择"自定义"手动输入。
/// </summary>
public class AiProviderPreset
{
    /// <summary>提供商显示名称，如 "OpenAI"、"DeepSeek"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>API 基础地址，如 "https://api.openai.com/v1"</summary>
    public string BaseUrl { get; set; } = string.Empty;

    /// <summary>该提供商支持的模型列表</summary>
    public List<string> Models { get; set; } = [];

    /// <summary>所有预设提供商的静态列表，包含 7 个选项（含"自定义"）</summary>
    public static readonly IReadOnlyList<AiProviderPreset> All =
    [
        new AiProviderPreset
        {
            Name = "OpenAI",
            BaseUrl = "https://api.openai.com/v1",
            Models = ["gpt-4o", "gpt-4o-mini", "gpt-4.1", "gpt-4.1-mini", "gpt-4.1-nano"]
        },
        new AiProviderPreset
        {
            Name = "DeepSeek",
            BaseUrl = "https://api.deepseek.com",
            Models = ["deepseek-v4-flash", "deepseek-v4-pro"]
        },
        new AiProviderPreset
        {
            Name = "Kimi (月之暗面)",
            BaseUrl = "https://api.moonshot.cn/v1",
            Models = ["kimi-k2.6", "kimi-k2.6-thinking", "kimi-k2.5", "kimi-k2-thinking-turbo"]
        },
        new AiProviderPreset
        {
            Name = "Qwen (阿里通义千问)",
            BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
            Models = ["qwen3-max", "qwen3.6-plus", "qwen3.6-flash", "qwen-plus", "qwen-flash", "qwen3-coder-plus"]
        },
        new AiProviderPreset
        {
            Name = "SiliconCloud (硅基流动)",
            BaseUrl = "https://api.siliconflow.cn/v1",
            Models = ["deepseek-ai/DeepSeek-V3.2", "deepseek-ai/DeepSeek-R1", "Pro/deepseek-ai/DeepSeek-V3.2", "Qwen/Qwen3-Coder-480B-A35B-Instruct", "Qwen/Qwen2.5-72B-Instruct"]
        },
        new AiProviderPreset
        {
            Name = "OpenRouter (聚合 350+ 模型)",
            BaseUrl = "https://openrouter.ai/api/v1",
            Models = [
                "anthropic/claude-sonnet-4-20250514",
                "anthropic/claude-opus-4-20250514",
                "openai/gpt-4o",
                "openai/gpt-4o-mini",
                "google/gemini-2.5-pro",
                "google/gemini-2.5-flash",
                "moonshotai/kimi-k2.6",
                "deepseek/deepseek-chat"
            ]
        },
        new AiProviderPreset
        {
            Name = "自定义",
            BaseUrl = "",
            Models = ["自行输入模型名称"]
        }
    ];
}
