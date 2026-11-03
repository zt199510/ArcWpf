# Carousel

状态：实现完成，真实窗口视觉与触摸验收待执行。

## 来源

Arc Free Carousel，采集批次 `2026-11-03-carousel`，来源冻结于 `tests/Baselines/References/2026-11-03-carousel/manifest.json`。Arc 源码使用居中滑轨、相邻项缩放/变暗、拖拽/甩动、方向键、指示点和可选自动播放。本项目采用 WPF 原生 `ItemsControl`、`ScrollViewer`、鼠标捕获和 `DispatcherTimer`。

## API

- `Label`：自动化名称。
- `SelectedIndex`：当前项，支持双向绑定。
- `SlideWidth` / `SlideGap`：卡片宽度和间距，默认 340/16 DIP。
- `Interval` / `IsPlaying`：可选自动播放。
- `Next`、`Previous`、`First`、`Last`：导航方法。
- `SelectedIndexChanged`：索引变化事件。

## 行为

支持鼠标和触摸拖拽、横向鼠标滚轮、左右/Home/End 键、底部箭头和指示点；首尾索引夹取。拖拽时相邻卡片会按连续距离同步缩放和变暗，释放速度超过 flick 阈值时会直接翻页；鼠标悬停、键盘焦点和拖拽期间暂停自动播放。回位使用可打断的 spring，释放速度会参与翻页投影。IntersectionObserver 属于 Web 专属机制，WPF 以布局更新和捕获输入替代；完整逐帧动效、触摸和 Reduced/Off 的真实窗口观感仍待验收。

## 验证

`CarouselTests` 4/4 通过；Controls 与 Gallery 构建通过。真实窗口的明暗主题、DPI、触摸、甩动惯性和动效观感待验收。
