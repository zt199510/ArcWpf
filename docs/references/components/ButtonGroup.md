# ButtonGroup 来源参考卡

## 来源与固定范围

- 来源：Arc 免费 Registry `https://uiarc.dev/r/button-group.json`（MIT），页面 <https://uiarc.dev/components/button-group>。
- 冻结批次：`tests/Baselines/References/2026-10-28-button-group`（项目采集批次，非上游版本）；`registry/button-group.tsx`、`registry/button-group.module.css`，文件与 SHA-256 见 manifest.json。
- 依赖：Radix DropdownMenu（菜单）、lucide 图标、motion tokens。WPF 不引入这些运行库；菜单复用项目 `Menu`。
- 页面与 Registry 同批采集；页面主示例的 reserve 文案（如 Failed）比 Usage 多，以页面预览为主示例依据。

## 主示例（页面预览，源码确认）

预览画布 max-width 760，三组纵向居中排列，gap 24（`button-group_demo`）。

| 组 | variant/size | 分段 |
|---|---|---|
| Document actions | outline / md | Share（link，reserve Share/Copied/Failed）、Duplicate（copy-plus，reserve Duplicate/Duplicated）、Archive（archive，reserve Archive/Restore）、More actions 菜单（Pin to sidebar、Delete 危险项，取自 Usage） |
| Zoom | outline / sm | Zoom out（minus，iconOnly）、reset（content 显示 100%，名称 “Zoom 100%, reset to 100%”）、Zoom in（plus，iconOnly） |
| Editor actions | solid / md | Save（save，reserve Saved）、Preview（eye，reserve Edit） |

## 几何与颜色（CSS 确认）

- 外框：border 1 `--border`（solid 为 foreground），radius control（18），背景 surface（solid 为 foreground，文字 background）。整体禁用 opacity .52。
- 分段高 = 控件高 −2（md 42 / sm 34）；min-width 同值；padding 0 14（sm 12）；text-sm 500，tracking −.01em。iconOnly 宽 = 高 −2，padding 0。chevron 宽 = 外高 −6（md 38），颜色 muted ink（outline 为 text-secondary），打开时恢复文字色。
- 内容：图标 16、stroke 1.75（24 viewBox），图标与文字 gap 8（sm 6）。reserve 文案全部隐形叠放在同一格，分段宽度取最宽。
- 分隔线：非首段左侧 1px，上下内缩 11（sm 9，vertical 10），颜色 border（solid 为 background 20% + foreground）。高亮所在段及其后一段的分隔线淡出（quiet）。
- 高亮：一个元素位于所有分段之下，盒子 = 当前段边界，可见形状内缩 3，radius = 18−3−1 = 14；颜色 surface-muted（按压：surface-muted 与 border-strong 30% 混合）；solid 为 background 13%/22% 混合。
- 单段禁用：content opacity .4，不响应。焦点环：全局 `--focus-ring: transparent`，键盘焦点只通过高亮表达。

## 状态与动效索引（源码确认）

- 高亮目标优先级：指针悬停段（非触摸，禁用段不显示）→ 按下段 → 菜单打开时的 chevron → 键盘焦点段（仅 focus-visible）。
- 无→有：在目标处直接出现，opacity 0.16s standard；段间移动：x/y/width/height 用 snappy spring（visualDuration .26，bounce 0）；消失：opacity 0.16s。分段尺寸变化时高亮锁定跟随。
- 按下：高亮颜色加深（0.12s）；content scale .97（iconOnly .9）按下 0.12s standard，释放 ease-spring 0.58s；chevron 不缩放。
- 文案/图标切换：旧 phase opacity 0、y −3、blur 4（0.16s standard）；新 phase 从 opacity 0、y 4、blur 4 进入（0.24s enter）。content 值变化不交叉淡入。未 reserve 的新文案宽度以 morph spring（.42）过渡，期间裁剪。
- 分隔线 opacity 0.16s standard。chevron 打开时旋转 180°（0.58s ease-spring）。菜单：从组的 end 边（vertical 为 start）向下 6 偏移展开，align offset −1；进入 opacity 0.16 + scale .97 / 偏移 5，条目 35ms 级联；退出 130ms。
- 标签折叠：水平且 collapseLabels 时，溢出容器则带图标的分段只留图标（宽 = 高 −2，名称移到 tooltip/无障碍名）；容器宽度恢复到折叠前全宽才展开。仍超出时横向滚动且隐藏滚动条。
- 键盘：Tab 逐段；沿方向箭头循环移动，Home/End；RTL 水平反向；chevron 上 ArrowDown 打开菜单。
- 播报：按下后 1 秒内文案改变（Share→Copied）由 polite live region 播报一次，回退不播报。
- Reduced：高亮跳变、无按压缩放与 chevron 旋转，文案和菜单仅淡入淡出（0.12s）。

## WPF 差异和回归入口

见[组件规格](../../components/ButtonGroup.md)与[验证记录](../../verification/2026-10-28-button-group.md)。
