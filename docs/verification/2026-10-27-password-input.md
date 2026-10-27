# PasswordInput 验证记录（2026-10-27）

本次实现完成，实际窗口待用户人工验收。遵循用户限制，只运行PasswordInput专项，不使用computer-use或其他控件回归。来源与差异见[参考卡](../references/components/PasswordInput.md)，API见[规格](../components/PasswordInput.md)。

## 构建与专项

环境：Windows、.NET SDK 10.0.402、net10.0-windows、Debug。执行：

```powershell
dotnet test tests/ArcWpf.Tests/ArcWpf.Tests.csproj --no-restore --filter FullyQualifiedName~ArcWpf.Tests.PasswordInputTests --logger 'trx;LogFileName=password-input.trx' --results-directory artifacts/password-input -v minimal
```

最终11/11通过，0失败，7秒；结果artifacts/password-input/password-input.trx。编译依赖包括Gallery/Consumer，没有执行其他控件测试。构建存在既有BreadcrumbPage.PageCommand.CanExecuteChanged未使用警告CS0067。

首轮9/11：修复Enter被自定义与原生逻辑重复切换；动画测试改为先推进真实Dispatcher时钟再读取中间帧，避免把动画开始前的目标基值误作动画帧。最终另修正消息槽透明度同时作用于退出字词，并复跑全部11项PasswordInput专项通过。

| 证据 | 结果与边界 |
|---|---|
| 数据/模板 | SecurePassword独立副本；Clear/恢复；旧模板编辑器清空；无Password DP |
| 编辑/事件 | 原生遮蔽和显示编辑各触发一次事件；切换不触发密码变化；显示编辑器禁止Undo |
| 绑定/焦点 | IsPasswordRevealed双向；翻译按钮名称；程序化Enter/UIA Toggle；焦点迁移 |
| 合成/生命周期 | 注入合成事件暂缓切换；禁用/不可见/卸载清理显示文本，保留内部值；关闭后动效停止 |
| 自动化 | 两编辑器密码语义与空Value；显示编辑器无可读TextPattern；禁用拒绝写入 |
| 动效 | 眼睛斜线中间帧、快速反向与终态；resolve blur；消息字词保留、换行及高度中间值；Reduced/Off无残留 |
| 验证/主题 | 显式错误优先与Validation.Errors；MaxLength/PasswordChar连接；主题画刷刷新 |
| 布局 | 400/180 DIP宽、壳44/按钮30/图标18；显示文字与placeholder起点差≤0.5 DIP；长文本不挤按钮，程序化命中有效 |
| Gallery | 浅色空值、显示值、暗色离屏图；均只使用合成演示密码 |

## 离屏检查与人工边界

artifacts/password-input/gallery-light.png、gallery-revealed.png、gallery-dark.png为720×960、96 DPI的RenderTargetBitmap/VisualBrush测试Window输出，非桌面截图。已查看标签、空值/显示值、眼睛斜线、180 DIP长值、禁用、错误与说明换行；未见按钮遮挡或壳高度变化。不是原站像素比对，也不作为已批准基准。

来源结构对应WPF：label/bottom8→独立标签；min-height44/radius18/padding12,6→输入壳；30按钮/18eye→固定列及矢量；说明top8→消息宿主。眼睛length240ms/opacity120ms，resolve240ms，状态色160ms；消息字词240ms进入/180ms退出、高度400ms采样spring。WPF字体/抗锯齿与颜色插值不同于CSS；spring由OwnedMotion采样近似，真实动态观感待人工。

隐藏状态使用原生PasswordBox；显示状态使用显式TextBox，切换后选区定位末尾，不能保持PasswordBox未公开的完整选区。显式Password读写或同步原生Password时会产生托管明文字符串；不承诺零明文内存。未加入强度评分、密码管理器集成或IsReadOnly。

人工验收：输入/粘贴，点击眼睛往返并继续编辑；Tab到眼睛用Space/Enter切换；合成中文时切换；填入演示值后检查窄框水平滚动；切换说明/错误及明暗/动效等级。真实键鼠、剪贴板、IME、多DPI、读屏及性能尚未运行，本版本尚无用户通过反馈。

## 最终构建与人工验收实例

- ArcWpf.Core.dll : SHA-256 477568CD7E8F7CA557E528D16D672FE20C0280E4285B11763F263AA03C595CDC；文件时间 2026-10-27T00:52:19.9809685+08:00。

- ArcWpf.Controls.dll : SHA-256 356009D9ADFAF360DF3B8B95A2F3D7AB0332465F60A287D1583C82844C9FBF01；文件时间 2026-10-27T01:02:55.6317875+08:00。

- ArcWpf.Gallery.dll : SHA-256 CA2482629599AEFE0B37EC24FA09F4DD5CA87B2E02464E0AA396A031C20FDFC9；文件时间 2026-10-27T01:02:57.1602217+08:00。

PID 10036，启动 2026-10-27T01:04:46.2894474+08:00，路径 D:\AI\ArcWpf\samples\ArcWpf.Gallery\bin\Debug\net10.0-windows\ArcWpf.Gallery.exe，参数 --page=password-input。仅打开供用户人工操作，未自动操作窗口。
