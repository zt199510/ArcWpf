# CopyButton 验证记录（2026-10-28）

## 实现

- 新增 `ArcWpf.Controls.CopyButton`，复用 Button 原生按压、命令、键盘和 Automation 行为。
- 实现 Clipboard.SetText、CopyCommand、CopyFailed、成功复位、文字 morph、宽度 spring、Copy/Check 图标 phase。
- Gallery 新增 CopyButton 页面，覆盖自动复位、保持成功和失败入口。

## 验证

- 构建/测试：CopyButtonTests 3/3 通过。
- 实际窗口：待人工验收。
- 本任务不运行其他控件测试，不使用 computer-use。

