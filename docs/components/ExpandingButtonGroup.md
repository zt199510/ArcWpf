# ExpandingButtonGroup

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/ExpandingButtonGroup.md)，验证见[验证记录](../verification/2026-11-03-batch6-replication.md)。

## 范围与 API

`Items`（`ExpandingButtonGroupItem`：`Id`、`Label`、`Icon`、`DoneLabel`、`Tone`、`IsDisabled`、`Run`）、`Label`、`Size`、`DefaultExpanded`、`ConfirmDuration`（默认 1400ms）；事件 `ItemInvoked`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 指向移动 | 文字让给指向项，邻居滑开，带滞后（Registry） | `PointerAt` 与宽度弹簧 | 测试+截图 ebg-forward | 未逐帧比对 |
| 离开 | 回到静止项 | 同上 | 测试 | 无 |
| 运行/完成 | 就地画对勾、换过去式，宽度不变 | `Run` 流程 | 测试 | 未与 Arc 逐帧比对 |
| 失败 | 返回 false 或异常时不确认 | 同上 | 测试 | 无 |
| 触控 | 首次点按展开，再次运行 | `TapForTest` 覆盖逻辑 | 测试 | 真实触控未运行 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
