# v136 VSLoader 主界面成熟产品化视觉系统重构编码规格说明

## 1. 背景

当前 VSLoader 主界面功能已经比较完整：

```text
工作区
快捷项列表
搜索
新增/打开/编辑/删除
地图
AdminUI
自动获取连接
检测更新/更新软件
导入导出全局配置
批量新增识别
```

但从成熟商用软件产品设计角度看，当前界面仍然更像“内部工具”：

```text
按钮数量多
按钮权重接近
颜色语义不够收口
顶部区域平铺感明显
表格视觉偏粗
边框和间距体系不够统一
```

本次需求是：

```text
在不改变业务逻辑的前提下，对 VSLoader 主界面做成熟产品化视觉重构。
```

目标不是做炫酷界面，而是让主界面更像成熟稳定的商用工具：

```text
清晰
克制
统一
高密度但不拥挤
主次明确
可长时间使用
```

## 2. 当前代码现状

### 2.1 主界面 XAML

文件：

```text
VSLoader/MainWindow.xaml
```

当前主界面主要结构：

```text
ModernTitleBar
状态横幅
更新提醒横幅
更新失败横幅
搜索框 + 数量 + 设置
WrapPanel 功能按钮区
DataGrid 快捷项列表
BusyOverlay
```

当前功能按钮区使用 `WrapPanel` 平铺：

```xml
<WrapPanel Grid.Row="1"
           Margin="0,10,0,0"
           Orientation="Horizontal">
    ...
</WrapPanel>
```

按钮主要使用：

```text
ModernButtonStyle
ModernPrimaryButtonStyle
ModernUpdateSoftwareButtonStyle
ModernDangerButtonStyle
```

### 2.2 视觉样式资源

文件：

```text
VSLoader/Styles/ModernTheme.xaml
```

当前已有基础资源：

```text
ModernAppBackgroundBrush
ModernSurfaceBrush
ModernSurfaceBorderBrush
ModernPrimaryTextBrush
ModernBodyTextBrush
ModernSecondaryTextBrush
ModernButtonStyle
ModernPrimaryButtonStyle
ModernUpdateSoftwareButtonStyle
ModernDangerButtonStyle
ModernDataGridStyle
ModernBusyPanelStyle
```

说明：

```text
项目已经有现代化样式基础。
本次不需要重建整套样式系统，而是把主界面视觉语言收口。
```

### 2.3 标题栏样式

文件：

```text
VSLoader/Styles/ModernWindowChrome.xaml
VSLoader/Views/Controls/ModernTitleBar.xaml
```

当前窗口已经使用自定义标题栏。

本次不重点改标题栏结构，只允许做轻微视觉微调：

```text
标题字体颜色
标题栏底部分割线
窗口按钮 hover 视觉
```

不要重写窗口控制逻辑。

### 2.4 表格样式

文件：

```text
VSLoader/MainWindow.xaml
VSLoader/Styles/ModernTheme.xaml
```

当前表格使用：

```xml
<DataGrid x:Name="ShortcutsGrid"
          Style="{StaticResource ModernDataGridStyle}"
          RowHeight="44"
          ColumnHeaderHeight="38"
          FontSize="14"
          ... />
```

当前视觉问题：

```text
行高偏大
名称列字重偏重
选中态较显眼
表格边框偏硬
数据表密度不够成熟工具化
```

## 3. 总体目标

### 3.1 用户目标

用户打开 VSLoader 后，应感觉：

```text
这是一个稳定、成熟、可长期使用的生产工具。
```

用户能更快区分：

```text
最常用操作
次常用操作
低频工具
危险操作
状态提醒
```

### 3.2 技术目标

1. 不改变任何业务命令绑定。
2. 不改变快捷项数据结构。
3. 不改变更新检测、地图、AdminUI、批量新增等业务逻辑。
4. 建立更清晰的主界面按钮层级。
5. 优化顶部工具区布局，减少平铺拥挤感。
6. 优化表格视觉密度和成熟度。
7. 统一颜色语义和按钮状态。
8. 保持现有可测试性，通过 XAML 静态测试约束关键结构。

## 4. 产品视觉方向

### 4.1 设计定位

本次主界面视觉方向定义为：

```text
清爽企业工具 / Calm Operations Console
```

关键词：

```text
浅色
克制
低噪声
高可读
高密度
稳定
操作导向
```

不要走：

```text
营销型首页
大面积渐变
卡片堆叠
夸张阴影
彩色装饰
过度圆角
大标题 Hero
```

### 4.2 色彩语义

颜色语义必须固定：

```text
蓝色：主操作 / 当前选中 / 焦点
绿色：软件更新可用 / 成功确认
黄色：提醒 / 需要用户知道
红色：危险操作 / 错误
灰蓝：普通文本 / 次级信息 / 边框
白色：主内容面
浅蓝灰：应用背景
```

禁止：

```text
同一语义使用多个不一致颜色
普通按钮使用高饱和色
低频按钮抢主操作权重
```

### 4.3 字体和密度

继续使用：

```text
Microsoft YaHei UI
```

主界面目标密度：

```text
按钮高度：32-34
搜索框高度：34-36
表格行高：38-40
表头高度：36
主体字体：13-14
表格名称列：SemiBold，不使用过重 Bold
```

注意：

```text
不允许为了高级感把字体做得过小。
生产工具要优先保证可读性。
```

## 5. 主界面布局重构方案

### 5.1 顶部结构分层

文件：

```text
VSLoader/MainWindow.xaml
```

当前顶部是：

```text
搜索区
WrapPanel 按钮区
```

建议改成三层：

```text
1. SearchBarRow：搜索框 + 数量 + 设置
2. PrimaryActionRow：常用主操作
3. UtilityActionRow：低频工具操作
```

推荐结构：

```text
SearchBarRow:
    搜索框
    55 / 55
    设置

PrimaryActionRow:
    工作区
    新增
    打开
    编辑
    地图
    AdminUI
    删除

UtilityActionRow:
    更新软件
    检测更新
    自动获取连接
    批量新增识别
    导出全局配置
    导入全局配置
```

原因：

```text
常用操作和工具操作分开后，页面主次更清楚。
低频功能不再和核心操作抢视觉权重。
```

### 5.2 按钮分组容器

推荐使用两个 `WrapPanel` 或两个横向 `Items` 区域。

不强制引入菜单或更多按钮。

原因：

```text
当前用户已经熟悉所有按钮直接可见。
第一阶段先通过分组和视觉权重降低拥挤感，不隐藏功能。
```

### 5.3 设置按钮位置

设置按钮保持在搜索行右侧。

不要移入工具区。

原因：

```text
设置是全局入口，不属于快捷项操作。
放在右上更符合成熟工具习惯。
```

## 6. 按钮体系重构

### 6.1 按钮语义分级

文件：

```text
VSLoader/Styles/ModernTheme.xaml
VSLoader/MainWindow.xaml
```

按钮语义：

```text
Primary：新增
Secondary：工作区 / 打开 / 编辑 / 地图 / AdminUI
Utility：检测更新 / 自动获取连接 / 批量新增识别 / 导入导出配置
Success：更新软件，且仅在有软件更新时绿色
Danger：删除
```

### 6.2 新增 Quiet 工具按钮样式

新增样式：

```text
ModernQuietButtonStyle
```

用途：

```text
低频工具按钮
```

建议视觉：

```text
背景：透明或接近背景色
边框：浅灰蓝
文字：次级深蓝灰
Hover：浅蓝灰背景
Pressed：浅蓝背景
高度：34
圆角：6
```

用于：

```text
检测更新
自动获取连接
批量新增识别
导出全局配置
导入全局配置
```

### 6.3 调整 Danger 样式

`删除` 按钮保持红色语义，但不要过于刺眼。

建议：

```text
默认：白底 + 浅红边 + 红字
Hover：浅红背景
Pressed：更深浅红
```

当前已有 `ModernDangerButtonStyle`，可以微调，不必重建。

### 6.4 更新软件按钮

保留 v133 已实现逻辑：

```text
无软件更新：普通/工具态
有软件更新：绿色强调态
```

注意：

```text
不要把更新软件永久设为绿色。
绿色只由 HasSoftwareUpdateNotice 驱动。
```

## 7. 横幅提醒优化

### 7.1 黄色更新横幅

当前黄色横幅整体可用，但需要更成熟：

```text
背景更浅
边框更细
按钮“我知道了”不要过重
文字颜色降低棕色感
```

建议：

```text
背景：#FFF8E1 或 #FFF7D6
边框：#F3D37A
文字：#5C4500
按钮：透明背景，hover 浅黄
```

### 7.2 错误横幅

红色错误横幅保持独立。

建议：

```text
背景：#FEF2F2
边框：#FCA5A5
文字：#7F1D1D
```

不改变命令：

```text
CloseUpdateNoticeCommand
CloseUpdateFailureCommand
```

## 8. 表格成熟化重构

### 8.1 DataGrid 外层容器

文件：

```text
VSLoader/MainWindow.xaml
```

当前外层 Border 可以保留，但建议：

```text
CornerRadius：6 或 8
BorderBrush：更浅
BorderThickness：1
Background：White
```

不要添加厚重阴影。

### 8.2 表格行样式

文件：

```text
VSLoader/Styles/ModernTheme.xaml
```

目标：

```text
RowHeight：38-40
表格主体 FontSize：13 或 14
行分割线更浅
选中态更柔和
```

选中态建议：

```text
背景：#EAF3FF
左侧选中条：#2563EB，宽度 3
文字：保持深色，不反白
```

### 8.3 表头样式

表头应更像成熟数据表：

```text
背景：#F8FAFC
文字：#475569
字重：SemiBold
高度：36
边框：底部细线
```

### 8.4 名称列字重

当前名称列视觉偏重。

建议：

```text
FontWeight="SemiBold"
```

不要使用：

```text
Bold / ExtraBold
```

原因：

```text
表格中大量粗体会造成视觉噪声。
成熟工具通常用 SemiBold 提示主字段。
```

## 9. 间距系统

主界面间距统一使用 4/8 系列：

```text
外边距：16
区域间距：12 或 16
按钮间距：8
按钮行间距：8
表格内边距：0 或 4
横幅内边距：12
```

禁止随意出现：

```text
10、13、17、19 这类无语义间距
```

例外：

```text
已有控件模板中为了视觉对齐的 1px/2px 边框和微调可以保留。
```

## 10. 实现范围

### 10.1 允许修改

```text
VSLoader/MainWindow.xaml
VSLoader/Styles/ModernTheme.xaml
VSLoader/Styles/ModernWindowChrome.xaml
VSLoader.Tests/MainWindow... 相关静态测试
VSLoader.Tests/ModernTheme... 相关静态测试
```

### 10.2 不允许修改

```text
MainViewModel 业务命令逻辑
ConfigService
UpdateCheckService
SoftwareUpdateService
FactoryMapWindow 业务逻辑
BatchImport 业务逻辑
AdminUI/WebUI 拼接逻辑
快捷项数据模型
```

除非测试发现视觉重构必须补充只读属性，否则不要碰 ViewModel。

## 11. 测试设计

### 11.1 主界面按钮分层静态测试

新增或修改测试：

```text
VSLoader.Tests/MainWindowProductVisualTests.cs
```

断言：

```text
MainWindow.xaml 中存在 PrimaryActionRow 或等价命名容器。
MainWindow.xaml 中存在 UtilityActionRow 或等价命名容器。
新增按钮仍使用 ModernPrimaryButtonStyle。
删除按钮仍使用 ModernDangerButtonStyle。
低频工具按钮使用 ModernQuietButtonStyle。
更新软件按钮仍使用 ModernUpdateSoftwareButtonStyle。
```

### 11.2 业务命令绑定保护测试

同一测试文件中读取 `MainWindow.xaml`，断言以下绑定仍存在：

```text
AddShortcutCommand
UpdateSoftwareCommand
ManualCheckUpdatesCommand
ExportGlobalConfigCommand
ImportGlobalConfigCommand
OpenBatchImportCommand
DownloadAdminUiLinksCommand
OpenAdminUiCommand
OpenShortcutCommand
EditShortcutCommand
DeleteShortcutCommand
FactoryMapButton_Click
WorkspaceButton_Click
```

目的：

```text
防止视觉重构误删功能入口。
```

### 11.3 样式资源测试

新增或修改测试：

```text
VSLoader.Tests/ModernThemeProductVisualTests.cs
```

断言：

```text
ModernQuietButtonStyle 存在。
ModernDataGridStyle 存在。
ModernUpdateSoftwareButtonStyle 保留 HasSoftwareUpdateNotice 绑定。
ModernDangerButtonStyle 保留。
```

### 11.4 表格样式静态测试

读取 `MainWindow.xaml`：

```text
ShortcutsGrid RowHeight 在 38-40 范围内。
ColumnHeaderHeight 为 36 或 38。
CanUserResizeColumns="True" 仍保留。
ItemsSource="{Binding ShortcutsView}" 仍保留。
```

静态测试不需要启动 WPF 窗口。

## 12. 验证命令

### 12.1 定向测试

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~MainWindowProductVisualTests|FullyQualifiedName~ModernThemeProductVisualTests"
```

### 12.2 主界面相关既有测试

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~MainWindowBannerTests|FullyQualifiedName~MainWindowColumnOrderTests|FullyQualifiedName~MainWindowSearchBoxTests"
```

### 12.3 Debug 构建

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore
```

如果 Debug 输出目录被正在运行的 VSLoader 占用，先关闭程序后重试。

## 13. 手动验收清单

实现后手动检查：

```text
1. 主界面第一眼主次更清楚。
2. 新增仍然是最明显的主操作。
3. 更新软件只有在有软件更新时绿色。
4. 删除保持危险语义但不刺眼。
5. 低频工具不再和主操作抢视觉权重。
6. 搜索框、数量、设置位置清晰。
7. 表格更像成熟数据表，行高不臃肿。
8. 选中行清楚但不刺眼。
9. 横幅提醒仍然明显但不廉价。
10. 所有原有按钮功能仍可点击。
```

## 14. 不做的事情

本次不做：

```text
1. 不隐藏任何功能按钮到菜单。
2. 不新增“更多”下拉菜单。
3. 不改业务逻辑。
4. 不改数据模型。
5. 不改地图窗口。
6. 不改设置窗口。
7. 不改工作区窗口。
8. 不引入第三方 UI 框架。
9. 不做动画系统。
10. 不做暗色模式。
```

原因：

```text
本次目标是主界面视觉系统收口，不扩大范围。
```

## 15. 预期效果

完成后主界面应从：

```text
功能平铺的内部工具
```

提升为：

```text
主次明确、颜色统一、密度适中、长期使用不累的成熟生产工具界面。
```

用户视觉感受应接近：

```text
稳定的企业级桌面软件
清爽的数据管理工具
克制但不单调的生产控制台
```

最终要求：

```text
看起来更成熟，但不能牺牲 VSLoader 当前高效直接的使用方式。
```
