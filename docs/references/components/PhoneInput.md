# PhoneInput 参考卡

来源：Arc `https://uiarc.dev/components/phone-input`（Registry `https://uiarc.dev/r/phone-input.json`），2026-10-31 冻结于 `tests/Baselines/References/2026-10-31-phone-input/`（manifest.json 记录 SHA-256 与许可，许可见 `license.html`）。证据等级：源码与 CSS 直接读取（A）；主示例内容取自页面懒加载 chunk（A）；真实浏览器状态截图未采集（用户已确认不作为前置条件）。

- `registry/components/phone-input/phone-input.tsx` SHA-256 `ef8ef7664319a8ef…`
- `registry/components/phone-input/phone-input.module.css` SHA-256 `d0f27fe2f5492773…`

## 还原要点

- 外形：外壳 44 高、圆角 18；左侧国家按钮（内缩 3、圆角 14、内边距 0 33 0 9，旗帜+“+区号”，右侧 16 箭头与 1 像素分隔线）；号码输入 0 36 0 12 内边距，有效时右侧 Success 对勾 snappy 弹入。
- 模型：50 国家表（模式、前缀 trunk、示例、加拿大区号），按模式格式化且不留悬挂分隔符；状态 empty/incomplete/valid/too-long；E.164 输出；解析“+44 (0)7911…”“0044…”自动选国家；后方淡色 PhoneGuide 显示示例剩余部分。
- 选择器：同一块表面从按钮宽高/圆角 17 弹簧变形为面板（≤340 宽，圆角 22，GROW .4 / SHRINK .32），搜索行“Country or code”（清除 24、关闭 36），列表 Suggested（US/CA/GB）+ All countries，最高 280，高亮圆角 16 GLIDE 滑动；号码中输入“+”直接打开并预填“+”。
- 校验：失焦后不完整显示“Numbers in {country} have {n} digits”。
- 主示例（chunk 49377）：380 宽，“Mobile number”，描述“Type + to search by calling code”，有效后可 Send code（900 ms → Resend code，“Code sent to +1 (201) 555-0123”）。
