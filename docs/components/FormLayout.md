# 基础表单布局规格

状态：实现及程序化验证完成，待用户实际窗口验收。Arc没有独立免费Form组件，本组件是ArcWpf自有WPF布局基础，不伪装为Arc原版事实。

`FormLayout : Panel` 纵向排列字段，默认字段间距20 DIP，最大内容宽度540 DIP。保留子控件实例、DataContext、绑定、命令和原生键盘行为；折叠子项不占空间。适合组合Input、Select、Checkbox、RadioGroup、Switch和Button。响应式行为由外层窗口/ScrollViewer决定，不替换字段控件。

本次不实现表单校验引擎、提交状态机、两列断点、业务字段模型或多步骤向导；这些属于业务区块或后续组件。Arc字段视觉继续由各自规格负责。
