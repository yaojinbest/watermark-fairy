# Contributing to Watermark Fairy

> 本项目目前为闭源商业项目，**暂不接受外部 Pull Request**。

## 反馈方式

- 🐛 Bug 报告：GitHub Issues
- 💡 功能建议：GitHub Discussions
- 🔒 安全漏洞：yaojinbest@example.com（请勿公开）

## 内部开发流程

内部开发者请参考：

- [docs/PRD.md](docs/PRD.md) - 产品需求
- [docs/SPEC.md](docs/SPEC.md) - 技术规格
- [docs/CHANGELOG.md](CHANGELOG.md) - 变更日志

### 分支策略

- `main` - 主分支（受保护，需 PR + review）
- `feature/*` - 功能分支
- `fix/*` - 修复分支
- `release/*` - 发布分支

### 提交规范

```
<type>(<scope>): <subject>

<body>

<footer>
```

类型：`feat` / `fix` / `docs` / `style` / `refactor` / `test` / `chore` / `perf`

示例：
```
feat(batch): add placeholder {date} support

支持 YYYY-MM-DD 格式占位符，可与 {n} 组合使用。

Closes #12
```

### PR 流程

1. Fork 内部分支
2. 提交并写清 description
3. 关联 issue / task
4. 通过 CI + 1+ reviewer approve
5. Squash merge 到 main

## License

本项目采用 Proprietary License，见 [LICENSE](LICENSE)。
