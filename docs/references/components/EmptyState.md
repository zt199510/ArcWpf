# EmptyState 参考卡

- 来源：`https://uiarc.dev/r/empty-state.json`，采集日 2026-10-31（b574880 提交时 Registry 被拒，本次可访问）。冻结批次：[2026-10-31-empty-state](../../../tests/Baselines/References/2026-10-31-empty-state/)（tsx `e99e39c0…`，css `e65e4c4b…`）。
- 主示例（源站 data preview chunk 确认）：label `Saved views`；初始 title `No saved views`、description `Create a view to keep your favorite filters close at hand.`、icon lucide folder（24、stroke 1.75）、action 为 sm primary 按钮 `Create view`；点击后变为 `View created` / `Launch review is saved with your current filters.` / folder-check / sm secondary `Undo`，再点回退。预览区宽度未确认（待核对）。
- 几何（源码确认）：root 纵向居中，padding clamp(32, 8vw, 48) × 20；icon 48 方块、1px border、radius-panel（26，在 48 上即圆）、surface-muted、text-secondary；默认 glyph folder 24 stroke 1.5；h3 margin-top 20、16/500、leading 1.4；p max-width 18rem（288）、margin-top 8、14 secondary；action margin-top 20、居中换行、gap 12；text-wrap balance。
- 状态：无内部状态，靠修改属性在空/成功等状态间变形；同一图标重渲染不动，不同图标交叉。
- 动效：首次出现 icon opacity 0→1、scale .92→1（considered .48 ease-enter，Reduced 无）；新图标 opacity 0、scale .6、blur 2 → 1 以 snappy spring 入，旧图标 .16 standard 退；文案新行自 y .3em、blur 4 以 .24 enter 升入，旧行 popLayout 脱离布局后 .16 standard 升出到 -.3em、blur 2；HeightFrame 在文案变化后 120ms 内以 smooth spring 从旧高度到新高度，仅运动时裁剪。Reduced：文案 .12 淡入淡出、高度跳变。
