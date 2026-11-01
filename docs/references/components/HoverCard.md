# HoverCard Arc 参考卡

来源：Arc Free Registry、Markdown、页面、共享动效令牌和 MIT 许可；2026-11-01 冻结于 [`tests/Baselines/References/2026-11-01-hover-card`](../../../tests/Baselines/References/2026-11-01-hover-card/manifest.json)。manifest 保存所有文件的 SHA-256。以下数值来自冻结 TSX/CSS，不是 WPF 测量结果。

## 结构与几何

- 触发器保留原有点击用途（例如链接）；卡片是只读预览，不接收自动焦点。
- 浮层宽 18.5rem，视口可用宽度上限减 24px；内边距 `space-4`（16px），1px 边框，`radius-panel`、`surface-raised` 和 `shadow-floating`。侧边间隙 8px，碰撞留白 12px。
- 默认 `side=bottom`、`align=start`。四方向可翻转；动效起点取实际放置的边。
- 来源附带 `HoverCardProfile`：48px 头像、姓名、角色、生平、可换行数据项与 meta 行。

## 状态与输入

- 首次鼠标/笔悬停等待 500ms；卡片打开或关闭后 300ms 内切换触发器，只等待 80ms，并仅淡入。
- 离开触发器或卡片后等待 140ms；跨 8px 间隙进入卡片取消关闭。一次只显示一个卡片。
- 键盘焦点立即打开，Enter/鼠标点击立即打开；触摸点击切换。Escape 关闭后，指针仍停在触发器时不重开，完全离开才解除抑制。
- 浮层只读，焦点不转移；关闭不强行抢回焦点。卸载清理计时器和活动卡片。

## 动效

- 冷打开：面板从靠触发器一侧 4px、scale .96 开始，以 smooth spring 到原位；透明度 160ms ease-enter。
- warm 打开：无位移/缩放，140ms 进度与 90ms 淡入；Reduced 无位移/缩放、150ms 淡入。
- 关闭：120ms 位移/缩放进度和 120ms 淡出；Reduced 淡出 100ms。重入从当前属性值反向，不重置后跳变。
- Profile 子行进入按 `standard` 240ms、item stagger 35ms；WPF 是否逐项实现须在规格中单列。

许可：Arc Free 组件 MIT；WPF 代码按来源行为和视觉重新实现，未引入 React/Radix/Motion 运行时。WPF 最终字体与像素外观仍需实际窗口对照。
