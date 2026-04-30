# 🤖 DeepSeekBot v2.0 (EXILED Plugin)

一个基于多大模型 API 的 SCP: Secret Laboratory (EXILED) 插件，让玩家可以在游戏内与 AI 对话，支持 DeepSeek、通义千问、豆包、Kimi 等国内主流模型。

---

## ✨ 功能特性

* 💬 游戏内命令调用 AI（`.bot`）
* 🧠 支持上下文对话（记住聊天历史）
* 🔀 **多模型支持**：DeepSeek / 通义千问 / 豆包 / Kimi
* 🔄 **`.model` 指令**：玩家可自由切换 AI 模型
* 🔒 白名单控制（限制使用玩家）
* ♻️ 会话重置（`.reset`）
* 📝 自动记录聊天日志
* ⚡ 支持多玩家并发
* 🛡️ 超时保护 + 上下文长度限制

---

## 📦 安装方法

```bash
dotnet build
```

将生成的 `DeepSeekBot.dll` 放入服务器的 `EXILED/Plugins/` 目录，重启服务器即可。

---

## ⚙️ 配置文件

首次运行后会自动生成：

```
configs/DeepSeekBot/config.yml
```

完整示例：

```yaml
IsEnabled: true
Debug: false

# ── API Keys（至少填写一个）──────────────────────────
DeepSeekApiKey: "sk-xxxxxxxx"
QwenApiKey: ""                    # 阿里云百炼 API Key
DoubaoApiKey: ""                  # 火山引擎 API Key
DoubaoEndpointId: ""              # 豆包专属 endpoint id，格式：ep-xxxxxxxx-xxxxx
KimiApiKey: ""                    # Moonshot AI API Key

# ── 默认模型 ─────────────────────────────────────────
# 可选值：deepseek | qwen | doubao | kimi
DefaultModel: "deepseek"

# ── 白名单 ───────────────────────────────────────────
Whitelist:
  - "76561198123456789"

# ── 高级限制 ─────────────────────────────────────────
MaxContextMessages: 20    # 保留最近几条对话（0 = 不限制，慎用）
MaxTokens: 2048           # 单次回复最大 token 数
RequestTimeoutSeconds: 90 # API 请求超时时间（秒），国内模型建议 60~120
```

### 参数说明

| 参数 | 说明 |
|------|------|
| `DeepSeekApiKey` | [DeepSeek](https://platform.deepseek.com) API Key |
| `QwenApiKey` | [阿里云百炼](https://bailian.console.aliyun.com) API Key |
| `DoubaoApiKey` | [火山引擎](https://console.volcengine.com/ark) API Key |
| `DoubaoEndpointId` | 豆包模型的 Endpoint ID（格式：`ep-xxxxxxxx`） |
| `KimiApiKey` | [Moonshot AI](https://platform.moonshot.cn) API Key |
| `DefaultModel` | 玩家连接时默认使用的模型 |
| `MaxContextMessages` | 上下文保留条数，防止 token 超限 |
| `RequestTimeoutSeconds` | API 超时时间，建议不低于 60 |

---

## 🎮 使用方法

### 调用 AI

```
.bot 你的问题
```

### 查看/切换模型

```
.model                # 查看当前模型及可用模型列表
.model deepseek       # 切换到 DeepSeek
.model qwen           # 切换到通义千问
.model doubao         # 切换到豆包
.model kimi           # 切换到 Kimi
```

> 切换模型时会自动重置当前对话上下文。

### 重置对话

```
.reset
```

---

## 📁 日志文件

聊天记录自动保存至服务器根目录：

```
DeepSeekBot_conversations.log
```

---

## 🔑 各平台 API Key 获取

| 模型 | 平台 | 地址 |
|------|------|------|
| DeepSeek | DeepSeek 开放平台 | https://platform.deepseek.com |
| 通义千问 | 阿里云百炼 | https://bailian.console.aliyun.com |
| 豆包 | 火山引擎方舟 | https://console.volcengine.com/ark |
| Kimi | Moonshot AI 开放平台 | https://platform.moonshot.cn |

---

## ⚠️ 注意事项

* 至少需要配置一个 API Key 插件才能正常工作
* 豆包需要额外填写 `DoubaoEndpointId`，在火山引擎控制台的"模型推理"→"在线推理"中创建
* 建议开启白名单防止 API 被滥用
* `RequestTimeoutSeconds` 建议设置为 60~120，低于 30 秒在国内网络环境下易超时
* 切换模型时上下文会自动重置，这是有意为之的行为

---

## 🛠 开发环境

* .NET Framework 4.8
* ExMod.Exiled 9.x
* Newtonsoft.Json

---

## 📌 更新日志

### 2026-4-26
- ✅ 新增 Qwen / 豆包 / Kimi 多模型支持
- ✅ 新增 `.model` 指令供玩家切换模型
- ✅ 修复超时无响应问题（超时时间从 30s 延长至可配置，默认 90s）
- ✅ 修复超时时静默失败，现在会明确提示玩家
- ✅ 修复 Task 内异常未捕获导致的静默失败
- ✅ 新增上下文长度限制，防止 token 爆炸
- ✅ API Key 按模型独立配置，未配置的模型自动标记不可用

---
## Stone Badge🪨
![Stone Badge](https://stone.professorlee.work/api/stone/DNTOF/DeepSeekBot)
## 🤝 贡献

欢迎提交 Issue 或 Pull Request！

---

