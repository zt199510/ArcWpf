# ExpandingSearch 参考卡

- 来源：`https://uiarc.dev/r/expanding-search.json`，采集日 2026-10-30（接续 c409412；该提交时 Registry 被拒绝，本次可访问）。
- 冻结批次：[2026-10-30-expanding-search](../../../tests/Baselines/References/2026-10-30-expanding-search/)，含 Registry、CSS、Markdown、页面、动效令牌、许可和 manifest（tsx `d76550fe…`，css `bd66136f…`）。
- 主示例（源站 demo chunk 确认，页面为客户端渲染）：label/placeholder `Search projects and docs`；9 项（Projects：Billing migration、Mobile onboarding、Q4 planning、Customer portal；Docs：Release checklist、Incident response runbook、Pricing page copy、API rate limits、Billing FAQ for support），图标 folder-kanban / file-text；suggestions = Release checklist、Billing migration、Pricing page copy；maxResults 5；emptyHint `Try a project or doc title, like billing or checklist.`；选择后状态行 `Opened {title}`。
- 展示容器（demo CSS 确认）：stage 顶部 72px、居中；lane `min(100%, 360px)`，空闲时 `translateX((44px - 100%) / 2)` 使按钮居中，搜索时回到 0，过渡 `--duration-spring`。
- 几何（源码确认）：shell 高 control-height-md 44，1px border，radius 从 22（圆）随宽度线性过渡到 radius-control 18；icon 18px 于 left 12（padding box）；input padding 0 40 0 38、14px；clear 28px pill，right 7，图标 16；panel 下方 8px、radius-panel 26、surface-raised、floating 阴影；body padding 6、max-height min(22rem, 60vh)；组标签 padding 8 12 4、12px/500 muted；option min-height 48、padding 6 12、gap 12、radius 20；高亮 foreground 6%。
- 状态机：collapsed →（点击/Enter/Space 按钮）expanded+focused → 输入：无字时 suggestions 模式、有结果 results、无结果 empty；Escape 或选择结果 → collapsed 并把焦点还给按钮；空查询失焦 → collapsed 不还焦点；有查询失焦保持展开、面板关闭；切换窗口保持原状。
- 键盘：ArrowDown/Up 循环（从无到有时 Down 选第一项、Up 选最后一项）；Enter 选择当前项；Escape 收起。
- 动效：宽度 spring morph(.42) 展开、smooth(.4) 收起；圆角随宽度；按钮按压 scale .97；input 透明度展开延迟 .08、.2 enter，收起 .08；clear opacity .16 + scale .6→1 snappy + blur 2；panel opacity .16 enter / .12 exit，高度 smooth 从 0 展开，宽度共享 shell；模式切换 blur 2 交叉；高亮在行间 snappy 平移，新集合时 .12 淡入；行 layout position spring。Reduced：无宽度/位移，仅淡入淡出。
