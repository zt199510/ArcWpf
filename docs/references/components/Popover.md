# Popover 参考基准

来源：[原页](https://uiarc.dev/components/popover)、[冻结批次](../2026-10-25-initial.manifest.json)、[源码](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/popover/popover.tsx)、[CSS](../../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/components/popover/popover.module.css)、[文档示例](../../../tests/Baselines/References/2026-10-25-initial/arc/docs/popover.md)。

| 对象 | 已确认参考 | 未确认/采用方式 |
|---|---|---|
| 对照样本 | 冻结文档示例：Secondary按钮Share；内容Anyone with the link can view. | 可作为首个文档对照样本，不能声称是原站实时主预览 |
| 初始状态 | 无open/defaultOpen的文档用法 | WPF规格初始关闭；运行未观察 |
| 本体 | min192/max352、padding16、圆角26、描边1；CSS与16px根字号映射 | 实际工作区更小时允许缩小 |
| 配色/阴影 | SurfaceRaised、Foreground、Border、shadow-floating；CSS | 阴影需WPF转换并核对可见边缘，不把阴影边界算入间距 |
| 锚点 | 不按压缩放；源码 | 使用Button.IsPressAnimationEnabled=false |
| 定位 | offset6、collisionPadding10、align=start；源码 | RTL/工作区/跨DPI由WPF实现并验证 |
| 动效 | 进入位移5/scale.97、退出位移2.5/scale.98；CSS | 进入透明度160ms/transform580ms；退出160ms；与文档140ms冲突采用CSS |
| 展示容器 | 未冻结 | 编码前取得原站主预览HTML/CSS或用户图；无法取得时保留“文档样本”标记 |

开发前整理阶段没有新访问原站或运行页面；本次开发补充静态HTML，仍没有新增视觉验证。已有事实足以开展状态机/定位实现；主预览完全对照尚缺证据，不阻断独立编码也不能宣称视觉完成。详细API和技术验证见 [Popover规格](../../components/Popover.md)。

## 2026-10-25 开发采集补充

已读取原页静态HTML：关闭态触发器为“Why this status?”，辅助文字为“Anchored context stays close to its trigger”。HTML未提供打开后的面板内容；没有执行浏览器点击或截图。当前部署与初始冻结批次不混为一个版本。

证据：tests/Baselines/References/2026-10-25-popover-page/page.html及同目录manifest.json。Gallery采用已冻结Share文档样本，显式标识来源，未猜测官网面板内容。实现差异与验收见 [验证记录](../../verification/2026-10-25-popover.md)。

WPF宿主机制参考：[Popup Placement Behavior](https://learn.microsoft.com/en-us/dotnet/desktop/wpf/controls/popup-placement-behavior)。它提供机制依据，不提供Arc视觉依据。
