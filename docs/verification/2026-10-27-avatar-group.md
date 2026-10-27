# AvatarGroup 验证记录 · 2026-10-27

状态：本次实现完成，专项9/9通过；真实界面待用户人工验收。保留Badge/Avatar及导航既有改动，未提交/推送。

## 环境与构建标识

Windows10.0.26200、SDK10.0.402、net10.0-windows，Segoe UI/中文回退。隐藏HwndSource无WS_VISIBLE，仅提供STA/Dispatcher与呈现树；未显示或操作实际窗口，不用computer-use/浏览器界面。离屏850×1120，96DPI，静态Off或Full稳定终态。实际验收进程未启动。

- D:/AI/ArcWpf/samples/ArcWpf.Gallery/bin/Debug/net10.0-windows/ArcWpf.Gallery.dll；2026-10-27 15:57:27 Asia/Hong_Kong；SHA-256 859337B01061FA11F181D7738D75595304DB50D0293BFE50D51A328BAFD3CDB8
- D:/AI/ArcWpf/src/ArcWpf.Controls/bin/Debug/net10.0-windows/ArcWpf.Controls.dll；2026-10-27 15:57:25 Asia/Hong_Kong；SHA-256 CCF03D54CA2FEAAF320FA8D221F5882E931150F009DAF3E90A3DBE91B2E709F1

~~~powershell
dotnet build samples/ArcWpf.Gallery/ArcWpf.Gallery.csproj --no-restore -v minimal
dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter FullyQualifiedName~AvatarGroupTests --logger 'trx;LogFileName=avatar-group.trx' -v minimal
~~~

最终构建成功，**AvatarGroup专项9/9通过**，约9秒；日志tests/ArcWpf.Tests/TestResults/avatar-group.trx。已有CS0067/MSTEST0037编译警告保留，无新增警告。没有运行Avatar、Badge、导航或其他专项，也没有全量回归；仅编译依赖工程。

## 已执行检查

| 检查 | 实测 |
|---|---|
| 集合/API绑定 | Max绑定未丢失，负值强制0不回写模型；改名/状态更新复用同一槽；Move重排、重复名字/重复对象、替换/null集合正常 |
| 布局 | D28/36/48，四槽总宽6+4*(D-6)，头像x=i*(D-6)；外环宽D+2/起点-1；空组宽6且换Size高度更新；RTL相对外层物理坐标逆序。容差0.1DIP |
| UIA | Group/Label，当前头像Image姓名+状态，溢出2 more editors；退场项非ControlElement；Max0后同一个Peer更新为单一4 more editors，无旧列表缓存 |
| 加入/退出/打断 | 实际组宽在中间帧介于旧宽与目标宽；退出再加入反向，快速增删不留幽灵槽；完成无时钟、后续刷新不回闪 |
| 数值滚动 | 增长入场y正、减少y负，旧文字与透明度/模糊；完成当前opacity1、旧0、blur0；Reduced无位移模糊，120ms淡化 |
| 悬停 | 程序化MouseEnter/Leave经控件路由；四槽指向第二项最终x=-9/-2/5/9，y=0/-2/0/0；组宽不变；tip上距8/水平居中，ring达到BorderStrong；Reduced静止但tip保留 |
| 生命周期/主题 | Hidden收口、缺失模板安全、恢复模板；真实移出/重新加入可视树后反映离线集合更新；Off无时钟；暗色ring动态跟随资源 |
| 可见项数量 | 1000成员、Max4只物化5个槽，Overflow996；每实例默认集合独立。该项不代表性能基准或虚拟化承诺 |
| Gallery | 来源文档三人+2、三尺寸、动态增减/重排/改名/Max、空组/Max0/重复名、窄宽/RTL；程序化按钮事件更新集合，非真实点击证明 |

初轮7/9，RTL检查原先用了组内逻辑坐标，计数检查在动画时钟起动前读取目标基值；调整到外层坐标与Dispatcher采样后通过。补查UIA发现真实缓存缺陷：仅InvalidatePeer仍返回4旧子节点，修正为同步ResetChildrenCache后同一Peer返回1溢出节点，回归通过。实现另补空组尺寸失效与退场删除排队，防止集合协调过程中同步完成回调重入。

[Microsoft AutomationPeer文档](https://learn.microsoft.com/dotnet/api/system.windows.automation.peers.automationpeer)确认ResetChildrenCache同步重新获取子节点；保留InvalidatePeer通知更新。程序化UIA检查不是读屏软件验收。

## 离屏证据

- [明色静态](../../artifacts/avatar-group/gallery-light.png)
- [明色悬停终态](../../artifacts/avatar-group/gallery-hover-light.png)
- [暗色悬停终态](../../artifacts/avatar-group/gallery-hover-dark.png)

已查看图像：重叠/描边/计数、姓名提示、三尺寸与RTL呈现。悬停图由程序化事件生成，静态帧不能证明完整动态体验；不是原站截图或经用户批准的回归基准。用户原站照片未取得固定副本，Maya使用原创几何示意图；其他差异见[规格](../components/AvatarGroup.md)。

## 人工测试入口

~~~powershell
dotnet run --project samples/ArcWpf.Gallery -- --page=avatar-group
~~~

Gallery→反馈→AvatarGroup：

1. 移入整组、逐个头像和+N，核对扇开/抬升/邻项让位、指向项置顶、姓名和描边；移出应恢复，不出现额外焦点框。
2. 连续添加、移除首位、增减Max（含0），核对槽宽滑动、+N增减方向、反向切换无残留；重排/改名/状态更新对应成员。
3. 切换Full/Reduced/Off、明暗；Reduced头像静止，计数淡化，姓名仍响应；Off直接更新。头像组不进入Tab。
4. 调整窗口检查滚动与窄容器；100%/150%/200%DPI核对外环/姓名；读屏应只读当前可见头像与溢出数。

真实鼠标命中/边界悬停抖动、tip进入60ms观感、动态动画、实际滚动/Tab、读屏、多DPI/高对比度和规模性能均未验收。没有本次用户通过反馈，不标记完整视觉还原。
