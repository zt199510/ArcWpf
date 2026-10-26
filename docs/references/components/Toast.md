# Toast参考卡

[原页](https://uiarc.dev/components/toast)、[冻结清单](../../../tests/Baselines/References/2026-10-26-toast/manifest.json)。2026-10-26-toast为项目采集批次，非上游版本；免费Registry/MIT，源码/HTML/预览JS证据，未做截图或实际交互观察。

| 对象 | 来源事实 | WPF采用 |
|---|---|---|
| 主样本 | Save a draft；Changes saved；Synced to all devices. | Gallery同源文案 |
| 尺寸 | max-width 26rem、padding14/14/14/16、gap12、标题单行省略 | WPF默认416宽（26rem），长内容换行 |
| 外形 | border1、surface混合背景、panel圆角、floating shadow | Surface/Border/Panel26/ShadowFloating |
| 状态图标 | 30px成功圆形边框与绘制勾 | 自绘矢量，不引入图标库 |
| 关闭 | 32px无边框按钮，Dismiss notification；点击关闭 | ButtonBase路由关闭 |
| 动效 | 入场向上16/scale.96，morph .4；退出下沉8/淡出；自动4500ms；可重新打开 | WPF OwnedMotion近似，定时器不因悬停暂停 |
| 无障碍 | role status、aria-live polite、atomic；提示不抢焦点 | Toast AutomationPeer，关闭按钮名称 |

本次首期将原版单Toast扩展为ToastHost队列，便于WPF应用使用；不实现滑动手势、动作按钮、类型变体和全局通知服务。复杂队列可在后续ToastStack中实现。
