# AnimatedCounter

状态：已实现，待真实窗口视觉验收。

像素对照：2026-11-02 已按线上页面重做，结果与已知差异见 [记录](../verification/2026-11-02-next-controls-pixel.md)；离屏对比已完成，真实窗口未验证。

来源：`tests/Baselines/References/2026-11-02-animated-counter/`。Arc 使用按位数字轮和进入视口触发；WPF 版本保留本地化格式、前后缀、标签、小数位，并将格式化结果拆成独立字符槽，在首次可见时及变化字符上做上下位移和淡入。控件同时更新无障碍名称与隐藏的完整格式文本，主题资源缺失时回退到控件前景色和 Consolas 字体。
