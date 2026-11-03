# BillingToggle

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/BillingToggle.md)，验证见[记录](../verification/2026-11-03-batch5-replication.md)。

## 范围与 API

`BillingToggle`：`Value`、`Options`、`Label`、`Size`、`ValueChanged`。`BillingPrice`：`Amount`、`Currency`、`Period`、`Was`、`Decimals`、`AmountSize`。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 切换周期 | 滑块 .34 弹簧；徽标换字 | `BillingToggle` | 测试+截图 bill-* | 中间帧未比对 |
| 价格变化 | 数字滚动；原价收放 | `BillingPrice`、`OdometerNumber` | 测试+截图 | 中间帧未比对 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
