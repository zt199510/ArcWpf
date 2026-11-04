# 第七批复刻验证：TextMorph / TextReveal / TextShimmer / IconMorph / InViewTitle

日期：2026-11-03
状态：五项均为**待验收**。

## 验证环境

Linux + Wine 9 + Xvfb，离屏渲染，字体为改名的 Liberation（无 Medium 字重，字宽与 Arc 不同）。结果不等于真实窗口验收。

## 构建与测试

- `tests/ArcWpf.Tests`、`samples/ArcWpf.Gallery` 构建：0 错误。
- 只运行本批测试（按用户要求）：`TextMorphTests`、`TextRevealTests`、`TextShimmerTests`、`IconMorphTests`、`InViewTitleTests`：36 项通过，0 项失败。
- 完整测试套件：未运行。

## 像素对比

Arc 为 uiarc.dev 公开页面截图，WPF 为 Gallery 离屏渲染。对比图和数据在 `artifacts/batch7-2026-11-03/`（未入库）。未做平移对齐和遮罩，表中是差异 >32 的像素比例。

| 状态 | 差异 >32 |
|---|---:|
| text-morph | 6.99% |
| tm-following | 8.08% |
| text-reveal | 14.57% |
| text-shimmer（ts-s0） | 3.70% |
| ts-done | 3.56% |
| im-rest | 1.79% |
| im-on | 2.73% |
| iv-0 Blur | 8.90% |
| iv-1 Words | 8.58% |
| iv-2 Lines | 8.61% |
| iv-3 Tracking | 9.38% |
| iv-4 Wipe | 8.85% |

差异主要来自替身字体的字宽和字重，大字号文字面积大，比例随之升高，数字不作通过结论。im-* 的 WPF 图比 Arc 宽 8px、高 8px（Gallery 用负边距抵消格子外边距），只比较重叠区域。ts-s0 是定格的光带位置，与 Arc 的时间点不严格相同。

## 动画中间帧

Xvfb 下渲染帧稀疏，普通截图常已落到终点，因此分三类：

- 逐帧捕获：TextReveal（tr-f0..3），带时间和数值日志，形态与 Arc tr-mid 目视一致。
- 定格：TextMorph 遮罩（tm-mask）、Wipe（iv-wipe）用反射设定中间值；IconMorph 八个图标四种状态（im-big）用 `HoldForTest`。只证明几何和遮罩位置，不证明时序。
- 仅测试：换标签、字母进出等起点和终点。

时序未与 Arc 逐帧比对。

## 对比中发现并修复

- 说明和标签文字用 `LineHeight` 时偏上 1px，改为固定高度容器内自然行高居中（IconMorph 标签、InViewTitle 说明、TextShimmer meta 和 Brand assets）。

## 未运行/待验证

- 真实 Windows 窗口：未运行，本环境没有。动效手感、Inter/Segoe 下的真实字宽字重都依赖它。
- 深色主题、高 DPI、触控、读屏软件：未运行。
- 平衡换行只验证了 TextReveal 示例文本。
- 复用提示的落实情况：OwnedMotion 未直接使用（TextMorph 需要每字形弹簧，用 `SpringDriver`）；TextColoring、Skeleton、ActionSwap、ThemeSwitch 未复用，原因见各参考卡；ScrollArea 通过测试验证可作为 InViewTitle 宿主。
