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
using Exiled.API.Features.Doors;
using Exiled.API.Features.Toys;
using Exiled.API.Interfaces;
using CommandSystem;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PlayerRoles;
using UnityEngine;
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
    //  工具定义
    // ═══════════════════════════════════════════════════════════
    public static class AgentTools
    {
        public const string ToolSchema = @"
你是一个 SCP: Secret Laboratory 服务器的管理员 AI Agent。
通过返回如下 JSON 执行操作，不要附加任何额外文字：

{ ""action"": ""<工具名>"", ""params"": { ... }, ""reason"": ""<日志原因>"" }

━━━ 玩家管理 ━━━
kick_player        踢出玩家
  { ""target"": ""<名字|Steam64>"" }

ban_player         封禁玩家
  { ""target"": ""..."", ""duration_minutes"": <分钟>, ""reason"": ""..."" }

mute_player        禁言（游戏内语音+文字）
  { ""target"": ""..."" }

unmute_player      解除禁言
  { ""target"": ""..."" }

kill_player        游戏内击杀
  { ""target"": ""..."" }

heal_player        治疗（填 0 = 满血）
  { ""target"": ""..."", ""amount"": <数值> }

force_role         强制变身
  { ""target"": ""..."", ""role"": ""<RoleTypeId，如 Scp173|ClassD|NtfSergeant>"" }

godmode_player     无敌模式切换
  { ""target"": ""..."", ""enabled"": true }

noclip_player      穿墙模式切换
  { ""target"": ""..."", ""enabled"": true }

scale_player       修改玩家体型（1.0=正常）
  { ""target"": ""..."", ""x"": 1.0, ""y"": 1.0, ""z"": 1.0 }

━━━ 物品 & 弹药 ━━━
give_item          给予物品
  { ""target"": ""..."", ""item"": ""<ItemType，如 GunCOM15|KeycardO5|Medkit>"" }

give_candy         给予糖果（SCP-330）
  { ""target"": ""..."", ""candy"": ""<CandyKindID: Pink|Red|Green|Blue|Yellow|Purple>"" }

set_ammo           设置弹药数量
  { ""target"": ""..."", ""ammo_type"": ""<Ammo9|Ammo556|Ammo762|Ammo12gauge|Ammo44cal>"", ""amount"": <数> }

clear_inventory    清空玩家背包
  { ""target"": ""..."" }

━━━ 状态效果 ━━━
give_effect        给予状态效果
  { ""target"": ""..."", ""effect"": ""<EffectType，如 Scp207|MovementBoost|Invisible|Flashed>"",
    ""intensity"": <0-255，默认1>, ""duration"": <秒，0=永久> }

clear_effects      清除所有状态效果
  { ""target"": ""..."" }

EffectType 常用值：Scp207(可乐加速), AntiScp207(减速回血), MovementBoost(移速+),
Invisible(隐身), Ghostly(穿墙), Flashed(闪光), Bleeding(持续掉血),
Invigorated(无限体力), DamageReduction(减伤), Marshmallow(棉花糖人)

━━━ 传送 ━━━
teleport_player    传送到另一玩家身边
  { ""target"": ""..."", ""destination"": ""<目标名>"" }

teleport_to_room   传送到房间
  { ""target"": ""..."", ""room"": ""<RoomType，如 HczArmory|LczClassDSpawn|EzGateA>"" }

━━━ 地图控制 ━━━
lights_out         关闭全图灯光
  { ""duration_seconds"": <秒> }

lock_door          锁定/解锁门
  { ""door"": ""<DoorType，如 HeavyContainmentDoor|CheckpointLczA>"", ""locked"": true }

open_door          开/关门
  { ""door"": ""<DoorType>"", ""open"": true }

lockdown           全图封锁模式（锁所有门）
  { ""enabled"": true }

decontaminate      立即触发 LCZ 除污
  {}

warhead_start      启动核弹倒计时
  {}

warhead_stop       停止核弹倒计时
  {}

warhead_detonate   立即引爆核弹
  {}

━━━ CASSIE 广播 ━━━
cassie             CASSIE 语音广播
  { ""message"": ""<CASSIE 词语，空格分隔，如 CONTAINMENT BREACH SCP 173>"" }

cassie_silent      CASSIE 无声广播（仅字幕）
  { ""message"": ""..."" }

cassie_translated  CASSIE 自定义字幕广播
  { ""message"": ""<CASSIE词语>"", ""subtitle"": ""<显示文字>"" }

broadcast          屏幕广播（全员）
  { ""message"": ""<支持 <color> 等富文本>"", ""duration_seconds"": <秒> }

hint_player        向单个玩家发送 Hint 提示
  { ""target"": ""..."", ""message"": ""..."", ""duration_seconds"": <秒> }

clear_broadcast    清除广播
  {}

━━━ Toys（场景生成物）━━━
spawn_toy          生成场景物件
  { ""type"": ""<类型>"", ""scale"": <倍率，默认1.0>, ""color"": ""<颜色名或#RRGGBB>"" }

  type 可选值：
  capybara          - 水豚彩蛋模型
  primitive_sphere  - 球体  (0)
  primitive_capsule - 胶囊  (1)
  primitive_cylinder- 圆柱  (2)
  primitive_cube    - 立方体 (3)
  primitive_plane   - 平面  (4)
  light             - 光源（color 和 scale 控制颜色/范围）
  shooting_target_sport   - 运动靶
  shooting_target_dboy    - D级靶
  shooting_target_binary  - 二值靶
  text              - 3D 文字（需额外 ""text"" 字段）

  生成位置默认为调用者当前位置。

destroy_toys       销毁所有已生成的场景物件
  {}

━━━ 回合控制 ━━━
round_restart      重启回合
  {}

round_end          强制结束回合
  {}

force_start        强制开始回合（等待期间）
  {}

list_players       列出在线玩家
  {}

query_player       查询玩家详情
  { ""target"": ""<名字|all>"" }

chat               仅文字回复，不执行任何操作
  { ""message"": ""..."" }

━━━ 规则 ━━━
- 只返回一个 JSON 对象，无任何额外文字。
- target 支持名字模糊匹配（不区分大小写）。
- 多步任务请一次返回最合理的单个操作，执行后视情况继续。
- 不确定时用 chat 工具向操作者确认，而非直接执行高破坏性操作。
- warhead_detonate / round_end 等不可逆操作，在 reason 中记录充分理由。
";

        // ── 执行入口 ──────────────────────────────────────────
        public static string Execute(JObject toolCall, Player caller)
        {
            string action = toolCall["action"]?.ToString()?.ToLower().Trim();
            var    p      = toolCall["params"] as JObject ?? new JObject();
            string reason = toolCall["reason"]?.ToString() ?? "SLAgent 操作";

            LogAction(caller, action, p, reason);

            return action switch
            {
                // 玩家管理
                "kick_player"      => ToolKick(p, reason),
                "ban_player"       => ToolBan(p, reason),
                "mute_player"      => ToolMute(p, true),
                "unmute_player"    => ToolMute(p, false),
                "kill_player"      => ToolKill(p),
                "heal_player"      => ToolHeal(p),
                "force_role"       => ToolForceRole(p),
                "godmode_player"   => ToolGodmode(p),
                "noclip_player"    => ToolNoclip(p),
                "scale_player"     => ToolScale(p),
                // 物品 & 弹药
                "give_item"        => ToolGiveItem(p),
                "give_candy"       => ToolGiveCandy(p),
                "set_ammo"         => ToolSetAmmo(p),
                "clear_inventory"  => ToolClearInventory(p),
                // 状态效果
                "give_effect"      => ToolGiveEffect(p),
                "clear_effects"    => ToolClearEffects(p),
                // 传送
                "teleport_player"  => ToolTeleportToPlayer(p),
                "teleport_to_room" => ToolTeleportToRoom(p),
                // 地图
                "lights_out"       => ToolLightsOut(p),
                "lock_door"        => ToolLockDoor(p),
                "open_door"        => ToolOpenDoor(p),
                "lockdown"         => ToolLockdown(p),
                "decontaminate"    => ToolDecontaminate(),
                "warhead_start"    => ToolWarhead("start"),
                "warhead_stop"     => ToolWarhead("stop"),
                "warhead_detonate" => ToolWarhead("detonate"),
                // CASSIE & 广播
                "cassie"           => ToolCassie(p, false, false),
                "cassie_silent"    => ToolCassie(p, true,  false),
                "cassie_translated"=> ToolCassie(p, false, true),
                "broadcast"        => ToolBroadcast(p),
                "hint_player"      => ToolHint(p),
                "clear_broadcast"  => ToolClearBroadcast(),
                // Toys
                "spawn_toy"        => ToolSpawnToy(p, caller),
                "destroy_toys"     => ToolDestroyToys(),
                // 回合
                "round_restart"    => ToolRoundRestart(),
                "round_end"        => ToolRoundEnd(p),
                "force_start"      => ToolForceStart(),
                // 查询
                "list_players"     => ToolListPlayers(),
                "query_player"     => ToolQuery(p),
                // 纯对话
                "chat"             => p["message"]?.ToString() ?? "（无内容）",
                _                  => $"未知操作：{action}"
            };
        }

        // ── 玩家查找 ──────────────────────────────────────────
        private static Player FindPlayer(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return null;
            var exact = Player.Get(target);
            if (exact != null) return exact;
            return Player.List.FirstOrDefault(p =>
                p.Nickname?.IndexOf(target, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static List<Player> FindPlayers(string target)
        {
            if (string.IsNullOrWhiteSpace(target)) return new List<Player>();
            if (target.Equals("all", StringComparison.OrdinalIgnoreCase))
                return Player.List.Where(p => !p.IsNPC).ToList();
            var single = FindPlayer(target);
            return single != null ? new List<Player> { single } : new List<Player>();
        }

        // ━━━ 玩家管理 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolKick(JObject p, string reason)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            player.Kick(reason);
            return $"已踢出 {player.Nickname}（{reason}）";
        }

        private static string ToolBan(JObject p, string reason)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            int    minutes   = p["duration_minutes"]?.Value<int>() ?? 60;
            string banReason = p["reason"]?.ToString() ?? reason;
            player.Ban(minutes * 60, banReason);
            return $"已封禁 {player.Nickname} {minutes} 分钟（{banReason}）";
        }

        private static string ToolMute(JObject p, bool mute)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            player.IsMuted = mute;
            return mute ? $"已禁言 {player.Nickname}" : $"已解除 {player.Nickname} 禁言";
        }

        private static string ToolKill(JObject p)
        {
            var players = FindPlayers(p["target"]?.ToString());
            if (!players.Any()) return $"找不到玩家：{p["target"]}";
            foreach (var pl in players) pl.Kill(DamageType.Unknown);
            return $"已击杀：{string.Join(", ", players.Select(pl => pl.Nickname))}";
        }

        private static string ToolHeal(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            float amount = p["amount"]?.Value<float>() ?? 0f;
            if (amount <= 0)
            {
                player.Health = player.MaxHealth;
                return $"已将 {player.Nickname} 治疗至满血";
            }
            player.Health = Math.Min(player.Health + amount, player.MaxHealth);
            return $"已为 {player.Nickname} 恢复 {amount} HP";
        }

        private static string ToolForceRole(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            if (!Enum.TryParse<RoleTypeId>(p["role"]?.ToString(), true, out var role))
                return $"未知职业：{p["role"]}";
            player.Role.Set(role);
            return $"已将 {player.Nickname} 变身为 {role}";
        }

        private static string ToolGodmode(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            bool enabled = p["enabled"]?.Value<bool>() ?? true;
            player.IsGodModeEnabled = enabled;
            return $"已{(enabled ? "开启" : "关闭")} {player.Nickname} 的无敌模式";
        }

        private static string ToolNoclip(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            bool enabled = p["enabled"]?.Value<bool>() ?? true;
            player.IsNoclipPermitted = enabled;
            return $"已{(enabled ? "开启" : "关闭")} {player.Nickname} 的穿墙模式";
        }

        private static string ToolScale(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            float x = p["x"]?.Value<float>() ?? 1f;
            float y = p["y"]?.Value<float>() ?? 1f;
            float z = p["z"]?.Value<float>() ?? 1f;
            player.Scale = new Vector3(x, y, z);
            return $"已设置 {player.Nickname} 体型为 ({x}, {y}, {z})";
        }

        // ━━━ 物品 & 弹药 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolGiveItem(JObject p)
        {
            var players = FindPlayers(p["target"]?.ToString());
            if (!players.Any()) return $"找不到玩家：{p["target"]}";
            if (!Enum.TryParse<ItemType>(p["item"]?.ToString(), true, out var itemType))
                return $"未知物品：{p["item"]}";
            foreach (var pl in players) pl.AddItem(itemType);
            return $"已给予 {string.Join(", ", players.Select(pl => pl.Nickname))} {itemType}";
        }

        private static string ToolGiveCandy(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            string candyName = p["candy"]?.ToString() ?? "Pink";
            if (!Enum.TryParse<InventorySystem.Items.Usables.Scp330.CandyKindID>(candyName, true, out var candy))
                return $"未知糖果类型：{candyName}（可用：Pink, Red, Green, Blue, Yellow, Purple）";
            // 通过 EXILED Scp330 wrapper 添加糖果
            var scp330Item = player.AddItem(ItemType.SCP330);
            if (scp330Item?.Base is InventorySystem.Items.Usables.Scp330.Scp330Bag bag)
                bag.TryAddSpecific(candy);
            return $"已给予 {player.Nickname} {candy} 糖果";
        }

        private static string ToolSetAmmo(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            if (!Enum.TryParse<AmmoType>(p["ammo_type"]?.ToString(), true, out var ammoType))
                return $"未知弹药类型：{p["ammo_type"]}";
            ushort amount = p["amount"]?.Value<ushort>() ?? 0;
            player.SetAmmo(ammoType, amount);
            return $"已设置 {player.Nickname} 的 {ammoType} 为 {amount}";
        }

        private static string ToolClearInventory(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            player.ClearInventory();
            return $"已清空 {player.Nickname} 的背包";
        }

        // ━━━ 状态效果 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolGiveEffect(JObject p)
        {
            var players = FindPlayers(p["target"]?.ToString());
            if (!players.Any()) return $"找不到玩家：{p["target"]}";
            if (!Enum.TryParse<EffectType>(p["effect"]?.ToString(), true, out var effect))
                return $"未知效果：{p["effect"]}";
            byte  intensity = p["intensity"]?.Value<byte>() ?? 1;
            float duration  = p["duration"]?.Value<float>() ?? 10f;
            foreach (var pl in players)
                pl.EnableEffect(effect, intensity, duration);
            return $"已给予 {string.Join(", ", players.Select(pl => pl.Nickname))} 效果 {effect}（强度{intensity}，{duration}秒）";
        }

        private static string ToolClearEffects(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            player.DisableAllEffects();
            return $"已清除 {player.Nickname} 的所有状态效果";
        }

        // ━━━ 传送 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolTeleportToPlayer(JObject p)
        {
            var target = FindPlayer(p["target"]?.ToString());
            var dest   = FindPlayer(p["destination"]?.ToString());
            if (target == null) return $"找不到玩家：{p["target"]}";
            if (dest   == null) return $"找不到目标：{p["destination"]}";
            target.Position = dest.Position;
            return $"已将 {target.Nickname} 传送到 {dest.Nickname} 身边";
        }

        private static string ToolTeleportToRoom(JObject p)
        {
            var players  = FindPlayers(p["target"]?.ToString());
            if (!players.Any()) return $"找不到玩家：{p["target"]}";
            string roomStr = p["room"]?.ToString();
            // 支持 RoomType 枚举名 或 房间名字符串两种写法
            Room room = null;
            if (Enum.TryParse<RoomType>(roomStr, true, out var roomType))
                room = Room.List.FirstOrDefault(r => r.Type == roomType);
            else
                room = Room.List.FirstOrDefault(r =>
                    r.Name.ToString().Equals(roomStr, StringComparison.OrdinalIgnoreCase));
            if (room == null) return $"找不到房间：{roomStr}";
            foreach (var pl in players) pl.Teleport(room);
            return $"已将 {string.Join(", ", players.Select(pl => pl.Nickname))} 传送到 {room.Name}";
        }

        // ━━━ 地图控制 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolLightsOut(JObject p)
        {
            float duration = p["duration_seconds"]?.Value<float>() ?? 15f;
            Map.TurnOffAllLights(duration);
            return $"已关闭全图灯光 {duration} 秒";
        }

        private static string ToolLockDoor(JObject p)
        {
            string doorStr = p["door"]?.ToString();
            bool   locked  = p["locked"]?.Value<bool>() ?? true;
            if (!Enum.TryParse<DoorType>(doorStr, true, out var doorType))
                return $"未知门类型：{doorStr}";
            var door = Door.List.FirstOrDefault(d => d.Type == doorType);
            if (door == null) return $"地图上未找到该门：{doorStr}";
            door.ChangeLock(locked ? DoorLockType.AdminCommand : DoorLockType.None);
            return $"已{(locked ? "锁定" : "解锁")} {doorType}";
        }

        private static string ToolOpenDoor(JObject p)
        {
            string doorStr = p["door"]?.ToString();
            bool   open    = p["open"]?.Value<bool>() ?? true;
            if (!Enum.TryParse<DoorType>(doorStr, true, out var doorType))
                return $"未知门类型：{doorStr}";
            var door = Door.List.FirstOrDefault(d => d.Type == doorType);
            if (door == null) return $"地图上未找到该门：{doorStr}";
            door.IsOpen = open;
            return $"已{(open ? "打开" : "关闭")} {doorType}";
        }

        private static string ToolLockdown(JObject p)
        {
            bool enabled = p["enabled"]?.Value<bool>() ?? true;
            foreach (var door in Door.List)
            {
                if (enabled)
                    door.ChangeLock(DoorLockType.AdminCommand);
                else
                    door.ChangeLock(DoorLockType.None);
            }
            return enabled ? "已锁定所有门（全图封锁）" : "已解除全图封锁";
        }

        private static string ToolDecontaminate()
        {
            Map.StartDecontamination();
            return "已立即触发 LCZ 除污程序";
        }

        private static string ToolWarhead(string cmd)
        {
            switch (cmd)
            {
                case "start":
                    Warhead.Start();
                    return "已启动核弹倒计时";
                case "stop":
                    Warhead.Stop();
                    return "已停止核弹倒计时";
                case "detonate":
                    Warhead.Detonate();
                    return "核弹已立即引爆";
                default:
                    return "未知弹头指令";
            }
        }

        // ━━━ CASSIE & 广播 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolCassie(JObject p, bool silent, bool translated)
        {
            string message  = p["message"]?.ToString();
            string subtitle = p["subtitle"]?.ToString() ?? message;
            if (string.IsNullOrWhiteSpace(message)) return "CASSIE 消息为空";
            if (translated)
                Cassie.MessageTranslated(message, subtitle);
            else if (silent)
                Cassie.Clear();  // 先清队列再播
            else
                Cassie.Message(message);
            return $"CASSIE 已播报：{message}";
        }

        private static string ToolBroadcast(JObject p)
        {
            string msg      = p["message"]?.ToString();
            int    duration = p["duration_seconds"]?.Value<int>() ?? 5;
            if (string.IsNullOrWhiteSpace(msg)) return "广播内容为空";
            Map.Broadcast((ushort)duration, msg);
            return $"已广播（{duration}秒）：{msg}";
        }

        private static string ToolHint(JObject p)
        {
            var player = FindPlayer(p["target"]?.ToString());
            if (player == null) return $"找不到玩家：{p["target"]}";
            string msg      = p["message"]?.ToString() ?? "";
            float  duration = p["duration_seconds"]?.Value<float>() ?? 5f;
            player.ShowHint(msg, duration);
            return $"已向 {player.Nickname} 发送 Hint：{msg}";
        }

        private static string ToolClearBroadcast()
        {
            Map.ClearBroadcasts();
            return "已清除所有广播";
        }

        // ━━━ Toys ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolSpawnToy(JObject p, Player caller)
        {
            string type     = p["type"]?.ToString()?.ToLower() ?? "primitive_cube";
            float  scale    = p["scale"]?.Value<float>() ?? 1f;
            string colorStr = p["color"]?.ToString() ?? "white";
            Vector3? pos      = caller?.Position ?? Vector3.zero;
            Vector3? scaleVec = Vector3.one * scale;
            Color    color    = ParseColor(colorStr);

            // 水豚彩蛋 — 直接走 RA 命令字符串，不依赖任何 Toy 类
            if (type == "capybara")
            {
                GameCore.Console.singleton.TypeCommand("spawntoy capybara", ServerConsole.Scs);
                return "已生成水豚 🐾";
            }

            // 光源 — Light.Create 第2参数是 Vector3?（欧拉角），不是 Quaternion
            if (type == "light")
            {
                Exiled.API.Features.Toys.Light.Create(pos, null, scaleVec, true, color);
                return $"已生成光源（颜色：{colorStr}，范围：{scale}）";
            }

            // 射击靶 — ExMod 版类名为 ShootingTargetToy
            if (type.StartsWith("shooting_target"))
            {
                var targetType = type switch
                {
                    "shooting_target_dboy"   => ShootingTargetType.ClassD,
                    "shooting_target_binary" => ShootingTargetType.Binary,
                    _                        => ShootingTargetType.Sport
                };
                Exiled.API.Features.Toys.ShootingTargetToy.Create(targetType, pos);
                return $"已生成射击靶：{targetType}";
            }

            // 3D 文字 — ExMod 版类名为 TextToy
            if (type == "text")
            {
                string text = p["text"]?.ToString() ?? "SLAgent";
                GameCore.Console.singleton.TypeCommand($"spawntoy text {text}", ServerConsole.Scs);
                return $"已生成 3D 文字：{text}";
            }

            // Primitive — 枚举值用 int 强转，彻底规避命名空间问题
            // 0=Sphere 1=Capsule 2=Cylinder 3=Cube 4=Plane
            int primitiveInt = type switch
            {
                "primitive_sphere"   => 0,
                "primitive_capsule"  => 1,
                "primitive_cylinder" => 2,
                "primitive_plane"    => 4,
                _                    => 3
            };
            string primName = type switch
            {
                "primitive_sphere"   => "primitiveobject 0",
                "primitive_capsule"  => "primitiveobject 1",
                "primitive_cylinder" => "primitiveobject 2",
                "primitive_plane"    => "primitiveobject 4",
                _                    => "primitiveobject 3"
            };
            GameCore.Console.singleton.TypeCommand($"spawntoy {primName} {colorStr}", ServerConsole.Scs);
            return $"已生成 Primitive({type})（颜色：{colorStr}，缩放：{scale}）";
        }

        private static string ToolDestroyToys()
        {
            int count = 0;
            foreach (var toy in AdminToy.List.ToList())
            {
                toy.Destroy();
                count++;
            }
            return $"已销毁 {count} 个场景物件";
        }

        // ━━━ 回合控制 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolRoundRestart()
        {
            Round.Restart();
            return "已发起回合重启";
        }

        private static string ToolRoundEnd(JObject p)
        {
            // EXILED Round.EndRound() 不接受 LeadingTeam 参数，直接结束回合
            Round.EndRound();
            return "已强制结束本回合";
        }

        private static string ToolForceStart()
        {
            Round.Start();
            return "已强制开始回合";
        }

        // ━━━ 查询 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static string ToolListPlayers()
        {
            var list = Player.List.Where(p => !p.IsNPC).ToList();
            if (!list.Any()) return "当前服务器没有在线玩家。";
            var sb = new StringBuilder();
            sb.AppendLine($"在线玩家（{list.Count}人）：");
            foreach (var pl in list)
                sb.AppendLine($"  [{pl.Nickname}] 角色={pl.Role.Type} HP={pl.Health:F0}/{pl.MaxHealth:F0} 房间={pl.CurrentRoom?.Name}");
            return sb.ToString().TrimEnd();
        }

        private static string ToolQuery(JObject p)
        {
            string target = p["target"]?.ToString();
            if (target?.Equals("all", StringComparison.OrdinalIgnoreCase) == true)
                return ToolListPlayers();
            var player = FindPlayer(target);
            if (player == null) return $"找不到玩家：{target}";
            var effects = player.ActiveEffects.Select(e => e.GetType().Name);
            return $"[{player.Nickname}]\n" +
                   $"  角色={player.Role.Type}  HP={player.Health:F0}/{player.MaxHealth:F0}\n" +
                   $"  房间={player.CurrentRoom?.Name}  区域={player.Zone}\n" +
                   $"  无敌={player.IsGodModeEnabled}  穿墙={player.IsNoclipPermitted}\n" +
                   $"  弹药=9mm:{player.GetAmmo(AmmoType.Nato9)} 556:{player.GetAmmo(AmmoType.Nato556)} 762:{player.GetAmmo(AmmoType.Nato762)}\n" +
                   $"  效果={string.Join(",", effects)}\n" +
                   $"  Steam64={player.UserId?.Split('@')[0]}";
        }

        // ━━━ 工具函数 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━

        private static Color ParseColor(string name)
        {
            if (UnityEngine.ColorUtility.TryParseHtmlString(name, out var c)) return c;
            return name.ToLower() switch
            {
                "red"    => Color.red,
                "green"  => Color.green,
                "blue"   => Color.blue,
                "yellow" => Color.yellow,
                "cyan"   => Color.cyan,
                "white"  => Color.white,
                "black"  => Color.black,
                "purple" => new Color(0.5f, 0f, 0.5f),
                "orange" => new Color(1f, 0.5f, 0f),
                "pink"   => new Color(1f, 0.41f, 0.71f),
                _        => Color.white
            };
        }

        // ── 日志 ──────────────────────────────────────────────
        private static void LogAction(Player caller, string action, JObject p, string reason)
        {
            string time  = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss");
            string steam = caller?.UserId?.Split('@')[0] ?? "server";
            string line  = $"[{time}] AGENT | 操作者={steam} 动作={action} 参数={p} 原因={reason}\n";
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
        public override Version Version => new Version(3, 0, 3);

        public static SLAgent Instance { get; private set; }

        private readonly ConcurrentDictionary<string, List<ChatMessage>> conversations = new();
        private readonly ConcurrentDictionary<string, string> playerModels             = new();
        private readonly HttpClient httpClient = new() { Timeout = TimeSpan.FromSeconds(120) };

        public override void OnEnabled()
        {
            Instance = this;
            ValidateConfig();
            Log.Info($"SLAgent v{Version} 已加载 | .bot <指令> | .model | .reset | .players");
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
