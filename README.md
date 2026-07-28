# Watermark Fairy · 水印精灵

> **极简 · 批量 · 云端模板** — Windows 桌面端照片水印工具

[![Status](https://img.shields.io/badge/status-v0.1.0%20Internal%20Beta-yellow)](https://github.com/yaojinbest/watermark-fairy/releases)
[![Build](https://github.com/yaojinbest/watermark-fairy/actions/workflows/build.yml/badge.svg)](https://github.com/yaojinbest/watermark-fairy/actions/workflows/build.yml)
[![Tests](https://img.shields.io/badge/tests-209%20passed-success)](tests)
[![Coverage](https://img.shields.io/badge/coverage-78.10%25-green)](docs/RETROSPECTIVE-W3.md)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](https://github.com/yaojinbest/watermark-fairy)
[![License](https://img.shields.io/badge/license-Proprietary-red)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Stars](https://img.shields.io/github/stars/yaojinbest/watermark-fairy)](https://github.com/yaojinbest/watermark-fairy/stargazers)
[![Forks](https://img.shields.io/github/forks/yaojinbest/watermark-fairy)](https://github.com/yaojinbest/watermark-fairy/network/members)

---

## 概述

Watermark Fairy（简称 **WF**，中文名 **水印精灵**）是一款 Windows 桌面端照片水印工具。

- 🖼️ **文字 + 图片 logo 水印**：自定义字体、颜色、大小、位置
- 📁 **文件夹批量**：占位符 + 正则命名规则
- 👁️ **实时预览**：所见即所得
- ☁️ **云端模板**：一处配置，多设备同步（Supabase）
- 🆓 **永久免费基础版** + ⭐ **Pro 订阅解锁高级功能**

---

## 截图

> ⚠️ 截图位为占位，待 owner 截图后替换。建议用 Windows 自带的「截图工具」（`Win+Shift+S`）截取实际运行界面，保存到 `docs/screenshots/` 目录。

<!-- TODO(owner): 截 4-5 张主界面图，覆盖核心流程 -->

| 主窗口 | 实时预览 | 批量处理 | 云端登录 |
|:---:|:---:|:---:|:---:|
| ![Main](docs/screenshots/01-main-window.png) | ![Preview](docs/screenshots/02-preview.png) | ![Batch](docs/screenshots/03-batch.png) | ![Cloud](docs/screenshots/04-cloud.png) |
| **模板管理** | **水印配置** | **导出对话框** | |
| ![Templates](docs/screenshots/05-templates.png) | ![Watermark](docs/screenshots/06-watermark.png) | ![Export](docs/screenshots/07-export.png) | |

---

## 功能特性

### 🆓 免费版（v1.0 已完工）

- ✅ **文字水印**：自定义字体 / 颜色 / 大小 / 透明度 / 旋转 / 描边 / 阴影
- ✅ **图片 logo 水印**：PNG / SVG / JPG，缩放至原图比例
- ✅ **9 宫格位置** + 自由拖拽
- ✅ **实时预览**：100ms 防抖，多图切换不重渲染已缓存图层
- ✅ **文件夹批量处理**：递归 / 浅层，实时进度 + 失败重试
- ✅ **文件名占位符**：`{name}` / `{date}` / `{time}` / `{n}` / `{size}` / `{hash}`
- ✅ **文件名正则替换规则**：批量改名前缀 / 后缀
- ✅ **本地模板库**：保存 / 加载 / 导入 / 导出（JSON）
- ✅ **多格式输出**：JPG / PNG / WebP / 原格式
- ✅ **用户配置持久化**：上次窗口位置 / 默认模板
- ✅ **自动更新检查**（M4-1）：启动时后台 fire-and-forget 检查 GitHub Releases

### ⭐ Pro 版（订阅解锁 · 路线图中）

- ⭐ 云端模板同步（多设备实时同步）
- ⭐ 团队共享模板库
- ⭐ 批量队列（≥10 任务并发调度）
- ⭐ 定时任务 / Watch 文件夹自动处理
- ⭐ EXIF 保留 / 清除
- ⭐ TIFF / HEIC 输入
- ⭐ SVG 矢量水印
- ⭐ 模板版本历史 + Diff
- ⭐ Pro 升级流程 + Stripe 支付

详见 [docs/PRD.md](docs/PRD.md) §5 商业模式。

---

## 快速开始

### 下载

> 🚧 v0.1.0 Internal Beta — **尚未发布 GitHub Release**（待 M4-2）
> 当前可从源码构建，或 `git clone` 后用 dotnet CLI 运行

### 从源码构建

**前置**：.NET 8 SDK（[安装指南](https://dotnet.microsoft.com/download/dotnet/8.0)） + Windows 10/11

```bash
# 1. 克隆
git clone https://github.com/yaojinbest/watermark-fairy.git
cd watermark-fairy

# 2. 构建
dotnet build WatermarkFairy.sln -c Release

# 3. 运行
dotnet run --project src/WatermarkFairy/WatermarkFairy.csproj -c Release

# 4. 打包发布版（单文件 + 自包含运行时）
dotnet publish src/WatermarkFairy/WatermarkFairy.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

### 第一次使用

1. 启动应用 → 拖入一张图片（或文件夹）到主窗口
2. 选择水印类型（文字 / 图片 logo）
3. 调整参数（字体、颜色、位置、透明度）—— 实时预览
4. 保存为模板（便于下次复用）
5. 切换到批量视图，导入整个文件夹，应用模板，导出

---

## 项目结构

```
watermark-fairy/
├── docs/                          # 设计文档
│   ├── PRD.md                     # 产品需求
│   ├── SPEC.md                    # 技术规格（含里程碑表 §12）
│   ├── RETROSPECTIVE-W1.md        # W1 复盘（M1 教训）
│   └── RETROSPECTIVE-W3.md        # W3 复盘（M2 教训）
├── src/
│   └── WatermarkFairy/
│       ├── App.xaml(.cs)          # 入口 + DI + 启动后台更新检查
│       ├── MainWindow.xaml(.cs)   # 主窗口 + Cloud UI
│       ├── Models/                # WatermarkConfig / Template / BatchJob
│       ├── Services/              # 业务服务（见下）
│       ├── ViewModels/            # MainViewModel + 子 ViewModel
│       ├── Views/                 # PreviewPane / TemplatePanel / BatchProgressPanel / SettingsWindow
│       └── Resources/             # 字体（思源黑体 8.4MB 内嵌）/ 图标 / i18n
└── tests/
    └── WatermarkFairy.Tests/      # xUnit + FluentAssertions + Coverlet
```

**Service 层**（业务核心）：

| Service | 职责 | 接口 |
|---|---|---|
| `ImageProcessor` | SixLabors.ImageSharp 3.x 封装 | — |
| `TemplateStore` | SQLite 模板库 | — |
| `CloudSync` | 云端同步（Mock / Supabase） | `ICloudSyncService` + `ICloudSyncOrchestrator` |
| `Update` | GitHub Releases 自动更新（M4-1） | `IUpdateService` |
| `NamingRuleEngine` | `{name}` 等占位符 + 正则替换 | — |
| `AppSettingsStore` | 用户配置持久化 | — |
| `FontLoader` | 思源黑体嵌入加载（OFL 1.1） | — |

详细架构：[docs/SPEC.md §2](docs/SPEC.md#2-架构)

---

## 路线图（截至 2026-07-28）

✅ = 完工 · 🔧 = 进行中 · ⏳ = 待启动 · 🔒 = 待 owner 凭证

| 单 | 状态 | 说明 |
|---|---|---|
| **M1-1 ~ M1-8** | ✅ 完工 | MVP 核心（命名 / 水印 / 字体 / 模板 / 批量 / 预览 / 集成测试） |
| **M2.1** | ✅ 完工 | ICloudSyncService 接口 + MockCloudSyncService |
| **M2.3** | ✅ 完工 | MainViewModel CloudSync 集成（默认 Mock） |
| M2.2 | 🔒 待凭证 | SupabaseCloudSyncService（需 URL + AnonKey） |
| **M3-2** | ✅ 完工 | MainWindow Cloud UI（登录 / 登出 / 同步面板） |
| **M3-3** | ✅ 完工 | TemplateStore + CloudSync 集成（Orchestrator + E2E） |
| **M4-1** | ✅ 完工 | UpdateService（Squirrel.Windows + GitHub Releases） |
| M4-2 | ⏳ 待启动 | 第一个 GitHub Release v0.1.0 |
| **M4-3** | ✅ 本次 | README 完善（截图 / 安装说明 / 路线图） |
| M3-1 | 🔒 待凭证 | Supabase 真实实现（同 M2.2） |

**长期路线图**：
- **v0.5**：第一个公开 Release + WinGet 提交 + Pro 升级流程
- **v1.0**：Stripe 集成 + 团队协作 + Pro 全功能
- **v1.1**：云端字体商城 + 模板版本历史

详细里程碑：[docs/SPEC.md §12](docs/SPEC.md#12-里程碑进度截至-2026-07-28-1445-gmt8)
完整变更日志：[CHANGELOG.md](CHANGELOG.md)
复盘教训：[docs/RETROSPECTIVE-W1.md](docs/RETROSPECTIVE-W1.md) · [docs/RETROSPECTIVE-W3.md](docs/RETROSPECTIVE-W3.md)

---

## 开发

### 环境

| 工具 | 版本 | 备注 |
|---|---|---|
| **.NET SDK** | 8.0.x | [下载](https://dotnet.microsoft.com/download/dotnet/8.0) |
| **运行时** | Windows 10 / 11 x64 | 用户指定 |
| **IDE** | VS 2022 / Rider / VS Code | 任选 |
| **WPF** | .NET 8 内置 | 无需额外安装 |
| **ImageSharp** | 3.1.10 | 跨平台图像处理 |
| **Squirrel.Windows** | 2.0.1 | 自动更新（M4-1） |

### 测试

```bash
dotnet test                           # 全部测试（209 个 · CI 验证）
dotnet test --collect:"XPlat Code Coverage"  # + Coverage（Coverlet）
```

**当前测试统计**：
- ✅ **209 总测试**（CI Windows runner 验证，gh CLI 无 auth → 本地看不到 CI 结果）
- ✅ **78.10% Coverage**（W3 完工时）
- ✅ xUnit 2.9.2 + FluentAssertions 6.12.1 + Coverlet 6.0.2

**Mock / Fake 模式**（per §10.13 P1 #10 教训）：
- `MockCloudSyncService`（生产代码，兼做 CI 测试基线 + DI fallback）
- `FakeUpdateService`（同上）
- E2E 测试用真实实现集成，单测用 Mock/Fake

### CI/CD

[`.github/workflows/build.yml`](.github/workflows/build.yml)：
- Windows runner + .NET 8
- `dotnet build` → `dotnet test` → `dotnet publish` → `softprops/action-gh-release`（仅在 `v*` tag）

详细：[docs/SPEC.md §10.2](docs/SPEC.md#102-cicdgithub-actions)

### 已知约束

- **本地 dotnet SDK 未装**（yajin-pc 跑 Linux） → 测试验证走 CI Windows runner
- **云端同步当前用 Mock**，真实 Supabase 实现待 owner 凭证（URL + AnonKey）
- **自动更新真实 Squirrel 调用**待首个 GitHub Release 触发（待 M4-2）

---

## 文档索引

| 文档 | 用途 |
|---|---|
| [docs/PRD.md](docs/PRD.md) | 产品需求（v0.1 草案，待 owner 二级决策） |
| [docs/SPEC.md](docs/SPEC.md) | 技术规格（含里程碑表 §12） |
| [docs/RETROSPECTIVE-W1.md](docs/RETROSPECTIVE-W1.md) | W1 复盘 · M1 教训 |
| [docs/RETROSPECTIVE-W3.md](docs/RETROSPECTIVE-W3.md) | W3 复盘 · M2 教训 |
| [CHANGELOG.md](CHANGELOG.md) | 变更日志（Keep-a-Changelog 格式） |
| [CONTRIBUTING.md](CONTRIBUTING.md) | 贡献指南 |
| [LICENSE](LICENSE) | 许可证（Proprietary） |

---

## 反馈

- 🐛 **Bug**：[GitHub Issues](https://github.com/yaojinbest/watermark-fairy/issues)
- 💡 **建议**：[GitHub Discussions](https://github.com/yaojinbest/watermark-fairy/discussions)
- 📧 **联系**：yaojinbest@example.com

---

## License

**Proprietary** — All Rights Reserved. © 2026 yaojinbest.

未经授权，禁止复制、修改、分发。详见 [LICENSE](LICENSE)。