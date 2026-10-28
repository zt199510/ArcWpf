# CopyButton 组件规格

状态：实现完成，程序化验证 3/3 通过，实际窗口待验收。

## 来源

Arc 官方 Copy button 页面：<https://uiarc.dev/components/copy-button>（2026-10-28 读取）。来源用途是将值复制到剪贴板并用短暂成功状态反馈；仅参考公开行为和视觉，不复用上游源码。

## API

`CopyButton : Button` 保留 WPF 原生命令、键盘、禁用和 Automation 语义，并增加：

- `Value`：复制到剪贴板的文本。
- `Label` / `CopiedLabel`：Idle/Success 文案。
- `ResetAfterMs`：成功态复位延迟；`0` 保持成功态。
- `IsCopied`：只读成功状态。
- `Copy()`：公开复制入口。
- `CopyCommand`：路由命令，支持快捷键和绑定。
- `CopyFailed`：携带异常的路由事件。

## 状态与动效

- Idle：显示复制图标和 Label。
- Success：文字 morph 到 CopiedLabel，内部宽度 spring，复制图标离场，勾号绘制进入。
- Reduced：直接切换文字和图标；Off 停止所有时钟并进入终态。
- Disabled：保留原生 Button 禁用语义和主题不透明度。

## WPF 适配

浏览器 Clipboard API 映射为 WPF `Clipboard.SetText`；异常通过 `CopyFailed` 暴露，不依赖异步 Web API。主模板保留 Button 的 `PART_LabelSlot`/`PART_Label`/`PART_PreviousLabel`，复用现有文字 morph 与内部宽度过渡。实际剪贴板权限、读屏 LiveRegion、DPI 和动态观感仍待窗口验收。

