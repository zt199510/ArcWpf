# Calendar 选中动画补齐

用户反馈：Calendar选择的动画效果还没完成。范围从此前暂缓中移出“选中圆盘”；月份条带、月份标题交叉过渡和按压缩放仍未实现，不将其统称为动画完成。

## 动效合同

- 来源：冻结的calendar.tsx SelectionDisc与motion.json；同一可见月份内用单个圆盘按实际日期格坐标移动。原生Calendar仍管理日期、选择、键盘、事件和自动化。
- 移动：ControlSpring临界阻尼解析解，visualDuration .42、omega=3.89/.42；缩放进入.6→1使用.26。这是项目WPF近似，不声称与Motion求解器逐帧一致。快速反向保留位置和速度，重新设置目标；长期暂停单步最多.05秒。
- 首次选择/选择重新进入：先定位目标格再放大/淡入（opacity .16秒）。清除：当前位置缩至.6并淡出，.14秒；透明度采用cubic ease-out近似来源曲线。
- 字色：根据真实移动圆盘覆盖范围决定反色，区别于Arc源码的距离延迟交接；不改变SelectedDate和事件时间。多选模式恢复原生逐格静态选中样式。
- 月份变化、尺寸改变：更新到新的实际格子坐标；月份动画没有实现，不跨两张月份网格模拟滑动。年/十年模式直接隐藏日期圆盘。
- Reduced/Off：直接定位、缩放1/透明度1或0，停止帧回调；未声明自动映射操作系统偏好。
- 属性归属：圆盘内部位置/速度/缩放/透明度由模板视觉专有；Fill为DynamicResource。不动画或覆盖SelectedDate/DisplayDate/使用方Transform/Opacity。文字状态是自有附加属性，卸载清理。
- 生命周期：仅运动中订阅CompositionTarget.Rendering，收敛后解除；隐藏、卸载、Popup关闭和换模板清理。重新显示/打开直接恢复当前选择，避免旧位置闪回。没有永久Timer或永久渲染订阅。

## 验证

Windows build26200，.NET10 SDK10.0.402。3项新增STA/真实CompositionTarget渲染帧测试，加既有5项Calendar、7项DatePicker，共15/15通过。覆盖：中间位置、反向前位置和速度连续、最终落点、帧订阅释放、停稳后切主题不闪回、清除退场、重新进入、运动中Reduced/Off切换、隐藏再显示、卸载重载、年模式及DatePicker Popup关闭/重开。不能用这些程序化结果代替鼠标、DPI或视觉用户验收。

命令：`dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore -p:BaseOutputPath=D:/AI/ArcWpf/artifacts/calendar-motion/ --filter "FullyQualifiedName~CalendarMotionTests|FullyQualifiedName~CalendarTests|FullyQualifiedName~DatePickerTests"`。日志：artifacts/calendar-motion-verification/calendar-motion.trx。

设置ARCWPF_CALENDAR_MOTION_DIR后生成start.png/moving.png/settled.png：固定2026年6月、328DIP、96DPI离屏白底渲染，采样于真实帧推进过程中；不是Windows窗口录像或已批准基准。实际窗口已启动新版Gallery并读取导航；点击失败 `coordinate input geometry is unavailable`，随后捕获窗口被其他应用遮挡，本次未完成可重复的真实鼠标动态验证。未测帧率，不宣称60FPS。DatePicker保留原生选日即关闭，其关闭时机会截断选中动画，不延迟绑定或关闭来展示动画。

待验收：主Calendar连续点选同排/跨排/反向，空值进入和清除，按键选择，主题/动效级别切换和高DPI。没有提交或推送。

最终全量构建/回归：输出目录artifacts/calendar-motion-final，232/232通过（约22秒），日志calendar-motion-final.trx；构建0错误，保留既有CS0067/MSTEST0037警告。运行中的旧Gallery不会自动更新，可使用该输出目录下的ArcWpf.Gallery.exe检查本次实现。
