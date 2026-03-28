# 🤖 DeepSeekBot (EXILED Plugin)

一个基于 DeepSeek API 的 SCP: Secret Laboratory 插件，让玩家可以在游戏内使用 AI 对话。

---

## ✨ 功能特性

* 💬 游戏内命令调用 AI（`.bot`）
* 🧠 支持上下文对话（记住聊天历史）
* 🔒 白名单控制（限制使用玩家）
* ♻️ 会话重置（`.reset`）
* 📝 自动记录聊天日志
* ⚡ 支持多玩家并发

---

## 📦 安装方法

1. 下载本项目
2. 在命令行中运行(你也可以在Release中直接下载已经更新好的插件)：

```
dotnet build
```

3. 启动服务器

---

## ⚙️ 配置文件

首次运行后会生成配置文件：

```
configs/DeepSeekBot/config.yml
```

示例：

```yaml
IsEnabled: true
Debug: false
ApiKey: "你的 DeepSeek API Key"
Whitelist:
  - 76561198123456789
```

### 参数说明

| 参数        | 说明                    |
| --------- | --------------------- |
| ApiKey    | DeepSeek API Key      |
| Whitelist | 允许使用插件的 Steam64 ID 列表 |

---

## 🎮 使用方法

### 调用 AI

```
.bot 你的问题
```

示例：

```
.bot SCP-173 怎么玩？
```

---

### 重置对话

```
.reset
```

---

## 📁 日志文件

聊天记录会自动保存到：

```
DeepSeekBot_conversations.log
```

---

## ⚠️ 注意事项

* 请确保已正确填写 API Key
* 建议开启白名单防止滥用 API
* 多人同时使用时会消耗较多 API 请求

---

## 🛠 开发环境

* .NET Framework 4.8
* ExMod.Exiled 9.x
* Newtonsoft.Json

---

## 📌 未来计划

* [ ] 限流系统（防止 API 滥用）
* [ ] 上下文长度控制
* [ ] 多模型支持

---

## 🤝 贡献

欢迎提交 Issue 或 Pull Request！

---

## 📄 许可证

本项目遵循 MIT License

---
