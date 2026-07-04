# codexU

[English](README.en.md)

codexU 是一个桌面小组件，用来查看 OpenAI Codex / ChatGPT Codex 的额度窗口、token 用量和今日任务状态。它把常用信息放在桌面上，帮助你快速判断剩余额度、重置时间和当天工作进展。

> **上游仓库**: [shanggqm/codexU](https://github.com/shanggqm/codexU) — 原始 macOS 版本  
> **本仓库**: [Yuna-Celisse/codexUWin](https://github.com/Yuna-Celisse/codexUWin) — 在上游基础上新增 Windows (Qt/QML) 移植

![codexU 桌面小组件截图](docs/screenshot-0.2.0.png)

## 平台支持

| 平台 | 技术栈 | 状态 |
|------|--------|------|
| macOS | Swift + SwiftUI | 上游原始版本，稳定 |
| Windows | C++ + Qt 6 QML | 本仓库移植，可用 |

## 功能

- 展示 Codex 5 小时和 7 天额度的剩余比例和重置时间。
- 汇总今日、近 7 天和累计 token 用量，并细分未缓存输入、缓存输入和输出。
- 按 OpenAI API token 价格估算本月 API 等效价值，并在 Plus、Pro 100、Pro 200 和满额月价值之间展示进度刻度（"羊毛进度"）。
- 从本机 Codex 线程和启用中的 automations 生成今日任务看板（进行中 / 待处理 / 定时 / 完成）。
- 本地读取数据，不上传 usage、线程或账户数据到第三方服务。
- 中英文界面，根据系统时区自动选择语言。
- 自动、浅色和深色外观模式（macOS）。
- 窗口固定功能（Windows）—— 固定后窗口不移动、不抢焦点。

## 快捷键和操作

**macOS**
- `Command + U`：在桌面层和前台层之间切换。
- 菜单栏仪表图标：同 `Command + U`。
- 顶部 `中 | EN`：切换中英文。
- 拖动窗口背景：移动窗口位置。

**Windows**
- `Ctrl + Alt + U`：显示/隐藏窗口。
- 系统托盘图标：双击切换显示，右键菜单可刷新或退出。
- 固定按钮（锁）：固定后窗口不移动、点击不激活。
- 拖动 Header 区域：移动窗口位置。

## macOS 安装与构建

### 运行要求

- macOS 14 或更新版本。
- 本机已安装 Codex 并至少使用过一次（生成 `~/.codex/state_5.sqlite`）。
- 已登录 Codex 账户。
- 从源码构建时需要 Xcode Command Line Tools。

### 从源码构建

```sh
make build    # 构建
make run      # 运行
make install  # 安装到 /Applications
make probe    # 输出数据源 JSON 用于调试
```

### 打包 DMG

```sh
make release         # 当前架构
make release-arm64   # Apple Silicon
make release-intel   # Intel
make release-all     # 双架构
```

更多信息见上游仓库 [shanggqm/codexU](https://github.com/shanggqm/codexU)。

## Windows 安装与构建

### 运行要求

- Windows 10 或更新版本。
- Qt 6.5+ (MinGW 64-bit)。
- 本机已安装 Codex 并至少使用过一次。
- 已登录 Codex 账户。
- CMake 3.16+ 和 Ninja（随 Qt 安装包提供）。

### 从源码构建

```powershell
# 配置
cmake -S codexU-qml -B codexU-qml/build -G Ninja `
  -DCMAKE_BUILD_TYPE=Release `
  -DCMAKE_PREFIX_PATH="C:\Qt\6.11.1\mingw_64" `
  -DCMAKE_CXX_COMPILER="C:\Qt\Tools\mingw1310_64\bin\g++.exe"

# 构建
cmake --build codexU-qml/build --config Release

# 部署 Qt DLL
windeployqt codexU-qml/build/codexU-qml.exe --qmldir codexU-qml/src/qml

# 复制 SQLite 驱动
mkdir codexU-qml/build/sqldrivers
copy C:\Qt\6.11.1\mingw_64\plugins\sqldrivers\qsqlite.dll codexU-qml/build/sqldrivers\
```

或使用构建脚本：

```powershell
.\codexU-qml\scripts\build.ps1
```

### 运行

```powershell
.\codexU-qml\build\codexU-qml.exe

# Mock 数据模式（开发预览）
.\codexU-qml\build\codexU-qml.exe --mock

# JSON 诊断输出
.\codexU-qml\build\codexU-qml.exe --dump-json
```

## 羊毛进度

"羊毛进度"是 codexU 对本月 Codex 使用量的 API 等效价值估算。它把本机解析到的 token 按对应模型的 OpenAI API 单价折算成美元金额，并和 Plus ($20)、Pro 100 ($100)、Pro 200 ($200) 以及满额月价值做对比。

单次 token 用量的估算公式为：

```text
API 等效价值 =
  未缓存输入 tokens / 1,000,000 × 未缓存输入单价
+ 缓存输入 tokens / 1,000,000 × 缓存输入单价
+ 输出 tokens / 1,000,000 × 输出单价
```

> 该金额仅是基于 API 价格的等效估算，不代表实际账单或官方返现金额。

## 数据来源

- **账户与额度**：`codex app-server` JSON-RPC (`account/read`, `account/rateLimits/read`, `account/usage/read`)
- **本机 token 总量**：`~/.codex/state_5.sqlite`（macOS）/ `%USERPROFILE%\.codex\state_5.sqlite`（Windows）
- **精细 token 拆分**：`token_count` 事件（来自 sessions/archived_sessions 的 JSONL 文件）
- **今日任务看板**：SQLite 中的 Codex 线程 + 归档记录
- **定时任务**：`automation.toml`（`~/.codex/automations/`）

当前 Codex 额度 API 暴露的是滚动窗口百分比和重置时间，不暴露绝对配额数量。更完整的数据口径见上游仓库 [RESEARCH.md](https://github.com/shanggqm/codexU/blob/main/RESEARCH.md)。

## 常见问题

### codexU 是官方 OpenAI 产品吗？

不是。codexU 是一个非官方的本地工具，用于读取本机 Codex 数据。

### codexU 会上传我的数据吗？

不会。codexU 只在本机读取数据，不向任何第三方服务上传。

### 支持 Intel Mac 吗？

支持。Intel Mac 下载 `codexU-<version>-mac-x86_64.dmg`。

### Windows 版本和 macOS 版本功能一致吗？

Windows (Qt/QML) 版本实现了 macOS 版本的核心功能（额度圆环、token 卡片、羊毛进度、任务看板），部分高级特性（session 缓存加速、每日趋势图、环境诊断清单）目前仅 macOS 版本支持。

## 贡献

- 上游 macOS 版本：[shanggqm/codexU](https://github.com/shanggqm/codexU)
- Windows 移植及其他修改：[Yuna-Celisse/codexUWin](https://github.com/Yuna-Celisse/codexUWin)

欢迎提交 Issue 和 Pull Request。

## License

MIT. See [LICENSE](LICENSE).

## 关注公众号

如果你关注 AI 工具、Codex 使用经验和独立产品构建，欢迎扫码关注原作者的公众号。

<img src="docs/wechat-official-account-qr.png" alt="公众号二维码" width="220" />
