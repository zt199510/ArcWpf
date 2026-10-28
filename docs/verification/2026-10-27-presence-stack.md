# PresenceStack 验证记录 · 2026-10-27

## 范围与环境

用户选择公开文档独立 WPF 实现。Windows、.NET SDK 10.0.402、net10.0-windows、Debug；最终记录时间2026-10-27 17:13 +08:00。源码未提交。未使用computer-use，未操作实际桌面窗口，未执行其他组件测试或全量套件。

## 最终命令与结果

- `dotnet build ArcWpf.slnx --no-restore -v minimal`：通过，0错误，2个既有BreadcrumbPage未使用事件警告。
- `dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-build --filter FullyQualifiedName~PresenceStackTests --logger "trx;LogFileName=presence-stack.trx" --results-directory artifacts/presence-stack`：9/9通过，0跳过，约6秒。
- TRX：artifacts/presence-stack/presence-stack.trx。

|专项|实际证据|
|---|---|
|身份/绑定|Max绑定在负值强制后保留；同Id重排复用；重复Id取首项；默认集合互不共享|
|布局|28/36/44高度；宽度D+4×.32D；加入/离开不改变预留宽度；End最后头像贴右端；空集合仍预留|
|生命周期|加载后真实WPF时钟、退出途中同id返回、typing循环、Reduced立即停、隐藏停/显示恢复、卸载/换模板/重载|
|无障碍/通知|Button、Invoke/ExpandCollapse、根不暴露装饰子项；加入离开消息，状态变化不新增播报；名单有名称的dialog Pane|
|名单|15人全部创建，滚动区实际高度≤336；选择模式事件与禁用选择模式；主题样式实际解析|
|键盘/共享标签|隐藏HwndSource内取得键盘焦点；左右键路由改变姓名；展开步长D+4；长姓名Width真实时钟过渡并收敛；End展开锚定|
|滚动文本|递增/递减真实TranslateTransform方向，中断后Off归零|
|主题|Gallery明暗离屏图，键盘展开及独立名单内容离屏图|

## 图像证据与解释

artifacts/presence-stack/gallery-light.png、gallery-dark.png：完整Gallery离屏图，已查看。keyboard-fan.png：隐藏宿主内程序化键盘焦点展开，背景和父布局尺寸保留，已查看。roster-light.png：将名单内容从未打开的Popover取出放入离屏Border后渲染，已查看；它只能证明名单布局与样式，不能证明原生Popup的定位/焦点/阴影。

曾发现名单按钮命名资源未从主题字典解析，改为ComponentResourceKey；最终专项断言实际Style非空，图像无默认系统按钮外框。所有当前图像与TRX来自下列最终构建。

## 最终程序集 SHA-256

- Controls：249A245B324E577C89B5989855842A1A2CFA8C4F56EF030F8191F0DBBD4A6563
- Gallery：14FCABF12CF3961CA26CB2CAE84188F9C7B179C7382D912445E9E5F0F9F742D0
- Tests：6922CD3BD68AF96BC2E0AF590C6B7A6F565485A67290399FC17E1A3ECCA92798

## 未验证与差异

实际窗口的鼠标悬停/触摸、Enter/Space打开Popup、Escape/外部关闭的焦点归还、碰撞位置、屏幕阅读器实际播报、多DPI及性能仍待用户人工验收。程序化Click/键盘路由不是人工交互结论。不声称帧率、零泄漏或原版像素一致。

Arc Pro源代码不可用，未获取或复制；只依据公开行为独立实现。几何解释、字体、颜色令牌、阻尼采样和时序选择见组件规格；重定向保位置但不保速度。没有网络presence提供者，应用在UI线程更新People。根FocusVisualStyle仅由WPF键盘焦点机制显示。通知用UIA MostRecent近似礼貌播报，名单Pane标为dialog，不提供独立WindowPattern。

结论：本次实现、Gallery、文档与专项验证完成；实际窗口验收未完成。

## 用户反馈修正批次（2026-10-27，覆盖上方原实现结论）

反馈重点：名单弹层与整体示例不一致。原9项测试只证明旧契约，未证明还原正确。公开页面HTML/CSS核对后撤销旧的.32D静止间距假设。

修正：Medium步长24、总宽132、前脸遮后脸；提示移至D+8下方、高26；输入状态深底浅点及公开尺寸/周期。名单padding6、32头像、姓名+单行说明，标题/人数左右分列，滚动320，空名单文字。Gallery恢复640最大宽文档卡、6人/+2、End右锚定、模拟与暂停，使用原创占位头像；自动变化3.5秒为独立演示选择。

原Gallery进程22400锁住常规输出DLL，因此常规目录构建复制失败；没有关闭或操作该进程。改用独立目录，已完成依赖构建及PresenceStack专项：

`dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --artifacts-path artifacts/presence-stack-correction-build --filter FullyQualifiedName~PresenceStackTests --logger "trx;LogFileName=presence-stack-correction.trx" --results-directory artifacts/presence-stack`

最终9/9通过、0跳过，5秒；只有既有其他组件编译警告，未运行其他组件测试。补充断言：公开Medium132/24、遮挡顺序、提示44/26、名单内容274+外padding12+border2=288、滚动320、主题变化后的状态点颜色。时钟断言在启动后立即检查，避免Dispatcher首次资源加载延迟误判动画已完成。

最终离屏Gallery明暗、名单内容与键盘展开图已生成，已查看明暗及名单；仍不是实际Popup交互验收。实际窗口效果待用户复验。

修正版程序：artifacts/presence-stack-correction-build/bin/ArcWpf.Gallery/debug/ArcWpf.Gallery.exe（参数--page=presence-stack）。当前运行的旧Gallery未替换。

最终程序集SHA-256（独立输出目录）：
- Controls：48C2CA13DB6044DA4921E536B890479C178D9CD8B7F3FE3285BBB04A55649508
- Gallery：024AB1DD323F80126A2620907A3515C209E9D598F9E8FA0A26C801ADC9FE0D6B
- Tests：12A111BDECC501D5E7735B6059EF8341A3F4C5183E0F554BEABDC50083695C84

公开资料本地缓存SHA-256：HTML DC5B239979351350A137C6D7401329726C58FFF8AC6380444DFE813FB070244B；CSS DD0881BDF7AC64F707CA05854F62E839AD6F8A193D940EEDF8A475EDCB070D2F。未读取实现JS或鉴权源码，CSS仅提取公开视觉事实，未纳入产品源码分发。
