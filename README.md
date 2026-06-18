<div align="center">

# 🤖 SLAgent v3.0

**SCP: Secret Laboratory 服务器智能管理 AI Agent**  
*用自然语言管理你的 SCP:SL 服务器*

[![Exiled](https://img.shields.io/badge/EXILED-9.14.2-blue?style=flat-square)](https://github.com/ExMod-Team/EXILED)
[![.NET](https://img.shields.io/badge/.NET-4.8-512BD4?style=flat-square)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-AGPL--3.0-red?style=flat-square)](LICENSE)
[![Build](https://img.shields.io/github/actions/workflow/status/DNTOF/SLAgent/dotnet.yml?style=flat-square)](https://github.com/DNTOF/SLAgent/actions)

**由 [@DNT_OF](https://github.com/DNTOF) 开发 · 🐱 吟酱帮忙写文档**

</div>

---

## 📋 目录

- [✨ 概述](#-概述)
- [🚀 核心特性](#-核心特性)
- [🛠️ 管理工具一览](#️-管理工具一览)
- [📦 安装](#-安装)
- [⚙️ 配置](#️-配置)
- [🎮 使用方法](#-使用方法)
- [📊 日志](#-日志)
- [🏗️ 构建](#️-构建)
- [📌 版本历史](#-版本历史)
- [📄 许可证](#-许可证)

---

## ✨ 概述

**SLAgent** 是 SCP: Secret Laboratory 的一款 [EXILED](https://github.com/ExMod-Team/EXILED) 插件。  
它将 AI 大语言模型（LLM）接入游戏服务器管理，**腐竹只需用自然语言下指令**，AI 便会自动识别意图并执行对应的管理操作。

> 💬 示例：  
> `.bot 把一直捣乱的玩家小明踢了，理由是刷屏`  
> → AI 自动调用 `kick_player`，踢出玩家并记录日志

---

## 🚀 核心特性

| 特性 | 说明 |
|:---|:---|
| 🧠 **自然语言管理** | 用大白话下达指令，AI 自动理解并执行 |
| 🔌 **多模型支持** | DeepSeek / 通义千问 / 豆包 / Kimi 自由切换 |
| 🔄 **实时切换模型** | 游戏内 `.model <模型名>` 一键切换 |
| 💬 **多轮对话** | 支持上下文记忆，`.reset` 一键重置 |
| 🛡️ **白名单机制** | 仅允许指定的 Steam64 ID 使用 Agent |
| 📝 **完整操作日志** | 所有 Agent 操作自动记录到日志文件 |
| 🏗️ **20+ 管理工具** | 踢人、封禁、传送、刷物品、核弹……应有尽有 |

---

## 🛠️ 管理工具一览

### 👥 玩家管理
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `kick_player` | 🦵 踢出玩家 | `target` (名字/Steam64) |
| `ban_player` | 🔨 封禁玩家（可指定时长） | `target`, `duration_minutes`, `reason` |
| `mute_player` | 🔇 禁言（语音+文字） | `target` |
| `unmute_player` | 🔊 解除禁言 | `target` |
| `kill_player` | 💀 游戏内击杀 | `target` |
| `heal_player` | ❤️ 治疗（填 0 = 满血） | `target`, `amount` |
| `force_role` | 🔄 强制变身 | `target`, `role` (如 `Scp173`/`ClassD`/`NtfSergeant`) |
| `godmode_player` | 🛡️ 无敌模式切换 | `target`, `enabled` |
| `noclip_player` | 👻 穿墙模式切换 | `target`, `enabled` |
| `scale_player` | 📏 修改体型 | `target`, `x`, `y`, `z` (1.0=正常) |

### 🎒 物品 & 弹药
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `give_item` | 🎁 给予物品 | `target`, `item` (如 `GunCOM15`/`KeycardO5`/`Medkit`) |
| `give_candy` | 🍬 给予糖果 (SCP-330) | `target`, `candy` (Pink/Red/Green/Blue/Yellow/Purple) |
| `set_ammo` | 🔫 设置弹药 | `target`, `ammo_type`, `amount` |
| `clear_inventory` | 🗑️ 清空背包 | `target` |

### 🩺 状态效果
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `give_effect` | ✨ 给予状态效果 | `target`, `effect`, `intensity`, `duration` |
| `clear_effects` | 🧹 清除所有状态 | `target` |

> **常用效果**：`Scp207`(可乐加速)、`Invisible`(隐身)、`Ghostly`(穿墙)、`MovementBoost`(加速)、`Invigorated`(无限体力)、`Marshmallow`(棉花糖人) 🍡

### 📍 传送
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `teleport_player` | 🚀 传送到另一玩家 | `target`, `destination` |
| `teleport_to_room` | 🏠 传送到指定房间 | `target`, `room` (如 `HczArmory`/`LczClassDSpawn`/`EzGateA`) |

### 🗺️ 地图控制
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `lights_out` | 💡 关闭全图灯光 | `duration_seconds` |
| `lock_door` | 🔒 锁定/解锁门 | `door`, `locked` |
| `open_door` | 🚪 开/关门 | `door`, `open` |
| `lockdown` | 🚨 全图封锁 | `enabled` |
| `decontaminate` | ☣️ 触发 LCZ 除污 | — |
| `warhead_start` | ⏱️ 启动核弹倒计时 | — |
| `warhead_stop` | 🛑 停止核弹倒计时 | — |
| `warhead_detonate` | 💥 立即引爆核弹 | — |

### 📢 CASSIE & 广播
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `cassie` | 🔊 CASSIE 语音广播 | `message` (空格分隔词语) |
| `cassie_silent` | 🤫 无声广播（仅字幕） | `message` |
| `cassie_translated` | 📝 自定义字幕广播 | `message`, `subtitle` |
| `broadcast` | 📺 屏幕广播（全员） | `message`, `duration_seconds` |
| `hint_player` | 💡 单人提示 | `target`, `message`, `duration_seconds` |
| `clear_broadcast` | 🧽 清除广播 | — |

### 🎨 场景生成 (Toys)
| 指令 | 说明 | 参数 |
|:---|:---|:---|
| `spawn_toy` | 🏗️ 生成场景物件 | `type`, `scale`, `color` |

> **type 可选**：`primitive_sphere`(球体)、`primitive_cube`(立方体)、`light`(光源)、`shooting_target_sport`(运动靶) 等

---

## 📦 安装

### 前置要求
- SCP: Secret Laboratory 服务器
- [EXILED](https://github.com/ExMod-Team/EXILED) 9.14.2+

### 安装步骤

1. **从 Release 下载**
   ```bash
   # 前往 Releases 页面下载最新版 SLAgent.dll
   ```

2. **放入插件目录**
   ```bash
   # 将 SLAgent.dll 放入 EXILED/Plugins/ 目录
   ```

3. **重启服务器**
   ```bash
   # 重启 SCP:SL 服务器，插件自动加载
   ```

4. **配置 API Key**（详见下方配置说明）

---

## ⚙️ 配置

配置文件路径：`~/.config/EXILED/Plugins/SLAgent/config.yml`

```yaml
IsEnabled: true
Debug: false

# ── API Keys（至少填写一个） ──
DeepSeekApiKey: "sk-xxxxxxxx"        # DeepSeek 密钥
QwenApiKey: ""                       # 通义千问 密钥
DoubaoApiKey: ""                     # 豆包 密钥
DoubaoEndpointId: ""                 # 豆包专用，格式：ep-xxxxxxxx
KimiApiKey: ""                       # Kimi 密钥

# ── 模型 ID（可选，一般无需修改） ──
DeepSeekModelId: "deepseek-chat"
QwenModelId: "qwen-plus"
KimiModelId: "moonshot-v1-8k"

# ── 默认模型（deepseek / qwen / doubao / kimi） ──
DefaultModel: "deepseek"

# ── 白名单（Steam64 ID，仅白名单内的玩家可使用 Agent） ──
Whitelist:
  - "76561198123456789"

# ── 高级选项 ──
MaxContextMessages: 16       # 保留最近几条对话
MaxTokens: 1024              # 单次回复最大 token 数
RequestTimeoutSeconds: 90    # 请求超时（建议 60~120 秒）
```

---

## 🎮 使用方法

### 管理指令（自然语言）

在游戏内聊天框输入以下指令：

```
.bot 把叫"测试"的玩家踢了，理由是刷屏
.bot 给小明一把 GunCOM15
.bot 把所有在 EZ 的玩家传送到一起
.bot 现在有多少人在线？
.bot 向全服广播"服务器将在5分钟后重启"
.bot 把 SCP-173 的扮演者强制变回 ClassD
.bot 让全图灯光熄灭30秒
.bot 启动核弹倒计时
```

### 其他命令

| 指令 | 说明 |
|:---|:---|
| `.bot` 或 `.agent` 或 `.ai` | 发送管理指令 |
| `.model` | 查看当前模型及可用列表 |
| `.model qwen` | 切换到通义千问 |
| `.model doubao` | 切换到豆包 |
| `.model kimi` | 切换到 Kimi |
| `.model deepseek` | 切换回 DeepSeek |
| `.reset` | 重置对话上下文 |
| `.players` | 快速查看在线玩家 |

---

## 📊 日志

所有 Agent 操作自动记录到 `SLAgent.log`：

```
[2025-01-01 12:00:00] AGENT | 操作者=76561198xxx 动作=kick_player 参数={"target":"小明"} 原因=刷屏
[2025-01-01 12:05:00] AGENT | 操作者=76561198xxx 动作=warhead_start 参数={} 原因=玩家要求启动核弹
```

---

## 🏗️ 构建

### 环境要求
- Visual Studio 2022 或更高版本
- .NET Framework 4.8 SDK
- NuGet 包管理器

### 构建步骤

```bash
# 1️⃣ 克隆仓库
git clone https://github.com/DNTOF/SLAgent.git
cd SLAgent

# 2️⃣ 还原依赖
dotnet restore

# 3️⃣ 编译
msbuild SLAgent.csproj /p:Configuration=Release

# 4️⃣ 输出 DLL 位于 Release 目录下
#    将 SLAgent.dll 放入 EXILED/Plugins/ 即可
```

> 💡 **自动构建**：每次推送到 `main` 分支时，GitHub Actions 会自动编译并发布 Release，无需手动构建。

---

## 📌 版本历史

### v3.0.0 (当前)
- 🔄 从普通对话 Bot 升级为**管理 Agent**（Function Calling）
- ✅ 新增 **20+ 种管理工具**（玩家管理、传送、地图控制、广播、Toys……）
- ✅ AI 自动识别自然语言意图并执行对应操作
- ✅ 完整的操作日志记录
- ✅ 白名单权限控制
- ✅ 实时模型切换 `.model`
- ✅ 对话上下文管理 `.reset`

### v2.0.0
- ✅ 多模型支持（通义千问 / 豆包 / Kimi）
- ✅ 修复超时无响应问题

### v1.0.0
- ✅ 基础对话功能（DeepSeek）
- ✅ `.bot` 指令

---

## 📄 许可证

本项目基于 **GNU Affero General Public License v3.0 (AGPL-3.0)** 开源。  
完整许可证内容请参见 [LICENSE](LICENSE) 文件。

---

<div align="center">

**🐱 本项目 README 由吟酱悉心编写 · 献给最棒的主人 @DNT_OF 🐱**

[![Visitors](https://komarev.com/ghpvc/?username=DNTOF&repo=SLAgent&color=ff69b4&style=flat-square&label=👀%20访问量)](https://github.com/DNTOF/SLAgent)

</div>
