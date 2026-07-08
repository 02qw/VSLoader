# v137 VSLoader 全应用成熟产品化视觉系统重构编码规格说明

## 1. 背景

v136 已经完成主界面的成熟产品化视觉重构：

```text
顶部工具区分层
按钮语义分级
表格密度优化
主界面视觉主次收口
```

现在需要把同一套成熟产品化视觉语言扩展到 VSLoader 的其他页面和弹窗，使程序整体不再出现：

```text
主界面较现代，但子窗口仍像旧式工具
不同页面按钮、输入框、外壳风格不一致
弹窗和任务窗口视觉层级松散
地图、批量导入、更新器之间像不同程序
```

本次采用方案 C：

```text
完整视觉系统收口
```

但必须控制逻辑边界：

```text
只做视觉系统、XAML 布局、样式资源、静态测试和必要的只读视觉辅助属性。
不改业务流程、不改数据模型、不改服务算法、不改命令语义。
```

## 2. 总体目标

### 2.1 产品目标

让 VSLoader 从“功能齐全的内部工具”提升为：

```text
稳定
统一
克制
专业
可长期使用
像成熟商用桌面软件
```

用户在不同窗口之间切换时，应感到：

```text
同一个产品
同一套交互语言
同一套颜色和按钮语义
同一套窗口外壳和弹窗规则
```

### 2.2 技术目标

1. 建立一套全应用可复用的现代视觉资源。
2. 统一主程序窗口、设置、工作区、快捷项编辑、批量导入、地图、消息弹窗、更新器的视觉语言。
3. 保留所有现有业务命令、事件处理、绑定路径和窗口生命周期逻辑。
4. 通过 XAML 静态测试保护关键入口不丢失。
5. 通过构建验证避免 XAML 资源引用错误。
6. 不引入第三方 UI 框架。
7. 不做暗色模式。
8. 不做大规模交互重构。

## 3. 实现边界

### 3.1 允许修改

主程序视觉文件：

```text
VSLoader/Styles/ModernTheme.xaml
VSLoader/Styles/ModernWindowChrome.xaml
VSLoader/Views/Controls/ModernTitleBar.xaml
VSLoader/MainWindow.xaml
VSLoader/Views/SettingsWindow.xaml
VSLoader/Views/WorkspaceSelectorWindow.xaml
VSLoader/Views/WorkspaceNameDialog.xaml
VSLoader/Views/ShortcutEditWindow.xaml
VSLoader/Views/BatchImportWindow.xaml
VSLoader/Views/MessageDialogWindow.xaml
VSLoader/Views/FactoryMapWindow.xaml
```

更新器视觉文件：

```text
VSLoader.Updater/MainWindow.xaml
VSLoader.Updater/UpdateCompletedDialog.xaml
VSLoader.Updater/App.xaml
```

测试文件：

```text
VSLoader.Tests/*VisualTests.cs
VSLoader.Tests/*Window*Tests.cs
VSLoader.Tests/ModernTheme*Tests.cs
VSLoader.Tests/ModernWindowChrome*Tests.cs
```

仅在必要时允许修改 code-behind 中的纯视觉辅助逻辑：

```text
窗口拖拽
窗口按钮 hover
视觉状态同步
不改变业务命令执行逻辑
```

### 3.2 不允许修改

以下内容本次不允许修改：

```text
MainViewModel 业务逻辑
SettingsViewModel 业务逻辑
ConfigService
WorkspaceService
UpdateCheckService
SoftwareUpdateService
GlobalConfigPackageService
BatchImport 识别算法
AdminUI/WebUI 拼接和启动逻辑
FactoryMap 数据、节点、连线、快捷键业务逻辑
UpdaterApplyService 更新覆盖逻辑
快捷项数据模型
工作区数据模型
配置文件读写结构
```

如果实现过程中发现必须改业务逻辑才能达到视觉目标，应停止并重新确认需求。

## 4. 视觉系统方向

### 4.1 设计定位

全应用统一为：

```text
Professional Operations Console
专业生产控制台
```

关键词：

```text
浅色
清爽
低噪声
高可读
高密度
状态明确
边界清楚但不生硬
```

禁止方向：

```text
大面积渐变
营销页 Hero
装饰性图形
复杂动画
厚重阴影
强烈玻璃拟态
过度圆角
一页一个风格
```

### 4.2 全应用颜色语义

统一颜色语义：

```text
蓝色：主操作、焦点、选中态
绿色：成功、可更新、正向完成
黄色：提醒、需要用户注意
红色：危险、错误、删除
灰蓝：普通边框、次级信息、背景层级
白色：内容面板和输入区域
```

要求：

```text
同一语义只能使用同一色系。
普通按钮不得使用高饱和色。
危险按钮默认不做大面积红底，只在 hover/pressed 增强。
更新软件按钮只有在 HasSoftwareUpdateNotice 为 true 时为绿色。
```

### 4.3 全应用密度

统一控件密度：

```text
普通按钮高度：34
紧凑工具按钮高度：32
输入框高度：34-36
表格行高：38-40
表头高度：36
弹窗按钮高度：34
页面外边距：16
页面区块间距：12 或 16
控件间距：8
```

字体：

```text
继续使用 Microsoft YaHei UI
主体字号 13
重要标题 15-16
表头 13 SemiBold
主字段 SemiBold
不使用大面积 Bold
```

## 5. 样式资源建设

文件：

```text
VSLoader/Styles/ModernTheme.xaml
```

在现有资源基础上补齐全应用视觉系统，不重复造多个相似样式。

### 5.1 推荐新增或确认存在的资源

基础面：

```text
ModernAppBackgroundBrush
ModernSurfaceBrush
ModernElevatedSurfaceBrush
ModernSurfaceBorderBrush
ModernWindowOuterBorderBrush
ModernDividerBrush
```

文本：

```text
ModernPrimaryTextBrush
ModernBodyTextBrush
ModernSecondaryTextBrush
ModernWeakTextBrush
```

按钮：

```text
ModernButtonStyle
ModernPrimaryButtonStyle
ModernQuietButtonStyle
ModernDangerButtonStyle
ModernUpdateSoftwareButtonStyle
ModernIconButtonStyle
ModernDialogPrimaryButtonStyle
ModernDialogSecondaryButtonStyle
```

输入：

```text
ModernTextBoxStyle
ModernPasswordBoxStyle
ModernReadOnlyTextBoxStyle
ModernMultilineTextBoxStyle
ModernComboBoxStyle
ModernCheckBoxStyle
```

容器：

```text
ModernSurfaceBorderStyle
ModernSectionBorderStyle
ModernDialogSurfaceStyle
ModernBusyPanelStyle
ModernToolStripStyle
```

表格：

```text
ModernDataGridStyle
ModernDataGridColumnHeaderStyle
ModernDataGridCellStyle
ModernDataGridRowStyle
```

菜单：

```text
ModernContextMenuStyle
ModernMenuItemStyle
```

横幅：

```text
ModernInfoBannerStyle
ModernWarningBannerStyle
ModernErrorBannerStyle
ModernSuccessBannerStyle
ModernBannerActionButtonStyle
```

### 5.2 资源使用原则

1. 页面 XAML 优先引用资源，不散落大量硬编码颜色。
2. 少量局部微调允许保留，但不能形成第二套风格。
3. 不覆盖全局滚动条样式，避免破坏原生滚动条可用性。
4. 不改变触控板滚动行为相关属性，除非测试明确覆盖。

## 6. 页面族重构方案

### 6.1 配置类页面

范围：

```text
SettingsWindow.xaml
WorkspaceSelectorWindow.xaml
WorkspaceNameDialog.xaml
ShortcutEditWindow.xaml
```

视觉目标：

```text
像系统设置/配置中心
信息分组清楚
输入项对齐
按钮主次明确
空白不拥挤
```

布局规则：

```text
顶部：标题 + 简短说明，不做大 Hero
主体：分组 Section，使用 ModernSectionBorderStyle
底部：固定操作区，右侧放主按钮和取消按钮
输入项：Label 在上或左侧统一，不混用太多布局方式
```

注意：

```text
设置页内容多，不要压缩到难读。
保留现有滚动容器和触控板滚动优化。
密码明文显示逻辑不在本次修改范围内。
工作区右键菜单功能不变。
```

### 6.2 任务类页面

范围：

```text
BatchImportWindow.xaml
VSLoader.Updater/MainWindow.xaml
VSLoader.Updater/UpdateCompletedDialog.xaml
```

视觉目标：

```text
像任务执行/导入向导
进度、状态、结果有清楚层级
用户知道当前正在发生什么
```

布局规则：

```text
顶部：任务标题 + 当前状态
中部：参数区 / 预览区 / 日志区明确分层
进度：统一 ProgressBar 和日志滚动区域样式
底部：左侧辅助信息，右侧主操作按钮
```

更新器特别要求：

```text
保留 releaseNotes 展示逻辑。
保留进度条和实时滚动详情。
更新完成后仍需用户确认才启动 VSLoader。
不改变 updater 外部临时副本运行逻辑。
```

批量导入特别要求：

```text
保留扫描预览、导入确认、网络预检、遮罩防乱点逻辑。
只优化面板、按钮、表格、进度遮罩视觉。
```

### 6.3 画布类页面

范围：

```text
FactoryMapWindow.xaml
```

视觉目标：

```text
地图是生产画布，不是表单页面。
背景、节点、连线、工具条、状态栏层级清楚。
```

布局规则：

```text
标题栏与其他窗口一致。
工具条使用 ModernToolStripStyle。
浏览/编辑模式颜色语义保持现有红锁/绿编辑逻辑，但视觉更克制。
底部状态栏必须在最大化和窗口化时都可见。
节点、连线、网格背景不要互相抢视觉。
```

禁止：

```text
不改节点数据结构。
不改连线删除权限。
不改快捷键 Alt+X 逻辑。
不改地图窗口独立窗口生命周期。
```

### 6.4 弹窗类页面

范围：

```text
MessageDialogWindow.xaml
WorkspaceNameDialog.xaml
UpdateCompletedDialog.xaml
所有 BusyOverlay / 进度遮罩面板
```

视觉目标：

```text
像成熟产品弹窗
边界清楚
标题明确
按钮主次明确
错误信息可追溯
```

布局规则：

```text
弹窗宽度按内容固定，不拉得过大。
标题 15-16 SemiBold。
正文 13，支持换行。
按钮区右对齐。
主按钮使用 Primary。
取消/我知道了使用 Secondary 或 Quiet。
危险确认使用 Danger，但默认不大面积红底。
```

## 7. 窗口外壳统一

文件：

```text
VSLoader/Styles/ModernWindowChrome.xaml
VSLoader/Views/Controls/ModernTitleBar.xaml
```

要求：

```text
所有主程序窗口使用统一标题栏高度。
窗口外边界可见但不突兀。
最大化、最小化、关闭按钮尺寸和图标语义一致。
hover 颜色克制。
关闭按钮 hover 可使用浅红背景。
```

不允许：

```text
不改变窗口最小化/关闭业务语义。
不改变地图独立窗口任务栏行为。
不改变主窗口托盘退出行为。
```

## 8. ContextMenu 统一

范围：

```text
主界面快捷项右键菜单
地图节点右键菜单
地图连线右键菜单
工作区右键菜单
```

要求：

```text
统一白底、浅边框、无灰色竖条。
菜单项高度 34-36。
hover 使用浅蓝灰背景。
危险项文字红色，但背景只在 hover 时浅红。
菜单项文字左对齐。
```

必须保留：

```text
菜单出现条件
浏览/编辑模式权限
现有命令绑定
```

## 9. 具体文件要求

### 9.1 SettingsWindow.xaml

重点：

```text
统一 Section 样式。
输入框、密码框、ComboBox、CheckBox 全部走 ModernTheme。
底部按钮区统一。
保持滚动体验，不缩小滚动条。
```

测试保护：

```text
设置项绑定路径仍存在。
AdminUI 自动粘贴相关配置仍存在。
WebUI/AdminUI/更新路径配置入口仍存在。
```

### 9.2 WorkspaceSelectorWindow.xaml

重点：

```text
保留已优化的现代工作区视觉。
进一步统一按钮、右键菜单、空状态、上次使用提示。
去除残留硬编码灰底。
```

测试保护：

```text
打开
重命名
打开工作区文件夹
删除
新增工作区
```

### 9.3 ShortcutEditWindow.xaml

重点：

```text
新增和编辑共用 UI。
修复文字过于靠右、输入框失焦体验保持。
字段分组更清楚。
保存/取消按钮统一。
```

测试保护：

```text
Name
TargetPath
Description
SourceModuleName
AdminUI/WebUI 相关字段
```

### 9.4 BatchImportWindow.xaml

重点：

```text
顶部参数区更像任务配置。
扫描预览按钮主次明确。
进度遮罩统一使用 ModernBusyPanelStyle。
预览表格使用 ModernDataGridStyle。
错误信息区域可读。
```

测试保护：

```text
扫描预览按钮
确认导入按钮
网络预检提示
目标父级文件夹路径
CSV 规则文件路径
```

### 9.5 FactoryMapWindow.xaml

重点：

```text
标题栏统一。
工具条按钮统一。
状态栏统一。
右键菜单统一。
地图背景和节点视觉不大改，只做系统化收口。
```

测试保护：

```text
Alt+X 地图快捷键相关入口不变。
自动获取连接按钮存在。
浏览/编辑模式视觉和权限不变。
底部状态栏字段存在。
```

### 9.6 MessageDialogWindow.xaml

重点：

```text
统一所有确认/错误/提醒弹窗基础样式。
标题、正文、按钮区层级清楚。
错误详情支持换行和复制友好。
```

测试保护：

```text
确认按钮
取消按钮
消息正文绑定
窗口返回结果逻辑不变
```

### 9.7 VSLoader.Updater 窗口

重点：

```text
更新器也要像 VSLoader 的一部分。
颜色、标题、进度、日志、releaseNotes 与主程序统一。
```

必须保留：

```text
外部临时 updater 运行逻辑
日志写入
失败回滚
完成确认后启动 VSLoader
releaseNotes 延迟展示
实时滚动详情
```

## 10. 测试设计

### 10.1 样式资源测试

新增或扩展：

```text
VSLoader.Tests/ModernThemeProductSystemTests.cs
```

断言：

```text
ModernQuietButtonStyle 存在
ModernDialogPrimaryButtonStyle 存在
ModernDialogSecondaryButtonStyle 存在
ModernContextMenuStyle 存在
ModernMenuItemStyle 存在
ModernInfoBannerStyle 存在
ModernWarningBannerStyle 存在
ModernErrorBannerStyle 存在
ModernToolStripStyle 存在
ModernDialogSurfaceStyle 存在
```

### 10.2 页面视觉静态测试

新增或扩展：

```text
VSLoader.Tests/AllWindowsProductVisualTests.cs
```

读取以下 XAML：

```text
SettingsWindow.xaml
WorkspaceSelectorWindow.xaml
WorkspaceNameDialog.xaml
ShortcutEditWindow.xaml
BatchImportWindow.xaml
MessageDialogWindow.xaml
FactoryMapWindow.xaml
VSLoader.Updater/MainWindow.xaml
VSLoader.Updater/UpdateCompletedDialog.xaml
```

断言：

```text
主要窗口引用 ModernTheme 关键样式。
窗口按钮不散落旧式灰底按钮。
输入类页面使用 ModernTextBoxStyle / ModernPasswordBoxStyle。
表格类页面使用 ModernDataGridStyle。
弹窗类页面使用 ModernDialogSurfaceStyle。
右键菜单使用 ModernContextMenuStyle 或 ModernMenuItemStyle。
```

### 10.3 业务入口保护测试

每个页面静态断言关键命令或事件名仍存在：

设置页：

```text
SaveCommand
CancelCommand
Browse...
AdminUi...
WebUi...
Update...
```

工作区：

```text
Open
Rename
Delete
OpenFolder
Create
```

快捷项编辑：

```text
Save
Cancel
Browse
```

批量导入：

```text
ScanPreview
ConfirmImport
Cancel
```

地图：

```text
Import
Export
AutoGetLinks
EditMode
BrowseMode
```

更新器：

```text
releaseNotes
progress
details
confirm
```

实际测试中以当前代码中的真实绑定和事件名为准，不凭空创造名称。

### 10.4 构建验证

必须运行：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~ModernThemeProductSystemTests|FullyQualifiedName~AllWindowsProductVisualTests"
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果有既有窗口测试，应补跑：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~SettingsWindow|FullyQualifiedName~WorkspaceSelectorWindow|FullyQualifiedName~MessageDialogWindow|FullyQualifiedName~FactoryMapWindow|FullyQualifiedName~Updater"
```

## 11. 分阶段实现建议

虽然用户目标是“接下来一次都搞定”，实现仍需按阶段落地，避免一次性改太多导致难排查。

### 阶段 1：视觉系统资源

```text
补齐 ModernTheme.xaml 中缺失的通用样式。
新增静态测试。
构建验证。
```

### 阶段 2：配置类页面

```text
SettingsWindow
WorkspaceSelectorWindow
WorkspaceNameDialog
ShortcutEditWindow
```

完成后运行窗口相关测试和构建。

### 阶段 3：任务类页面

```text
BatchImportWindow
Updater MainWindow
UpdateCompletedDialog
BusyOverlay
```

完成后运行更新器、批量导入相关测试和构建。

### 阶段 4：画布类和菜单

```text
FactoryMapWindow
所有 ContextMenu
地图工具条
地图状态栏
```

完成后运行地图相关测试和构建。

### 阶段 5：全量视觉一致性扫尾

```text
扫描硬编码颜色和旧式 Button。
确认没有残留灰底旧外壳。
确认弹窗按钮语义统一。
最终 Debug 构建。
```

## 12. 手动验收清单

完成后人工检查：

```text
1. 主窗口、设置、工作区、批量导入、地图、更新器看起来属于同一个产品。
2. 所有窗口外边界清楚但不过分突兀。
3. 所有按钮主次明确。
4. 危险操作仍然红色，但不刺眼。
5. 更新软件按钮仍然只有在有更新时变绿。
6. 设置页输入框文字不下沉、不被裁切。
7. 工作区窗口没有多余灰底块。
8. 快捷项编辑窗口文字不靠右，输入体验正常。
9. 批量导入进度遮罩清楚，不影响防误操作。
10. 地图最大化/窗口化底部状态栏都保留。
11. 地图右键菜单不会错误拉起主窗口。
12. 更新器 releaseNotes、进度条、滚动详情正常。
13. 所有弹窗按钮统一，不再出现旧式蓝色 hover。
14. 触控板滚动体验不退化。
15. Debug 构建通过。
```

## 13. 风险和规避

### 13.1 XAML 资源引用错误

风险：

```text
新增样式名拼写错误会导致窗口启动崩溃。
```

规避：

```text
先写静态测试。
每阶段运行 dotnet build。
不要一次改完所有页面再构建。
```

### 13.2 业务绑定误删

风险：

```text
视觉重排时删掉 Command 或 Click。
```

规避：

```text
每个窗口测试关键绑定字符串。
改布局时搬迁原控件，不重写业务入口。
```

### 13.3 滚动体验退化

风险：

```text
全局滚动条样式覆盖导致触控板滚动变差。
```

规避：

```text
不写全局 ScrollBar 样式。
保留已有 SmoothTouchpadScrollBehavior。
保留 ScrollViewer.PanningMode 等现有属性。
```

### 13.4 地图窗口逻辑被破坏

风险：

```text
地图窗口现在有独立窗口、Alt+X、状态保存、最大化工作区约束等复杂逻辑。
```

规避：

```text
FactoryMapWindow 只做 XAML 视觉层。
不改 FactoryMapWindow.xaml.cs 中窗口生命周期和快捷键逻辑。
运行地图窗口相关测试。
```

### 13.5 Updater 更新流程被破坏

风险：

```text
Updater 负责替换程序本体，逻辑不能被视觉重构影响。
```

规避：

```text
只改 Updater XAML。
不改 UpdaterApplyService。
不改外部副本启动逻辑。
运行 Updater 相关测试。
```

## 14. 不做的事情

本次不做：

```text
不改业务逻辑
不改配置结构
不改工作区结构
不改更新检测算法
不改地图节点/连线算法
不新增暗色模式
不引入第三方 UI 框架
不做动画系统
不把按钮隐藏进更多菜单
不做全新信息架构
```

## 15. 最终交付要求

完成后应交付：

```text
1. 全应用统一视觉系统资源。
2. 所有主要窗口和弹窗视觉统一。
3. 新增或更新对应静态测试。
4. 阶段性测试通过记录。
5. Debug 构建通过。
```

最终效果：

```text
VSLoader 不只是主界面变好看，而是每个窗口都像同一个成熟产品的一部分。
```

