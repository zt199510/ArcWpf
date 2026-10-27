# MultiSelect 验证（2026-10-26）

状态：本次实现已建立，待用户人工验收；没有提交或推送。来源与WPF差异见[参考卡](../references/components/MultiSelect.md)和[规格](../components/MultiSelect.md)。用户最新指令为“显示的标签WPF 是被遮挡的 需要调整 后面不用做computer-use 我来人工测试”；收到后未再调用 computer-use。

## 实现范围

多选集合双向绑定/外部通知、可移除标签与+N、原生Search本地过滤、原生ListBox Multiple选择/禁用项、Popup定位/滚动、键盘/焦点、只读/验证错误、明暗主题与逐项动效均已接入。没有DataContext接管或业务网络请求。Gallery包含文档 Labels/Bug主例（关闭搜索/逐标签删除）和独立WPF扩展区。

## 环境和命令

Windows NT 10.0.26200，SDK 10.0.402，net10.0-windows。布局与离屏图记录为96 DPI、明色、Full；不代表150%/200%或跨屏通过。最终构建的路径、文件时间、SHA-256及进程记录在 artifacts/multi-select/build-info.json；下面的程序集标识同步保存为本记录末尾。

- `dotnet build ArcWpf.slnx --no-restore -v minimal`：成功，0错误；已有12项其他组件/测试警告。
- `dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter FullyQualifiedName~MultiSelectTests --logger "trx;LogFileName=multiselect-final.trx" --results-directory artifacts/multi-select`：10/10通过。
- 关联选择过滤器包括 MultiSelectTests、SearchTests、SelectTests、PopoverTests、SampleWindowsLoadXaml；当时共42项，41通过，1失败。失败为既有 `SelectTests.RetargetedMenuMotionDoesNotRunStaleCloseCompletion`，组跑实测closed为1而期望0；独立复跑为motion.IsRunning未清理。相关代码未修改，未归因或宣称已解决。见 related-final.trx、select-recheck.trx。
  后续状态（2026-10-27）：SelectTests/MultiSelectTests过滤器22/22通过，包含该动效回归；用户明确确认可调整为成功，当前问题已关闭。上述失败保留为历史证据，详见[Select最新记录](2026-10-26-select.md#动效回归关闭与用户确认2026-10-27)。
- 没有重跑全量基线；上一轮Dialog两项持续失败、Calendar时序偶发失败仍以Pagination记录为准。

## 程序化证据

| 专项 | 实测结果 |
|---|---|
| 绑定/筛选 | 过滤后添加Feature仍保留隐藏Bug；绑定表达式存在；外部集合增删刷新；Clear/Remove回写；只读拒绝用户修改 |
| 数据边界 | 不改变共享CollectionView；选项删除保留未知选中值；重复值去重操作 |
| 标签退场/反向 | 已加载窗口推进Dispatcher时钟；删除中恢复选择，最终无旧标签；clear完成占位恢复；Reduced/Off停止自有时钟 |
| 外框/箭头 | 400/180宽下空值、多标签、长文本、清除前/中/后：外框高44、宽和相对窗口位置、箭头位置稳定；项目容差0.1 DIP |
| Popup/主题/生命周期 | 退出30ms后重开仍展示；暗色Popup资源更新；退出完成、换模板、隐藏后无自有活动时钟 |
| 键盘/UIA | 模拟组合输入时Escape不关闭；结束后方向键/Enter选择、Escape归还；Up打开到末项、循环跳过禁用；Tab到下一个外部按钮；原生List Selection多选、Search Value、外层ExpandCollapse存在 |
| 滚动 | Gallery29项，列表高度<320且ScrollViewer可滚动，ScrollToEnd产生正偏移；这是程序化滚动，不是鼠标验收 |
| 勾号/计数 | 真实时钟中勾号出现、撤回透明且清钟；计数快速反向/卸载重载后无残留 |
| 标签遮挡回归 | 旧版本失败，修正版通过；详见下节 |

## 标签遮挡根因与修正

旧版天然文本宽度直接用作排版宽度，Reference的Bug文字宿主为20.736666666666665 DIP，实际天然字宽20.736666666666668 DIP，TextBlock在边界触发省略，离屏图显示B…。180 DIP窄框两项各槽31.5 DIP，扣除边框/内边距/删除操作后文字宿主为0。

修正采用当前字体、FlowDirection、DPI的FormattedText测量，向上取整并保留1物理像素余量；不重新测量正在裁剪的TextBlock。窄框动态减少可见标签，把剩余项并入+N。修正后Reference和扩展Bug文字宿主22 DIP，180 DIP长标签文字宿主26 DIP，剩余+3。外框与箭头位置仍满足布局合同。

artifacts/multi-select/tag-layout-before.png、tag-layout-before.txt为旧版证据；tag-layout.png、tag-layout.txt为最终离屏图与尺寸。新测试先在旧版失败，再在修正版通过。离屏结果不代替用户窗口复验。

## 动效逐项与边界

| 来源动效 | WPF实现 | 证据边界 |
|---|---|---|
| 外框/标签/clear颜色160ms；选项80ms | 私有画刷OwnedMotion，公共资源/绑定保留 | Popup暗色测试通过；所有颜色中间帧观感未人工确认 |
| 标签槽width/opacity | 独立slot，.4采样弹簧/160进入120淡出；退出后移除 | 反向/退出/窄布局通过 |
| 标签scale/blur | .9→1，.26采样；blur4→0/退出2 | 时钟终态/清理通过；动态观感待验收 |
| +N滚动/blur | 新旧两层文字，±6 DIP，240/120ms，blur2 | 计数反向终态通过；逐帧视觉未验收 |
| placeholder | 独立非布局占位120ms淡化 | 清除终态与外框测量通过 |
| clear | .6→1/blur2，160/120淡化，原生按钮按压.96 | 显隐终态通过；实际按压观感未验收 |
| chevron | 180°，580ms采样 | 关闭等待完整580ms后清理通过 |
| menu | -6/.97进入、-4/.98退出，.26采样/160淡入120退出 | 退出重开、生命周期通过 |
| check | dash offset模拟路径绘制240ms、透明120ms | 真实时钟进入/退出通过 |

Reduced为短淡化、无位移/缩放/模糊/宽度旅行；Off即时。弹簧为项目已有采样近似，不保持反向速度，非原Motion求解器。标签为28 DIP以容纳18 DIP删除操作；CSS正文行高与WPF字体度量不是像素等效。来源foundation已补充冻结：trigger44/radius18，menu radius26、option21；原生阴影与Web阴影允许实现差异。

## 实际窗口和待人工项

此前启动过Gallery并识别可执行文件路径，但导航操作受到用户输入保护两次阻止，另一次取景被遮挡，未取得MultiSelect的真实鼠标选择/滚动通过证据。用户随后明确由其人工测试，停止computer-use。最终构建只启动到MultiSelect页供人工操作，不声称已通过实际窗口验收。

待人工：点开/选择/逐标签删除/Clear、搜索输入粘贴撤销与中文IME候选、滚轮与末项命中、键盘焦点框（鼠标不误显）、退出/快速反向/主题与动效观感、不同DPI/跨屏、读屏。实际效果未复验前，标签问题结论是“已修改，程序化与离屏检查通过，实际效果待复验”。

暂缓功能为自由文本创建、异步加载/网络、虚拟化，均不属于本次必做范围。尚未建立人工审查通过的WPF像素基准。

## 最终构建标识

- ArcWpf.Controls.dll：SHA-256 `12FE04A0A7AE3DE20F39BEF80CEBB4D22C24DFBCB5BAB47152BEC7D94652D64C`，文件时间 2026-10-26T22:07:15.4174723+08:00。
- ArcWpf.Gallery.dll：SHA-256 `56E79C83F57B9E1D400A3155FC586F8D053F5A3DD8D534962A56C6FBF77D4903`，文件时间 2026-10-26T22:07:16.1380619+08:00。
- 人工验收实例：PID 28624，启动 2026-10-26T22:09:29.7534208+08:00，路径 `D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Gallery.exe`，参数 `--page=multi-select`。未自动操作该最终窗口。


## 用户确认（2026-10-26）

用户在标签遮挡修正及最终Gallery交付后反馈“测试没问题”。记录为当前版本MultiSelect人工测试通过，覆盖本轮标签修正后的用户确认；未逐项确认IME、多DPI、跨屏、读屏或全部动效场景，不扩展为所有专项通过。此前“实际效果待复验”为确认前的历史状态，以本节为当前用户反馈。继续遵循“不做computer-use，由用户人工测试”。本反馈不包含提交或推送授权。
