# Drawer 侧面板规格

状态：代码及程序化验证完成，待实际窗口验收。见[验证记录](../verification/2026-10-26-drawer.md)与[参考卡](../references/components/Drawer.md)。

`Drawer : ContentControl` 在 `DrawerHost` 内提供临时的模态侧面板。它不创建独立 OS 窗口，背景由宿主保留，面板可以从左、右、上或下进入。

## WPF公开契约

- `DrawerHost : ContentControl`：`Content` 是背景，`Drawer` 是当前面板；同一宿主同时只有一个 Drawer，替换会强制关闭旧项。宿主模板使用 `PART_Background` 和 `PART_Drawer`。
- `Drawer.IsOpen`：`bool`，默认 `false`，双向绑定；未附加宿主时保留使用方意图。
- `Drawer.Title` / `Description`：`string`，默认空；标题用于自动化名称回退，说明用于帮助文本回退。
- `Drawer.Side`：`DrawerSide`，默认 `Right`，取 `Left`、`Right`、`Top`、`Bottom`。
- `CloseOnEscape`、`CloseOnOverlayClick`：`bool`，默认 `true`。
- `InitialFocusElement`：`IInputElement?`，必须属于当前 Drawer；否则进入面板后聚焦第一个可聚焦后代，最后回退面板本身。
- 只读 `IsPresented`：包含进入、打开和退场阶段；关闭动画结束后为 `false`。
- `Show(FrameworkElement origin)`、`Close()`、静态 `CloseCommand`；`Closing` 可取消，`Opened` / `Closed` 表示实际状态边界。
- `DrawerCloseReason`：`Programmatic`、`Escape`、`Overlay`、`CloseButton`、`HostUnavailable`、`Replaced`、`TemplateChanged`。

宿主在 Drawer 呈现期间禁用并模糊背景，保留消费方 `DataContext`、绑定、Effect 和 Transform。Drawer 的内容仍沿 WPF 原生 ContentControl 继承。没有必要模板部件时不留下遮罩或持续时钟。

## 状态与交互

| 状态/触发 | 外观 | 操作结果 | 焦点/键盘 | 数据与事件 |
|---|---|---|---|---|
| `IsOpen=true` | 面板贴靠 `Side` 边缘，遮罩出现 | 进入并设置 `IsPresented` | 进入后聚焦初始元素或首个后代 | `Opened` 一次 |
| Escape | 面板保持内容，退场到边缘 | 若 `CloseOnEscape` 则关闭 | 退场结束归还打开前焦点 | `Closing(Escape)`，可取消 |
| 遮罩左键 | 遮罩捕获点击 | 若 `CloseOnOverlayClick` 则关闭 | 退场期间继续阻止背景输入 | `Closing(Overlay)`，可取消 |
| 关闭按钮 / `CloseCommand` | 同普通退出 | 关闭 | 归还焦点 | `Closing(CloseButton/Programmatic)` |
| 绑定改为 false | 退场，内容保持到完成 | 更新 `IsOpen`，不破坏绑定 | 归还焦点 | `Closed` 在完成后触发 |
| Host 卸载/替换/模板更换 | 立即清理 | 强制关闭，不允许取消 | 清除排队焦点与动画 | 对应强制原因 |

Tab 与 Shift+Tab 在面板内循环；Host 预览焦点事件阻止模态期间焦点逃到背景。Drawer 自身不接管使用方 DataContext。

## 视觉与动效

左右面板最大宽度 480 DIP，上下最大高度 512 DIP；面板边框1、SurfaceRaised、Floating shadow，默认内边距24，头部与正文分隔线1，圆角22（窄视口18）。正文用原生 ScrollViewer；标题18/500，说明14，说明上间距8；关闭按钮32方形，沿用 DialogClose 背景与 16 DIP 的 `M 4,4 L 12,12 M 12,4 L 4,12` 圆角 X 几何。

Full：面板从边缘平移到0并以约0.4秒弹簧进入，退出约0.16秒 tween；遮罩进入约0.24秒、退出约0.16秒。Reduced：面板不平移，只做约0.12秒透明度过渡，关闭拖拽。Off：立即达到终态。动画只占用模板内部 `PART_Surface` 的 Opacity/TranslateTransform 和 `PART_Overlay` 的 Opacity；停止、反向、卸载和换模板会清理时钟并以代次保护回调。

拖拽手柄、rubber-band、快速 flick 保速关闭、标题/说明逐层替换和跨设备性能观测暂缓；不把源码参数近似写成逐帧相同。

## 验收场景

1. 在 `DrawerHost` 中声明绑定的 Drawer，布局后打开，确认四个 `Side` 的面板尺寸、标题/说明/内容和主题资源。
2. Escape、遮罩和关闭按钮分别触发关闭；取消 `Closing` 时保留打开状态，绑定仍存在。
3. 打开前有焦点元素，打开后进入 Drawer，Tab/Shift+Tab 不逃出，关闭后还原焦点。
4. 连续打开/关闭、关闭期间重开、卸载、更换模板和 `ArcMotionLevel` Full/Reduced/Off，确认旧动画不清理新状态且无持续时钟。
5. Gallery 显示 Arc 主样本与左/上/下、长内容、关闭保护扩展；Consumer 只引用 Controls 和公共主题。

实际窗口交互、视觉截图、DPI、读屏、拖拽和性能由用户既定约束延期，保持待验收。

## 实施与完整度

代码：`src/ArcWpf.Controls/Drawer/`；Gallery：`samples/ArcWpf.Gallery/Pages/DrawerPage.xaml`；测试：`tests/ArcWpf.Tests/DrawerTests.cs`；来源基准：`tests/Baselines/References/2026-10-26-drawer/`。

| 交付项 | 范围 | 状态 | 证据/剩余差异 |
|---|---|---|---|
| API、绑定、Host、关闭事件 | 必做 | 已实现，程序化验证 | STA 测试；实际窗口待验收 |
| 四边几何、长内容、主题 | 必做 | 已实现，程序化验证 | 布局测量；视觉/DPI待验收 |
| 进退场、打断、清理、Reduced/Off | 必做 | 已实现，程序化验证 | Dispatcher 时钟；拖拽暂缓 |
| 焦点、键盘、自动化 | 必做 | 已实现，程序化验证 | 真实焦点/读屏待验收 |
| Arc 主示例与扩展示例、Consumer | 必做 | 已实现 | 不启动可见窗口 |
| 截图、实际窗口、性能、拖拽 | 暂缓 | 未运行 | 依用户约定；不能由单测替代 |
