# AnnouncementBar

状态：WPF 轮播、暂停/恢复、倒计时、CTA、tone 和收起动效已完成；controls 模式已加入 Arc 风格间隔进度轨道，真实窗口焦点、DPI、主题和响应式宽度仍待人工验收。

像素对照：2026-11-02 已按线上页面重做，结果与已知差异见 [记录](../verification/2026-11-02-next-controls-pixel.md)；离屏对比已完成，真实窗口未验证。

来源：`tests/Baselines/References/2026-11-02-announcement-bar/`，Arc 免费 Registry。Arc 结构为公告文本、前后导航、暂停/关闭和高度收缩；WPF 版本保留多消息轮播、悬停暂停、间隔、关闭、tone 和键盘按钮，并支持 CTA 与公告项倒计时显示。

主要 API：`Messages`（兼容简化文本列表）、`Announcements`（消息、CTA、倒计时目标）、`Index`、`IsOpen`、`AutoPlay`、`Interval`、`Controls`、`IsPaused`、`Dismissible`、`Tone`。消息切换在 Full 动效级别下按方向做上下滑入/滑出并清理旧时钟；controls 开启时进度轨道随轮播间隔运行，悬停、焦点和暂停会停止并保留当前进度；Gallery 使用 Arc 主示例的低噪声中性条。
