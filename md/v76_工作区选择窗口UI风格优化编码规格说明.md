# v76 工作区选择窗口UI风格优化编码规格说明

## 1. 文档目的

本文件用于指导编程 Agent 优化 VSLoader 的“选择工作区”窗口 UI。

本次只做视觉和布局优化：

```text
让选择工作区窗口的风格更接近当前主界面工具区和主列表区域。
```

不改变现有工作区业务逻辑。

## 2. 当前问题

当前文件：

```text
VSLoader\Views\WorkspaceSelectorWindow.xaml
```

存在以下 UI 问题：

```text
1. 底部按钮越来越多，全部挤在一行。
2. 按钮高度没有统一设置，视觉上比主界面按钮小。
3. “新建 / 重命名 / 删除 / 打开工作区文件夹 / 取消 / 打开”横向排列拥挤。
4. 工作区列表使用 ListBox 边框 + 每项卡片边框，视觉上有点重。
5. 窗口整体没有采用主界面淡灰背景和白色内容容器风格。
6. 列表项选中态不如主界面主列表清晰。
7. 字体、间距、按钮尺寸和主界面不够统一。
```

主界面参考文件：

```text
VSLoader\MainWindow.xaml
```

主界面已有的视觉特征：

```text
1. 外层背景：#F6F7F9
2. 主内容白色容器：Background="White"
3. 边框色：#DDE1E7
4. 圆角：CornerRadius="6"
5. 顶部工具按钮高度：36
6. 工具按钮横向间距：10
7. 主列表字体：Microsoft YaHei UI
8. 主列表选中态：浅蓝底 + 左侧蓝色指示条
```

## 3. 需求目标

完成后应达到：

```text
1. 选择工作区窗口整体更像 VSLoader 主界面的一部分。
2. 按钮不再拥挤。
3. 按钮尺寸统一，点击区域更舒服。
4. 工作区列表更清爽，不像卡片堆叠。
5. 选中工作区时有清晰但不夸张的选中态。
6. 保留所有现有功能：新建、重命名、删除、打开工作区文件夹、取消、打开、双击打开。
```

## 4. 非目标范围

本阶段不实现：

```text
1. 不修改 WorkspaceSelectorViewModel 的业务规则。
2. 不修改工作区新建逻辑。
3. 不修改工作区重命名逻辑。
4. 不修改工作区删除逻辑。
5. 不修改 App.xaml.cs 的工作区切换逻辑。
6. 不新增搜索工作区功能。
7. 不新增工作区排序功能。
8. 不新增工作区图标。
9. 不新增深色模式。
```

如果实现中发现必须改 code-behind，也只能做和样式相关的最小调整。

## 5. 目标文件

主要修改：

```text
VSLoader\Views\WorkspaceSelectorWindow.xaml
```

原则上不需要修改：

```text
VSLoader\ViewModels\WorkspaceSelectorViewModel.cs
VSLoader\Views\WorkspaceSelectorWindow.xaml.cs
VSLoader\Models\Services\WorkspaceService.cs
```

如果必须改 code-behind，必须说明原因，并保证不改变业务行为。

## 6. 窗口整体布局

### 6.1 窗口尺寸

当前：

```xml
Height="480"
Width="680"
MinHeight="420"
MinWidth="620"
```

建议调整为：

```xml
Height="540"
Width="760"
MinHeight="480"
MinWidth="700"
```

原因：

```text
当前窗口宽度对 6 个按钮过紧。稍微放大后，列表和底部工具区都有更舒服的呼吸感。
```

### 6.2 根容器背景和字体

根容器建议改为：

```xml
<Grid Background="#F6F7F9"
      Margin="16"
      TextElement.FontFamily="Microsoft YaHei UI">
```

对齐主界面：

```text
主窗口 DockPanel Background="#F6F7F9"
主窗口控件整体使用 Microsoft YaHei UI
```

## 7. 标题区优化

当前标题区可以保留，但调整间距，让其和主界面顶部工具区节奏接近。

建议：

```xml
<StackPanel Grid.Row="0"
            Margin="0,0,0,12">
    <TextBlock FontSize="22"
               FontWeight="SemiBold"
               Foreground="#111827"
               Text="VSLoader 工作区" />
    <TextBlock Margin="0,6,0,0"
               FontSize="13"
               Foreground="#6B7280"
               Text="请选择要打开的工作区" />
</StackPanel>
```

说明：

```text
标题继续保留 22 号 SemiBold。
说明文字增加 FontSize=13，和主界面的辅助信息一致。
```

## 8. 工作区列表容器优化

### 8.1 外层白色容器

当前 `ListBox` 自己带边框。  
建议改为：

```xml
<Border Grid.Row="1"
        Background="White"
        BorderBrush="#DDE1E7"
        BorderThickness="1"
        CornerRadius="6">
    <ListBox ... />
</Border>
```

ListBox 自身：

```xml
BorderThickness="0"
Background="Transparent"
Padding="0"
```

目的：

```text
列表区域和主界面 DataGrid 外层白色容器风格一致。
```

### 8.2 去掉每项厚重卡片感

当前每项：

```xml
<Border Margin="8"
        Padding="12"
        BorderBrush="#E5E7EB"
        BorderThickness="1"
        CornerRadius="6"
        Background="White">
```

建议改为更像主列表的行式布局：

```xml
<Border x:Name="ItemRoot"
        Margin="0"
        Padding="14,12"
        Background="White"
        BorderBrush="#EEF0F3"
        BorderThickness="0,0,0,1">
```

并增加左侧选中指示条：

```xml
<Grid>
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="3" />
        <ColumnDefinition Width="*" />
    </Grid.ColumnDefinitions>

    <Border x:Name="SelectedIndicator"
            Background="Transparent" />

    <Grid Grid.Column="1"
          Margin="12,0,0,0">
        ...
    </Grid>
</Grid>
```

## 9. 工作区列表项信息层级

建议每个工作区项保留三层信息：

```text
第一层：工作区名称 + 状态标签
第二层：路径
第三层：创建时间 / 更新时间
```

推荐样式：

```xml
<TextBlock FontSize="15"
           FontWeight="SemiBold"
           Foreground="#111827"
           Text="{Binding Name}"
           TextTrimming="CharacterEllipsis" />
```

路径：

```xml
<TextBlock Margin="0,7,0,0"
           FontSize="13"
           Foreground="#4B5563"
           Text="{Binding Path}"
           TextTrimming="CharacterEllipsis"
           ToolTip="{Binding Path}" />
```

时间：

```xml
<TextBlock Margin="0,6,0,0"
           FontSize="12"
           Foreground="#6B7280">
```

状态标签：

```xml
<Border Margin="10,0,0,0"
        Padding="8,2"
        CornerRadius="4"
        Background="#F3F4F6">
    <TextBlock FontSize="12"
               Foreground="#4B5563"
               Text="{Binding StatusText}" />
</Border>
```

说明：

```text
状态标签保留，但弱化为辅助信息，不要抢工作区名称的主视觉。
```

## 10. 列表选中态和悬停态

需要参考主界面主列表：

```text
悬停：#F6FAFF
选中：#EEF6FF
左侧指示条：#2563EB
```

在 `ListBoxItem` 样式中重写模板，避免默认蓝色系统选中态。

建议样式：

```xml
<ListBox.ItemContainerStyle>
    <Style TargetType="ListBoxItem">
        <Setter Property="HorizontalContentAlignment" Value="Stretch" />
        <Setter Property="Padding" Value="0" />
        <Setter Property="Margin" Value="0" />
        <Setter Property="Background" Value="Transparent" />
        <Setter Property="BorderThickness" Value="0" />
        <Setter Property="FocusVisualStyle" Value="{x:Null}" />
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="ListBoxItem">
                    <ContentPresenter />
                </ControlTemplate>
            </Setter.Value>
        </Setter>
    </Style>
</ListBox.ItemContainerStyle>
```

在 ItemTemplate 的 `DataTemplate.Triggers` 中处理：

```xml
<DataTemplate.Triggers>
    <DataTrigger Binding="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem}, Path=IsMouseOver}"
                 Value="True">
        <Setter TargetName="ItemRoot" Property="Background" Value="#F6FAFF" />
    </DataTrigger>
    <DataTrigger Binding="{Binding RelativeSource={RelativeSource AncestorType=ListBoxItem}, Path=IsSelected}"
                 Value="True">
        <Setter TargetName="ItemRoot" Property="Background" Value="#EEF6FF" />
        <Setter TargetName="SelectedIndicator" Property="Background" Value="#2563EB" />
    </DataTrigger>
</DataTemplate.Triggers>
```

## 11. 底部按钮区优化

### 11.1 分组布局

当前底部按钮用 `Grid` 横向硬排，按钮多时拥挤。

建议改为：

```xml
<Grid Grid.Row="2"
      Margin="0,14,0,0">
    <Grid.ColumnDefinitions>
        <ColumnDefinition Width="*" />
        <ColumnDefinition Width="Auto" />
    </Grid.ColumnDefinitions>

    <WrapPanel Grid.Column="0"
               Orientation="Horizontal">
        管理按钮...
    </WrapPanel>

    <StackPanel Grid.Column="1"
                Orientation="Horizontal"
                Margin="12,0,0,0">
        取消 / 打开...
    </StackPanel>
</Grid>
```

左侧管理按钮：

```text
新建工作区
重命名
删除
打开工作区文件夹
```

右侧主动作按钮：

```text
取消
打开
```

### 11.2 按钮尺寸

参考主界面按钮：

```text
Height = 36
Margin = 0,0,10,8
```

推荐尺寸：

```text
新建工作区：116 x 36
重命名：96 x 36
删除：96 x 36
打开工作区文件夹：144 x 36
取消：96 x 36
打开：96 x 36
```

示例：

```xml
<Button Width="116"
        Height="36"
        Margin="0,0,10,8"
        Command="{Binding StartCreateWorkspaceCommand}"
        Content="新建工作区" />
```

右侧按钮：

```xml
<Button Width="96"
        Height="36"
        Margin="0,0,10,8"
        Command="{Binding CancelCommand}"
        Content="取消" />
<Button Width="96"
        Height="36"
        Margin="0,0,0,8"
        Command="{Binding OpenSelectedWorkspaceCommand}"
        Content="打开" />
```

## 12. 响应式与不拥挤规则

必须满足：

```text
1. 窗口默认宽度下按钮不能重叠。
2. 窗口缩到 MinWidth 时，左侧管理按钮允许 WrapPanel 换行。
3. 右侧“取消 / 打开”按钮始终保持完整显示。
4. 工作区路径过长时使用省略号，不撑破布局。
5. 工作区名称过长时使用省略号，并保留 ToolTip。
```

## 13. 业务行为保持不变

实现后必须保持：

```text
1. 双击工作区仍然打开。
2. 新建工作区仍然弹出新建窗口。
3. 重命名仍然弹出重命名窗口。
4. 删除仍然先弹确认窗口。
5. 打开工作区文件夹仍然可用。
6. 取消仍然关闭选择窗口。
7. 打开仍然进入选中工作区。
8. 按钮启用/禁用状态仍由原有命令控制。
```

## 14. 测试要求

### 14.1 自动化测试

本次主要是 XAML 样式调整，现有 ViewModel 业务测试应全部通过。

必须运行：

```powershell
dotnet test C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

预期：

```text
所有现有测试通过。
```

### 14.2 构建验证

必须运行：

```powershell
dotnet build C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

预期：

```text
0 error。
```

### 14.3 手工验收

手工打开程序并检查：

```text
1. 启动后选择工作区窗口能正常显示。
2. 窗口整体背景是淡灰色。
3. 列表区域是白色容器。
4. 工作区项不再是厚重卡片堆叠。
5. 鼠标悬停工作区项有浅蓝反馈。
6. 选中工作区项有浅蓝底和左侧蓝色指示条。
7. 底部按钮高度和主界面按钮一致。
8. 底部按钮不拥挤。
9. 缩小窗口到最小宽度时按钮不重叠。
10. 新建、重命名、删除、打开文件夹、取消、打开功能仍正常。
```

## 15. 风险点

### 15.1 ListBox 默认选中态残留

风险：

```text
如果只改 DataTemplate，不重写 ListBoxItem 模板，系统默认蓝色选中态可能和自定义选中态叠加。
```

规避：

```text
ListBoxItem 设置 FocusVisualStyle="{x:Null}" 并重写 Template。
```

### 15.2 按钮区右侧动作被挤压

风险：

```text
如果继续使用单行 Grid，按钮数量多时仍然会拥挤。
```

规避：

```text
左侧管理按钮用 WrapPanel，右侧确认按钮独立 StackPanel。
```

### 15.3 改 UI 时误改业务绑定

风险：

```text
按钮 Command 绑定写错会导致功能失效。
```

规避：

```text
保留原有 Command 名称：
StartCreateWorkspaceCommand
StartRenameWorkspaceCommand
StartDeleteWorkspaceCommand
OpenWorkspaceFolderCommand
CancelCommand
OpenSelectedWorkspaceCommand
```

## 16. 验收标准

本需求完成必须满足：

```text
1. 工作区选择窗口视觉风格接近主界面。
2. 按钮高度为 36。
3. 底部按钮不拥挤。
4. 列表区域使用白色容器和淡灰边框。
5. 工作区项有清晰悬停态和选中态。
6. 工作区项长文本不会撑破布局。
7. 所有原有工作区操作仍可用。
8. dotnet build 通过。
9. dotnet test 通过。
```

## 17. 推荐执行命令

实现前停止运行中的程序：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

构建：

```powershell
dotnet build C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

测试：

```powershell
dotnet test C:\Users\shee_\OneDrive\Desktop\VSLoader\VSLoader.sln -p:UseSharedCompilation=false
```

## 18. 推荐提交信息

```text
style: refine workspace selector layout
```
