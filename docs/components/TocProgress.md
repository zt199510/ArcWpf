# TocProgress

状态：待验收。阅读进度目录：一条线沿标题层级弯折，读过的部分填色，圆点跟随滚动，并显示剩余时间。事实见[参考卡](../references/components/TocProgress.md)，验证见[验证记录](../verification/2026-11-03-batch9-replication.md)。

Arc Pro 组件：依据公开文档和公开页面的显示结果独立实现，没有使用 Pro 源码。

## 范围与 API

`Items`（`TocProgressItem`：`Id`、`Label`、`Level` 1–3、`Target`）、`ScrollContainer`（ScrollViewer 或 ScrollArea）、`Offset`（96）、`ActiveId`、`ReadingTime`、`Title`、`ShowTimeLeft`、`Label`、`Accent`；事件 `ActiveChanged`、`Navigate`。

## 复用情况

复用 `ScrollArea`（通过 `Viewport` 跟随，Gallery 主示例即如此）和 `SwapText`。任务点名的 `Progress` **未直接复用**：进度是沿折线的路径长度，不是直线条。

## 动效核对

| 触发状态 | 属性与参数依据 | WPF 实现位置 | 验证证据 | 剩余差异 |
|---|---|---|---|---|
| 滚动带动进度 | snappy 弹簧；运行观察 | `Follow`、`_position` | 真实滚轮：tp-mid | — |
| 当前标题变色 | 字色过渡；运行观察 | `Follow` | `ScrollingMoves...` | — |
| 点击滚动到标题 | 平滑滚动；文档描述 | `Choose` | 真实点击：滚到 1113、当前项 8 | — |
| 剩余时间变化 | 文字纵向交替；运行观察 | `SwapText` | tp-mid、tp-end | 边界待核对 |
| 减弱动效 | 直接到位；文档描述 | `Choose` | 未单独运行 | 待验证 |

## 已知差异

- 剩余分钟数的跳变边界与 Arc 有偏差（待核对）。
- Gallery 文章正文为本项目原创，所以文章区的像素差值不可比。
- Arc 标题字距（-.84 等）未在 Gallery 文章标题上应用，TextBlock 没有字距属性。
