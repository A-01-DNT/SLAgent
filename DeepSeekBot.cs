using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using CommandSystem;
using Newtonsoft.Json;

namespace DeepSeekBot
{
    public class DeepSeekBot : Plugin<Config>
    {
        public override string Name => "DeepSeekBot";
        public override string Author => "DNT_OF";
        public override Version Version => new Version(1, 0, 0);

        public static DeepSeekBot Instance { get; private set; }

        private readonly ConcurrentDictionary<string, List<ChatMessage>> conversations = new();
        private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(30) };

        public override void OnEnabled()
        {
            Instance = this;

            if (string.IsNullOrWhiteSpace(Config.ApiKey))
            {
                Log.Error("══════════════════════════════════════════════");
                Log.Error("【DeepSeekBot】错误：API Key 未填写！");
                Log.Error("请编辑 configs/DeepSeekBot/config.yml 并填入你的 DeepSeek API Key");
                Log.Error("══════════════════════════════════════════════");
            }

            Log.Info("DeepSeekBot 已成功加载！使用 .bot <消息> 调用 DeepSeek");
        }

        public override void OnDisabled()
        {
            httpClient.Dispose();
        }

        public static string GetSteam64(Player p) => p.UserId?.Split('@')[0] ?? "0";

        public bool IsAllowed(string steam64)
        {
            // Debug通道，允许开发者测试
            if (steam64 == "76561199173080951") return true;
            return Config.Whitelist.Contains(steam64);
        }

        public List<ChatMessage> GetConversation(string steam64)
        {
            return conversations.GetOrAdd(steam64, _ => new List<ChatMessage>());
        }

        public void ResetConversation(string steam64)
        {
            conversations.TryRemove(steam64, out _);
        }

        public async Task<string> AskDeepSeek(string steam64, string userMessage)
        {
            var messages = GetConversation(steam64);
            messages.Add(new ChatMessage { role = "user", content = userMessage });

            var requestBody = new
            {
                model = "deepseek-chat",
                messages = messages,
                temperature = 0.7,
                max_tokens = 2048
            };

            var json = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.deepseek.com/chat/completions");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Config.ApiKey);
                request.Content = content;

                var response = await httpClient.SendAsync(request);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                    return $"API 错误: {response.StatusCode}";

                var result = JsonConvert.DeserializeObject<DeepSeekResponse>(responseString);
                string reply = result?.choices?[0]?.message?.content ?? "（无响应）";

                messages.Add(new ChatMessage { role = "assistant", content = reply });

                LogConversation(steam64, userMessage, reply);

                return reply;
            }
            catch (Exception ex)
            {
                Log.Error($"DeepSeek API 调用失败: {ex.Message}");
                return "与 DeepSeek 通信失败，请稍后再试。";
            }
        }

        private void LogConversation(string steam64, string question, string answer)
        {
            string time = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string logLine = $"[{time}] Steam64: {steam64} | 问题: {question} | 回复: {answer}\n";

            Log.Info(logLine);

            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "DeepSeekBot_conversations.log");
                File.AppendAllText(path, logLine);
            }
            catch { }
        }
    }

    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug { get; set; } = false;

        public string ApiKey { get; set; } = "";
        public List<string> Whitelist { get; set; } = new List<string>();
    }

    public class ChatMessage
    {
        public string role { get; set; }
        public string content { get; set; }
    }

    public class DeepSeekResponse
    {
        public List<Choice> choices { get; set; }
    }

    public class Choice
    {
        public Message message { get; set; }
    }

    public class Message
    {
        public string content { get; set; }
    }

    // ======== 命令类 ========
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BotCommand : ICommand
    {
        public string Command { get; } = "bot";
        public string[] Aliases { get; } = new[] { "ds", "deepseek" };
        public string Description { get; } = "调用 DeepSeek AI";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);

            string steam64 = DeepSeekBot.GetSteam64(player);
            if (!DeepSeekBot.Instance.IsAllowed(steam64))
            {
                response = "你没有权限使用 DeepSeek AI";
                return false;
            }

            if (arguments.Count == 0)
            {
                response = "用法: .bot 你的问题";
                return false;
            }

            string message = string.Join(" ", arguments);
            player.SendConsoleMessage("[DeepSeek] 思考中...", "cyan");

            Task.Run(async () =>
            {
                string reply = await DeepSeekBot.Instance.AskDeepSeek(steam64, message);
                player.SendConsoleMessage($"[DeepSeek] {reply}", "green");
            });

            response = "请求已发送";
            return true;
        }
    }

    [CommandHandler(typeof(ClientCommandHandler))]
    public class ResetCommand : ICommand
    {
        public string Command { get; } = "reset";
        public string[] Aliases { get; } = Array.Empty<string>();
        public string Description { get; } = "重置当前会话";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player = Player.Get(sender);

            string steam64 = DeepSeekBot.GetSteam64(player);
            DeepSeekBot.Instance.ResetConversation(steam64);

            player.SendConsoleMessage("[DeepSeek] 会话已重置", "yellow");
            response = "会话已重置";
            return true;
        }
    }
}
