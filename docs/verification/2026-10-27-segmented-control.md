# SegmentedControl验证记录（2026-10-27）

本次实现完成，实际窗口待用户人工验收。仅执行SegmentedControl专项，不使用computer-use。来源及布局依据见[参考卡](../references/components/SegmentedControl.md)，API与范围见[规格](../components/SegmentedControl.md)。

## 命令与结果

Windows、.NET SDK 10.0.402、net10.0-windows、Debug。

```powershell
dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter FullyQualifiedName~ArcWpf.Tests.SegmentedControlTests --logger 'trx;LogFileName=segmented-control.trx' --results-directory artifacts/segmented-control -v quiet
```

最终8/8通过，0失败，9秒；artifacts/segmented-control/segmented-control.trx。只编译测试依赖中的其他项目，没有执行其他控件测试。控件库单独构建0警告/0错误；Gallery和测试工程存在既有CS0067、MSTEST0037警告，本次未改无关代码。

首轮测试编译时辅助方法Key与Key枚举重名，已改名PressKey。首轮运行6/8，清空/删除当前选择时SelectedIndex=-1仍查询生成器而抛异常，已显式处理无选择；RTL离屏发现选中背景偏到相邻选项，原因是镜像元素局部原点转换不能代表屏幕左边界，改为TransformBounds并增加同一Window下的实际矩形比较。最终上述路径均通过。

## 证据矩阵

| 项目 | 已执行结果 |
|---|---|
| 绑定/集合 | SelectedValue双向绑定保留，鼠标选择回写，外部更新选中，ItemsSource动态增删；SelectionMode强制Single |
| 点击/Intent | 原生鼠标路由入口更新选择，重复点击不取消；聚焦触发OptionIntent；InputHitTest命中项；禁用项阻止用户选择 |
| 键盘/Tab入口 | Right/Down/Home/End循环并跳过禁用，焦点跟随；RTL左右反向；仅一个可用TabStop；整组禁用不响应 |
| UIA | 原生List/Selection与ListItem/SelectionItem，名称Range，程序化provider.Select更新选择；未运行真实读屏 |
| 几何 | 正常整体44高、项36高、起点4、间隔2；图标与Accessory扩宽后背景跟随；RTL转换到同一Window的实际矩形一致 |
| 溢出 | 150 DIP有限宽ScrollViewer有可滚动范围/渐变；选择末项滚动显露，返回首项offset0；扩大宽度消除渐变 |
| 动效 | Dispatcher推进弹簧中间位置、快速反向、终态及后续刷新；文字色160ms终态；Reduced/Off即时，无剩余自有动画 |
| 生命周期/主题 | 隐藏与空模板停止动画；恢复模板重建背景；无选择隐藏背景；独立实例不串选；暗色资源更新 |
| Gallery | 720×1060的浅/深色、溢出末项离屏输出；主例Day/Week/Month且Week选中 |

## 动效与布局映射

外框Border(border1/radius18)，内部ScrollViewer padding3；SegmentedPanel仅在可见项之间加入2 DIP。SegmentedItem采用minheight36、padding12，图标/文字/Accessory独立槽，间隔6，无槽时Collapsed。共享Border在ItemsPresenter下方，不覆盖文字、不拦截命中。

背景x/width/height由ControlSpring解析临界阻尼(.42视觉时长)更新；目标改变保留当前速度；首次/尺寸变化直接定位，收敛后解除Rendering订阅。文字由SegmentedItem私有画刷160ms OwnedMotion插值，结束后清钟；hover/selected用Foreground，字重/大小不切换。内部滚动使用240ms标准缓动，是浏览器smooth的WPF适配；Reduced/Off全部即时。边缘mask按可见方向与viewport缓存，20 DIP淡出；静止不创建连续动画。

公开绑定、RenderTransform、DataContext不被动画占用。自有背景属性由控件管理；原生选择保留ItemsSource与UIA。Icon与Accessory用于非交互装饰，不用于在选项内嵌套复杂表单。全局主题令牌没有改动。

## 离屏检查与剩余边界

artifacts/segmented-control/gallery-light.png、gallery-overflow.png、gallery-dark.png：96 DPI，720×1060约束，系统Segoe UI/微软雅黑回退，合成示例。RenderTargetBitmap/VisualBrush输出，非实际桌面截图；已检查主例、文字/图标/徽标、禁用、窄宽末项、RTL背景位置与明暗资源，不冒充原站像素基准。

原站为group+pressed按钮，本实现为WPF原生List/SelectionItem；Label是自动化名。默认未选中由调用方给初值。禁用/RTL/Icon为本次WPF扩展。CSS viewport≤380的padding10未映射为控件宽度断点；保持padding12并内部滚动。字体、阴影与浏览器弹簧不是逐像素/逐帧等价。

实际鼠标、滚轮、触摸板、Tab/Shift+Tab、真实组合键、DPI、高对比度、读屏和性能尚未人工验收。程序化鼠标路由/命中、焦点与键盘事件不代表真实窗口操作已通过。当前版本尚无用户通过反馈。

人工步骤：主例来回点击观察背景；Tab进入后方向/Home/End切换；验证禁用项跳过；点“选中月”和“增减选项”检查绑定；窄宽行用方向键/滚轮显露末项；切换明暗/Full/Reduced/Off，检查RTL。

## 最终构建及人工实例

- D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Core.dll：SHA-256 13F5F4C67915B6A534EDCECF3CFAA7C491B746191180B3C1CCFB7BAA3A12E892；文件时间 2026-10-27T10:25:25.1119229+08:00。

- D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Controls.dll：SHA-256 9B5578B072542D1C02E7E915ACB630823140520DFF69906EAD66B23643A692FD；文件时间 2026-10-27T10:29:15.5100540+08:00。

- D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Gallery.dll：SHA-256 CBFEB5AAA4FA06C846C2DED319E9176ED9E86FB363F07EC8607B271C726BBDA2；文件时间 2026-10-27T10:29:16.2974728+08:00。

PID 39140，启动 2026-10-27T10:31:42.5513174+08:00，路径 D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Gallery.exe，参数 --page=segmented-control。仅启动供用户人工验收，未自动操作窗口。

## 用户黑框反馈修正（2026-10-27）

用户截图显示Week选中后有额外深色轮廓，与原版不符。截图归档artifacts/segmented-control/user-focus-outline-before.png，SHA-256 A996DC5BDD464285DB774CC5810711CED628F542530D53CA86A9A71AF7BAB99E；用户桌面DPI未知。定位到项目额外定义的ArcWpf.SegmentedControl.Focus（2 DIP、Focus画刷、.72透明度），已删除该装饰并将SegmentedItem.FocusVisualStyle设为null。共享选中背景及原版1 DIP浅边框保留，键盘导航不变。

首次构建因旧Gallery PID 39140占用DLL而失败；关闭该已确认路径的演示实例后重建成功。仅复跑SegmentedControl专项8/8通过，9秒；键盘焦点测试增加聚焦后无额外FocusVisualStyle断言，既有选择/导航/动效测试通过。未使用computer-use，修正后的实际黑框现象待用户复验，不声明用户已通过。

修正版 D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Controls.dll；SHA-256 89870B0857D1AEA3091B483580118BC3180D949E8BACDB619325BD0AE08C8E1E；文件时间 2026-10-27T10:33:25.5016089+08:00。

修正版 D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Gallery.dll；SHA-256 CBFEB5AAA4FA06C846C2DED319E9176ED9E86FB363F07EC8607B271C726BBDA2；文件时间 2026-10-27T10:33:26.3871956+08:00。

修正版Gallery PID 43188，启动 2026-10-27T10:34:27.6348895+08:00，路径 D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Gallery.exe，参数 --page=segmented-control。仅启动供人工复验。
