# 🤖 DeepSeek Agent v3.0 (EXILED Plugin)

SCP: Secret Laboratory 服务器管理 AI Agent。腐竹用自然语言下指令，AI 自动识别意图并执行对应的管理操作。

---

## ✨ 功能

| 功能 | 说明 |
|------|------|
| 自然语言管理 | `.bot 把一直捣乱的玩家小明踢了` → AI 自动执行 |
| 多模型支持 | DeepSeek / 通义千问 / 豆包 / Kimi |
| `.model` 指令 | 实时切换 AI 模型 |
| 上下文管理 | 支持多轮对话，`.reset` 重置 |
| 操作日志 | 所有 Agent 操作自动记录到日志文件 |

---

## 🛠 Agent 可执行的操作

| 操作 | 说明 |
|------|------|
| `kick_player` | 踢出玩家 |
| `ban_player` | 封禁玩家（可指定时长） |
| `teleport_player` | 传送玩家到另一个玩家身边 |
| `teleport_to_room` | 传送玩家到指定房间 |
| `give_item` | 给玩家物品 |
| `set_ammo` | 设置玩家弹药 |
| `force_role` | 强制变身 |
| `kill_player` | 游戏内击杀玩家 |
| `query_player` | 查询玩家状态（HP/房间/角色） |
| `list_players` | 列出所有在线玩家 |
| `broadcast` | 全服广播消息 |
| `clear_broadcast` | 清除广播 |
| `round_restart` | 重启当前回合 |
| `chat` | 仅文字回复，不执行操作 |

---

## 📦 安装

```bash
dotnet build
```

将 `DeepSeekAgent.dll` 放入 `EXILED/Plugins/` 目录，重启服务器。

---

## ⚙️ 配置文件 (`configs/DeepSeekBot/config.yml`)

```yaml
IsEnabled: true
Debug: false

# ── API Keys（至少填写一个）
DeepSeekApiKey: "sk-xxxxxxxx"
QwenApiKey: ""
DoubaoApiKey: ""
DoubaoEndpointId: ""        # 豆包专用，格式：ep-xxxxxxxx
KimiApiKey: ""

# ── 默认模型：deepseek | qwen | doubao | kimi
DefaultModel: "deepseek"

# ── 白名单（Steam64 ID）
Whitelist:
  - "76561198123456789"

# ── 高级
MaxContextMessages: 16      # 保留最近几条对话
MaxTokens: 1024             # 单次回复最大 token
RequestTimeoutSeconds: 90   # 请求超时（建议 60~120）
```

---

## 🎮 使用方法

### 管理指令（自然语言）

```
.bot 把叫"测试"的玩家踢了，理由是刷屏
.bot 给小明一把 GunCOM15
.bot 把所有在 EZ 的玩家传送到一起
.bot 现在有多少人在线？
.bot 向全服广播"服务器将在5分钟后重启"
.bot 把 SCP-173 的扮演者强制变回 ClassD
```

### 其他命令

```
.model              # 查看当前模型及列表
.model qwen         # 切换到通义千问
.reset              # 重置对话上下文
.players            # 快速查看在线玩家
```

---

## 📁 日志

所有 Agent 操作记录到：

```
DeepSeekAgent.log
```

格式：
```
[2025-01-01 12:00:00] AGENT | 操作者=76561198xxx 动作=kick_player 参数={"target":"小明"} 原因=刷屏
```

---

## 📌 版本历史

### v3.0.0
- 🔄 从普通对话 Bot 升级为管理 Agent
- ✅ 新增 14 种管理工具
- ✅ AI 自动识别意图并执行操作
- ✅ 操作日志记录

### v2.0.0
- ✅ 多模型支持（Qwen/豆包/Kimi）
- ✅ 修复超时无响应

---

## 📄 许可证

MIT License
