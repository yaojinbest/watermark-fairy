# Changelog

Watermark Fairy 的所有重要变更记录于此。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 立项阶段
- 2026-07-27 · 项目立项草案（PRD v0.1 + SPEC v0.1）

### M2 CloudSync（已完工）
- **M2.1** ICloudSyncService 接口 + MockCloudSyncService（CI 可测，Coverage 76.06%，+16 tests）
- **M2.3** MainViewModel CloudSync 集成（默认 Mock，Coverage 78.10%，+16 tests）
- **M2.2** SupabaseCloudSyncService ⏳ **待 owner 凭证**（URL + AnonKey）

### M3 CloudSync 集成 + Orchestrator（已完工）
- **M3-2** MainWindow Cloud UI（登录/登出/同步状态面板）+ 21 tests（feat `42f7ce1`）
- **M3-3** TemplateStore + CloudSync 集成：
  - `28b8d88` feat: TemplateStore 加 TemplateChanged 事件 + ICloudSyncOrchestrator 接口
  - `e790ab6` feat: DefaultCloudSyncOrchestrator 实现 + 单元测试 +14
  - `ae0403f` feat: MainViewModel 集成 Orchestrator + E2E 集成测试 +6
  - `70ccf5a` fix: TemplateStore 实现 IDisposable + fixture Dispose 顺序（资源泄漏修复）
  - `0b39887` fix: test 文件加 using System.IO + TextWatermarkLayer cast
  - `2b6aa11` fix: ICloudSyncOrchestrator 加 using WatermarkFairy.Models
  - `49fcc1f` fix: PushAllLocal 调用顺序 + pre-existing RelayCommand CanExecute
- **M3-1** Supabase 真实实现 ⏳ **待 owner 凭证**（同 M2.2）

### 测试统计
- W3 完工：156 测试
- M3-2：+21 = 177
- M3-3：+14 +6 = +20 = **197 总测试**（CI Windows runner 验证，gh CLI 无 auth → 本地看不到结果）

### M4 自动更新（进行中）
- **M4-1 UpdateService（Squirrel.Windows 集成）**：
  - `Services/IUpdateService.cs` —— 接口 + 2 个 result record（UpdateCheckResult / UpdateDownloadResult）
  - `Services/FakeUpdateService.cs` —— 默认 fallback（DI 兜底 + 测试基线 + 离线模式）
  - `Services/SquirrelUpdateService.cs` —— 生产实现（GitHub Releases 自动更新）
  - `tests/.../FakeUpdateServiceTests.cs` —— 12 个测试（默认无更新 / 强制有更新 / 取消 / 下载成功 / 下载失败 / 进度回调 / ApplyAndRestart / LastCheckTime / IsBusy 翻转 / CurrentVersion 同步 AssemblyVersion / 中文）
  - `App.xaml.cs` —— 启动时后台 fire-and-forget 检查更新，仅记录日志（不阻塞启动，不弹 UI；UI 通知留 M4-2/M4-3）
- **M4-2 第一个 GitHub Release v0.1.0** ⏳ 待启动
- **M4-3 README 完善** ✅：
  - 重写 README.md（4024 → ~14KB）
  - 加截图占位（docs/screenshots/01-07.png · TODO owner 截图替换）
  - 路线图更新：M1~M3-3 + M4-1 已完工状态 + M4-2/3-1 阻塞标注
  - 加项目结构 + Service 层表格
  - 加使用指南（第一次使用 / 保存模板 / 批量）
  - 加开发指南（测试统计 209 / Coverage 78.10% / CI 模式）
  - 加文档索引（PRD/SPEC/RETRO/CHANGELOG/CONTRIBUTING/LICENSE）
  - 加反馈渠道 + License 段落

### 测试统计（M4-1 增量）
- 197（上一节） + 12 = **209 总测试**

[Unreleased]: https://github.com/yaojinbest/watermark-fairy/compare/main...HEAD
