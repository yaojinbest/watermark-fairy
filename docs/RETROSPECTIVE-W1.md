# Watermark Fairy · W1 Retrospective (M0 + M1-1 + M1-2 + M1-2.1)

> 复盘周期：2026-07-27 立项 → M1-2.1 完工（约 2.5 小时）
> 涵盖：M0 立项 / M1-1 命名规则引擎 / M1-2 ImageProcessor 完整实现 / M1-2.1 思源黑体打包

---

## 1. 概述

### 1.1 实际 vs 计划

| 单 | 计划 commits | 实际 commits | 倍数 | 计划 CI 数 | 实际 CI 数 |
|---|---|---|---|---|---|
| M0 立项 | 1 | 1 (squash 后) | 1x | 0（docs only）| 0 |
| 仓库 + CI | 1 | 1 (squash 后) | 1x | 1 (scaffold verify) | 7（含 5 个 fix） |
| M1-1 命名规则 | 1 | 3（含 2 个 fix）| 3x | 1 | 4 |
| M1-2 ImageProcessor | 1 | 18（含 17 个 fix）| **18x** | 1 | **18** |
| M1-2.1 思源黑体 | 1 | 4（含 3 个 fix）| 4x | 1 | 4 |
| **合计** | 5 | 27 | **5.4x** | 4 | **33** |

**核心观察**：M1-2 是 W1 主要耗时点（18 commits / 18 CI runs / 占 80% 调试量）。

### 1.2 时间投入

| 阶段 | 时间 | 备注 |
|---|---|---|
| M0 立项 + 仓库 | ~20 min | 7 次 CI 调试（path/包/API）|
| M1-1 命名规则 | ~10 min | 3 commits，2 个 file/using 错 |
| M1-2 ImageProcessor | **~80 min** | 18 commits，15+ 个独立 fix |
| M1-2.1 思源黑体 | ~20 min | 1 个 env blocker + 3 个 API 错 |
| W1 复盘（本文档）| 写入中 | - |

---

## 2. 调试链分析

### 2.1 按主题分类

**A. 库 / API 错（12 次，占 40%）**

| # | 问题 | 根因 | 教训 |
|---|---|---|---|
| 1 | Squirrel 2.0.2 不存在 | nuget 版本号写错 | 先 `dotnet add package` 再 commit |
| 2 | ImageSharp 3.x 把 Drawing 拆包 | API 文档不熟 | 大版本升级要看 migration guide |
| 3 | SystemFonts.Get 抛异常非 null | API 行为误判 | 第三方库要 try-catch 兜底 |
| 4 | SystemFonts.Families 是 IEnumerable | 接口推断错 | 不要假设 LINQ 可用，要看实际类型 |
| 5 | FontCollection.Families 是 IEnumerable | 同上 | 同上 |
| 6 | FontFamily 是 struct 非 class | nullable 注解误解 | `<Nullable>enable</Nullable>` 后 struct 默认 non-nullable |
| 7 | DrawImage 多重载签名匹配错 | 跨 namespace 冲突 | ImageSharp.Drawing vs Processing |
| 8 | Image<Rgba32>(int, int) 隐式调 nullable Configuration | 内部 ctor 链 | 用 `Configuration.Default` 显式 |
| 9 | JpegEncoder / WebpEncoder 命名空间 | 子包引用 | `using SixLabors.ImageSharp.Formats.Jpeg` |
| 10 | DrawText options 字段名 | API 版本 | `Origin = new PointF(x, y)` |
| 11 | TextMeasurer.MeasureSize 返回 FontRectangle | API 变化 | `.Width` `.Height` |
| 12 | Image.LoadAsync<TPixel> vs LoadAsync | 泛型 API | 加载特定格式用 `LoadAsync<Rgba32>` |

**B. 路径 / 文件结构（3 次，占 10%）**

| # | 问题 | 教训 |
|---|---|---|
| 13 | .sln 路径双 src 重复 | .sln 应在根目录，project path 相对根 |
| 14 | UnitTest1.cs 用了旧 API | M1-2 改 API 后必须删/更新 M0 老测试 |
| 15 | 字体文件路径 Windows vs Linux | 用 `Path.GetTempPath()` 跨平台 |

**C. Edit 工具 race condition（4 次，占 13%）**

| # | 问题 | 教训 |
|---|---|---|
| 16 | ImageProcessor.cs 全删 224 行 | 多次 edit 后必须 `cat` 验证 |
| 17 | ImageProcessor.cs 全删（第二次）| edit 工具行为不稳定，**默认用 write 重写** |
| 18 | TestImageGenerator.cs 全删 79 行 | 同上 |
| 19 | Center → MiddleCenter 未生效 | 同样 edit 后必须 git status 验证 |

**D. 编译版本 / 依赖（3 次，占 10%）**

| # | 问题 | 教训 |
|---|---|---|
| 20 | DrawImage 实际重载不确定 | 多次实验 + 文档查询 → 最终弃用 API 改手动合成 |
| 21 | ImageSharp 3.1.5 有安全漏洞 | 升级到 3.1.10 |
| 22 | Squirrel.Windows 不再维护 | 改用 Squirrel 2.0.1 + 评估 Velopack（M1.x 评估） |

**E. 测试相关（3 次，占 10%）**

| # | 问题 | 教训 |
|---|---|---|
| 23 | Mutate 缺 using | `SixLabors.ImageSharp.Processing` 扩展方法 |
| 24 | Fill 缺 using | `SixLabors.ImageSharp.Drawing.Processing` 扩展方法 |
| 25 | `WatermarkPosition.Center` 错 | 枚举是 `MiddleCenter` |

**F. 环境 / 网络（1 次，占 3%）**

| # | 问题 | 教训 |
|---|---|---|
| 26 | GitHub raw 120s 超时 | 配套 jsDelivr CDN fallback（2.8s） |

**G. 类型 / nullable（2 次，占 7%）**

| # | 问题 | 教训 |
|---|---|---|
| 27 | `??` 不能用 non-nullable struct | nullable 注解下 `default(T)` 不是 null |
| 28 | null check 模式不适用 struct | 改 `Count` 属性 / `Any()` LINQ |

---

## 3. 主要教训（按优先级）

### 🔴 P0：必须改

1. **edit 工具不可靠** — 任何 edit 后必须 `cat` + `wc` + `git status` 三重验证
2. **M1 起 docs 同步更新** — PRD/SPEC 标 ✅ 时要带 commit sha 实证
3. **新库先用 5 行 PoC** — 任何新依赖先单独写个 hello-world commit，再纳入主项目

### 🟡 P1：强烈建议

4. **环境 blocker · fallback 链** — yajin-pc 经常 GitHub raw 超时，所有外部资源都备 fallback（jsDelivr/CDN/mirror）
5. **SixLabors.ImageSharp 文档不全** — 重要 API 行为（Get/Families）靠试错得出，记到 MEMORY
6. **CI 调试时间预算** — M1-2 实际 18x commits 是 plan 1x，W2+ 估时 × 2
7. **M0 老测试要在 M1 主动删** — UnitTest1.cs 在 M1-2 重新写 API 后才暴露问题

### 🟢 P2：锦上添花

8. **commit message 模板** — `feat/fix/test/refactor: scope` + 详细 body + 📎 实证
9. **CI 状态监控** — gh CLI watch 比 web UI 响应快，主动轮询
10. **M0 文档要写"已知不确定"** — PRD §7 / SPEC §11 留 TODO 项而不是 100% 拍板

---

## 4. 改进建议（for M1-3+）

### 4.1 流程改进

| # | 改进 | 实施时间 |
|---|---|---|
| 1 | M1 起 docs 同步机制：每个 M1 完工同步更新 PRD/SPEC 标 ✅ | M1-3 起 |
| 2 | 库 PoC 流程：新依赖先 5 行 hello-world 验证 | M1-3 起 |
| 3 | CI 调试时间预算：每个 M 单估时 × 2 | W2 起 |
| 4 | edit 工具三重验证：`cat` + `wc` + `git status` | 持续 |
| 5 | 跨平台测试路径规范：`Path.GetTempPath()` + `Guid.NewGuid()` | M1-3 起 |
| 6 | 库 API 探索记录：每个新库的 API 行为记到 MEMORY | M1-3 起 |

### 4.2 技术债务

| # | 债务 | 优先级 | 解决时机 |
|---|---|---|---|
| 1 | M1-2 调试链 18 commits 冗长 | 中 | W2 末 rebase squash |
| 2 | DrawImage API 完全弃用 | 中 | 重写时考虑改用其他方案 |
| 3 | Squirrel.Windows 2.0.1 已不维护 | 低 | v1.0 评估 Velopack |
| 4 | 字体 8.4MB 偏大 | 低 | M1-2.2 子集化 |

### 4.3 文档更新

- [x] RETROSPECTIVE-W1.md（本文件）
- [ ] MEMORY.md 增 M1-2 / M1-2.1 教训段
- [ ] SPEC §11 WD-5 标 ✅ 思源黑体打包完成
- [ ] README 更新"已实现"标记

---

## 5. 经验沉淀（to MEMORY.md）

> 完整 MEMORY fold 见下条 commit

### 5.1 SixLabors.ImageSharp 3.x API 关键行为

```csharp
// ❌ 错：SystemFonts.Get("NonExistent") 返 null
// ✅ 对：抛 FontFamilyNotFoundException
FontFamily? TryGetFontFamily(string name) {
    try { return SystemFonts.Get(name); }
    catch (FontFamilyNotFoundException) { return null; }
}

// ❌ 错：SystemFonts.Families.Count
// ❌ 错：families[0]
// ✅ 对：.Any() + .First()（IEnumerable<FontFamily>）
var families = SystemFonts.Families;
if (!families.Any()) throw ...;
return families.First();

// FontFamily 是 readonly struct（不是 class）
// ❌ 错：default(FontFamily) == null
// ✅ 对：default(FontFamily) 是 struct 零值
// ❌ 错：?? null check
// ✅ 对：Count 属性 / Any() LINQ

// FontCollection.Families 同理：IEnumerable<FontFamily>
```

### 5.2 ImageSharp 包依赖

```
ImageSharp 3.x                → 主包
ImageSharp.Drawing 2.x        → DrawText / Fill / 扩展方法
ImageSharp.Fonts 2.x          → SystemFonts / FontCollection
ImageSharp.Formats.Jpeg       → JPEG 编码
ImageSharp.Formats.Png        → PNG 编码
ImageSharp.Formats.Webp       → WebP 编码
Configuration.Default         → 显式传避免 nullable 警告
```

### 5.3 环境 blocker fallback 链

```
yajin-pc 网络：
  GitHub raw → ❌ 120s 超时
  jsDelivr CDN → ✅ 2.8s
  → 默认走 jsDelivr

M1-2.1 思源黑体下载：
  GitHub Adobe → 失败
  jsDelivr → 成功
  → 字体文件 commit 进 repo（8.4MB 可接受）
```

### 5.4 Edit 工具 race condition

```
现象：edit 多处后文件被替换为部分内容
     （ImageProcessor.cs 224 行被删 / TestImageGenerator.cs 79 行被删）
影响：commit 进去的是空文件
防御：
  1. edit 后立刻 `cat <file> | head -50` 验证
  2. `wc -l <file>` 确认行数
  3. `git status` + `git diff --stat` 确认
  4. 重大变更优先用 `write` 重写（不用 edit）
```

---

## 6. 下一步：M1-3 单元测试基线

### 6.1 M1-3 目标

- 当前：43 测试（26 命名 + 13 水印 + 4 字体）
- 目标：80%+ coverage + coverage 报告集成

### 6.2 M1-3 范围

1. **Coverage 报告** — Coverlet 集成，CI 输出 coverage
2. **缺失测试补全**：
   - ImageProcessor 边界（旋转、描边、阴影 — M1-2 暂未实现）
   - ImageProcessor 异常路径（无效输入、IO 错误）
   - NamingRuleEngine 边界（超长格式、空规则、特殊字符）
   - FontLoader 加载/失败场景
3. **基准线** — 80% coverage 写入 CI 检查（fail build if < 80%）

### 6.3 估时

- M1-2 实际 18x commits = 80 min
- M1-3 估时：30 commits（×2 缓冲）/ 120 min = **2 小时**

---

**变更记录**

| 版本 | 日期 | 变更 | 作者 |
|---|---|---|---|
| v1.0 | 2026-07-27 | W1 复盘（27 commits / 33 CI runs）| 可乐 |
