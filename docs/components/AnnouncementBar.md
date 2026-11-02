# AnnouncementBar

状态：实现中，待真实窗口视觉验收。

来源：`tests/Baselines/References/2026-11-02-announcement-bar/`，Arc 免费 Registry。Arc 结构为公告文本、前后导航、暂停/关闭和高度收缩；WPF 版本保留多消息轮播、悬停暂停、间隔、关闭、tone 和键盘按钮，并支持 CTA 与公告项倒计时显示。

主要 API：`Messages`（兼容简化文本列表）、`Announcements`（消息、CTA、倒计时目标）、`Index`、`IsOpen`、`AutoPlay`、`Interval`、`Controls`、`IsPaused`、`Dismissible`、`Tone`。Gallery 使用 Arc 主示例的低噪声中性条。
