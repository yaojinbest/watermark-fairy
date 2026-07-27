# Watermark Fairy · W3 Retrospective (M2 CloudSync)

> 复盘周期：2026-07-27 21:50 GMT+8
> 涵盖：M2.1 接口 + Mock + M2.3 MainViewModel 集成
> M2.2 Supabase 真实实现待 owner 凭证

---

## 1. 概述

### 1.1 M2 完工情况

| Phase | 状态 | Coverage | 测试 | 备注 |
|---|---|---|---|---|
| M2.1 ICloudSyncService + MockCloudSyncService | ✅ 完工 | 76.06% | +16 | CI 可测 |
| M2.3 MainViewModel CloudSync 集成 | ✅ 完工 | 78.10% | +16 | 默认 Mock，UI 集成好 |
| M2.2 SupabaseCloudSyncService 真实实现 | ⏳ 待 owner 凭证 | — | — | 需 URL + AnonKey |
| M2.4 Cloud UI（MainWindow.xaml） | ⏳ M4 阶段 | — | — | 登录/同步 UI |

### 1.2 实际 vs 计划

| 指标 | 计划 | 实际 | 倍数 |
|---|---|---|---|
| M2.1 commits | 1 | 1 | 1x |
| M2.3 commits | 1 | 2（1 fix） | 2x |
| CI runs | 2 | 3 | 1.5x |
| 调试时长 | 0 min | ~15 min | — |

**M2 比 M1 顺利**（W1 调试链 25+ commits，M2 只 3 commits）。原因：接口先 Mock → CI 可测，UI 集成更早暴露问题。

### 1.3 总测试数演进

| 阶段 | 测试数 | 累计 |
|---|---|---|
| W2 完工（M1-8） | 124 | 124 |
| M2.1 | +16 | 140 |
| M2.3 | +16 | 156 |
| **M2 完工** | **+32** | **156** |

---

## 2. M2 调试教训

### 2.1 按主题分类

**A. 第三方库 API 行为（0 次）** — M2 没踩库 API 坑（W1 已踩完，W2/M2 复用 MEMORY）

**B. 路径 / 文件结构（0 次）**

**C. Edit 工具 race condition（0 次）** — M2 全部用 `write` 重写，零 race condition

**D. 编译版本 / 依赖（0 次）** — Supabase NuGet 没装（M2.2 还没做）

**E. 测试相关（2 次，占 67%）**：
- **CloudStatusText race condition**：LoginAsync 末尾调 RefreshCloudTemplatesAsync，后者覆盖 status text 为 "已加载 N 个云端模板"，导致测试断言 "已登录" 失败
- **Mock 需要 auth 才能 upload**：LoginAsync_AfterSuccess_RefreshesCloudTemplates 测试在 Login 前调 mock.UploadTemplateAsync 失败（mock.IsAuthenticated = false）

**F. 环境 / 网络（0 次）**

**G. 类型 / nullable（0 次）**

### 2.2 详细教训

#### E1. CloudStatusText race condition（新发现）

**根因**：
- LoginAsync 末尾自动调 RefreshCloudTemplatesAsync 提升 UX（用户登录后立即看到模板列表）
- RefreshCloudTemplatesAsync 设置 `CloudStatusText = "已加载 N 个云端模板"`
- 测试断言 `vm.CloudStatusText.Should().Contain("已登录")` 失败（实际值是 "已加载 0 个云端模板"）

**修复**：
- 测试改断言 `IsCloudAuthenticated` / `CloudUserEmail`（状态属性比 status text 稳定）
- VM 行为不变（自动 refresh 是好 UX）
- 教训：测试断言应优先用状态属性，status text 是易变的人类可读消息

**W3 + MEMORY §10 增量**：
```
**Status text 是易变的人类可读消息**（race condition 高发区）
→ 测试优先断言 ObservableProperty 状态（IsAuthenticated / UserEmail）
→ 状态属性稳定，status text 会被后续操作覆盖
→ 仅在测试 final 操作（无后续副作用）时断言 status text
```

#### E2. Mock 需要 auth 才能 upload（新发现）

**根因**：
- MockCloudSyncService.UploadTemplateAsync 检查 `if (!IsAuthenticated) return new CloudUploadResult(false, ErrorMessage: "未登录")`
- 测试 `LoginAsync_AfterSuccess_RefreshesCloudTemplates` 在 Login 前调 `await mock.UploadTemplateAsync(...)`
- Upload 失败（未登录），Mock store 仍为空
- Login 后 Refresh 看到 0 个模板，断言 `vm.CloudTemplates.Count.Should().Be(1)` 失败

**修复**：
- 测试改顺序：先 Login → 再 Upload → 再 Refresh → 最后断言

**W3 + MEMORY §10 增量**：
```
**Mock 设计：isAuthenticated 检查要真"严格"**（避免绕过）
→ 测试用 mock 时要按真实 API 顺序调用（auth 优先于 upload）
→ 否则 mock 静默失败，断言通过但实际行为错
→ E2E 测试用真实现（Supabase 真实），单测用 mock
```

---

## 3. 主要教训（按优先级）

### 🔴 P0（必改）— 无新增（M1 阶段已覆盖）

### 🟡 P1（强烈建议）— 新增 2 条：

1. **Status text 是易变的人类可读消息**（race condition 高发区）
   - 测试优先断言 ObservableProperty 状态
   - 状态属性稳定，status text 会被后续操作覆盖
2. **Mock 设计要"严格"**（避免绕过）
   - Mock 的 auth / 状态检查要和真实实现一致
   - 测试用 mock 时要按真实 API 顺序调用

### 🟢 P2（锦上添花）— 无新增

---

## 4. 改进建议（for M3 / W4）

### 4.1 流程改进

| # | 改进 | 实施时间 |
|---|---|---|
| 1 | Mock 设计规范文档化：每个 mock 都要列「和真实实现的行为差异」 | M3-1 |
| 2 | 测试断言规范：state property > status text | M3+ |
| 3 | E2E 测试用真实现（Supabase 本地），单测用 mock | M3+ |

### 4.2 技术债务

| # | 债务 | 优先级 | 解决时机 |
|---|---|---|---|
| 1 | M2.2 Supabase 真实实现（缺凭证） | 高 | owner 提供 URL + AnonKey |
| 2 | M2.4 Cloud UI（MainWindow.xaml） | 中 | M3 阶段 |
| 3 | Auto-update 服务（Squirrel 集成） | 低 | v0.5 |
| 4 | Schema 版本迁移（cloud_templates 表） | 低 | Pro 上线前 |

---

## 5. 经验沉淀（to MEMORY §10）

### 5.1 M2 CloudSync 完整流程

```
ICloudSyncService 接口
  ↓
MockCloudSyncService（CI 可测）
  ↓
MainViewModel 集成（默认 Mock）
  ↓
SupabaseCloudSyncService（需 owner 凭证）
  ↓
MainWindow.xaml Cloud UI（M3 阶段）
```

### 5.2 W3 关键代码模式

```csharp
// MainViewModel 构造函数：DI 友好 + 默认 Mock
public MainViewModel(ImageProcessor processor, TemplateStore? templateStore, ICloudSyncService? cloudSync = null)
{
    _processor = processor;
    _templateStore = templateStore;
    _cloudSync = cloudSync ?? new MockCloudSyncService();
    
    // 同步 cloud 初始状态
    _isCloudAuthenticated = _cloudSync.IsAuthenticated;
    _cloudUserEmail = _cloudSync.CurrentUserEmail;
}

// LoginAsync 末尾自动 refresh（UX 体验）
public async Task<CloudAuthResult> LoginAsync(string email, string password, CancellationToken ct = default)
{
    IsCloudSyncing = true;
    CloudStatusText = "登录中...";
    try
    {
        var result = await _cloudSync.LoginAsync(email, password, ct);
        IsCloudAuthenticated = result.Success;
        CloudUserEmail = result.UserEmail;
        CloudStatusText = result.Success ? "已登录：..." : "登录失败：...";
        if (result.Success)
        {
            await RefreshCloudTemplatesAsync(ct);  // ← 自动 refresh
        }
        return result;
    }
    finally
    {
        IsCloudSyncing = false;
    }
}
```

### 5.3 CloudTemplateInfo + Cloud*Result 模式

```csharp
// 4 个 result record（统一 API 返回值）
public sealed record CloudAuthResult(bool Success, string? UserEmail, string? ErrorMessage);
public sealed record CloudUploadResult(bool Success, long? CloudId, string? ErrorMessage);
public sealed record CloudDownloadResult(bool Success, TemplateRecord? Template, string? ErrorMessage);
// Info 列表
public sealed record CloudTemplateInfo(long CloudId, string Name, DateTime CreatedAt, DateTime UpdatedAt);
```

---

## 6. W3 → W4 / M3 路线图

### 6.1 M3（Cloud UI + 真实 Supabase）

| 单 | 内容 | 估时 |
|---|---|---|
| **M3-1** | M2.2 Supabase 真实实现（需 owner 凭证） | 1-2h |
| **M3-2** | M2.4 MainWindow.xaml Cloud UI（登录/登出/同步状态面板） | 2-3h |
| **M3-3** | TemplateStore + CloudSync 集成（本地 + 云端双写） | 1-2h |

### 6.2 M4+（Auto-update + 第一个 Release）

| 单 | 内容 | 估时 |
|---|---|---|
| M4-1 | UpdateService（Squirrel.Windows 集成） | 1-2h |
| M4-2 | 第一个 GitHub Release（v0.1.0） | 30 min |
| M4-3 | README 完善（截图 / 安装说明 / 路线图） | 1h |

### 6.3 长期（v1.0 上线前）

- Pro 功能完整实现（Pro 升级流程 / Stripe 集成 / 许可证管理）
- v1.0 发布（首个 stable release）
- 项目官网 + 用户文档

---

## 7. 项目总览（M0 + W1 + W2 + W3）

| 阶段 | 状态 | Commits | 测试 | Coverage |
|---|---|---|---|---|
| M0 立项 | ✅ | 3 | 4 | — |
| W1 复盘 | ✅ | 27 | 51 | 83.30% |
| W2（4 单） | ✅ | 14 | 124 | 76.43% |
| **W3（M2）** | **✅** | **3** | **156** | **78.10%** |
| **总计** | **✅** | **47** | **156** | **78.10%** |

**M0 → W3 总耗时**：~3.5 小时（21:00 立项 → 21:50 W3 收口）

**关键成就**：
- ✅ MVP 核心功能全部完工（M1-1 ~ M1-8）
- ✅ 用户配置持久化（M1-5）
- ✅ 模板本地存储（M1-4）
- ✅ 批量处理 UI（M1-6）
- ✅ 实时预览（M1-7）
- ✅ 端到端集成测试（M1-8）
- ✅ 云端同步 Mock（M2.1 + M2.3）
- ⏳ 云端同步真实（Supabase 需凭证）

**当前可演示的功能**：
- 选图 → 配置文字/图片水印 → 实时预览 → 批量处理 → 导出
- 模板本地保存/加载/导入/导出
- 用户配置持久化
- 云端同步 Mock（登录/上传/下载/列表/删除）

**待 Supabase 凭证后立即可用**：
- 真实云端模板同步
- 多设备同步
- 团队共享

---

## 8. 立即下一步

**W4 启动选项**：
- **A. M3-1 Supabase 真实实现**（需 owner 凭证，1-2h）
- **B. M3-2 MainWindow Cloud UI**（先做 UI，凭证后接入，2-3h）
- **C. M4-1 Auto-update（Squirrel）**（独立功能，1-2h）
- **D. 项目发布 v0.1.0**（截图 + README + 第一个 GitHub Release）

我推 **B**（M3-2 Cloud UI 先行，CI 用 mock 跑通，凭证后无缝切换到真实服务）。

进哥定 A/B/C/D？或者我先更新 MEMORY §10（+30 行 M2 增量）再等你定？