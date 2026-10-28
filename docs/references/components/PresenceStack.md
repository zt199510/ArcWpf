# PresenceStack 公开来源调查

状态：公开来源分析完成，用户已授权独立 WPF 实现。2026-10-27用户授权开始PresenceStack；核对发现其属于Arc Pro，已选择公开文档独立实现依据。此前推荐未核实免费范围，本次纠正，不将付费组件当作免费MIT源码。

## 已确认来源

[公开Markdown](https://uiarc.dev/components/presence-stack/markdown)明确Access: Arc Pro，说明预览公开、源码和安装需Pro。免费/r/presence-stack.json实测404。未访问鉴权Registry、未获取token、未抓取或逆向付费实现。

冻结公开文档：work/references/presence-stack；manifest记录获取日期、URL、SHA-256。该副本仅为公开接口/行为说明，不是实现源码，也未确认可按MIT再分发；暂保存在本地调查记录，不用于产品源代码复制。副本仅放work缓存，不纳入版本化源码基准。

## 公开文档已确认的目标

- 稳定id标识人员；name、src、active/idle/typing、detail；selfId标注本人。
- max默认4，三尺寸28/36/44（不同于AvatarGroup的Large48）。start/end锚定，预留完整堆叠宽度。
- 进入、退出、位置重排、+N方向滚动；共享姓名标签位置/宽度过渡，hover/键盘展开。
- idle头像淡化；typing点变成三个跳动点的小胶囊；Reduced全部立即变化。
- 整体为单按钮，左右键切换提示、Enter/Space开名单、Escape关闭归还焦点。
- 名单弹层宽288、约六行后滚动，显示所有人的状态/说明，可选人员选择回调；加入/离开可用礼貌live region播报。
- 名单进入、行进退、标题计数与detail滚动均为独立动效。

这些是公开文档确认，不是源码参数或运行观察。具体几何、颜色、完整spring参数、tip及typing时间线仍未知；不能沿用AvatarGroup参数后声称原版还原。

## 已选实现依据

用户回复 1，授权仅依据公开接口和行为独立设计 WPF 实现。未公开参数记录在组件规格，复用本项目 Avatar/Popover；不获取或复制 Pro 源码。实际界面由用户人工验收。

## 2026-10-27 用户反馈后的公开页面核对

用户指出名单弹层与整体示例不一致。本次读取公开页面HTML及其公开CSS，不读取JS实现或鉴权源码。缓存 public-page.html、public-styles.css 位于 work/references/presence-stack。

HTML确认：Medium36，trigger宽132，脸X=0/24/48/72，计数X=96，脸z=5/4/3/2，计数z=0，计数文字偏移6。上一版把68%理解成遮盖面积导致步长11.52，是错误假设，撤销；其他尺寸使用floor(.68D)作为明确WPF推导，非已测源码值。

CSS确认：姓名提示位于D+8下方，高26、胶囊；输入状态深色底浅色点，宽2.1dot、高dot+2，点循环1.1秒/错开.14秒；名单宽288、padding6、标题/计数左右分列，名单滚动上限320，行padding8/10、头像32、姓名+单行说明。主示例max-width640，横向文档卡min-height76，左侧Q3 launch plan/You and 5 others，右侧End堆叠；初始6人（4脸+2）。公开头像许可未知，Gallery保留本地原创头像替代并标注。

这些是公开HTML/CSS静态事实；未进行浏览器或Windows实际交互观察，不声称所有spring参数已核实。
