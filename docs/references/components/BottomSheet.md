# BottomSheet 参考卡

Arc 页面：`https://uiarc.dev/components/bottom-sheet`。2026-11-01 冻结 Registry、CSS、Markdown、页面、许可和动效令牌；来源 URL 与各文件 SHA-256 见 `tests/Baselines/References/2026-11-01-bottom-sheet/manifest.json`。组件为 Free/MIT。

源码确认：默认 detents 为视口高度的 `[.45, .92]`，按升序过滤正值；`initialDetent=0`。表面宽度 `min(100%, 36rem)`，最大档决定表面高度，底部额外延伸 160px 以隐藏上拉空隙。标题栏是拖拽区，抓手 56×20px，内条 36×5px；内容只在最大档滚动。主体文本和关闭按钮在标题栏下方同一行。标题栏横向内边距 20px，窄于 40rem 时为 16px。

状态与动效：首次从底部外 40px 入场，位置由 smooth spring 驱动；关闭视觉时长约 .3s。最小档遮罩强度为最大档的 .78，拖拽时随表面位置连续变化。上拉超过最大档按 `(1 - 1 / (distance * .55 / 120 + 1)) * 120` 抵抗；释放取最近 80ms 位移，末次采样超过 60ms 则丢弃速度，以 `position + velocity * .2` 选择最近档或关闭。速度大于 320px/s 时至少跨一档。Reduced 跳到档位并淡入/淡出。

键盘与焦点：初始焦点落到 sheet 表面；抓手 Up/Down 调相邻档、Home/End 到最高/最低档、Enter/Space 在两端切换。折叠时向下滚轮和被遮挡内容获得焦点会展开，Escape 关闭并归还触发器焦点。触摸可在标题栏及折叠内容上拖动；展开后从滚动顶部下拉重新拖动。

WPF 映射：`BottomSheet : Drawer` 复用 DrawerHost 的模态、遮罩关闭、Escape、焦点范围及关闭命令；自身持有 Surface 高度、位置弹簧、遮罩强度与抓手/内容输入。百分比档位按 DrawerHost 可用 DIP 高度计算；Web 的动态浏览器视口、safe-area 和 160px 底部延伸没有直接桌面对应物。来源事实不等于所有真实输入和 DPI 已验收。
