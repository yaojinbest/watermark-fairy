# Watermark Fairy · 水印精灵

> 极简 · 批量 · 云端模板 · Windows 桌面端照片水印工具

[![Status](https://img.shields.io/badge/status-v0.1.0%20Internal%20Alpha-yellow)](https://github.com/yaojinbest/watermark-fairy/releases)
[![Build](https://github.com/yaojinbest/watermark-fairy/actions/workflows/build.yml/badge.svg)](https://github.com/yaojinbest/watermark-fairy/actions/workflows/build.yml)
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)](https://github.com/yaojinbest/watermark-fairy)
[![License](https://img.shields.io/badge/license-Proprietary-red)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)](https://dotnet.microsoft.com/)
[![Stars](https://img.shields.io/github/stars/yaojinbest/watermark-fairy)](https://github.com/yaojinbest/watermark-fairy/stargazers)
[![Forks](https://img.shields.io/github/forks/yaojinbest/watermark-fairy)](https://github.com/yaojinbest/watermark-fairy/network/members)

## 概述

Watermark Fairy（简称 **WF**，中文名 **水印精灵**）是一款 Windows 桌面端照片水印工具。

- **文字 + 图片 logo 水印**：自定义字体、颜色、大小、位置
- **文件夹批量**：占位符 + 正则命名规则
- **实时预览**：所见即所得
- **云端模板**：一处配置，多设备同步（Supabase）
- **永久免费基础版** + **Pro 订阅解锁高级功能**

## 功能特性

### 免费版（v1.0）

- ✅ 文字水印（字体 / 颜色 / 大小 / 透明度 / 旋转）
- ✅ 图片 logo 水印（PNG / SVG）
- ✅ 9 宫格位置 + 自由拖拽
- ✅ 实时预览
- ✅ 文件夹批量处理（含子文件夹）
- ✅ 文件名占位符（`{name}` / `{date}` / `{n}` 等）
- ✅ 文件名正则替换规则
- ✅ 本地模板保存 / 加载 / 导入 / 导出
- ✅ JPG / PNG / WebP / 原格式输出
- ✅ 启动时联网更新检查

### Pro 版（订阅解锁）

- ⭐ 云端模板同步（多设备）
- ⭐ 团队共享模板库
- ⭐ 批量队列（≥10 任务）
- ⭐ 定时任务 / Watch 文件夹
- ⭐ EXIF 保留 / 清除
- ⭐ TIFF / HEIC 输入
- ⭐ SVG 矢量水印
- ⭐ 模板版本历史

详见 [docs/PRD.md](docs/PRD.md) §5 商业模式。

## 快速开始

> 🚧 v0.1.0 Internal Alpha 阶段，尚未发布可执行文件

### 从源码构建

```bash
# 1. 克隆
git clone https://github.com/yaojinbest/watermark-fairy.git
cd watermark-fairy

# 2. 安装 .NET 8 SDK
# https://dotnet.microsoft.com/download/dotnet/8.0

# 3. 构建
dotnet build WatermarkFairy.sln -c Release

# 4. 运行
dotnet run --project src/WatermarkFairy/WatermarkFairy.csproj -c Release
```

### 下载发布版

> 🚧 发布版待 M1 阶段提供

## 开发

### 环境

- .NET 8 SDK（[安装指南](https://dotnet.microsoft.com/download/dotnet/8.0)）
- Windows 10 / 11（运行时）
- Visual Studio 2022 / Rider / VS Code（可选）

### 构建

```bash
dotnet build WatermarkFairy.sln -c Release
```

### 测试

```bash
dotnet test
```

### 发布

```bash
dotnet publish src/WatermarkFairy/WatermarkFairy.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -o ./publish
```

CI/CD 全自动：[`.github/workflows/build.yml`](.github/workflows/build.yml)

## 文档

- [PRD v0.1](docs/PRD.md) - 产品需求文档
- [SPEC v0.1](docs/SPEC.md) - 技术规格
- [CHANGELOG](CHANGELOG.md) - 变更日志
- [CONTRIBUTING](CONTRIBUTING.md) - 贡献指南
- [LICENSE](LICENSE) - 许可证

## 路线图

| 阶段 | 周期 | 状态 |
|---|---|---|
| M0 立项 | 2026-07 W1 | ✅ 完成 |
| M1 Internal Alpha | 2026-08 W2 | ⏳ 下一步 |
| M2 Beta | 2026-09 W2 | ⏳ 待启动 |
| M3 v1.0.0 | 2026-10 W2 | ⏳ 待启动 |
| M4 v1.1 | 2026-11 | ⏳ 待启动 |

## 反馈

- 🐛 Bug：GitHub Issues
- 💡 建议：GitHub Discussions
- 📧 联系：yaojinbest@example.com

## License

Proprietary - All Rights Reserved. © 2026 yaojinbest.

未经授权，禁止复制、修改、分发。
