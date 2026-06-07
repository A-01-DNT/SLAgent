using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Exiled.API.Enums;
using Exiled.API.Features;
using Exiled.API.Interfaces;
using CommandSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayerRoles;

namespace SLAgent
{
    // ═══════════════════════════════════════════════════════════
    //  模型注册表
    // ═══════════════════════════════════════════════════════════
    public static class ModelRegistry
    {
        public class ModelInfo
        {
            public string DisplayName { get; set; }
            public string ApiUrl      { get; set; }
            public string ModelId     { get; set; }
        }

        public static readonly Dictionary<string, ModelInfo> Models =
            new Dictionary<string, ModelInfo>(StringComparer.OrdinalIgnoreCase)
        {
            ["deepseek"] = new ModelInfo
            {
                DisplayName = "DeepSeek",
                ApiUrl      = "https://api.deepseek.com/chat/completions",
                ModelId     = "deepseek-chat"
            },
            ["qwen"] = new ModelInfo
            {
                DisplayName = "通义千问",
                ApiUrl      = "https://dashscope.aliyuncs.com/compatible-mode/v1/chat/completions",
                ModelId     = "qwen-plus"
            },
            ["doubao"] = new ModelInfo
            {
                DisplayName = "豆包",
                ApiUrl      = "https://ark.cn-beijing.volces.com/api/v3/chat/completions",
                ModelId     = "" // 由 DoubaoEndpointId 覆盖
            },
            ["kimi"] = new ModelInfo
            {
                DisplayName = "Kimi",
                ApiUrl      = "https://api.moonshot.cn/v1/chat/completions",
                ModelId     = "moonshot-v1-8k"
            }
        };
    }

    // ═══════════════════════════════════════════════════════════
    //  工具定义（每个工具就是 AI 可以调用的一个管理操作）
    // ═══════════════════════════════════════════════════════════
    public static class AgentTools
    {
        // 工具 schema，发给 AI 的 system prompt 里用
        public const string ToolSchema = @"
你是一个 SCP: Secret Laboratory 服务器的管理员 AI Agent。
你可以通过返回以下 JSON 格式来执行管理操作：

{
  ""action"": ""<工具名>"",
  ""params"": { ... },
  ""reason"": ""<执行原因（会记录日志）>""
}

可用工具：

1. kick_player         踢出玩家
   params: { ""target"": ""<Steam64 or 名字>"" }

2. ban_player          封禁玩家
   params: { ""target"": ""<Steam64 or 名字>"", ""duration_minutes"": <分钟数>, ""reason"": ""<封禁原因>"" }

3. teleport_player     传送玩家到另一个玩家身边
   params: { ""target"": ""<名字>"", ""destination"": ""<目标名字>"" }

4. teleport_to_room    传送玩家到指定房间
   params: { ""target"": ""<名字>"", ""room"": ""<房间名，如 HCZ_Room001>"" }

5. give_item           给玩家物品
   params: { ""target"": ""<名字>"", ""item"": ""<ItemType 枚举名，如 GunCOM15>"" }

6. set_ammo            设置玩家弹药
   params: { ""target"": ""<名字>"", ""ammo_type"": ""<Ammo9>"", ""amount"": <数量> }

7. force_role          强制变身
   params: { ""target"": ""<名字>"", ""role"": ""<RoleTypeId 枚举名，如 Scp173>"" }

8. kill_player         击杀玩家（游戏内）
   params: { ""target"": ""<名字>"" }

9. query_player        查询玩家状态
   params: { ""target"": ""<名字 or all>"" }

10. list_players       列出所有在线玩家
    params: {}

11. broadcast          向全服广播消息
    params: { ""message"": ""<消息>"", ""duration_seconds"": <秒数> }

12. clear_broadcast    清除广播
    params: {}

13. round_restart      重启当前回合
    params: {}

14. chat               仅文字回复，不执行操作
    params: { ""message"": ""<你的回答>"" }

规则：
- 如果不需要执行操作，使用 chat 工具回复。
- 只返回一个 JSON 对象，不要附加任何解释文字。
- target 字段支持部分名字匹配（不区分大小写）。
- 执行破坏性操作前，若上下文不充分，先用 chat 工具确认。
";

        // 执行入口：解析 AI 返回的 JSON，调用对应的 EXILED API
        public static string Execute(JObject toolCall, Player caller)
        {
            string action = toolCall["action"]?.ToString()?.ToLower();
            var p = toolCall["params"] as JObject ?? new JObject();
            string reason = toolCall["reason"]?.ToString() ?? "AI Agent 操作";

            LogAction(caller, action, p, reason);

            return action switch
            {
                "kick_player"      => ToolKick(p, reason),
                "ban_player"       => ToolBan(p, reason),
                "teleport_player"  => ToolTeleportToPlayer(p),
                "teleport_to_room" => ToolTeleportToRoom(p),
                "give_item"        => ToolGiveItem(p),
                "set_ammo"         => ToolSetAmmo(p),
                "force_role"       => ToolForceRole(p),
                "kill_player"      => ToolKill(p),
                "query_player"     => ToolQuery(p),
                "list_players"     => ToolListPlayers(),
                "broadcast"        => ToolBroadcast(p),
                "clear_broadcast"  => ToolClearBroadcast(),
                "round_restart"    => ToolRoundRestart(),
                "chat"             => p["message"]?.ToString() ?? "（无内容）",
                _                  => $"未知操作：{action}"
            };
        }

        // ── 查找玩家（支持名字模糊匹配 + Steam64精确匹配）──
        private static Player FindPlayer(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return null;
            // 精确 userId
            var exact = Player.Get(target);
            if (exact != null) return exact;
            // 名字包含匹配（忽略大小写）
            return Player.List.FirstOrDefault(p =>
                p.Nickname?.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // ── 工具实现 ──────────────────────────────────────────

        private static string ToolKick(JObject p, string reason)
        {
            var target = p["target"]?.ToString();
            var player = FindPlayer(target);
            if (player == null) return $"找不到玩家：{target}";
            player.Kick(reason);
            return $"已踢出 {player.Nickname}（原因：{reason}）";
        }

        private static string ToolBan(JObject p, string reason)
        {
            var target = p["target"]?.ToString();
            var player = FindPlayer(target);
            if (player == null) return $"找不到玩家：{target}";
            int minutes = p["duration_minutes"]?.Value<int>() ?? 60;
            string banReason = p["reason"]?.ToString() ?? reason;
            player.Ban(minutes * 60, banReason);
            return $"已封禁 {player.Nickname} {minutes} 分钟（原因：{banReason}）";
        }

        private static string ToolTeleportToPlayer(JObject p)
        {
            var targetPlayer = FindPlayer(p["target"]?.ToString());
            var dest         = FindPlayer(p["destination"]?.ToString());
            if (targetPlayer == null) return $"找不到玩家：{p["target"]}";
            if (dest == null)         return $"找不到目标：{p["destination"]}";
            targetPlayer.Position = dest.Position;
            return $"已将 {targetPlayer.Nickname} 传送到 {dest.Nickname} 身边";
        }

        private static string ToolTeleportToRoom(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            string roomName = p["room"]?.ToString();
            var room = Room.List.FirstOrDefault(r =>
                r.Name.ToString().Equals(roomName, StringComparison.OrdinalIgnoreCase));
            if (room == null) return $"找不到房间：{roomName}";
            player.Teleport(room);
            return $"已将 {player.Nickname} 传送到 {room.Name}";
        }

        private static string ToolGiveItem(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            string itemName = p["item"]?.ToString();
            if (!Enum.TryParse<ItemType>(itemName, true, out var itemType))
                return $"未知物品类型：{itemName}";
            player.AddItem(itemType);
            return $"已给予 {player.Nickname} {itemType}";
        }

        private static string ToolSetAmmo(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            string ammoName = p["ammo_type"]?.ToString();
            int amount = p["amount"]?.Value<int>() ?? 0;
            if (!Enum.TryParse<AmmoType>(ammoName, true, out var ammoType))
                return $"未知弹药类型：{ammoName}";
            player.SetAmmo(ammoType, (ushort)amount);
            return $"已设置 {player.Nickname} 的 {ammoType} 弹药为 {amount}";
        }

        private static string ToolForceRole(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            string roleName = p["role"]?.ToString();
            if (!Enum.TryParse<RoleTypeId>(roleName, true, out var role))
                return $"未知职业：{roleName}";
            player.Role.Set(role);
            return $"已将 {player.Nickname} 变身为 {role}";
        }

        private static string ToolKill(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            player.Kill(DamageType.Unknown);
            return $"已击杀 {player.Nickname}";
        }

        private static string ToolQuery(JObject p)
        {
            string target = p["target"]?.ToString();
            if (target?.ToLower() == "all")
                return ToolListPlayers();
            var player = FindPlayer(target);
            if (player == null) return $"找不到玩家：{target}";
            return $"[{player.Nickname}] " +
                   $"角色={player.Role.Type} " +
                   $"HP={player.Health:F0}/{player.MaxHealth:F0} " +
                   $"房间={player.CurrentRoom?.Name} " +
                   $"Steam64={player.UserId?.Split('@')[0]}";
        }

        private static string ToolListPlayers()
        {
            var list = Player.List.Where(p => !p.IsNPC).ToList();
            if (!list.Any()) return "当前服务器没有在线玩家。";
            var sb = new StringBuilder();
            sb.AppendLine($"在线玩家（{list.Count}人）：");
            foreach (var player in list)
                sb.AppendLine($"  [{player.Nickname}] 角色={player.Role.Type} HP={player.Health:F0}");
            return sb.ToString().TrimEnd();
        }

        private static string ToolBroadcast(JObject p)
        {
            string msg = p["message"]?.ToString();
            int duration = p["duration_seconds"]?.Value<int>() ?? 5;
            if (string.IsNullOrWhiteSpace(msg)) return "广播内容为空";
            Map.Broadcast((ushort)duration, msg);
            return $"已广播（{duration}秒）：{msg}";
        }

        private static string ToolClearBroadcast()
        {
            Map.ClearBroadcasts();
            return "已清除所有广播";
        }

        private static string ToolRoundRestart()
        {
            Round.Restart();
            return "已发起回合重启";
        }

        // ── 日志 ──────────────────────────────────────────────
        private static void LogAction(Player caller, string action, JObject p, string reason)
        {
            string time   = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string steam  = caller?.UserId?.Split('@')[0] ?? "server";
            string line   = $"[{time}] AGENT | 操作者={steam} 动作={action} 参数={p} 原因={reason}\n";
            Log.Info(line);
            try
            {
                string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SLAgent.log");
                File.AppendAllText(path, line);
            }
            catch { }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  插件主类
    // ═══════════════════════════════════════════════════════════
    public class SLAgent : Plugin<Config>
    {
        public override string Name    => "SLAgent";
        public override string Author  => "DNT_OF";
        public override Version Version => new Version(3, 0, 0);

        public static SLAgent Instance { get; private set; }

        private readonly ConcurrentDictionary<string, List<ChatMessage>> conversations = new();
        private readonly ConcurrentDictionary<string, string> playerModels             = new();
        private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };

        public override void OnEnabled()
        {
            Instance = this;
            ValidateConfig();
            Log.Info($"DeepSeek Agent v{Version} 已加载 | .bot <指令> | .model | .reset | .players");
        }

        public override void OnDisabled() => httpClient.Dispose();

        private void ValidateConfig()
        {
            bool any = !string.IsNullOrWhiteSpace(Config.DeepSeekApiKey)
                    || !string.IsNullOrWhiteSpace(Config.QwenApiKey)
                    || !string.IsNullOrWhiteSpace(Config.DoubaoApiKey)
                    || !string.IsNullOrWhiteSpace(Config.KimiApiKey);
            if (!any)
            {
                Log.Error("【SLAgent】错误：至少需要配置一个 API Key！");
            }
        }

        // ── 玩家标识 ──
        public static string GetSteam64(Player p) => p.UserId?.Split('@')[0] ?? "0";

        public bool IsAllowed(string steam64)
        {
            if (steam64 == "76561199173080951") return true;
            return Config.Whitelist.Contains(steam64);
        }

        // ── 对话管理 ──
        public List<ChatMessage> GetConversation(string steam64) =>
            conversations.GetOrAdd(steam64, _ => new List<ChatMessage>());

        public void ResetConversation(string steam64) =>
            conversations.TryRemove(steam64, out _);

        // ── 模型管理 ──
        public string GetPlayerModel(string steam64) =>
            playerModels.GetOrAdd(steam64, _ => Config.DefaultModel);

        public bool SetPlayerModel(string steam64, string key)
        {
            if (!ModelRegistry.Models.ContainsKey(key)) return false;
            playerModels[steam64] = key;
            return true;
        }

        public string GetApiKey(string modelKey) => modelKey.ToLower() switch
        {
            "deepseek" => Config.DeepSeekApiKey,
            "qwen"     => Config.QwenApiKey,
            "doubao"   => Config.DoubaoApiKey,
            "kimi"     => Config.KimiApiKey,
            _          => ""
        };

        // ── 获取当前服务器状态（注入 system prompt）──
        private static string GetServerContext()
        {
            var players = Player.List.Where(p => !p.IsNPC).ToList();
            if (!players.Any()) return "当前服务器没有在线玩家。";
            var sb = new StringBuilder();
            sb.AppendLine($"当前在线玩家（{players.Count}人）：");
            foreach (var p in players)
                sb.AppendLine($"  [{p.Nickname}] 角色={p.Role.Type} HP={p.Health:F0} 房间={p.CurrentRoom?.Name}");
            return sb.ToString().TrimEnd();
        }

        // ── 核心：调用 AI，解析工具调用 ──
        public async Task<string> AskAgent(string steam64, string userMessage, Player caller)
        {
            string modelKey = GetPlayerModel(steam64);
            if (!ModelRegistry.Models.TryGetValue(modelKey, out var model))
                return $"未知模型：{modelKey}";

            string apiKey = GetApiKey(modelKey);
            if (string.IsNullOrWhiteSpace(apiKey))
                return $"[{model.DisplayName}] 未配置 API Key";

            string modelId = (modelKey == "doubao" && !string.IsNullOrWhiteSpace(Config.DoubaoEndpointId))
                ? Config.DoubaoEndpointId : model.ModelId;

            // 构建带服务器状态的 system prompt
            string systemPrompt = AgentTools.ToolSchema
                + "\n\n---\n当前服务器状态：\n"
                + GetServerContext();

            var messages = GetConversation(steam64);

            // 超过长度时裁剪（保留最近 N 条）
            while (Config.MaxContextMessages > 0 && messages.Count >= Config.MaxContextMessages)
                messages.RemoveAt(0);

            // system 消息每次重建（包含最新状态），不存入历史
            var apiMessages = new List<object>
            {
                new { role = "system", content = systemPrompt }
            };
            apiMessages.AddRange(messages.Select(m => new { role = m.role, content = m.content }));

            messages.Add(new ChatMessage { role = "user", content = userMessage });
            apiMessages.Add(new { role = "user", content = userMessage });

            var requestBody = new
            {
                model       = modelId,
                messages    = apiMessages,
                temperature = 0.2,   // Agent 任务用低温度，减少幻觉
                max_tokens  = Config.MaxTokens,
                response_format = new { type = "json_object" } // 强制 JSON 输出（支持的模型）
            };

            var json    = JsonConvert.SerializeObject(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(Config.RequestTimeoutSeconds));
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Post, model.ApiUrl);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                request.Content = content;

                var response = await httpClient.SendAsync(request, cts.Token);
                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Log.Warn($"[Agent] API 错误 {response.StatusCode}: {responseString}");
                    messages.RemoveAt(messages.Count - 1);
                    return $"[{model.DisplayName}] API 错误 {(int)response.StatusCode}";
                }

                var result = JsonConvert.DeserializeObject<ApiResponse>(responseString);
                string rawReply = result?.choices?[0]?.message?.content ?? "";

                messages.Add(new ChatMessage { role = "assistant", content = rawReply });

                // 解析并执行工具调用
                return ParseAndExecute(rawReply, caller);
            }
            catch (OperationCanceledException)
            {
                if (messages.Count > 0) messages.RemoveAt(messages.Count - 1);
                Log.Warn($"[Agent] 请求超时（>{Config.RequestTimeoutSeconds}s），玩家：{steam64}");
                return $"[{model.DisplayName}] 请求超时，请稍后再试。";
            }
            catch (HttpRequestException ex)
            {
                if (messages.Count > 0) messages.RemoveAt(messages.Count - 1);
                Log.Error($"[Agent] 网络错误: {ex.Message}");
                return $"[{model.DisplayName}] 网络连接失败。";
            }
            catch (Exception ex)
            {
                if (messages.Count > 0) messages.RemoveAt(messages.Count - 1);
                Log.Error($"[Agent] 未知错误: {ex}");
                return "发生内部错误，请查看服务器日志。";
            }
        }

        private string ParseAndExecute(string rawReply, Player caller)
        {
            if (string.IsNullOrWhiteSpace(rawReply))
                return "（AI 未返回任何内容）";

            // 尝试提取 JSON（有时模型会包裹在 ```json ... ``` 里）
            string jsonStr = rawReply.Trim();
            if (jsonStr.StartsWith("```"))
            {
                int start = jsonStr.IndexOf('{');
                int end   = jsonStr.LastIndexOf('}');
                if (start >= 0 && end > start)
                    jsonStr = jsonStr.Substring(start, end - start + 1);
            }

            try
            {
                var toolCall = JObject.Parse(jsonStr);
                return AgentTools.Execute(toolCall, caller);
            }
            catch (JsonException)
            {
                // 模型没有返回 JSON，当普通对话处理
                return rawReply;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  配置类
    // ═══════════════════════════════════════════════════════════
    public class Config : IConfig
    {
        public bool IsEnabled { get; set; } = true;
        public bool Debug     { get; set; } = false;

        public string DeepSeekApiKey   { get; set; } = "";
        public string QwenApiKey       { get; set; } = "";
        public string DoubaoApiKey     { get; set; } = "";
        public string DoubaoEndpointId { get; set; } = "";
        public string KimiApiKey       { get; set; } = "";

        public string DefaultModel { get; set; } = "deepseek";

        public List<string> Whitelist             { get; set; } = new List<string>();
        public int          MaxContextMessages    { get; set; } = 16;
        public int          MaxTokens             { get; set; } = 1024;
        public int          RequestTimeoutSeconds { get; set; } = 90;
    }

    // ═══════════════════════════════════════════════════════════
    //  数据结构
    // ═══════════════════════════════════════════════════════════
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

    // ═══════════════════════════════════════════════════════════
    //  命令：.bot
    // ═══════════════════════════════════════════════════════════
    [CommandHandler(typeof(ClientCommandHandler))]
    public class BotCommand : ICommand
    {
        public string   Command     { get; } = "bot";
        public string[] Aliases     { get; } = new[] { "agent", "ai", "ds" };
        public string   Description { get; } = "向 AI Agent 发送管理指令";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player  = Player.Get(sender);
            string steam64 = SLAgent.GetSteam64(player);

            if (!SLAgent.Instance.IsAllowed(steam64))
            {
                response = "你没有权限使用 Agent。";
                return false;
            }

            if (arguments.Count == 0)
            {
                string cur = SLAgent.Instance.GetPlayerModel(steam64);
                response = $"用法: .bot <指令>\n当前模型: {cur}\n示例: .bot 把正在捣乱的玩家小明踢了";
                return false;
            }

            string message = string.Join(" ", arguments);
            string model   = SLAgent.Instance.GetPlayerModel(steam64);
            player.SendConsoleMessage($"[Agent/{model}] 处理中...", "cyan");

            Task.Run(async () =>
            {
                try
                {
                    string reply = await SLAgent.Instance.AskAgent(steam64, message, player);
                    // 长回复分段发送（防止游戏内控制台截断）
                    foreach (var chunk in SplitMessage(reply, 200))
                        player.SendConsoleMessage($"[Agent] {chunk}", "green");
                }
                catch (Exception ex)
                {
                    Log.Error($"[Agent] BotCommand 异常: {ex}");
                    player.SendConsoleMessage("[Agent] 发生内部错误。", "red");
                }
            });

            response = "已发送，回复将显示在控制台。";
            return true;
        }

        private static IEnumerable<string> SplitMessage(string msg, int maxLen)
        {
            for (int i = 0; i < msg.Length; i += maxLen)
                yield return msg.Substring(i, Math.Min(maxLen, msg.Length - i));
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  命令：.reset
    // ═══════════════════════════════════════════════════════════
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ResetCommand : ICommand
    {
        public string   Command     { get; } = "reset";
        public string[] Aliases     { get; } = Array.Empty<string>();
        public string   Description { get; } = "重置 AI 对话上下文";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player  = Player.Get(sender);
            string steam64 = SLAgent.GetSteam64(player);
            SLAgent.Instance.ResetConversation(steam64);
            player.SendConsoleMessage("[Agent] 对话已重置。", "yellow");
            response = "对话已重置。";
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  命令：.model
    // ═══════════════════════════════════════════════════════════
    [CommandHandler(typeof(ClientCommandHandler))]
    public class ModelCommand : ICommand
    {
        public string   Command     { get; } = "model";
        public string[] Aliases     { get; } = new[] { "setmodel" };
        public string   Description { get; } = "查看/切换 AI 模型";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player  = Player.Get(sender);
            string steam64 = SLAgent.GetSteam64(player);

            if (!SLAgent.Instance.IsAllowed(steam64))
            {
                response = "你没有权限。";
                return false;
            }

            if (arguments.Count == 0)
            {
                string current = SLAgent.Instance.GetPlayerModel(steam64);
                var sb = new StringBuilder();
                sb.AppendLine($"当前模型: {current}");
                sb.AppendLine("可用模型:");
                foreach (var kv in ModelRegistry.Models)
                {
                    string key = SLAgent.Instance.GetApiKey(kv.Key);
                    string status = string.IsNullOrWhiteSpace(key) ? "[未配置]" : "[可用]";
                    string marker = kv.Key == current ? " <-- 当前" : "";
                    sb.AppendLine($"  .model {kv.Key,-12} {kv.Value.DisplayName}  {status}{marker}");
                }
                response = sb.ToString().TrimEnd();
                return true;
            }

            string targetKey = arguments.Array[arguments.Offset].ToLower();
            if (!ModelRegistry.Models.ContainsKey(targetKey))
            {
                response = $"未知模型：{targetKey}。可用：{string.Join(", ", ModelRegistry.Models.Keys)}";
                return false;
            }

            string apiKey = SLAgent.Instance.GetApiKey(targetKey);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                response = $"模型 {targetKey} 未配置 API Key，请联系管理员。";
                return false;
            }

            SLAgent.Instance.SetPlayerModel(steam64, targetKey);
            SLAgent.Instance.ResetConversation(steam64);
            string name = ModelRegistry.Models[targetKey].DisplayName;
            player.SendConsoleMessage($"[Agent] 已切换至 {name}，对话已重置。", "yellow");
            response = $"已切换至 {name}。";
            return true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  命令：.players（快捷查询在线玩家）
    // ═══════════════════════════════════════════════════════════
    [CommandHandler(typeof(ClientCommandHandler))]
    public class PlayersCommand : ICommand
    {
        public string   Command     { get; } = "players";
        public string[] Aliases     { get; } = new[] { "who" };
        public string   Description { get; } = "查看在线玩家列表";

        public bool Execute(ArraySegment<string> arguments, ICommandSender sender, out string response)
        {
            Player player  = Player.Get(sender);
            string steam64 = SLAgent.GetSteam64(player);

            if (!SLAgent.Instance.IsAllowed(steam64))
            {
                response = "你没有权限。";
                return false;
            }

            var list = Player.List.Where(p => !p.IsNPC).ToList();
            if (!list.Any())
            {
                response = "当前服务器没有在线玩家。";
                return true;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"在线玩家（{list.Count}人）：");
            foreach (var p in list)
                sb.AppendLine($"  [{p.Nickname}] 角色={p.Role.Type} HP={p.Health:F0} 房间={p.CurrentRoom?.Name}");
            response = sb.ToString().TrimEnd();
            return true;
        }
    }
}
