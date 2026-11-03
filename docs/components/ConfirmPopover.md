# ConfirmPopover

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/ConfirmPopover.md)，验证见[记录](../verification/2026-11-03-batch5-replication.md)。

## 范围与 API

`Label`、`Title`、`Description`、`Icon`、`ConfirmText`、`ConfirmLabel`、`CancelLabel`、`PendingLabel`、`SuccessLabel`、`ErrorTitle`、`RetryLabel`、`Tone`、`Align`、`Side`、`CardWidth`、`ResultDuration`、`IsOpen`、`ConfirmAction`；事件 `Cancelled`、`Done`、`Failed`、`PhaseChanged`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 展开 | 宽高圆角同一表面变形（.42，推测） | `OpenCard` | 测试+截图 conf-remove | 参数为推测 |
| 输入短语 | 填充按匹配比例 | `OnTyped` | 测试+截图 conf-typing | 无 |
| 进行中 | Cancel 收起，确认键即进度条 | `RunAction` | 测试+截图 conf-work | 未与 Arc 逐帧比对 |
| 成功 | 折回按钮形状显示对勾 | `ShowSuccess` | 测试+截图 conf-success | 无 |
| 失败 | 留在卡片，错误块+抖动 | `ShowError` | 测试+截图 conf-error | 抖动幅度为推测 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
