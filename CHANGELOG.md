# Changelog

Watermark Fairy 的所有重要变更记录于此。

格式基于 [Keep a Changelog](https://keepachangelog.com/zh-CN/1.1.0/)，
本项目遵循 [Semantic Versioning](https://semver.org/lang/zh-CN/)。

## [Unreleased]

### 待 v0.2.0
- **M2.2 SupabaseCloudSyncService** · 需 owner 提供 Supabase 项目凭证（URL + AnonKey）
- **M3-1 Supabase 真实集成** · 同 M2.2（解锁 v0.1.0 Mock 模式 → 真实云端同步）
- **M4.5 真自动更新** · Squirrel.Windows 不兼容 net8.0-windows，需选替代方案（Velopack / 自建 / 跳过）
- 截图替换（`docs/screenshots/01-07.png` · README 占位 · 待 owner 截图）

## [0.1.0] - 2026-07-28

首个 **Internal Beta Release**。基于 commit `8388fdf` 构建。

### Added

#### M1 · MVP 核心（M1-1 ~ M1-8）
- 选图 → 配置水印（文字 / 图片）→ 实时预览 → 批量导出
- 模板本地 CRUD（SQLite 持久化 + JSON 导入导出）
- 命名规则引擎（regex / 占位符 / 日期格式）
- 用户配置持久化（应用设置 + 字体路径）

#### M2 · CloudSync（mock 模式）
- **M2.1** `ICloudSyncService` 接口 + `MockCloudSyncService`（CI 可测，Coverage 76.06%，+16 tests）
- **M2.3** `MainViewModel` CloudSync 集成（默认 Mock 兜底，Coverage 78.10%，+16 tests）

#### M3 · CloudSync 集成 + Orchestrator
- **M3-2** `MainWindow.xaml` Cloud UI（登录 / 登出 / 同步状态面板）+ 21 tests（`42f7ce1`）
- **M3-3** TemplateStore + CloudSync 集成：
  - `28b8d88` feat: TemplateChanged 事件 + `ICloudSyncOrchestrator` 接口
  - `e790ab6` feat: `DefaultCloudSyncOrchestrator` + 单测 +14
  - `ae0403f` feat: MainViewModel 集成 Orchestrator + E2E 集成测试 +6
  - `70ccf5a` fix: TemplateStore IDisposable + fixture Dispose 顺序
  - `49fcc1f` fix: PushAllLocal 调用顺序 + pre-existing RelayCommand CanExecute

#### M4 · 自动更新（基础）
- **M4-1** `IUpdateService` 接口 + `FakeUpdateService` fallback（DI 兜底 + 测试基线 + 离线模式）
  + `SquirrelUpdateService` 生产实现（GitHub Releases 自动更新）
  + `App.xaml.cs` 启动时后台 fire-and-forget 检查更新（仅日志，不弹 UI）
  + 12 tests（默认无更新 / 强制有更新 / 取消 / 下载成功 / 下载失败 / 进度回调 / ApplyAndRestart / LastCheckTime / IsBusy 翻转 / CurrentVersion 同步 / 中文）
- **M4-3** README 完善（截图占位 + 路线图 + 项目结构 + 使用指南 + 开发指南 + 文档索引）

### Fixed

#### B1 · CI 6 处修复（`c80a754` + `8388fdf`）
1. `TemplateStore.OpenConnection` 加 `Pooling=False`（连接 Dispose 真关 · 释放文件句柄）
2. `TemplateStore.Dispose` 加 `SqliteConnection.ClearAllPools()`（释放池化历史连接）
3. `DefaultCloudSyncOrchestrator` 加 `_suppressAutoPush` flag + `OnTemplateChanged` 早返回 + `PullAllCloudAsync` 包裹 try/finally
4. `DefaultCloudSyncOrchestrator.PushAllLocalAsync` 跳过 `_localToCloudId` 已映射项（避免 FullSync 重复 push）
5. `MockCloudSyncService.UploadTemplateAsync` 用 record 真实 `CreatedAt`/`UpdatedAt`（不再覆盖为 now）
6. 测试：`DefaultCloudSyncOrchestratorTests` 调换 Add/Attach 顺序 + `MainViewModelCommandsTests.DeleteCloudCommand` 改传 `CloudTemplateInfo` 而非 long

效果：CI 23 failed → 0 failed（CI run #30357856323 全绿）

### Known Limitations（v0.2.0 解锁）

| 项 | 阻塞 | 解锁方式 |
|---|---|---|
| **M2.2 Supabase 真实实现** | 待 owner 凭证（URL + AnonKey） | owner 跑 Supabase 建项目 → 写入 `appsettings.json` / 环境变量 |
| **M3-1 Supabase 集成** | 同 M2.2 | 同上 |
| **M4.5 真自动更新** | Squirrel.Windows 不兼容 net8.0-windows | 选 Velopack / 自建 OTA / 跳过（手动下载 Release） |
| **截图替换** | 待 owner 截图 | owner 跑应用截图 → 替换 `docs/screenshots/*.png` |

### 测试统计

- **233 个测试方法**（`[Fact]` + `[Theory]` 声明）→ **CI 实际 245 passed / 3 skipped / 0 failed**（`8388fdf` on commit `8388fdf`）
- Coverage：**78.10%+**（CI Windows runner + Coverlet 测量）
- CI：GitHub Actions `Build & Test` workflow（`windows-latest` + .NET 8）· 全绿

### 安装 / 使用

1. 下载 `WatermarkFairy-v0.1.0-win-x64.zip`（Release 资产）
2. 解压到任意目录
3. 双击 `WatermarkFairy.exe` 运行
4. 前置：**Windows 10/11**（net8.0-windows TFM）
5. 已知不兼容：**macOS / Linux**（跨平台在 v0.5 规划）

[Unreleased]: https://github.com/yaojinbest/watermark-fairy/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/yaojinbest/watermark-fairy/releases/tag/v0.1.0