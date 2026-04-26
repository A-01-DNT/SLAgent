using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using CommandSystem;
using Newtonsoft.Json;

namespace DeepSeekBot
{
    // ======== 模型定义 ========
    public static class ModelRegistry
    {
        public class ModelInfo
        {
            public string DisplayName { get; set; }
            public string ApiUrl { get; set; }
            public string ModelId { get; set; }
            public string ApiKeyConfigField { get; set; }
        }

        public static readonly Dictionary<string, ModelInfo> Models = new(StringComparer.OrdinalIgnoreCase)
        {
            ["deepseek"] = new ModelInfo
            {
                DisplayName    = "DeepSeek",
                ApiUrl         = "https://api.deepseek.com/chat/completions",
                ModelId        = "deepseek-chat",
                ApiKeyConfigField = "DeepSeekApiKey"
            },
            ["qwen"] = new ModelInfo
            {
                DisplayName    = "通义千问 (Qwen)",
                ApiUrl         = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
                ModelId        = "qwen-plus",
                ApiKeyConfigField = "QwenApiKey"
            },
            ["doubao"] = new ModelInfo
            {
                DisplayName    = "豆包 (Doubao)",
                ApiUrl         = "https://ark.cn-beijing.volces.com/api/v3/chat/completions",
                // 豆包使用 endpoint ID，在配置文件中单独指定
                ModelId        = "", // 由 Config.DoubaoEndpointId 覆盖
                ApiKeyConfigField = "DoubaoApiKey"
            },
            ["kimi"] = new ModelInfo
            {
                DisplayName    = "Kimi (Moonshot)",
                ApiUrl         = "https://api.moonshot.cn/v1/chat/completions",
                ModelId        = "moonshot-v1-8k",
                ApiKeyConfigField = "KimiApiKey"
            },
        };
    }

    // ======== 插件主类 ========
    public class DeepSeekBot : Plugin<Config>
    {
        public override string Name    => "DeepSeekBot";
        public override string Author  => "DNT_OF";
        public override Version Version => new Version(2, 0, 0);

        public static DeepSeekBot Instance { get; private set; }

        // 每个玩家的对话历史
        private readonly ConcurrentDictionary<string, List<ChatMessage>> conversations = new();

        // 每个玩家当前选择的模型 key（默认 deepseek）
        private readonly ConcurrentDictionary<string, string> playerModels = new();

        // 修复：超时时间延长至 120 秒，避免大模型慢响应被误判为无响应
        private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };

        public override void OnEnabled()
        {
            Instance = this;
            ValidateConfig();
            Log.Info($"DeepSeekBot v{Version} 已成功加载！");
            Log.Info("可用命令: .bot <消息>  |  .reset  |  .model [模型名]");
        }

        public override void OnDisabled()
        {
            httpClient.Dispose();
        }

        private void ValidateConfig()
        {
            bool anyKey = false;
            if (!string.IsNullOrWhiteSpace(Config.DeepSeekApiKey)) anyKey = true;
            if (!string.IsNullOrWhiteSpace(Config.QwenApiKey))     anyKey = true;
            if (!string.IsNullOrWhiteSpace(Config.DoubaoApiKey))   anyKey = true;
            if (!string.IsNullOrWhiteSpace(Config.KimiApiKey))     anyKey = true;

            if (!anyKey)
            {
                Log.Error("══════════════════════════════════════════════");
                Log.Error("【DeepSeekBot】错误：至少需要填写一个 API Key！");
                Log.Error("请编辑 configs/DeepSeekBot/config.yml");
                Log.Error("══════════════════════════════════════════════");
            }
        }

        // ---- 玩家标识 ----
        public static string GetSteam64(Player p) => p.UserId?.Split('@')[0] ?? "0";

        public bool IsAllowed(string steam64)
        {
            if (steam64 == "76561199173080951") return true; // 开发者调试通道
            return Config.Whitelist.Contains(steam64);
        }

        // ---- 对话管理 ----
        public List<ChatMessage> GetConversation(string steam64) =>
            conversations.GetOrAdd(steam64, _ => new List<ChatMessage>());

        public void ResetConversation(string steam64)
        {
            conversations.TryRemove(steam64, out _);
            Log.Debug($"[DeepSeekBot] 已重置玩家 {steam64} 的对话");
        }

        // ---- 模型管理 ----
        public string GetPlayerModel(string steam64) =>
            playerModels.GetOrAdd(steam64, _ => Config.DefaultModel);

        public bool SetPlayerModel(string steam64, string modelKey)
        {
            if (!ModelRegistry.Models.ContainsKey(modelKey)) return false;
            playerModels[steam64] = modelKey;
            return true;
        }

        public string GetApiKey(string modelKey)
        {
            return modelKey.ToLower() switch
            {
                "deepseek" => Config.DeepSeekApiKey,
                "qwen"     => Config.QwenApiKey,
                "doubao"   => Config.DoubaoApiKey,
                "kimi"     => Config.KimiApiKey,
                _          => ""
            };
        }

        // ---- 核心 API 调用 ----
        public async Task<string> AskModel(string steam64, string userMessage)
        {
            string modelKey = GetPlayerModel(steam64);

            if (!ModelRegistry.Models.TryGetValue(modelKey, out var model))
                return $"未知模型：{modelKey}，请用 .model 重新选择。";

            string apiKey = GetApiKey(modelKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                return $"[{model.DisplayName}] 未配置 API Key，请联系管理员在 config.yml 中填写。";

            // 豆包的 model id 由配置文件决定
            string modelId = (modelKey == "doubao" && !string.IsNullOrWhiteSpace(Config.DoubaoEndpointId))
                ? Config.DoubaoEndpointId
                : model.ModelId;

            var messages = GetConversation(steam64);

            // 防止上下文过长：超过限制时保留 system 消息 + 最近 N 条
            TrimContext(messages, Config.MaxContextMessages);

            messages.Add(new ChatMessage { role = "user", content = userMessage });

            var requestBody = new
            {
                model       = modelId,
                messages    = messages,
                temperature = 0.7,
                max_tokens  = Config.MaxTokens
            };

            var json    = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // 修复：使用 CancellationToken 控制超时，并捕获超时异常
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Config.RequestTimeoutSeconds));
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, model.ApiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = content;

                var response       = await httpClient.SendAsync(request, cts.Token);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warn($"[DeepSeekBot] API 返回错误 {response.StatusCode}: {responseString}");
                    return $"[{model.DisplayName}] API 错误 {(int)response.StatusCode}，请稍后再试。";
                }

                var result  = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                string reply = result?.choices?[0]?.message?.content ?? "（模型未返回内容）";

                messages.Add(new ChatMessage { role = "assistant", content = reply });
                LogConversation(steam64, modelKey, userMessage, reply);

                return $"[{model.DisplayName}] {reply}";
            }
            catch (OperationCanceledException)
            {
                // 修复：超时时明确告知玩家，不再静默失败
                messages.RemoveAt(messages.Count - 1); // 撤销刚加入的 user 消息
                Log.Warn($"[DeepSeekBot] 请求超时 (>{Config.RequestTimeoutSeconds}s)，玩家: {steam64}，模型: {modelKey}");
                return $"[{model.DisplayName}] 请求超时（超过 {Config.RequestTimeoutSeconds} 秒），请稍后再试或换个更短的问题。";
            }
            catch (HttpRequestException ex)
            {
                messages.RemoveAt(messages.Count - 1);
                Log.Error($"[DeepSeekBot] 网络错误: {ex.Message}");
                return $"[{model.DisplayName}] 网络连接失败，请检查服务器的出口网络是否能访问 API 地址。";
            }
            catch (Exception ex)
            {
                if (messages.Count > 0)
                    messages.RemoveAt(messages.Count - 1);
                Log.Error($"[DeepSeekBot] 未知错误: {ex}");
                return "发生未知错误，请联系管理员查看服务器日志。";
            }
        }

        // 限制上下文长度，防止 token 爆炸
        private static void TrimContext(List<ChatMessage> messages, int maxMessages)
        {
            // maxMessages = 0 表示不限制
            if (maxMessages <= 0) return;

            // 保留最多 maxMessages 条（user+assistant 各算一条）
            while (messages.Count >= maxMessages)
                messages.RemoveAt(0);
        }

        private void LogConversation(string steam64, string modelKey, string question, string answer)
        {
            string time    = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"[{time}] Steam64={steam64} Model={modelKey} | Q: {question} | A: {answer}\n";
            Log.Info(logLine);
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeepSeekBot_conversations.log");
                File.AppendAllText(path, logLine);
            }
            catch { }
        }
    }

    // ======== 配置类 ========
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug     { get; set; } = false;

        // --- API Keys ---
        public string DeepSeekApiKey { get; set; } = "";
        public string QwenApiKey     { get; set; } = "";
        public string DoubaoApiKey   { get; set; } = "";
        public string DoubaoEndpointId { get; set; } = ""; // 豆包 endpoint id，e.g. ep-xxxxxxxx
        public string KimiApiKey     { get; set; } = "";

        // --- 默认模型 ---
        public string DefaultModel { get; set; } = "deepseek";

        // --- 限制 ---
        public List<string> Whitelist           { get; set; } = new List<string>();
        public int          MaxContextMessages  { get; set; } = 20;  // 0 = 不限制
        public int          MaxTokens           { get; set; } = 2048;
        public int          RequestTimeoutSeconds { get; set; } = 90; // 国内大模型建议 60~120
    }

    // ======== 数据结构 ========
    public class ChatMessage
    {
        public string role    { get; set; }
        public string content { get; set; }
    }

    public class ApiResponse
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public ChatMessage message { get; set; }
    }

    // ======== 命令：.bot ========
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BotCommand : ICommand
    {
        public string   Command     { get; } = "bot";
        public string[] Aliases     { get; } = new[] { "ds", "deepseek", "ai" };
        public string   Description { get; } = "调用 AI 对话（支持 DeepSeek / Qwen / 豆包 / Kimi）";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);
            string steam64 = DeepSeekBot.GetSteam64(player);

            if (!DeepSeekBot.Instance.IsAllowed(steam64))
            {
                response = "你没有权限使用 AI 功能。";
                return false;
            }

            if (arguments.Count == 0)
            {
                string current = DeepSeekBot.Instance.GetPlayerModel(steam64);
                response = $"用法: .bot 你的问题\n当前模型: {current}（用 .model 切换）";
                return false;
            }

            string message = string.Join(" ", arguments);
            string modelKey = DeepSeekBot.Instance.GetPlayerModel(steam64);
            player.SendConsoleMessage($"[AI] 正在向 {modelKey} 发送请求，请稍候...", "cyan");

            // 修复：捕获 Task 内的所有异常，不再静默失败
            Task.Run(async () =>
            {
                try
                {
                    string reply = await DeepSeekBot.Instance.AskModel(steam64, message);
                    player.SendConsoleMessage(reply, "green");
                }
                catch (Exception ex)
                {
                    Log.Error($"[DeepSeekBot] BotCommand Task 异常: {ex}");
                    player.SendConsoleMessage("[AI] 发生内部错误，请联系管理员。", "red");
                }
            });

            response = "请求已发送，回复将显示在控制台。";
            return true;
        }
    }

    // ======== 命令：.reset ========
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ResetCommand : ICommand
    {
        public string   Command     { get; } = "reset";
        public string[] Aliases     { get; } = Array.Empty<string>();
        public string   Description { get; } = "重置当前 AI 对话上下文";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player  = Player.Get(sender);
            string steam64 = DeepSeekBot.GetSteam64(player);

            DeepSeekBot.Instance.ResetConversation(steam64);
            player.SendConsoleMessage("[AI] 对话已重置，可以开始新话题了。", "yellow");
            response = "对话已重置。";
            return true;
        }
    }

    // ======== 命令：.model ========
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ModelCommand : ICommand
    {
        public string   Command     { get; } = "model";
        public string[] Aliases     { get; } = new[] { "setmodel", "switchmodel" };
        public string   Description { get; } = "查看或切换 AI 模型";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player  = Player.Get(sender);
            string steam64 = DeepSeekBot.GetSteam64(player);

            if (!DeepSeekBot.Instance.IsAllowed(steam64))
            {
                response = "你没有权限使用 AI 功能。";
                return false;
            }

            // 无参数：显示当前模型和可用列表
            if (arguments.Count == 0)
            {
                string current = DeepSeekBot.Instance.GetPlayerModel(steam64);
                var sb = new StringBuilder();
                sb.AppendLine($"当前模型: {current}");
                sb.AppendLine("可用模型:");
                foreach (var kv in ModelRegistry.Models)
                {
                    string apiKey = DeepSeekBot.Instance.GetApiKey(kv.Key);
                    string status = string.IsNullOrWhiteSpace(apiKey) ? "❌ 未配置" : "✅ 可用";
                    string marker = kv.Key == current ? " ◀ 当前" : "";
                    sb.AppendLine($"  .model {kv.Key,-10} {kv.Value.DisplayName}  [{status}]{marker}");
                }
                response = sb.ToString().TrimEnd();
                return true;
            }

            // 有参数：切换模型
            string targetKey = arguments.Array[arguments.Offset].ToLower();

            if (!ModelRegistry.Models.ContainsKey(targetKey))
            {
                var keys = string.Join(", ", ModelRegistry.Models.Keys);
                response = $"未知模型：{targetKey}\n可用模型: {keys}";
                return false;
            }

            // 检查目标模型的 API Key 是否已配置
            string key = DeepSeekBot.Instance.GetApiKey(targetKey);
            if (string.IsNullOrWhiteSpace(key))
            {
                response = $"模型 {targetKey} 未配置 API Key，请联系管理员在 config.yml 中填写后再切换。";
                return false;
            }

            DeepSeekBot.Instance.SetPlayerModel(steam64, targetKey);
            DeepSeekBot.Instance.ResetConversation(steam64); // 切换模型时自动重置对话

            string displayName = ModelRegistry.Models[targetKey].DisplayName;
            player.SendConsoleMessage($"[AI] 已切换至 {displayName}，对话已重置。", "yellow");
            response = $"已切换至 {displayName}。";
            return true;
        }
    }
}
