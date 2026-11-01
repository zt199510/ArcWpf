# 滚动条样式统一记录（2026-11-01）

用户反馈：滚动条样式有些控件没同步。

## 定位

控件内部模板层。此前只有 ScrollArea 自绘 Arc 滚动条，List、Select、ComboBox、MultiSelect、Dialog、Drawer、Popover、HoverCard、ContextMenu、TreeView、Textarea、MoneyInput/PhoneInput 菜单、PresenceStack、FilterToolbar 等都显示 Windows 默认滚动条。

## 修改

- 新增 `src/ArcWpf.Core/Themes/ScrollBar.xaml`，由 `ArcThemeResources` 合并，对所有原生 `ScrollBar` 生效。
- 取值来自 `tests/Baselines/References/2026-10-30-scroll-area` 的 scroll-area.module.css：轨道 11 DIP，两端内缩 3，pill 5→7 DIP，颜色为前景 30%→46%（复用 `ScrollAreaThumb` / `ScrollAreaThumbHover`）。
- 外观取自 ScrollArea 是本次的统一决定：Arc 这些列表多用浏览器原生 `scrollbar-width: thin`，源码没有为它们定义统一的 thumb 样式。
- 无箭头按钮；保留点击轨道翻页和拖动。

## 验证（Wine + 替代字体，非真实窗口）

- 构建：0 错误。
- 新增 `ScrollBarThemeTests` 4 项通过：List、TextBox、ScrollViewer 纵横向、Select 下拉、Drawer；翻页、主题切换、应用覆盖。
- 全量：改动后 551 项 537 通过、14 失败；基线 `62ce13a` 547 项 534 通过、13 失败。13 项两边相同，属既有失败，未处理。
- 多出的 1 项 `SelectionGlidesAndRetargetsFromCurrentPositionThenStops` 是全量负载下的动效计时断言（48.87 对 49.86），单独重复 6 次在两边均通过，判断为计时抖动。
- 离屏渲染：浅色、深色下 pill 外观和颜色正常。

未运行：真实 Windows 窗口、DPI、悬停放大和拖动的实际手感。

## 剩余差异

- 不自动淡出：滚动条在可滚动时常显，ScrollArea 仍是悬停或滚动时显示。
- 悬停放大和变色无过渡，也不受 ArcMotion 控制；ScrollArea 有 160ms 过渡。
- 最小滑块由 WPF `Track` 决定，长列表的滑块会比 ScrollArea 的最小 28 DIP 短。
- 应用内所有原生 ScrollBar 都受影响，包括使用方自己的 ScrollViewer；可在外层字典用隐式样式覆盖。

## 顺带发现（未修改）

`Textarea` 模板根是 StackPanel，设固定 `Height` 不会限制内部高度：`Height=120` 加 40 行文本时没有出现滚动条。
