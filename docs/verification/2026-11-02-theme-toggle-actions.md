# ThemeSwitch / ToggleGroup / ActionSwap / UndoToast / ExpandableText 验证记录

日期：2026-11-02

来源冻结：五个控件均已保存 Registry（UndoToast 为 404）、Markdown、页面 HTML、动效令牌和许可快照；每个批次的 `manifest.json` 记录 SHA-256。

实现范围：

- `ThemeSwitch`：Light/Dark、四种 variant、iconOnly、主题事件和键盘激活。
- `ToggleGroup`：单选/多选、禁用项、ListBox 选择、方向键/Home/End 和 Arc surface/fill 外观。
- `ActionSwap`：状态列表、异步工作/成功/失败、HoldFor 和可绑定 Value。
- `UndoToast`：批次移除、倒计时、悬停暂停、Undo、Commit 和 Ctrl+Z。该控件按 Arc Pro 公开文档独立实现，没有复用付费源码。
- `ExpandableText`：按行折叠、溢出按钮、展开状态绑定和高度过渡。

验证命令与结果：

- `dotnet build src/ArcWpf.Controls/ArcWpf.Controls.csproj --no-restore`：0 错误。
- `dotnet build samples/ArcWpf.Gallery/ArcWpf.Gallery.csproj --no-restore`：0 错误，6 个既有警告。
- `dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-build --filter "FullyQualifiedName~ThemeSwitchTests|FullyQualifiedName~ToggleGroupTests|FullyQualifiedName~ActionSwapTests|FullyQualifiedName~UndoToastTests|FullyQualifiedName~ExpandableTextTests"`：10/10 通过。

待验收：Windows 实际窗口中的主题切换观感、真实鼠标/键盘焦点、明暗主题、多 DPI、Reduced/Off 动效、ExpandableText 的真实行高测量，以及 UndoToast 的完整行布局/服务端失败回滚。程序化测试和构建结果不替代这些窗口证据。

后续：同日对照线上页面重做，结果见 [像素对照记录](2026-11-02-theme-toggle-actions-pixel.md)；上面的范围和 10 项专项数字仅指首版。
