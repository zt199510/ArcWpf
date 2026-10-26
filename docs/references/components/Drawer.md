# Drawer 参考基准

[原页](https://uiarc.dev/components/drawer)、[Markdown 合约](https://uiarc.dev/components/drawer/markdown)、[冻结清单](../../../tests/Baselines/References/2026-10-26-drawer/manifest.json)。采集批次 `2026-10-26-drawer`，官方免费 Registry 与页面已固定；没有做截图或运行观察。

| 对象 | Arc事实 | WPF采用 |
|---|---|---|
| 主样本 | Filters；用于保持页面上下文的侧面板 | Gallery 使用 Filters / Narrow the list of projects 文案，改用原生 `DrawerHost` 宿主 |
| 组成 | `Drawer`、`DrawerTrigger`、`DrawerContent`、`DrawerClose` | `DrawerHost` 管理背景与当前 `Drawer`；`Drawer` 保留 `ContentControl` 内容、CloseCommand 和关闭按钮 |
| 位置 | `side` 为 left/right/top/bottom，默认 right | `DrawerSide` 枚举，默认 Right；面板挂在宿主边缘 |
| 几何 | 左右 `min(30rem, 100vw - gutter)`；上下 `min(32rem, 100dvh)` | 左右最大480 DIP，上下最大512 DIP；宿主尺寸不足时由WPF布局压缩 |
| 面板 | Surface、边框、浮动阴影、圆角；正文可滚动 | `PART_Surface` 内部 Border/ScrollViewer，保留动态主题资源 |
| 头部 | 标题、可选说明、关闭按钮；头部也是拖拽手柄 | 标题/说明/关闭按钮；保留标题可访问语义。拖拽首期暂缓，关闭按钮始终可用 |
| 遮罩 | 模态背景，4px backdrop blur | `DrawerHost` 禁用并模糊背景 Presenter，`PART_Overlay` 捕获外部点击 |
| 键盘 | Escape 关闭并还原触发焦点；Tab/Shift+Tab 在面板内循环 | Host 级焦点保护与 Drawer 内循环；真实窗口焦点仍待用户验收 |
| 动效 | 进入边缘弹簧，退出较快 tween；Reduced 关闭拖拽并仅淡化 | WPF `OwnedMotion` 近似实现；Full 平移/透明度，Reduced 仅透明度，Off 即时 |

来源只作行为、几何和令牌依据，不直接复用 React/CSS 源码。Registry 标识组件为 free/open source；本项目保留来源链接与批次哈希，重新分发前仍需复核许可边界。

## 状态与动效索引

源码快照与页面内容位于冻结批次；标题/说明切换、拖拽 rubber-band 和触摸交互只有文本/源码证据，没有本项目运行观察。WPF 版本首期支持四边、模态、关闭/取消、绑定、焦点、主题和基础进退场；拖拽关闭、保留释放速度和自适应触摸手势暂缓。

## WPF差异和回归入口

- React/Radix 的 root/portal 拆分为 `DrawerHost` + `Drawer`，避免把网页 Portal API 暴露给 WPF 使用方。
- 退出期间保留 `IsPresented` 和遮罩，直到 Dispatcher 动画完成；重新打开会使旧代次回调失效。
- 模糊只影响 Host 的 WPF 背景 Presenter；独立 HwndHost 不保证参与。
- `tests/ArcWpf.Tests/DrawerTests.cs` 覆盖 API、布局、绑定、关闭取消、生命周期和程序化时钟；实际窗口、截图、DPI、读屏与性能待验收。
