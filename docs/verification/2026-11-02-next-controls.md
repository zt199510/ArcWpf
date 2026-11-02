# 2026-11-02 下一批控件验证

## 已执行

- `dotnet build src/ArcWpf.Controls/ArcWpf.Controls.csproj --no-restore`：0 错误，保留既有警告。
- `dotnet build samples/ArcWpf.Gallery/ArcWpf.Gallery.csproj --no-restore`：0 错误、0 警告。
- Gallery 新增 Arc Next Batch 页面，展示 8 个控件。
- 启动 `ArcWpf.Gallery.exe --page=arc-next-batch` 3 秒：进程保持运行，随后正常结束测试进程；说明页面与合并资源可加载。
- 在 Timeline 增加按日期分组、最新事件排序和 Expander 详情后，重新启动 `--page=arc-next-batch` 2 秒，页面仍保持运行并正常结束。
- 完成 Countdown 紧凑单行布局、AnnouncementBar CTA/暂停控制、MentionInput `#channel` 和 AvatarUpload 图片/首字母切换后，Gallery 构建通过并再次启动页面成功。
- AnimatedCounter 改为 Arc 的纵向标签/数字比例；FileDropzone 增加 Arc 风格留白、圆角和拖拽高亮状态，Gallery 重新构建并启动成功。
- MentionInput 建议面板增加 Arc surface、边框、阴影和上下碰撞定位；EmojiPicker Popup 改为自定义上下翻转定位，Gallery 构建和页面启动成功。
- AnnouncementBar 关闭状态改为 Root 高度收缩动画，Full 动效使用 WPF 高度过渡，Reduced/Off 直接进入折叠终态；Gallery 构建和页面启动成功。
- Countdown 和 AnimatedCounter 的数值更新增加可打断的上下位移、淡入过渡，Reduced/Off 保持即时更新；Controls/Gallery 构建与页面启动成功。
- Timeline 的 `MaxHeight` 现在真正约束外壳并启用内部滚动，同时支持 `DefaultExpanded`；Gallery 构建和页面启动成功。
- EmojiPicker 增加搜索字段、即时过滤、自动聚焦选项和可 Tab 到的 emoji 按钮；Controls/Gallery 构建与页面启动成功。

## 待执行

真实窗口中的 Arc 页面比例、主题切换、鼠标悬停、Popup 碰撞/翻转、键盘焦点、中文 IME、拖放、DPI 和动效中间帧尚未逐项验收。AvatarUpload 与 EmojiPicker 没有 Registry 源码，独立实现差异按各自规格记录。

## 2026-11-02 交互补强

- EmojiPicker 增加空搜索结果、Escape 先清空搜索再关闭、方向键/Home/End/PageUp/PageDown 网格导航，并监听 `Emojis` 集合变化。
- FileDropzone 的条目实现属性变更通知，增加上传取消、失败重试和移除入口；上传使用每项独立 `CancellationTokenSource`，取消和失败状态会回写到条目。
- 补强后的 Controls 构建、Gallery 构建和 `NextArcControlsTests` 8/8 通过；Gallery `--page=arc-next-batch` 实际启动保持运行 4 秒后正常退出。
- 上述结果仍属于程序与启动验证；Arc Web 与 WPF 的真实尺寸、鼠标高亮、主题切换、DPI 和逐帧动效仍需窗口级对照。

## 数字控件补强

- AnimatedCounter 从整段 TextBlock 动画改为按格式化字符串拆分字符槽，数字、分组分隔符、前后缀分别呈现，变化字符独立上下位移和淡入。
- 该实现仍是 WPF 字符槽近似，Countdown 的完整多单位自定义集合和真正循环轮仍待继续对齐。

## Countdown 单位补强

- Countdown 增加 `CountdownUnit` 与 `Units` 集合，按 Arc 的最大单位吸收剩余时间规则动态生成大号分栏，紧凑模式复用相同拆分结果。
- 自定义单位集合、完成态和 Gallery 页面已通过构建、8 项专项测试及实际启动检查；数字列仍为 WPF 文本槽近似，未宣称完整循环轮。
