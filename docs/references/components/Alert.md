# Alert 参考基准

来源：[Arc Alert](https://uiarc.dev/components/alert)、[冻结批次](../../../tests/Baselines/References/2026-10-26-alert/manifest.json)、[源码快照](../../../tests/Baselines/References/2026-10-26-alert/alert.json)。

| 维度 | Arc事实 | WPF落地 |
|---|---|---|
| 语义 | 页面内状态提示，支持 info、success、warning、danger 四种语气 | `Alert : ContentControl`，`Tone` 使用 `AlertTone` 枚举 |
| 布局 | 图标、标题、描述、可选关闭按钮横向排列；内容高度随文案变化 | Grid 四列，标题/说明与自定义 Content 保留原生 WPF 内容 |
| 外形 | Surface 背景、1px 描边、控制圆角、16px 内边距、轻阴影 | 圆角18、描边1、Padding16，使用 Arc 主题资源 |
| 图标 | 18px；语气决定颜色和图标 | 使用 WPF Path 绘制，不引入 Lucide 运行库 |
| 关闭 | 可选 dismiss 按钮；关闭时收起并触发回调 | `IsDismissible`、`IsOpen`、`Dismiss()`、`Dismissed` 事件 |
| 动效 | 入场/退出透明度与垂直位移，减少动效时缩短过渡 | 使用项目 `OwnedMotion`；Off 立即完成，实际时钟仍待窗口验收 |

Arc 源码中的复杂内容高度弹簧与字词替换动效不直接移植为 Web API；WPF 版本优先保证布局稳定、绑定不被覆盖、可访问名称和原生 Content 行为。
