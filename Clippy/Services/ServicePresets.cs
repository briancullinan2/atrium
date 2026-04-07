using System.Diagnostics.CodeAnalysis;

namespace Clippy.Services;


[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public class ServicePreset
{
    public bool IsDefault { get; set; } = false;
    public bool IsPrevious { get; set; } = false;
    public string Name { get; set; } = "";
    public string Url { get; set; } = "";
    public string DefaultModel { get; set; } = "";
    public string ApiKey { get; set; } = "";
    public string ResponsePath { get; set; } = "";
    public List<DynamicParam> Parameters { get; set; } = [];
}

public class DynamicParam
{
    public string Key { get; set; } = "";
    public string Type { get; set; } = "string";
    public string Value { get; set; } = "";
    public bool BoolValue { get; set; }
}


public static class ServicePresets
{
    public static ServicePreset Ollama { get; } = new ServicePreset
    {
        Name = "Ollama (Local /generate)",
        Url = "http://localhost:11434/api/generate",
        DefaultModel = "qwen3.5:cloud",
        ResponsePath = "response",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "prompt", Value = "{Message}", Type = "string" },
                new DynamicParam { Key = "stream", BoolValue = false, Type = "boolean" }
            ]
    };

    public static ServicePreset OpenAI { get; } = new ServicePreset
    {
        Name = "OpenAI (v1/chat)",
        Url = "https://api.openai.com/v1/chat/completions",
        DefaultModel = "gpt-4o",
        ApiKey = "sk-...",
        ResponsePath = "choices[0].message.content",
        Parameters = [
                // Note: OpenAI expects a 'messages' object, 
                // but for a generic builder, we track the key/value
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                new DynamicParam { Key = "temperature", Value = "0.8", Type = "number" },
                new DynamicParam { Key = "max_tokens", Value = "500", Type = "number" },
                new DynamicParam { Key = "top_p", Value = "1", Type = "number" },
                new DynamicParam { Key = "presence_penalty", Value = "0", Type = "number" },
                new DynamicParam { Key = "stream", BoolValue = false, Type = "boolean" }
            ]
    };


    public static ServicePreset Groq { get; } = new ServicePreset
    {
        Name = "Groq (Cloud)",
        Url = "https://api.groq.com/openai/v1/chat/completions",
        DefaultModel = "llama-3.3-70b-versatile",
        ApiKey = "gsk_...",
        ResponsePath = "choices[0].message.content",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                new DynamicParam { Key = "temperature", Value = "0.5", Type = "number" }
            ]
    };


    public static ServicePreset Anthropic { get; } = new ServicePreset
    {
        Name = "Anthropic (Claude)",
        Url = "https://api.anthropic.com/v1/messages",
        DefaultModel = "claude-3-5-sonnet-20240620",
        ApiKey = "sk-ant-api03-...",
        ResponsePath = "content[0].text",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "max_tokens", Value = "1024", Type = "number" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
            ]
    };


    public static ServicePreset DeepSeek { get; } = new ServicePreset
    {
        Name = "DeepSeek",
        Url = "https://api.deepseek.com/chat/completions",
        DefaultModel = "deepseek-chat",
        ApiKey = "sk-...", // DeepSeek uses standard sk- format
        ResponsePath = "choices[0].message.content",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
            ]
    };


    public static ServicePreset OpenRouter { get; } = new ServicePreset
    {
        Name = "OpenRouter (Universal)",
        Url = "https://openrouter.ai/api/v1/chat/completions",
        DefaultModel = "google/gemini-2.0-flash-001",
        ApiKey = "sk-or-v1-...", // OpenRouter keys start with sk-or-v1-
        ResponsePath = "choices[0].message.content",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" }
            ]
    };

    public static ServicePreset Mistral { get; } = new ServicePreset
    {
        Name = "Mistral AI",
        Url = "https://api.mistral.ai/v1/chat/completions",
        DefaultModel = "mistral-tiny",
        ResponsePath = "choices[0].message.content",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                new DynamicParam { Key = "safe_prompt", BoolValue = true, Type = "boolean" }
            ]
    };



    public static ServicePreset LLMStudio { get; } = new ServicePreset
    {
        Name = "LLM Studio (OpenAI Format)",
        Url = "http://localhost:1234/v1/chat/completions",
        DefaultModel = "local-model",
        ResponsePath = "choices[0].message.content",
        Parameters = [
                new DynamicParam { Key = "model", Value = "{Model}", Type = "string" },
                new DynamicParam { Key = "messages", Value = "[{\"role\": \"user\", \"content\": \"{Message}\"}]", Type = "string" },
                new DynamicParam { Key = "temperature", Value = "0.8", Type = "number" }
            ]
    };


    public static List<ServicePreset> Predefined { get; } = [
        Ollama, OpenAI, Groq, Anthropic, DeepSeek, OpenRouter, Mistral, LLMStudio            
    ];
}
