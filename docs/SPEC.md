# Watermark Fairy · SPEC v0.1

> 技术规格 · v0.1 草案（2026-07-27）
> 与 PRD v0.1 对齐；待 owner 确认 WD 决策

---

## 1. 技术栈

| 层级 | 选型 | 理由 |
|---|---|---|
| **平台** | Windows 10 / 11 x64 | 用户指定 |
| **UI 框架** | WPF (.NET 8) | 用户指定；Windows 原生 |
| **MVVM** | CommunityToolkit.Mvvm 8.x | Microsoft 官方，零反射，编译生成 |
| **UI 主题** | ModernWPF UI 2.x | 现代化外观，无 XAML 资源冲突 |
| **图像处理** | SixLabors.ImageSharp 3.x | 跨平台 / MIT / 高质量 / 批量友好 |
| **图像显示** | WPF 内置 | 与 UI 渲染管道无缝 |
| **本地存储** | SQLite (Microsoft.Data.Sqlite) | 模板库 + 配置 + 历史 |
| **配置** | System.Text.Json | 内置，零依赖 |
| **日志** | Serilog | 结构化日志，文件 + 控制台 |
| **更新** | Squirrel.Windows | GitHub Releases 自动更新 |
| **云端模板** | Supabase (Postgres + Auth + Storage) | 待 owner 拍 |
| **支付** | 微信 + 支付宝 + Stripe | 待 owner 拍 |
| **CI/CD** | GitHub Actions | Windows runner + .NET 8 |

## 2. 架构

### 2.1 总体架构

```
┌─────────────────────────────────────────────────────┐
│                    View (XAML)                       │
│  MainWindow / PreviewPane / TemplatePanel / Settings │
└────────────────────┬────────────────────────────────┘
                     │ Data Binding
┌────────────────────▼────────────────────────────────┐
│                  ViewModel (MVVM)                    │
│  MainViewModel / PreviewViewModel / TemplateListVM  │
└────────────────────┬────────────────────────────────┘
                     │ Async / Await
┌────────────────────▼────────────────────────────────┐
│                   Service                            │
│  ImageProcessor / TemplateStore / CloudSync / Update │
└────────────────────┬────────────────────────────────┘
                     │
┌────────────────────▼────────────────────────────────┐
│                     Model                            │
│  WatermarkConfig / ProjectFile / Template / Job     │
└─────────────────────────────────────────────────────┘
```

### 2.2 模块划分

```
src/WatermarkFairy/
├── App.xaml / App.xaml.cs              ← 入口 + DI 容器
├── MainWindow.xaml / .cs                ← 主窗口
├── Views/                               ← 视图（XAML）
│   ├── PreviewPane.xaml
│   ├── TemplatePanel.xaml
│   ├── BatchProgressPanel.xaml
│   └── SettingsWindow.xaml
├── ViewModels/                          ← 视图模型
│   ├── MainViewModel.cs
│   ├── PreviewViewModel.cs
│   ├── TemplateViewModel.cs
│   └── BatchViewModel.cs
├── Models/                              ← 实体
│   ├── WatermarkConfig.cs
│   ├── TextWatermark.cs
│   ├── ImageWatermark.cs
│   ├── BatchJob.cs
│   └── Template.cs
├── Services/                            ← 业务服务
│   ├── ImageProcessor.cs                ← ImageSharp 封装
│   ├── TemplateStore.cs                 ← SQLite + JSON
│   ├── CloudSyncService.cs              ← Supabase 同步
│   ├── UpdateService.cs                 ← Squirrel 集成
│   ├── NamingRuleEngine.cs              ← 占位符 + 正则
│   └── AppSettings.cs                   ← 应用配置
├── Resources/Assets/                    ← 图标 / 字体 / i18n
└── Properties/
    ├── AssemblyInfo.cs
    └── Settings.settings
```

### 2.3 关键数据流

**单文件加水印**：
```
User 拖拽文件 → MainViewModel.ImportFiles()
                              │
                              ▼
             ImageProcessor.LoadAsync(path)
                              │
                              ▼
             PreviewViewModel.RenderPreview()
                              │
                              ▼
             User 调整参数（字体/颜色/大小/位置）
                              │
                              ▼
             User 点击"导出" → ExportAsync(path)
                              │
                              ▼
             ImageProcessor.ApplyWatermarkAsync()
                              │
                              ▼
             写入文件 + 更新历史
```

**批量处理**：
```
User 选择文件夹 → BatchViewModel.ScanFolder()
                              │
                              ▼
             NamingRuleEngine.ParsePattern()
                              │
                              ▼
             Parallel.ForEachAsync (channels)
                              │
                              ▼
             Progress<T> 实时回传
                              │
                              ▼
             失败重试 + 错误日志
```

## 3. 核心设计

### 3.1 水印配置模型

```csharp
public class WatermarkConfig
{
    public List<WatermarkLayer> Layers { get; set; } = new();
    public OutputOptions Output { get; set; } = new();
}

public abstract class WatermarkLayer
{
    public Position Position { get; set; } = Position.BottomRight;
    public double Opacity { get; set; } = 0.8;
    public int Rotation { get; set; } = 0;
    public double Margin { get; set; } = 20;
}

public class TextWatermark : WatermarkLayer
{
    public string Text { get; set; } = "";
    public string FontFamily { get; set; } = "Microsoft YaHei";
    public float FontSize { get; set; } = 24;
    public string Color { get; set; } = "#FFFFFF";
    public bool Stroke { get; set; } = false;
    public string StrokeColor { get; set; } = "#000000";
    public bool Shadow { get; set; } = false;
}

public class ImageWatermark : WatermarkLayer
{
    public string ImagePath { get; set; } = "";
    public float Scale { get; set; } = 0.2f;  // 占原图比例
}
```

### 3.2 命名规则引擎

支持的占位符：
- `{name}` - 原始文件名（不含扩展名）
- `{ext}` - 扩展名（不含点）
- `{date}` - 当前日期（YYYY-MM-DD）
- `{time}` - 当前时间（HHmmss）
- `{n}` - 序号（3 位补零）
- `{size}` - 图像尺寸（WxH）
- `{hash}` - 文件 MD5 短哈希（前 8 位）

示例：
```
{name}_watermarked_{date}_{n}
→ DSC0001_watermarked_2026-07-27_001.jpg
```

正则替换规则：
```
规则 1: IMG_(\d+) → photo-{n:000}
规则 2: \s+ → _
规则（条件）: if size > 1920×1080 then HD
```

### 3.3 实时预览

- 使用 WPF 的 `RenderTargetBitmap` 渲染预览
- 调参 → 防抖 100ms → 重新渲染（避免卡顿）
- 多图列表时切换不重渲染已缓存的图层

### 3.4 批量处理

- `System.Threading.Channels.Channel<T>` 任务队列
- `Parallel.ForEachAsync` 消费（N 核）
- 单图失败不影响其他图片
- 实时进度 + ETA 估算
- 输出格式支持原格式 / 统一格式

## 4. 云端架构（待 owner 拍）

### 4.1 Supabase 方案（推荐）

```
Supabase 项目
├── Auth (邮箱 / OAuth GitHub)
├── Postgres
│   ├── users (id, email, plan, created_at)
│   ├── templates (id, user_id, name, config_json, created_at, updated_at)
│   ├── template_shares (template_id, user_id, role)
│   └── team_members (team_id, user_id, role)
├── Storage (logo 资源云端存放)
└── Edge Functions (Pro 解锁校验 / 许可证生成)
```

### 4.2 同步策略

- 本地优先：所有操作先写本地 SQLite
- 后台 sync：每 30 秒 / 应用激活时 / 手动触发
- 冲突解决：last-write-wins（v1.0），乐观锁（v1.1）

## 5. 更新机制

- Squirrel.Windows + GitHub Releases
- 启动时检查更新（可关闭）
- 手动检查：设置 → 关于 → 检查更新
- 自动下载 + 提示安装
- 增量更新（differential）

## 6. 数据存储

### 6.1 本地

| 路径 | 内容 |
|---|---|
| `%APPDATA%\WatermarkFairy\config.json` | 应用设置 |
| `%APPDATA%\WatermarkFairy\templates.db` | 模板库（SQLite） |
| `%APPDATA%\WatermarkFairy\cache\` | 图片缩略图缓存 |
| `%APPDATA%\WatermarkFairy\logs\` | Serilog 日志 |
| `%LOCALAPPDATA%\WatermarkFairy\Updates\` | Squirrel 更新暂存 |

### 6.2 模板格式（JSON）

```json
{
  "version": "1.0",
  "name": "电商-右下角",
  "layers": [
    {
      "type": "text",
      "text": "@my_shop",
      "fontFamily": "Microsoft YaHei",
      "fontSize": 24,
      "color": "#FFFFFF88",
      "position": "bottomRight",
      "margin": 20
    }
  ]
}
```

## 7. 性能预算

| 指标 | 目标 |
|---|---|
| 启动时间 | < 2s |
| 内存占用（空闲） | < 200 MB |
| 内存占用（100 图批量） | < 600 MB |
| 单图加文字水印 (1080p JPG) | < 500ms |
| 100 图批量处理 | < 60s |
| 安装包大小 | < 50 MB |

## 8. 安全

- 本地 SQLite 数据库加密（DPAPI）
- 云端通信全部 HTTPS
- 敏感配置（API Key）存 Windows Credential Manager
- 日志脱敏（无水印内容 / 无文件名）

## 9. 测试策略

| 层 | 工具 | 覆盖率 |
|---|---|---|
| 单元测试 | xUnit + FluentAssertions | 80%+ |
| 图像处理 | 视觉回归测试（基线图对比） | 关键场景 |
| UI 自动化 | FlaUI / Appium | 主要流程 |
| 集成 | Testcontainers + 本地服务 | 关键链路 |

## 10. 部署

### 10.1 构建

```bash
dotnet build src/WatermarkFairy.sln -c Release
dotnet publish src/WatermarkFairy/WatermarkFairy.csproj \
  -c Release -r win-x64 --self-contained true \
  /p:PublishSingleFile=true
```

### 10.2 CI/CD（GitHub Actions）

```yaml
# .github/workflows/build.yml
name: Build
on: [push, pull_request]
jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x
      - run: dotnet build -c Release
      - run: dotnet test
      - run: dotnet publish -c Release -r win-x64 --self-contained
      - uses: softprops/action-gh-release@v1
        if: startsWith(github.ref, 'refs/tags/v')
        with:
          files: src/WatermarkFairy/bin/Release/net8.0-windows/win-x64/publish/*
```

### 10.3 发布

- GitHub Releases（自动）
- 官网下载（README 跳转）
- WinGet 提交（v1.0）

## 11. 待 owner 拍板的二级决策

| # | 决策点 | 选项 | 我的建议 |
|---|---|---|---|
| WD-1 | 本地构建 | A. .NET SDK + Wine / B. 远程 Windows / C. GitHub Actions Windows runner | **C** |
| WD-2 | 云端服务 | A. Supabase / B. Firebase / C. 自建 | **A** |
| WD-3 | 支付 | A. 微信 + 支付宝 / B. Stripe / C. Paddle | **A + B** |
| WD-4 | 更新 | A. Squirrel.Windows / B. WinGet 提交 / C. 自建 | **A + B** |
| WD-5 | 字体 | A. 仅系统字体 / B. 内置开源字体 / C. 云端字体商城 | **A → B（v1.1）** |

---

**变更记录**

| 版本 | 日期 | 变更 | 作者 |
|---|---|---|---|
| v0.1 | 2026-07-27 | 立项草案 | 可乐 |
