# WalletStack

状态：待验收。卡包堆叠：悬停扇开，拖动翻阅，点开后卡片升起，其余收入底部并显示余额和交易。事实见[参考卡](../references/components/WalletStack.md)，验证见[验证记录](../verification/2026-11-03-batch9-replication.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Cards`（`WalletStackCard`）、`Label`、`Currency`（USD）、`Locale`；事件 `SelectedChanged`。`WalletStackCard`：`Id`、`Issuer`、`Product`、`LastFour`、`Holder`、`Variant`（Metal/Black/Glass/Color）、`Tint`、`Expires`、`ShowNetwork`、`Image`、`Balance`、`BalanceLabel`、`Transactions`。只接受后四位，网络标识是虚构的。

## 复用情况

复用弹簧、帧循环与 `SwapText`。任务点名的 `CardStack`、`Card` **未直接复用**：所有卡的姿态是连续位置的纯函数（`PoseOf`），Z 序不变，CardStack 的滑走模型无法表达。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 悬停扇开 | smooth 弹簧 + 44.8×…；运行观察 | `PoseOf`、`_fan` | 真实鼠标：ws-hover | — |
| 拖动翻阅 | 一比一跟手，向下推入底袋；首尾橡皮筋；文档描述 | `DragTo`、`Up` | 真实鼠标：位置 0→0.58→1 | 甩动手感未在真实窗口评估 |
| 打开与关闭 | 选中 .88，其余收入底袋；运行观察 | `Choose`、`Close` | 真实点击：ws-open；`ChoosingOpens...` | — |
| 指针光斑 | 跟随指针；页面 CSS 确认 | `Sheen` | ws-hover | — |
| 余额滚动 | `OdometerText`；运行观察 | `_money` | `ChoosingOpens...` | — |
| 减弱动效 | 直接到位；文档描述 | `各动画入口` | 未单独运行 | 待验证 |

## 已知差异

- 没有真实 3D 透视：用 SkewTransform 模拟倾斜（WPF 适配）。
- 玻璃卡没有背景模糊，WPF 没有该能力；不透明度比实测略高，后方卡片的文字仍会隐约可见。
- 材质纹理由程序生成，颗粒与 Arc 不同。
- 设备陀螺仪倾斜未实现。
