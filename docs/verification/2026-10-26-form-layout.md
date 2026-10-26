# 基础表单布局实现与验证（2026-10-26）

Arc没有独立免费Form组件；本轮实现ArcWpf自有`FormLayout`，作为Input/Select/Checkbox/RadioGroup/Switch/Button的组合布局基础。默认Spacing=20、MaxContentWidth=540，折叠子项不占空间，保留原生绑定和输入行为。

程序化验证新增2项通过：宽度、纵向坐标、折叠项和子控件实例。结果artifacts/tests/form-layout.trx。使用现有运行中的Gallery窗口导致完整解决方案复制Debug DLL时被锁定；改用`-p:BuildProjectReferences=false`运行测试，构建阻塞属于运行进程文件锁，不是代码编译错误。实际窗口表单响应式视觉尚未验收。

