# v159 地图分支连接点 Junction 与受约束移动编码规格说明

## 1. 背景

当前工厂地图已经完成 Version 4 点线拓扑和浏览/编辑双模式改造，底层关系为：

```text
连接点 ↔ 独立线段 ↔ 连接点
```

当前连接点类型包括：

```text
Attached：节点附属连接点。
Free：普通独立连接点。
Bend：正交线路折弯点。
```

当前在线段中间点击或执行“插入普通连接点”时，`FactoryMapTopologyService.SplitSegmentAt` 会：

```text
1. 把点击位置投影到线段。
2. 创建一个 Kind = Free 的连接点。
3. 删除原线段。
4. 创建“原起点 → Free”和“Free → 原终点”两条线段。
```

但 `FactoryMapMovementService.MovePoint` 对除 Attached 之外的连接点都允许 X/Y 二维自由移动。

当用户把这个在线段中生成的 Free 点拖离原线段时，相邻线段变成斜线，`RepairOrthogonality` 为了恢复正交关系会自动增加 Bend 点和额外线段，最终产生用户难以预测的弯折。

问题本质是：

```text
在线段上的分支/汇流点
空白区域中的普通独立连接点
控制线路形状的折弯点
```

三种语义没有完全分开。

本次需要参考 UML、流程图和网络拓扑编辑器的成熟交互，将“线段上的分支连接点”定义为独立的 `Junction` 类型，并为不同点类型建立不同移动约束。

## 2. 核心结论

本次必须遵循：

```text
Free 可以二维自由移动。
Junction 只能沿原主干线方向移动。
Bend 只负责线路形状。
Attached 只跟随所属节点。
```

用户普通单击线段只选择线段，不创建任何连接点。

在线段上建立分支时，程序只在用户完成连接后，原子创建 Junction 并拆分线段。

用户取消连接时，不得在线段上留下孤立点或被拆分的线段。

## 3. 目标

本次必须实现：

```text
1. 新增 Junction 分支连接点类型。
2. 线段拆分产生 Junction，不再产生可二维自由移动的 Free。
3. Junction 保存原主干方向 Horizontal / Vertical / Locked。
4. Junction 只能沿主干方向移动。
5. Junction 垂直于主干方向的拖动不得生成随机折线。
6. 普通点击线段只选择或拖动线段，不创建点。
7. 删除“插入普通连接点”菜单入口。
8. “从此处建立分支”使用连接草稿，完成前不修改地图。
9. 点连接线、线连接点、线连接线均使用原子事务。
10. 分支删除后自动清理不再需要的 Junction。
11. 空白右键新增的 Free 点继续允许自由移动。
12. 旧 Version 4 地图可以安全迁移到新格式。
```

## 4. 非目标

本次不做：

```text
1. 不实现斜线。
2. 不实现全图自动避障布线。
3. 不因为视觉交叉自动创建 Junction。
4. 不因为线段视觉重叠合并底层拓扑关系。
5. 不实现完整撤销/重做栈。
6. 不修改浏览/编辑双模式结构。
7. 不修改地图窗口快捷键和窗口状态恢复。
8. 不修改主窗口、AdminUI、更新器或工作区逻辑。
9. 不允许 Junction 脱离主干变成自由二维点。
10. 不删除空白区域创建 Free 点的入口。
```

## 5. 成熟图形编辑器对象语义

### 5.1 Attached

```text
属于节点上、右、下、左四个固定连接点。
位置由节点尺寸和方向派生。
不能脱离节点移动。
移动 Attached 等价于移动所属节点。
```

### 5.2 Free

```text
由用户在地图空白区域显式创建。
可以作为独立中转、起点或终点。
允许 X/Y 二维自由移动。
可以连接多条线段。
不会因为度数变化被自动删除或转换。
```

### 5.3 Junction

```text
在线段主干上创建的分支或汇流连接点。
必须位于一条水平或垂直主干上。
保存创建时的主干方向。
可以连接多条分支线段。
只能沿主干方向滑动。
不能通过普通拖动脱离主干。
```

### 5.4 Bend

```text
只控制正交路径形状。
默认只在线段或路径被选中时显示。
不作为普通连接起点或终点。
不加入矩形框选。
必要时可以显式转换为 Free。
```

### 5.5 Segment

```text
连接两个连接点的水平或垂直线段。
普通点击只选择。
拖动表示移动整个线路通道。
线段不能直接连接线段，必须通过 Junction 表达真实连接。
```

## 6. 数据模型

### 6.1 Junction 类型

修改：

```text
VSLoader\Models\FactoryMapConnectionPointKinds.cs
```

新增：

```csharp
public const string Junction = "junction";
```

`Normalize` 必须识别：

```text
attached
free
bend
junction
```

未知值继续保守归一化为 `Free`，避免旧文件因为未知类型崩溃。

### 6.2 Junction 主干方向

新增：

```text
VSLoader\Models\FactoryMapJunctionAxes.cs
```

建议定义：

```csharp
public static class FactoryMapJunctionAxes
{
    public const string Horizontal = "horizontal";
    public const string Vertical = "vertical";
    public const string Locked = "locked";
}
```

修改：

```text
VSLoader\Models\FactoryMapConnectionPoint.cs
```

新增字段：

```csharp
public string JunctionAxis { get; set; } = string.Empty;
```

规则：

```text
Attached / Free / Bend：JunctionAxis 为空。
水平线段生成 Junction：JunctionAxis = horizontal。
垂直线段生成 Junction：JunctionAxis = vertical。
旧数据无法推断唯一主干：JunctionAxis = locked。
```

### 6.3 布局格式 Version 5

当前布局版本为 Version 4。

本次将地图布局升级为：

```text
Version 5
```

原因：

```text
新增 Junction 类型。
新增 JunctionAxis 持久化语义。
Free 与 Junction 的移动和清理规则不同。
```

Version 5 示例：

```json
{
  "Version": 5,
  "ConnectionPoints": [
    {
      "Id": "junction-001",
      "Kind": "junction",
      "JunctionAxis": "horizontal",
      "X": 420,
      "Y": 260
    }
  ]
}
```

## 7. 连接草稿模型

### 7.1 当前不足

当前 `FactoryMapInteractionState` 只保存：

```text
PendingConnectionPointId
```

这只能表达“从已有点开始连接”，不能表达“从一条尚未拆分的线段开始连接”。

当前从线段开始连接时会立即调用 `SplitSegmentAt` 并保存，用户后续取消仍可能留下一个没有实际分支意义的 Free 点。

### 7.2 新连接草稿

新增：

```text
VSLoader\Models\FactoryMapConnectionDraft.cs
VSLoader\Models\FactoryMapConnectionOriginKinds.cs
```

建议模型：

```csharp
public sealed class FactoryMapConnectionDraft
{
    public string OriginKind { get; init; } = string.Empty;
    public string PointId { get; init; } = string.Empty;
    public string SegmentId { get; init; } = string.Empty;
    public double SegmentX { get; init; }
    public double SegmentY { get; init; }
}
```

起点类型：

```csharp
public static class FactoryMapConnectionOriginKinds
{
    public const string Point = "point";
    public const string Segment = "segment";
}
```

`FactoryMapInteractionState` 将 `PendingConnectionPointId` 收束为：

```text
ConnectionDraft
```

兼容 helper 可以提供：

```text
BeginPointConnectionDraft
BeginSegmentConnectionDraft
CancelConnectionDraft
HasConnectionDraft
```

连接草稿只存在于内存 Session，不写入布局文件。

## 8. 线段点击和右键菜单

### 8.1 普通左键

普通左键点击线段：

```text
只选择线段。
不创建 Free。
不创建 Junction。
不拆分线段。
```

拖动超过阈值：

```text
移动线路通道。
```

### 8.2 右键菜单

当前菜单：

```text
从此处开始连接
插入普通连接点
断开/删除线段
```

改为：

```text
从此处建立分支
断开/删除线段
```

必须删除：

```text
插入普通连接点
```

“从此处建立分支”只创建 `FactoryMapConnectionDraft`：

```text
OriginKind = segment
SegmentId = 当前逻辑线段 ID
SegmentX/Y = 点击位置投影并网格吸附后的坐标
```

此时不得：

```text
拆分线段。
创建 Junction。
写入地图文件。
```

状态提示：

```text
请选择分支连接终点
```

## 9. 完成连接规则

### 9.1 点连接点

沿用当前逻辑：

```text
已有连接点 A → 已有连接点 B
```

通过 `FactoryMapTopologyService.ConnectPoints` 创建正交段链。

### 9.2 点连接线

```text
起点：Attached / Free / Junction
终点：Segment
```

事务步骤：

```text
1. 捕获拓扑快照。
2. 在目标线段投影位置创建 Junction。
3. 根据 JunctionAxis 拆分目标线段。
4. 连接起点到 Junction。
5. 校验拓扑。
6. 一次保存。
7. 成功后清空连接草稿。
```

### 9.3 线连接点

```text
起点：Segment 草稿
终点：Attached / Free / Junction
```

事务步骤：

```text
1. 捕获拓扑快照。
2. 在草稿线段位置创建 Junction。
3. 拆分草稿线段。
4. 连接 Junction 到目标点。
5. 校验并一次保存。
```

### 9.4 线连接线

```text
起点：Segment 草稿
终点：另一个 Segment
```

事务步骤：

```text
1. 在起点线段创建 Junction A。
2. 在终点线段创建 Junction B。
3. 分别拆分两条线段。
4. 连接 Junction A 与 Junction B。
5. 一次校验和保存。
```

第一阶段禁止：

```text
同一逻辑线段上的两个位置互相连接。
```

原因是容易生成回环、零长度或重叠关系，需要独立设计后再开放。

### 9.5 取消连接

以下操作只清空连接草稿：

```text
按 Escape。
单击空白区域。
切换浏览模式。
导入或重新加载地图。
地图窗口失焦。
关闭地图窗口。
```

因为草稿阶段没有修改地图，所以取消后：

```text
不需要回滚文件。
不留下 Junction。
不拆分任何线段。
```

## 10. 拓扑服务接口

### 10.1 创建 Junction 并拆分

建议新增：

```csharp
FactoryMapTopologyOperationResult SplitSegmentWithJunctionAt(
    FactoryMapDeviceViewData map,
    string segmentId,
    double x,
    double y,
    double gridSize,
    double endpointThreshold)
```

行为：

```text
水平线段 → JunctionAxis.Horizontal。
垂直线段 → JunctionAxis.Vertical。
距离端点小于阈值 → 复用端点，不创建 Junction。
斜线或无效线段 → 拒绝。
```

现有 `SplitSegmentAt` 不得继续默认创建 Free。

实现要求：

```text
删除普通编辑路径对 SplitSegmentAt 的调用。
SplitSegmentAt 只保留给旧数据迁移兼容，或在确认没有调用者后删除。
新的普通编辑路径必须显式调用 SplitSegmentWithJunctionAt。
```

### 10.2 原子完成草稿

建议新增服务：

```text
VSLoader\Models\Services\FactoryMapConnectionDraftService.cs
```

职责：

```text
完成点到点。
完成点到线。
完成线到点。
完成线到线。
一次快照、一次校验、一次保存前结果。
失败时恢复快照。
```

窗口只负责：

```text
收集鼠标目标。
显示连接草稿视觉。
调用服务。
显示成功或失败信息。
```

禁止在 `FactoryMapWindow.xaml.cs` 中直接组合多次集合修改。

## 11. Junction 移动规则

### 11.1 水平 Junction

```text
JunctionAxis = horizontal
```

允许：

```text
左右移动。
方向键 Left / Right。
Shift + Left / Right 精细移动。
Ctrl + Left / Right 快速移动。
```

禁止：

```text
上下自由移动。
方向键 Up / Down。
```

鼠标拖动时只取 `deltaX`，忽略 `deltaY`。

光标使用：

```text
SizeWE
```

### 11.2 垂直 Junction

```text
JunctionAxis = vertical
```

允许：

```text
上下移动。
方向键 Up / Down。
```

禁止左右自由移动。

鼠标拖动时只取 `deltaY`，光标使用：

```text
SizeNS
```

### 11.3 Locked Junction

```text
JunctionAxis = locked
```

不允许直接移动。

状态提示：

```text
该分支连接点的主干方向不唯一，请移动相邻线段调整布局。
```

### 11.4 分支局部重路由

Junction 沿主干移动时：

```text
主干两侧线段继续保持共线。
与 Junction 相连的支线路径执行局部正交重路由。
优先调整支线相邻 Bend，不重复制造新 Bend。
只有不存在可调整 Bend 时才创建一个必要 Bend。
```

不得对整张地图重新布线。

## 12. 主干通道移动

用户需要垂直于 Junction 主干方向调整布局时，应拖动主干线段，而不是拖动 Junction。

### 12.1 通道识别

拖动某条线段时，识别与其：

```text
同轴
共线
通过 Bend 或同轴 Junction 连续连接
```

的最大主干通道。

不得沿垂直支线继续扩散。

### 12.2 通道移动

水平通道上下移动：

```text
移动通道内 Bend 和 Junction 的 Y。
保持 JunctionAxis = horizontal。
在通道边界调整或创建必要 Bend。
```

垂直通道左右移动规则相同。

操作必须：

```text
一次快照。
一次局部正交归一化。
一次保存。
失败整体回滚。
```

## 13. Junction 自动清理

每次断开、删除线段或删除分支后执行 Junction 归一化。

### 13.1 度数大于等于 3

```text
保留 Junction。
```

### 13.2 度数等于 2且两条线段共线

```text
删除 Junction。
合并两条线段。
保留较高 ZIndex。
```

### 13.3 度数等于 2且形成直角

```text
将 Junction 转换为 Bend。
清空 JunctionAxis。
```

### 13.4 度数等于 1

```text
将 Junction 转换为 Free。
清空 JunctionAxis。
```

这样可以保留合法悬空端点，不静默删除用户剩余连接。

### 13.5 度数等于 0

```text
删除 Junction。
```

这些规则不得应用到用户显式创建的 Free 点。

## 14. Junction 右键菜单

建议菜单：

```text
开始连接
断开全部连接
转换为普通连接点
删除分支连接点
```

### 14.1 转换为普通连接点

这是高级操作，用于用户明确希望二维自由移动的情况。

执行前确认：

```text
转换后该连接点可以自由移动，并可能改变线路形状，是否继续？
```

转换后：

```text
Kind = Free
JunctionAxis = 空
```

不得自动转换。

## 15. 视觉规范

### 15.1 区分 Free 与 Junction

统一视觉：

```text
Free：圆形连接点。
Junction：小型菱形连接点。
Bend：选中线路时显示的小型浅色控制点。
Attached：节点边缘小圆点。
```

Junction 建议尺寸：

```text
8px 到 10px。
```

不得比节点端口明显大一圈，也不得小到无法命中。

### 15.2 移动提示

Junction hover 时通过光标表达约束：

```text
Horizontal → SizeWE
Vertical → SizeNS
Locked → No
```

拖动过程中可以显示一条轻量主干方向参考线，但不得使用虚线框装饰。

### 15.3 连接草稿

从线段建立分支时：

```text
在线段投影位置显示临时 Junction 预览。
预览不加入 currentMap.ConnectionPoints。
预览不参与保存。
取消时立即消失。
```

## 16. Version 4 到 Version 5 迁移

修改：

```text
VSLoader\Models\Services\FactoryMapLayoutService.cs
VSLoader\Models\Services\FactoryMapLayoutTopologyConverter.cs
```

迁移前必须保留原文件备份。

### 16.1 Attached 和 Bend

```text
保持原类型。
JunctionAxis 为空。
```

### 16.2 明确分叉 Free

满足以下条件：

```text
degree >= 3
存在唯一一对反向共线线段
```

迁移为：

```text
Kind = Junction
JunctionAxis = 共线方向
```

### 16.3 共线 Free

满足：

```text
degree == 2
两条线段共线
Free 位于两个邻点之间
```

迁移时保守转换为：

```text
Kind = Junction
JunctionAxis = 共线方向
保留原 ID、坐标和两条线段。
```

原因：

```text
Version 4 无法可靠判断该点是用户显式创建，还是旧版在线段中插入。
迁移阶段不得直接删除用户对象。
后续只有在断开、删除等拓扑操作明确影响该 Junction 时，才根据自动清理规则合并。
```

### 16.4 模糊 Free

无法可靠判断语义时：

```text
继续保留为 Free。
不得擅自转换或删除。
记录迁移警告数量。
```

### 16.5 十字或多主干 Free

如果 degree >= 4 且同时存在水平、垂直反向共线对：

```text
Kind = Junction
JunctionAxis = locked
```

### 16.6 迁移失败

```text
不得覆盖 Version 4 原文件。
显示可追溯错误信息。
地图窗口不得崩溃。
```

## 17. 导入导出和全局配置

必须保证：

```text
地图单独导入/导出支持 Version 5。
导出全局配置包含 Version 5 地图文件。
导入全局配置可以恢复 Junction 和 JunctionAxis。
旧全局配置中的 Version 4 地图可以自动迁移。
更新检测和其他全局配置字段不受影响。
```

## 18. 保存与回滚

以下操作必须使用拓扑快照：

```text
完成点到线连接。
完成线到点连接。
完成线到线连接。
移动 Junction。
移动包含 Junction 的主干通道。
Junction 自动清理。
Junction 转换为 Free。
```

要求：

```text
1. 草稿阶段不修改持久化模型。
2. 完成连接时一次性修改全部集合。
3. 修改后先 ValidateTopology。
4. 保存失败恢复快照。
5. 回滚后清空连接草稿。
6. 不留下半拆分线段、孤立 Junction 或失效引用。
```

## 19. 性能约束

```text
1. Junction 拖动只重算相邻线段和支线路径。
2. 通道拖动只处理当前共线通道及边界段。
3. 不在 MouseMove 中写文件。
4. 鼠标释放后保存一次。
5. 键盘连续移动继续使用约 150ms 防抖。
6. 连接草稿预览不得全量重建地图 Canvas。
7. Junction 归一化只扫描受影响连接点邻域。
8. 不为每帧创建完整拓扑快照。
```

## 20. 建议修改文件

### 20.1 新增

```text
VSLoader\Models\FactoryMapJunctionAxes.cs
VSLoader\Models\FactoryMapConnectionDraft.cs
VSLoader\Models\FactoryMapConnectionOriginKinds.cs
VSLoader\Models\Services\FactoryMapConnectionDraftService.cs
VSLoader.Tests\FactoryMapConnectionDraftServiceTests.cs
VSLoader.Tests\FactoryMapLayoutVersion5MigrationTests.cs
```

### 20.2 修改

```text
VSLoader\Models\FactoryMapConnectionPoint.cs
VSLoader\Models\FactoryMapConnectionPointKinds.cs
VSLoader\Models\FactoryMapInteractionState.cs
VSLoader\Models\Services\FactoryMapTopologyService.cs
VSLoader\Models\Services\FactoryMapMovementService.cs
VSLoader\Models\Services\FactoryMapLayoutService.cs
VSLoader\Models\Services\FactoryMapLayoutTopologyConverter.cs
VSLoader\Views\FactoryMapWindow.xaml.cs
```

### 20.3 更新测试

```text
VSLoader.Tests\FactoryMapTopologyServiceTests.cs
VSLoader.Tests\FactoryMapMovementServiceTests.cs
VSLoader.Tests\FactoryMapWindowRuntimeLoadTests.cs
VSLoader.Tests\FactoryMapLayoutServiceTests.cs
VSLoader.Tests\GlobalConfigPackageServiceTests.cs
```

## 21. 测试要求

### 21.1 Junction 创建

```text
水平线段创建 horizontal Junction。
垂直线段创建 vertical Junction。
靠近端点时复用端点。
斜线拒绝创建 Junction。
普通点击线段不创建点。
右键建立分支只创建草稿。
取消草稿不修改地图。
```

### 21.2 连接草稿

```text
点到点连接成功。
点到线创建一个 Junction 并连接。
线到点创建一个 Junction 并连接。
线到线创建两个 Junction 并连接。
同一线段线到线被拒绝。
业务失败恢复快照并保留合理错误信息。
保存失败恢复快照并清空草稿。
```

### 21.3 Junction 移动

```text
horizontal Junction 只改变 X。
horizontal Junction 上下方向键不移动。
vertical Junction 只改变 Y。
vertical Junction 左右方向键不移动。
locked Junction 不移动。
沿主干移动后主干保持共线。
支线只产生必要的局部重路由。
不得因为垂直拖动生成随机 Bend 链。
```

### 21.4 通道移动

```text
水平主干通道可以上下移动。
垂直主干通道可以左右移动。
通道中的 Junction 随通道移动。
垂直支线不会被错误纳入水平通道。
通道移动失败整体回滚。
```

### 21.5 自动清理

```text
degree >= 3 Junction 保留。
degree 2 共线 Junction 被删除并合并线段。
degree 2 直角 Junction 转为 Bend。
degree 1 Junction 转为 Free。
degree 0 Junction 删除。
Free 不受 Junction 自动清理影响。
```

### 21.6 迁移

```text
Version 4 明确分叉 Free 迁移为 Junction。
Version 4 共线 Free 保留 ID 并迁移为受约束 Junction。
模糊 Free 保持 Free。
十字 Free 迁移为 locked Junction。
迁移失败不覆盖原文件。
Version 5 保存和加载后 JunctionAxis 一致。
```

### 21.7 UI 回归

```text
浏览/编辑双模式不变。
框选仍只包含节点和 Free，不包含 Junction。
Junction 可以单击选择，但不进入矩形多选。
线段菜单不再出现“插入普通连接点”。
空白右键仍可新增 Free。
滚轮缩放和编辑模式平移不变。
```

## 22. 验证命令

执行目录：

```text
C:\Users\shee_\OneDrive\Desktop\VSLoader
```

目标测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore --filter "FullyQualifiedName~FactoryMap"
```

全量测试：

```powershell
dotnet test .\VSLoader.Tests\VSLoader.Tests.csproj -c Debug --no-restore
```

Debug 构建：

```powershell
dotnet build .\VSLoader.sln -c Debug --no-restore
```

要求：

```text
测试全部通过。
构建 0 错误。
不得新增编译警告。
```

## 23. 人工验收

```text
1. 进入编辑模式，普通单击线段，确认不会新增连接点。
2. 右键线段，确认没有“插入普通连接点”。
3. 点击“从此处建立分支”，确认只显示临时 Junction 预览。
4. 按 Escape，确认地图没有新增点或拆分线段。
5. 再次建立分支并选择节点端口，确认成功创建 Junction。
6. 拖动水平 Junction，确认只能左右移动。
7. 尝试上下拖动水平 Junction，确认不会产生新折线。
8. 拖动垂直 Junction，确认只能上下移动。
9. 拖动主干线段，确认 Junction 随主干通道移动。
10. 删除分支线，确认冗余 Junction 自动合并或转换。
11. 在空白区域新增 Free，确认仍可二维自由移动。
12. 打开旧 Version 4 地图，确认迁移后布局和连接关系正确。
13. 导出并重新导入地图，确认 JunctionAxis 保持一致。
14. 导出并导入全局配置，确认 Version 5 地图正常恢复。
```

## 24. 验收标准

全部满足才算完成：

```text
1. Free、Junction、Bend、Attached 语义完全分离。
2. 线段上的分支点使用 Junction。
3. 普通点击线段不会创建点。
4. 取消分支连接不会留下孤立点。
5. Junction 只能沿主干方向移动。
6. Junction 不会因为任意二维拖动制造随机折线。
7. 主干通道可以作为整体调整。
8. 分支删除后 Junction 能正确归一化。
9. 空白创建的 Free 仍可自由移动。
10. Version 4 地图能安全迁移到 Version 5。
11. 地图导入导出和全局配置支持 Version 5。
12. 保存失败可以完整回滚。
13. FactoryMap 目标测试通过。
14. 全量测试通过。
15. Debug 构建 0 错误且无新增警告。
```

本次改造的核心原则：

```text
拓扑分支由 Junction 表达；自由位置由 Free 表达；线路形状由 Bend 和 Segment 表达。
```
