# SplitButton 组件规格

状态：实现完成，程序化验证 2/2 通过，实际窗口待验收。

## 来源

Arc 官方 Split button 页面：<https://uiarc.dev/components/split-button>（2026-10-28 读取）。用途是一个默认主动作加一个相邻的菜单动作入口；行为和视觉仅作参考，不复用上游源码。

## API

`SplitButton : Control`：

- `Label`：主动作文字。
- `Command` / `CommandParameter`：主动作命令。
- `Items`：右侧菜单的 `ObservableCollection<MenuItem>`。
- `MenuLabel`：右侧菜单的辅助名称。
- `IsOpen`：受控 Popup 状态。
- `Click`：主动作调用事件。
- `ItemInvoked`：菜单动作转发事件。

## 交互与键盘

左侧区域执行主命令，右侧 chevron 打开 Menu。主区域保留 WPF Button 键盘/禁用语义；右侧复用 Menu 的 Enter、Space、ArrowDown、Escape、焦点归还和禁用条目处理。主动作和菜单动作互相独立，主命令不会因打开菜单而执行。

## 视觉与动效

共享外壳、分隔线、主动作区域和 chevron 菜单区域；Popup 复用 Menu 的 Surface、焦点、高亮和开关动效。Reduced/Off 由 Menu 和 Button 的现有状态处理；拆分外壳的圆角与命中区域保留为实际窗口验收项。

## 明确差异

主动作使用 WPF `ICommand`，菜单条目使用项目 `MenuItem.Command`；不引入 React `onClick` 或 Radix trigger。实际窗口需验收主/副区域命中、Popup 对齐、键盘焦点、主题和 DPI。
