# DropdownMenu

状态：待验收。专项测试与离屏对比已完成，真实窗口未运行。事实见[参考卡](../references/components/DropdownMenu.md)，验证见[记录](../verification/2026-11-03-batch5-replication.md)。

## 范围与 API

`Menu` 的全部 API（`Label`、`Icon`、`Items`、`IsOpen`、`ItemInvoked`）。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 打开/关闭 | 按 CSS 进出场 | `AnimatePopupIn`、`BeginExit` | 截图 dd-open | 中间帧未比对 |
| 指针悬停 | 高亮滑动 snappy；离开 70ms 后清除 | `UpdateHighlight` | 测试+截图 dd-hover | 无 |
| 键盘移动 | 高亮直接跳转 | `DropdownMenu` | 未运行真实按键 | 待真实窗口 |
| V形箭头 | .58 弹簧令牌 | `AnimateChevron` | 截图 | 中间帧未比对 |

## 已知差异

见参考卡“WPF差异和回归入口”。文字宽度差异来自替身字体，不作通过结论。
