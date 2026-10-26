# Dialog参考卡

[原页](https://uiarc.dev/components/dialog)、[冻结清单](../../../tests/Baselines/References/2026-10-26-dialog/manifest.json)。采集批次2026-10-26-dialog不是官方版本；免费Registry/MIT。源码、页面和预览JS确认，未做截图/运行观察。

| 对象 | 来源事实 | WPF采用 |
|---|---|---|
| 主样本 | Review changes；提示Opens a focused decision surface | 同文案，初始关闭 |
| 面板 | Publish workspace；Your team will see these changes immediately. | 标题/说明同源 |
| 正文/操作 | Three files are ready to publish. You can still revise them later.；Keep editing、Publish now | 普通正文及两按钮，按钮仅演示关闭，不发布数据 |
| 轮廓 | 最大宽440，视口边距16，最大高视口减32；border1，radius-surface34，SurfaceRaised，Floating shadow | DialogHost可用区域内居中；标题及正文整体滚动 |
| 头部/正文 | padding24；标题18/500/line1.4；说明14，top8；头部与关闭按钮gap20；分隔线1 | 固定几何，系统字体回退 |
| 关闭按钮 | 32方形，半径18，16矢量X/1.75；标题第一行中心对齐，负上边距(25.2-32)/2=-3.4；hover颜色/press .96 | 使用Arc按钮按压机制和专属模板 |
| 遮罩 | oklch(10% 0 0 / .46)，backdrop blur7 | 专属背景呈现器BlurEffect7，不修改消费内容Effect |
| 进入 | 遮罩240ms；面板opacity160ms、y8到0、scale.96到1，smooth .4 | WPF关键帧近似spring，不承诺逐帧相同 |
| 退出 | 遮罩/面板160ms，面板y4/scale.98 | 保留至退场完成；重新打开从当前值重定向 |
| 文案切换 | 新文本y+.3em/blur4淡入240ms，旧文本y-.3em/blur2淡出160ms | 独立内部文本层，旧层不参与输入/可访问树 |
| Reduced/Off | Reduced遮罩/面板120ms纯淡化，文字即时 | Off全部即时 |

参数使用Registry及其foundation令牌；网站营销页局部皮肤不是控件默认主题。保留字体、阴影、模糊算法差异。宿主限制及退场期间模态规则见[规格](../../components/Dialog.md)。
