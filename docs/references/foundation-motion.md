# 公共设计与动效依据

批次和许可见 [采集报告](2026-10-25-initial.md)。下面为源码确认；WPF 映射是暂定设计，尚无运行验证。

## 设计令牌

来源：[foundation.css](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/foundation.css)。

| 原始令牌 | 冻结源码值 | WPF 规划 |
|---|---|---|
| radius-control / panel / surface | 1.125 / 1.625 / 2.125rem | 若以16px根字号作为映射基准，对应18/26/34 DIP；根字号尚未运行确认 |
| control-height-sm / md / lg | 2.25 / 2.75 / 3.125rem | 同一假设下36/44/50 DIP |
| text-xs / sm / base | .75 / .875 / 1rem | 同一假设下12/14/16 DIP |
| font-body / display | Inter Variable / Geist Variable（带变量及sans-serif回退） | 字体资产、授权文件和中文回退仍待采集验证 |
| 明暗与强调色 | CSS包含中性色、明暗规则及8种强调色规则 | 转换为语义资源，不把所有前景强行绑定强调色 |
| 阴影 | 多层CSS阴影与OKLCH颜色 | WPF效果需独立验证，不能简单照抄CSS字符串 |

首批只引入实际用到的语义资源。颜色空间转换、DIP映射、文本度量和阴影效果均需在 WPF Gallery 核对。

## 动效参数

来源：[motion-tokens.ts](../../tests/Baselines/References/2026-10-25-initial/arc/extracted/registry/motion-tokens.ts)。

| 参数 | 源码值 |
|---|---|
| instant / fast / exit / standard / considered | 0.12 / 0.16 / 0.18 / 0.24 / 0.48秒 |
| snappy | visualDuration=0.26，bounce=0 |
| smooth | visualDuration=0.4，bounce=0 |
| morph | visualDuration=0.42，bounce=0 |
| responsive | stiffness=520，damping=44 |
| gentle | stiffness=340，damping=34 |
| enter ease | [0.16,1,0.3,1] |
| standard ease | [0.22,1,0.36,1] |

CSS foundation 另有 duration-spring=580ms 与 ease-spring 曲线，Popover 使用这条 CSS 路径。Switch 位移另用0.3秒、bounce=0；不要把共享预设覆盖组件局部参数。visualDuration 是上游算法参数，不等于 WPF 固定补间时长，等效转换需检查响应曲线和收敛行为。

本项目 Full/Reduced/Off 属于 WPF 设计契约；原版 useReducedMotion 和 CSS 分支提供 Reduced 行为依据，不意味着源码提供完全相同的三级 API。
