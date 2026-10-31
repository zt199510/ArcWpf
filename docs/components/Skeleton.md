# Skeleton 组件规格

状态：已按 2026-10-31 冻结源码重做，构建通过；专项测试与实际窗口 **未运行/待验证**（Linux 环境）。事实见 [参考卡](../references/components/Skeleton.md)。

## API

`Skeleton : ContentControl`：`Label`（`Loading content`）、`Lines`（3，强制 1–6）、`HasAvatar`（false）、`IsLoading`（true，仅在有 Content 时生效）、只读 `IsBusy`；Content 为加载完成后显示的内容。旧版 `Shape`/`Fill`/`CornerRadius`/`IsShimmering` 与 shimmer 已移除（原版为逐块脉动，无 shimmer）。

## 范围矩阵

| 项 | 状态 |
|---|---|
| 头像 44 圆 + 文本行（首行 48%/16，末行 72%，其余 100%/12，gap 12） | 已实现 |
| 颜色 border-strong 38% 混 surface（新令牌 `ArcWpf.Brush.SkeletonFill`） | 已实现 |
| 错开脉动（1.8s in-out，90ms 步进，11 次） | 已实现；仅 Full |
| 占位 ⇄ 内容交叉（.16 淡出 / 上移 4 .24 进入）+ 高度弹簧 | 已实现（共享 `HeightMorph`） |
| status/busy 自动化 | 已实现：Group + Name=Label + ItemStatus busy |
| Gallery 主示例（380 卡片、1.6s 自动加载、Reload） | 已实现；状态文字未做按词滚动 |
| 实际窗口、明暗主题、Reduced/Off、读屏 | 未运行/待验证 |
