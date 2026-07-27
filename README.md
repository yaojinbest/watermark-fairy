# Watermark Fairy · 水印精灵

> 极简 · 批量 · 云端模板 · Windows 桌面端照片水印工具

[![Status](https://img.shields.io/badge/status-pre--alpha-yellow)]()
[![Platform](https://img.shields.io/badge/platform-Windows%2010%2F11-blue)]()
[![License](https://img.shields.io/badge/license-Proprietary-red)]()
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4)]()

## 概述

Watermark Fairy（简称 **WF**，中文名 **水印精灵**）是一款 Windows 桌面端照片水印工具。

- **文字 + 图片 logo 水印**：自定义字体、颜色、大小、位置
- **文件夹批量**：占位符 + 正则命名规则
- **实时预览**：所见即所得
- **云端模板**：一处配置，多设备同步
- **永久免费基础版** + **Pro 订阅解锁高级功能**

## 截图

> 🚧 立项阶段，截图待补

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

> 🚧 立项阶段，尚未发布

## 开发

### 环境

- .NET 8 SDK
- Windows 10 / 11（运行时）
- Visual Studio 2022 / Rider / VS Code

### 构建

```bash
dotnet build src/WatermarkFairy.sln -c Release
dotnet publish src/WatermarkFairy/WatermarkFairy.csproj \
  -c Release -r win-x64 --self-contained true
```

### 测试

```bash
dotnet test
```

详见 [docs/SPEC.md](docs/SPEC.md) §10 构建发布。

## 文档

- [PRD v0.1](docs/PRD.md) - 产品需求文档
- [SPEC v0.1](docs/SPEC.md) - 技术规格
- [CHANGELOG](CHANGELOG.md) - 变更日志
- [CONTRIBUTING](CONTRIBUTING.md) - 贡献指南
- [LICENSE](LICENSE) - 许可证

## 路线图

| 阶段 | 周期 | 状态 |
|---|---|---|
| M0 立项 | 2026-07 W1 | 🚧 进行中 |
| M1 Internal Alpha | 2026-08 W2 | ⏳ 待启动 |
| M2 Beta | 2026-09 W2 | ⏳ 待启动 |
| M3 v1.0.0 | 2026-10 W2 | ⏳ 待启动 |
| M4 v1.1 | 2026-11 | ⏳ 待启动 |

## 反馈

- 🐛 Bug：GitHub Issues
- 💡 建议：GitHub Discussions
- 📧 联系：yaojinbest@example.com

## License

Proprietary - All Rights Reserved. © 2026 Watermark Fairy.

未经授权，禁止复制、修改、分发。
