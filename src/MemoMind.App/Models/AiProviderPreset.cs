using System.Collections.Generic;

namespace MemoMind.App.Models;

public class AiProviderPreset
{
    public string Name { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public List<string> Models { get; set; } = [];

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
            Models = ["deepseek-v4-pro", "deepseek-v4-flash"]
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
