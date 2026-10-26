# Switch 参考基准

来源：[原页](https://uiarc.dev/components/switch)、[冻结批次](../2026-10-25-initial.manifest.json)、[源码](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/switch/switch.tsx)、[CSS](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/switch/switch.module.css)。

图片：[缺陷对比A](../../../tests/Baselines/References/2026-10-25-user-feedback/switch-comparison-a.png)、[目标对比B](../../../tests/Baselines/References/2026-10-25-user-feedback/switch-comparison-b.png)、[归档清单](../../../tests/Baselines/References/2026-10-25-user-feedback/manifest.json)。图片由用户提供，捕获时间、DPI及浏览器环境未知；不是自动接受的WPF回归基准。

| 对象 | 基准/证据 | 当前实现/限制 |
|---|---|---|
| 主示例 | Weekly digest，初始On；页面HTML读取确认 | 已使用同文案与状态 |
| 排列 | 轨道在左，标签在右，间隔12；源码及用户图 | 对齐同一垂直中心 |
| 轨道 | 42×24，内距3；源码 | WPF圆角12形成平直段+半圆端 |
| 滑块 | 18×18，位移18，按压宽23；源码 | WPF圆角9；不得套9999 |
| 状态 | On/Off/禁用，Space按压和程序切换；源码/文档 | 真实键盘专项未逐项确认 |
| 动效 | 位移glide .3、宽度snappy .26、脉冲.34；源码 | 保速解析近似；轨道淡化/阴影仍未实现 |
| 容器 | 之前读取CSS：预览上限620，状态行间距16/最小18/字号12 | Gallery外层卡片是项目容器，非Switch几何 |
| 字体 | 原站字体未分发 | Windows系统回退 |

用户在胶囊外形修复后确认测试没问题。几何回归检查胶囊平直段像素、圆端外角透明、伸长后的半径。详见 [验证记录](../../verification/2026-10-25-switch.md) 与 [规格矩阵](../../components/Switch.md)。源码批次与在线部署版本一致性未独立确认。
