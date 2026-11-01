# HoverCard 组件规格

状态：Arc Registry 已冻结；WPF 定时、放置、焦点与 Profile 行级动效已按来源核对，实际鼠标/触摸和视觉待验收。来源与参数见[参考卡](../references/components/HoverCard.md)。

`HoverCard : ContentControl`：`Content` 是原生可聚焦触发器，`CardContent` 是只读预览；`Side`、`Alignment`、`OpenDelay`（默认 500ms）、`CloseDelay`（默认 140ms）、`IsOpen` 为外观与状态契约。`HoverCardProfile` 提供头像、姓名、角色、生平、数据项和 meta 的可复用预览内容。

| 交付项 | 本次范围 | 状态/差异 |
|---|---|---|
| 鼠标/笔首次延迟、warm 切换、跨间隙保留 | 必做 | WPF 计时器与窗口共享 warm 状态 |
| 键盘焦点、Enter、Escape 抑制重开、触摸点击 | 必做 | 触发器原生焦点，不将焦点移入卡片 |
| 四方向/三对齐、屏幕边缘翻转 | 必做 | 复用 PopoverPositioner、MonitorWorkArea 与 Popup CustomPlacement；ActualSide 反映最终方向，12 DIP 碰撞留白 |
| 冷/warm/Reduced/Off 进退场和反向打断 | 必做 | OwnedMotion 近似 Motion spring；速度不保留 |
| 296 DIP 面板、16 DIP 内边距、主题/阴影 | 必做 | CSS px→WPF DIP 适配；多 DPI 待测 |
| Profile 内容与子行 stagger | 必做 | 头像、Bio、Stats、Meta 使用 240ms 入场与 35ms 错峰，Reduced/Off 即时 |
| 单一活动卡片、卸载/宿主失活清理 | 必做 | Window 状态槽、生命周期释放 |

真实 Windows 鼠标穿越、焦点外观、四边碰撞、明暗主题、Reduced/Off、100/150/200% DPI 与读屏需要与程序化证据分开记录。

当前 Popup 外框承担阴影留白与鼠标跨缝命中，卡片宽度上限 296 DIP，长内容在内部 ScrollViewer 滚动。触发器 ContentPresenter 不再新增 Tab 停靠点，由原生子按钮/链接承载焦点；外部窗口点击即时关闭且不抢回触发器焦点。Popup 框架显式继承控件的 DataContext、FlowDirection 与 ArcMotion.Level。窗口位置/尺寸/DPI、锚点布局和内容尺寸变化触发重排；来源 12 DIP 碰撞区域和实际翻转已在显示窗口中测量。触摸合成鼠标事件的抑制已编码，物理触摸仍待验收。
