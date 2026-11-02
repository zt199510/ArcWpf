# AnimatedCounter

状态：实现中，待真实窗口视觉验收。

来源：`tests/Baselines/References/2026-11-02-animated-counter/`。Arc 使用按位数字轮和进入视口触发；WPF 版本保留本地化格式、前后缀、标签、小数位，并将格式化结果拆成独立字符槽，在首次可见时及变化字符上做上下位移和淡入。
