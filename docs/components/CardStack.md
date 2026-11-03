# CardStack

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/CardStack.md)，验证见[验证记录](../verification/2026-11-03-batch6-replication.md)。

## 范围与 API

`ItemsSource`、`CardTemplate`、`LeftLabel`、`RightLabel`、`Label`、`CardHeight`、`LabelSelector`、`LeftOutcome`、`RightOutcome`、`EmptyFactory`、`EmptyText`；事件 `Decided`、`Undone`、`ResetCompleted`；方法 `Decide`、`Undo`、`Reset`；`Position`、`Remaining`。

`EmptyText` 是 WPF 便利入口：Arc 用 `renderEmpty` 写自定义内容，这里只换标题和说明时不必重建按钮。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 拖动 | 旋转随抓取半区，印章淡入，后卡上升（Registry） | `CardView`、`CardStackMath` | 测试+截图 cs-drag | 未逐帧比对 |
| 甩出/判定 | 速度 500、距离阈值、飞出方向 | `Decides`、`Exit` | 测试 | 真实指针未运行 |
| 撤销 | 从离开位置飞回 | `Undo` | 测试 | 未逐帧比对 |
| 用尽 | 空状态与 Start over | `ShowEmpty` | 测试+截图 cs-empty | 无 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
