# v47 工厂地图附属窗口与 JSON 流程布局编码规格说明

## 1. 需求背景

当前 VSLoader 主界面以列表形式展示快捷项。用户希望增加一个“地图”视图，把工厂里的机器按照车间工序走线显示成流程图。

这个流程图不是简单横向线性排序，而是要支持：

```text
分支
折线
并线
设备类型节点
真实机器节点
点击地图节点联动左侧快捷项
```

因此不能只用 CSV 顺序表。需要把地图布局从代码中解耦出来，使用可配置文件定义节点位置和连线。

## 2. 需求目标

在主界面第二行按钮区新增：

```text
地图
```

点击后，在主窗口右侧打开一个附属地图窗口。

地图窗口中：

1. 按 JSON 配置绘制设备类型节点。
2. 按 JSON 配置绘制节点之间的连线。
3. 连线支持折线。
4. 当前主列表中的快捷项会自动匹配到对应设备类型节点下。
5. 每台真实机器显示为一个可点击方框按钮。
6. 点击地图中的机器方框后，左侧主窗口对应快捷项被选中并高亮。

## 3. 非目标范围

第一版不实现：

1. 不做拖拽式地图编辑器。
2. 不在软件内编辑 JSON。
3. 不支持缩放和平移的复杂画布操作。
4. 不做真实 CAD 级车间平面图。
5. 不自动识别车间物理坐标。
6. 不自动推断流程连线。
7. 不新增数据库。
8. 不改变现有快捷项数据结构。

## 4. 地图窗口行为

地图窗口是主窗口的附属窗口，不是普通一次性弹窗。

### 4.1 打开

用户点击主界面第二行：

```text
地图
```

如果地图窗口当前未打开：

```text
创建并显示地图窗口
```

显示位置：

```text
贴在主窗口右侧
```

### 4.2 关闭

用户再次点击：

```text
地图
```

如果地图窗口当前已打开：

```text
关闭地图窗口
```

### 4.3 不受其他操作影响

以下操作不应关闭地图窗口：

```text
搜索
新增
编辑
删除
打开
AdminUI
WebUI
右键菜单
自动获取连接
```

### 4.4 跟随主窗口隐藏

如果主窗口：

```text
最小化
隐藏到托盘
关闭到托盘
```

地图窗口应同步隐藏。

### 4.5 跟随主窗口恢复

如果地图窗口在隐藏前处于打开状态，主窗口恢复显示后，地图窗口也应恢复显示并重新贴到主窗口右侧。

### 4.6 跟随主窗口移动

主窗口移动时，地图窗口应保持贴在主窗口右侧。

推荐监听：

```text
LocationChanged
SizeChanged
StateChanged
```

## 5. 地图配置文件

新增 JSON 配置文件：

```text
VSLoader/Config/factory-map.example.json
```

运行时推荐默认读取：

```text
%AppData%\VSLoader\factory-map.json
```

如果用户目录没有该文件，可以提示用户从安装目录 `Config\factory-map.example.json` 复制一份。

第一版也可以直接读取安装目录示例文件，后续再加入用户配置复制逻辑。

推荐优先级：

1. `%AppData%\VSLoader\factory-map.json`
2. 程序目录 `Config\factory-map.example.json`

## 6. JSON 结构设计

第一版 JSON 只配置“设备类型节点”和“连线”，不配置每一台具体机器。

示例：

```json
{
  "canvas": {
    "width": 1200,
    "height": 720
  },
  "nodes": [
    {
      "key": "印刷机",
      "label": "印刷机",
      "x": 80,
      "y": 120
    },
    {
      "key": "SPI",
      "label": "SPI",
      "x": 260,
      "y": 120
    },
    {
      "key": "AMX银烧结",
      "label": "AMX银烧结",
      "x": 440,
      "y": 120
    },
    {
      "key": "矩子3D-AOI",
      "label": "矩子3D-AOI",
      "x": 640,
      "y": 60
    },
    {
      "key": "3D-X-RAY",
      "label": "3D-X-RAY",
      "x": 640,
      "y": 200
    }
  ],
  "edges": [
    {
      "from": "印刷机",
      "to": "SPI"
    },
    {
      "from": "SPI",
      "to": "AMX银烧结"
    },
    {
      "from": "AMX银烧结",
      "to": "矩子3D-AOI",
      "points": [
        { "x": 540, "y": 120 },
        { "x": 540, "y": 60 }
      ]
    },
    {
      "from": "AMX银烧结",
      "to": "3D-X-RAY",
      "points": [
        { "x": 540, "y": 120 },
        { "x": 540, "y": 200 }
      ]
    }
  ]
}
```

## 7. JSON 字段说明

### 7.1 canvas

```json
{
  "width": 1200,
  "height": 720
}
```

含义：

```text
地图画布逻辑宽高
```

窗口可通过 ScrollViewer 查看超出区域。

### 7.2 nodes

字段：

| 字段 | 必填 | 含义 |
| --- | --- | --- |
| key | 是 | 设备类型匹配键 |
| label | 是 | 地图上显示的设备类型标题 |
| x | 是 | 节点左上角 X 坐标 |
| y | 是 | 节点左上角 Y 坐标 |

### 7.3 edges

字段：

| 字段 | 必填 | 含义 |
| --- | --- | --- |
| from | 是 | 起点节点 key |
| to | 是 | 终点节点 key |
| points | 否 | 折线中间点 |

如果没有 `points`：

```text
from -> to 画直线
```

如果有 `points`：

```text
from -> point1 -> point2 -> to 画折线
```

## 8. 快捷项与地图节点匹配规则

第一版采用最简单稳定的匹配规则：

```text
快捷项名称去掉最后的 _No 后，得到设备类型名。
设备类型名与 JSON node.key 精确匹配。
```

示例：

| 快捷项名称 | 设备类型名 | 匹配节点 key |
| --- | --- | --- |
| 矩子3D-AOI_007 | 矩子3D-AOI | 矩子3D-AOI |
| AMX银烧结_001 | AMX银烧结 | AMX银烧结 |
| SPI_003 | SPI | SPI |

提取规则：

```regex
^(?<Type>.+)_(?<No>\d+)$
```

如果快捷项名称不符合：

```text
不显示在地图节点中
```

如果类型名没有匹配任何 JSON 节点：

```text
可放入“未配置”区域，或第一版直接不显示。
```

推荐第一版：

```text
不显示，并在地图窗口底部状态栏显示未匹配数量。
```

## 9. 地图节点显示规则

一个 JSON node 表示一个设备类型区域。

设备类型区域显示：

```text
设备类型标题
该类型下的真实机器按钮列表
```

例如：

```text
┌──────────────┐
│ 矩子3D-AOI   │
│ ┌──────────┐ │
│ │ 007      │ │
│ └──────────┘ │
│ ┌──────────┐ │
│ │ 008      │ │
│ └──────────┘ │
└──────────────┘
```

机器按钮显示文本推荐：

```text
No
```

Tooltip 显示完整快捷项名称：

```text
矩子3D-AOI_007
```

如果只有一台，也可以显示完整名称。

## 10. 点击联动规则

点击地图中的机器按钮：

1. 找到对应的 `ShortcutItem`。
2. 设置主窗口 `SelectedShortcut`。
3. 主列表高亮该快捷项。
4. 主列表滚动到该快捷项位置。

需要在主窗口提供方法：

```csharp
public void SelectShortcutFromMap(ShortcutItem shortcut)
```

或通过 ViewModel 命令/事件实现。

推荐第一版：

```text
MapWindow 构造时接收 MainViewModel 和一个滚动回调。
点击节点后调用主窗口方法完成选择和滚动。
```

## 11. UI 实现建议

地图窗口：

```text
VSLoader/Views/FactoryMapWindow.xaml
VSLoader/Views/FactoryMapWindow.xaml.cs
```

地图 ViewModel：

```text
VSLoader/ViewModels/FactoryMapViewModel.cs
```

地图模型：

```text
VSLoader/Models/FactoryMapConfig.cs
VSLoader/Models/FactoryMapNode.cs
VSLoader/Models/FactoryMapEdge.cs
VSLoader/Models/FactoryMapPoint.cs
VSLoader/Models/FactoryMapMachineNode.cs
```

服务：

```text
VSLoader/Models/Services/FactoryMapService.cs
```

绘制方式：

```text
WPF Canvas + Border/Button + Polyline
```

窗口内容结构：

```xml
<ScrollViewer>
    <Canvas>
        <!-- 先画连线 -->
        <!-- 再画设备类型节点和机器按钮 -->
    </Canvas>
</ScrollViewer>
```

## 12. 地图按钮位置

在主窗口第二行按钮区新增按钮：

```text
地图
```

建议放在：

```text
自动获取连接 后面，AdminUI 前面
```

即：

```text
新增 | 批量新增识别 | 自动获取连接 | 地图 | AdminUI | 打开 | 编辑 | 删除
```

## 13. 地图窗口尺寸与位置

默认尺寸：

```text
Width = 620
Height = 主窗口当前高度
```

默认位置：

```text
Left = 主窗口.Left + 主窗口.Width
Top = 主窗口.Top
```

如果右侧屏幕空间不足：

```text
可以仍然贴右侧，允许部分超出；第一版不做复杂屏幕边界适配。
```

后续可优化为自动贴左侧。

## 14. 主窗口生命周期联动

需要在 `MainWindow.xaml.cs` 中维护：

```csharp
private FactoryMapWindow? _factoryMapWindow;
private bool _isFactoryMapOpen;
```

地图按钮逻辑：

```text
如果 _factoryMapWindow == null 或未显示：
    创建并显示
    _isFactoryMapOpen = true
否则：
    关闭
    _isFactoryMapOpen = false
```

主窗口隐藏/最小化：

```text
如果地图窗口存在，Hide()
```

主窗口恢复：

```text
如果 _isFactoryMapOpen == true，Show()
```

主窗口关闭退出：

```text
Dispose/Close 地图窗口
```

## 15. 数据刷新规则

地图窗口打开后，需要跟随主列表快捷项变化。

触发刷新场景：

```text
新增快捷项
编辑快捷项
删除快捷项
批量新增识别
批量清理重复项
搜索不影响地图数据
```

第一版可简单处理：

```text
地图窗口每次显示时重新根据 Shortcuts 生成节点。
主列表发生新增/编辑/删除/批量导入后，如果地图窗口已打开，则调用 RefreshMap()
```

## 16. 错误处理

### 16.1 配置文件不存在

显示轻量提示：

```text
未找到工厂地图配置文件，请检查 Config\factory-map.example.json。
```

地图窗口显示空状态。

### 16.2 JSON 格式错误

显示：

```text
工厂地图配置读取失败：{错误信息}
```

### 16.3 edge 引用不存在节点

忽略该连线，并在状态栏显示：

```text
存在无效连线配置。
```

## 17. 示例配置文件

新增：

```text
VSLoader/Config/factory-map.example.json
```

初始内容可包含一条示例主流程和分支：

```text
印刷机 -> SPI -> AMX银烧结
AMX银烧结 -> 矩子3D-AOI
AMX银烧结 -> 3D-X-RAY
```

后续用户可以按车间实际流程修改坐标和连线。

## 18. 测试要求

建议新增测试：

```text
VSLoader.Tests/FactoryMapServiceTests.cs
```

覆盖：

1. 可以读取合法 JSON。
2. JSON node/edge 数量正确。
3. 快捷项 `矩子3D-AOI_007` 能匹配到 `矩子3D-AOI` 节点。
4. 快捷项 `InvalidName` 不匹配任何节点。
5. edge 引用不存在节点时不抛异常。

UI 手工验收：

1. 点击地图按钮，右侧出现地图窗口。
2. 再次点击地图按钮，地图窗口关闭。
3. 地图窗口打开时，点击主窗口其他按钮不会关闭地图。
4. 主窗口最小化时地图窗口隐藏。
5. 主窗口恢复时地图窗口恢复。
6. 点击地图机器按钮，左侧列表对应快捷项高亮。

## 19. 预期改动文件

预计新增：

- `VSLoader/Views/FactoryMapWindow.xaml`
- `VSLoader/Views/FactoryMapWindow.xaml.cs`
- `VSLoader/ViewModels/FactoryMapViewModel.cs`
- `VSLoader/Models/FactoryMapConfig.cs`
- `VSLoader/Models/FactoryMapNode.cs`
- `VSLoader/Models/FactoryMapEdge.cs`
- `VSLoader/Models/FactoryMapPoint.cs`
- `VSLoader/Models/Services/FactoryMapService.cs`
- `VSLoader/Config/factory-map.example.json`
- `VSLoader.Tests/FactoryMapServiceTests.cs`

预计修改：

- `VSLoader/MainWindow.xaml`
- `VSLoader/MainWindow.xaml.cs`
- `VSLoader/ViewModels/MainViewModel.cs`
- `VSLoader/VSLoader.csproj`

## 20. 构建与测试验证

实现完成后执行：

```powershell
cd C:\Users\shee_\OneDrive\Desktop\VSLoader
dotnet test .\VSLoader.sln -p:UseSharedCompilation=false
dotnet build .\VSLoader.sln -p:UseSharedCompilation=false
```

如果 `VSLoader.exe` 被占用，先执行：

```powershell
Get-Process -Name VSLoader -ErrorAction SilentlyContinue | Stop-Process -Force
```

最终要求：

- 测试全部通过。
- 构建 `0 个错误`。
- 不引入新的无关警告。

## 21. 第一版默认约定

当前仍有一些细节未来可以调整。第一版先采用以下约定：

1. 地图布局用 JSON，不用 CSV。
2. JSON 配置设备类型节点，不配置每台机器。
3. 真实机器从当前快捷项自动匹配。
4. 快捷项名称按 `{类型}_{No}` 拆分。
5. 地图窗口是独立 Window，贴在主窗口右侧。
6. 绘图用 WPF Canvas。
7. 折线通过 edge.points 配置。
8. 第一版不做拖拽编辑器。

## 22. 最终效果

用户点击：

```text
地图
```

右侧出现一张基于 JSON 配置的工厂流程图。

地图上每个设备类型节点会自动挂载当前快捷项中的真实机器按钮。

用户点击地图中的：

```text
矩子3D-AOI_007
```

左侧主列表中对应快捷项会被选中并高亮。

这样 VSLoader 同时具备：

```text
列表视图
工厂地图流程视图
```

用户既能按文字搜索，也能按车间工序走线查找机器。
