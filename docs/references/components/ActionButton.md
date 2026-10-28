# ActionButton 来源参考卡

- 来源：Arc 免费 Registry `https://uiarc.dev/r/action-button.json`（MIT），页面 <https://uiarc.dev/components/action-button>。
- 冻结批次：`tests/Baselines/References/2026-10-28-action-button`（项目采集批次，非上游版本），文件与 SHA-256 见 manifest.json。
- 主示例：预览文案 `Save changes`，单个实心按钮，无变体。

## 源码确认的状态链

| 状态 | 文案 | 图标槽（17×17） |
|---|---|---|
| idle | label | lucide arrow-right，17px，stroke 2 |
| pending | pendingLabel | 16px 圆环 spinner，1.5px 边框、右边透明，0.7s 线性旋转 |
| success | successLabel | 勾号 `M4 12l5 5L20 6`，自绘 |

- `run()` 只在 pending 时忽略点击；success 状态再次点击会重新执行。成功后立即进入 success，没有中间阶段。失败直接回 idle 并调用 onActionError。
- 文案：旧文案 opacity→0（0.1s，ease.standard）；新文案从 opacity 0、y 2 进入（0.16s，ease.enter）；不带模糊。宽度 spring visualDuration 0.24、bounce 0，形变期间裁剪。
- 图标：一次只显示一个。箭头离场 x→8、opacity 0、blur 2（0.16s standard），复位时从 x -6 返回；spinner/勾号从 scale .6、opacity 0、blur 2 进入（scale 用 snappy spring 0.26，opacity/blur 0.16s enter），离场反向。勾号 pathLength 0→1，0.24s enter，延迟 0.05s。
- 按压 scale .97（宽 >220 时 .985），pending/disabled 不按压。Reduced：无按压、无宽度 spring、无描绘，文案瞬切，图标 0.12s 淡入淡出。
- 外形：min-height control-height-md（44），padding 0 20，gap 8，1px foreground 边框，背景 foreground，文字 background，text-sm/500；hover 阴影 shadow-resting；disabled（非 pending）opacity .7；pending 光标 progress。
