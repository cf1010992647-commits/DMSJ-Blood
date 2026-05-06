# Graph Report - D:\DMSJ项目\Blood Alcohol Porject\Blood Alcohol  (2026-05-05)

## Corpus Check
- Large corpus: 242 files · ~105,655 words. Semantic extraction will be expensive (many Claude tokens). Consider running on a subfolder, or use --no-semantic to run AST-only.

## Summary
- 789 nodes · 1726 edges · 37 communities (30 shown, 7 thin omitted)
- Extraction: 100% EXTRACTED · 0% INFERRED · 0% AMBIGUOUS · INFERRED: 4 edges (avg confidence: 0.8)
- Token cost: 0 input · 0 output

## Community Hubs (Navigation)
- [[_COMMUNITY_Community 0|Community 0]]
- [[_COMMUNITY_Community 1|Community 1]]
- [[_COMMUNITY_Community 2|Community 2]]
- [[_COMMUNITY_Community 3|Community 3]]
- [[_COMMUNITY_Community 4|Community 4]]
- [[_COMMUNITY_Community 5|Community 5]]
- [[_COMMUNITY_Community 6|Community 6]]
- [[_COMMUNITY_Community 7|Community 7]]
- [[_COMMUNITY_Community 8|Community 8]]
- [[_COMMUNITY_Community 9|Community 9]]
- [[_COMMUNITY_Community 10|Community 10]]
- [[_COMMUNITY_Community 11|Community 11]]
- [[_COMMUNITY_Community 12|Community 12]]
- [[_COMMUNITY_Community 13|Community 13]]
- [[_COMMUNITY_Community 14|Community 14]]
- [[_COMMUNITY_Community 16|Community 16]]
- [[_COMMUNITY_Community 17|Community 17]]
- [[_COMMUNITY_Community 18|Community 18]]
- [[_COMMUNITY_Community 19|Community 19]]
- [[_COMMUNITY_Community 21|Community 21]]
- [[_COMMUNITY_Community 22|Community 22]]
- [[_COMMUNITY_Community 23|Community 23]]
- [[_COMMUNITY_Community 25|Community 25]]
- [[_COMMUNITY_Community 26|Community 26]]
- [[_COMMUNITY_Community 27|Community 27]]
- [[_COMMUNITY_Community 28|Community 28]]
- [[_COMMUNITY_Community 29|Community 29]]

## God Nodes (most connected - your core abstractions)
1. `WorkflowEngine` - 55 edges
2. `CoordinateProfileViewModel` - 46 edges
3. `TcpServer` - 36 edges
4. `HomePlcGateway` - 33 edges
5. `Lx5vPlc` - 30 edges
6. `string` - 24 edges
7. `ConfigService` - 22 edges
8. `int` - 18 edges
9. `App` - 17 edges
10. `AxisAddressConfigItemViewModel` - 15 edges

## Surprising Connections (you probably didn't know these)
- `WorkflowEngine` --references--> `WeightToZCalibrationConfig`  [EXTRACTED]
  ViewModels/Home/HomeViewModel.cs → Services/WorkflowEngine.cs
- `FaultAlarmItemViewModel` --references--> `DateTime`  [EXTRACTED]
  ViewModels/FaultDebugViewModel.cs → App.xaml.cs
- `Lx5vPlc` --references--> `ushort`  [EXTRACTED]
  Services/PlcPollingService.cs → Communication/Serial/Lx5vPlc.cs
- `HomePlcGateway` --references--> `ushort`  [EXTRACTED]
  ViewModels/Home/HomeViewModel.cs → Communication/Serial/Lx5vPlc.cs
- `Lx5vPlc` --references--> `Rs485Helper`  [EXTRACTED]
  Services/PlcPollingService.cs → Communication/Serial/Lx5vPlc.cs

## Communities (37 total, 7 thin omitted)

### Community 0 - "Community 0"
Cohesion: 0.05
Nodes (17): AxisBinding, BaseViewModel, bool, Brush, CoordinatePointItemViewModel, DateTime, double, FaultAlarmItemViewModel (+9 more)

### Community 1 - "Community 1"
Cohesion: 0.06
Nodes (14): AxisAddressConfigItemViewModel, Dispatcher, ushort, AlarmReadItem, AlarmReadSegment, FaultAlarmDefinition, FaultDebugConfig, FaultEventRecordViewModel (+6 more)

### Community 2 - "Community 2"
Cohesion: 0.08
Nodes (8): HomePlcCommandCoordinator, HomePlcGateway, ILx5vPlcTransport, IModbusSerialMaster, Lx5vPlc, Blood_Alcohol.Communication.Serial, FromFailure(), FromSuccess()

### Community 3 - "Community 3"
Cohesion: 0.05
Nodes (13): HomeDetectionStateCoordinator, HomeTubeDetailPresenter, HomeTubeDetailApplyContext, Regex, string, TimeSpan, UserControl, PointAxisControlItemViewModel (+5 more)

### Community 4 - "Community 4"
Cohesion: 0.09
Nodes (8): CancellationTokenSource, HomeBackgroundTaskCoordinator, HomeBackgroundTaskSlot, LogTool, CoilSubscription, WorkflowLogMessage, Task, WorkflowEngine

### Community 5 - "Community 5"
Cohesion: 0.09
Nodes (6): INotifyPropertyChanged, int, ObservableCollection, PlcPoint, SemaphoreSlim, LogItem

### Community 6 - "Community 6"
Cohesion: 0.12
Nodes (4): Action, ICommand, Predicate, CoordinateProfileViewModel

### Community 7 - "Community 7"
Cohesion: 0.09
Nodes (5): byte, Blood_Alcohol.Protocols, Blood_Alcohol.Communication.Protocols, ScannerProtocolService, TemperatureService

### Community 8 - "Community 8"
Cohesion: 0.1
Nodes (12): AxisDebugAddressConfig, CommunicationSettings, ConfigFile, ConfigService, HomeConditionCoordinator, HomeSampleVolumeConverter, JsonSerializerOptions, AxisAddressProfile (+4 more)

### Community 9 - "Community 9"
Cohesion: 0.07
Nodes (13): HomeLogOutputCoordinator, IUiDispatcher, List, object, OperationMode, Queue, AppLogEntry, IAppLogSink (+5 more)

### Community 10 - "Community 10"
Cohesion: 0.12
Nodes (7): ConcurrentDictionary, ConcurrentQueue, Fail(), Ok(), TcpClientSession, TcpServer, TcpListener

### Community 11 - "Community 11"
Cohesion: 0.12
Nodes (5): Dictionary, HashSet, HomeLogController, HomeRackProcessState, HomeTubeProcessState

### Community 12 - "Community 12"
Cohesion: 0.16
Nodes (5): CommunicationConnectionCoordinator, DeviceRegistry, Func, IReadOnlyList, LogMessage

### Community 14 - "Community 14"
Cohesion: 0.13
Nodes (6): IDisposable, IStreamResource, long, Rs485Helper, SerialPort, Subscription

### Community 16 - "Community 16"
Cohesion: 0.18
Nodes (3): Application, App, Blood_Alcohol

### Community 18 - "Community 18"
Cohesion: 0.15
Nodes (3): Blood_Alcohol.Helpers, RowBorderThicknessConverter, IValueConverter

### Community 19 - "Community 19"
Cohesion: 0.16
Nodes (7): HomeAlarmMonitorContext, HomeMonitorLoops, HomeOperationModeMonitorContext, HomeProcessModeMonitorContext, HomeRackProcessMonitorContext, HomeTemperatureMonitorChannelState, HomeTemperatureMonitorContext

### Community 22 - "Community 22"
Cohesion: 0.25
Nodes (6): HomeAutoStopCommandContext, HomeEmergencyStopCommandContext, HomeInitializeCommandContext, HomeStartCommandContext, HomeStopCommandContext, HomeDetectionCommandCoordinator

### Community 23 - "Community 23"
Cohesion: 0.4
Nodes (3): HomeExportDirectorySelectionContext, HomeTubeSlotClickContext, HomeInteractionCoordinator

### Community 25 - "Community 25"
Cohesion: 0.4
Nodes (3): HomeLogWriteContext, HomeWorkflowLogIngressContext, HomeLogIngressCoordinator

## Knowledge Gaps
- **54 isolated node(s):** `IModbusSerialMaster`, `long`, `TcpClientSession`, `ConcurrentDictionary`, `TcpListener` (+49 more)
  These have ≤1 connection - possible missing edges or undocumented components.
- **7 thin communities (<3 nodes) omitted from report** — run `graphify query` to explore isolated nodes.

## Suggested Questions
_Questions this graph is uniquely positioned to answer:_

- **Why does `WorkflowEngine` connect `Community 4` to `Community 0`, `Community 2`, `Community 3`, `Community 5`, `Community 7`, `Community 8`, `Community 11`, `Community 12`, `Community 13`?**
  _High betweenness centrality (0.135) - this node is a cross-community bridge._
- **Why does `TcpServer` connect `Community 10` to `Community 0`, `Community 4`, `Community 5`, `Community 6`, `Community 9`, `Community 11`, `Community 12`?**
  _High betweenness centrality (0.101) - this node is a cross-community bridge._
- **Why does `string` connect `Community 3` to `Community 0`, `Community 1`, `Community 4`, `Community 5`, `Community 6`, `Community 8`, `Community 9`, `Community 12`, `Community 13`?**
  _High betweenness centrality (0.098) - this node is a cross-community bridge._
- **What connects `IModbusSerialMaster`, `long`, `TcpClientSession` to the rest of the system?**
  _54 weakly-connected nodes found - possible documentation gaps or missing edges._
- **Should `Community 0` be split into smaller, more focused modules?**
  _Cohesion score 0.05 - nodes in this community are weakly interconnected._
- **Should `Community 1` be split into smaller, more focused modules?**
  _Cohesion score 0.06 - nodes in this community are weakly interconnected._
- **Should `Community 2` be split into smaller, more focused modules?**
  _Cohesion score 0.08 - nodes in this community are weakly interconnected._