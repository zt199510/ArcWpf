# 2026-11-03 ConfirmPopover / DropdownMenu / BillingToggle / ChatThread / CommentThread / ImageCompare 复刻

状态：**待验收**。环境：Wine + Xvfb 离屏渲染，字体为 Liberation 改名替身（文字宽度、字重、纵向 1px 差异属环境差异），不是真实 Windows 窗口。

## 已运行

- 构建：测试项目与 Gallery 均 0 错误。
- 专项测试（按用户要求只跑这六个控件，未跑全量）：`ConfirmPopoverTests`、`DropdownMenuTests`、`BillingToggleTests`、`ChatThreadTests`、`CommentThreadTests`、`ImageCompareTests`，53/53 通过。
- `MenuTests.HighlightCoversSelectedRowAndLabelsStartAtItemPadding` 失败（标签 X 为 38，期望 11）。在未含本批改动的 HEAD 基线上同样失败，不是本批引入。其余 `MenuTests`、`ContextMenu` 相关测试未运行，尽管 `DropdownMenu` 改动了 `Menu` 基类的成员可见性和 `PlacePopup`。

## 截图差异（灰度差 > 32 的像素占比）

| 状态 | 占比 | 状态 | 占比 |
|---|---|---|---|
| bill-monthly | 4.14% | comm-stage | 6.73% |
| bill-yearly | 4.37% | comm-delete | 6.89% |
| conf-rest | 3.42% | comm-edit | 7.75% |
| conf-remove | 4.82% | comm-picker | 7.07% |
| conf-delete | 4.77% | comm-reply | 6.70% |
| conf-typing | 4.99% | comm-resolved | 5.41% |
| conf-unlocked | 4.99% | chat-top | 7.38% |
| conf-error | 4.80% | chat-sent | 7.38% |
| conf-after | 3.76% | chat-after | 7.75% |

差异主要来自替身字体、头像和图片替换，占比不能单独作为通过结论。`conf-*` 打开态的 WPF 截图四周多 40 留白，已裁掉后计算。comm 系列的 WPF 与 Arc 截图都显示操作按钮（WPF 用 `AlwaysShowActions`），真实情况是悬停才显示。

以下状态**不可比**，占比不作结论：

- `img-h`、`img-v`（64%）：Gallery 用原创插画替换 Arc 照片。
- `chat-picker`（25.44%）：两边滚动位置和所选消息不同。
- `chat-typed`（12.31%）：两边输入文字和滚动位置不同。
- `comm-collapsed`（8.56%，高度 498 对 420）：WPF 对比脚本处于回复状态，Arc 截图不是。
- `dd-open`：Popup 单独截取，无同尺寸 Arc 图；只按 DOM 几何核对，菜单 192×129 一致，相对触发器 x 按右缘对齐，y 为 42（Arc 41.5）。

## 未运行 / 待验证

- 真实窗口、DPI、真实键盘与鼠标输入、IME、触控、真实剪贴板和文件拖放。
- 彩色 emoji：Wine 前缀没有 Segoe UI Emoji，显示为方块，渲染结果未验证。
- 动效只核对终态，未逐帧对比；`conf-work`、`conf-success` 未计算差异，`img-drag` 未比对。
- 全量测试。

## 已知差异

见各组件规格和参考卡。ConfirmPopover 为 Arc Pro，证据上限为运行观察，动效参数是推测。
