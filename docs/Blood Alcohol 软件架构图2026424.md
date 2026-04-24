# Blood Alcohol 软件架构图

## 1. 文档说明

- 生成时间：2026-04-24
- 文档目的：说明当前上位机项目的分层结构、启动链路、运行期协作关系与现场设备连接方式
- 依据代码：
  - `App.xaml.cs`
  - `MainWindow.xaml`
  - `MainWindow.xaml.cs`
  - `ViewModels/Home/HomeViewModel.cs`
  - `Services/CommunicationManager.cs`
  - `Services/WorkflowEngine.cs`
  - `Services/TemperatureService.cs`
  - `Communication/Tcp/TcpServer.cs`

## 2. 总体分层架构图

```mermaid
flowchart TB
    subgraph UI[表示层 WPF]
        App[App]
        MainWindow[MainWindow]
        HomeView[HomeView]
        CommunicationView[CommunicationDebugView]
        DebugView[DebugView]
    end

    subgraph VM[视图模型层]
        HomeVM[HomeViewModel]
        CommunicationVM[CommunicationViewModel]
        DebugVM[DebugViewModel]
        HomeCoord[首页协调组件]
        HomeLog[首页日志组件]
        HomePresentation[首页展示组件]
        HomeProcessing[首页处理状态组件]
    end

    subgraph SVC[业务服务层]
        Workflow[WorkflowEngine]
        CommMgr[CommunicationManager]
        Polling[PlcPollingService]
        TempSvc[TemperatureService]
        ModeSvc[OperationModeService]
        ConfigSvc[ConfigService<T>]
        LogTool[LogTool]
    end

    subgraph COMM[通信与协议层]
        Rs485[Rs485Helper]
        Plc[Lx5vPlc]
        Tcp[TcpServer]
        Scanner[ScannerProtocolService]
        Balance[BalanceProtocolService]
        Shimaden[ShimadenSrs11A]
    end

    subgraph DATA[配置与持久化]
        ConfigJson[Config/*.json]
        ModelDefs[Models/*.cs]
        LogFiles[Logs/*.log 与 CSV]
    end

    subgraph DEVICE[现场设备]
        PlcDevice[PLC]
        ScannerDevice[扫码枪]
        BalanceDevice[天平]
        TempDevice[温控器]
    end

    App --> MainWindow
    MainWindow --> HomeView
    MainWindow --> CommunicationView
    MainWindow --> DebugView

    HomeView --> HomeVM
    CommunicationView --> CommunicationVM
    DebugView --> DebugVM

    HomeVM --> HomeCoord
    HomeVM --> HomeLog
    HomeVM --> HomePresentation
    HomeVM --> HomeProcessing
    HomeVM --> Workflow
    HomeVM --> CommMgr
    HomeVM --> Polling
    HomeVM --> TempSvc
    HomeVM --> ModeSvc
    HomeVM --> ConfigSvc

    Workflow --> CommMgr
    Workflow --> ConfigSvc
    Workflow --> LogTool
    TempSvc --> CommMgr

    CommMgr --> Polling
    CommMgr --> Plc
    CommMgr --> Tcp
    CommMgr --> Balance
    CommMgr --> Shimaden

    Plc --> Rs485
    Rs485 --> PlcDevice
    Tcp --> ScannerDevice
    Tcp --> BalanceDevice
    Tcp --> TempDevice
    Workflow --> Scanner
    TempSvc --> Shimaden

    ConfigSvc --> ConfigJson
    ModelDefs --> ConfigJson
    HomeLog --> LogTool
    LogTool --> LogFiles
```

## 3. 首页业务编排图

```mermaid
flowchart LR
    HomeVM[HomeViewModel]
    Workflow[WorkflowEngine]
    CommMgr[CommunicationManager]

    subgraph Coordination[协调层]
        DetectionState[HomeDetectionStateCoordinator]
        DetectionCmd[HomeDetectionCommandCoordinator]
        PlcCmd[HomePlcCommandCoordinator]
        PlcGateway[HomePlcGateway]
        Interaction[HomeInteractionCoordinator]
        Background[HomeBackgroundTaskCoordinator]
        Condition[HomeConditionCoordinator]
        LogIngress[HomeLogIngressCoordinator]
        LogOutput[HomeLogOutputCoordinator]
    end

    subgraph Presentation[展示层]
        RackPresenter[HomeRackVisualPresenter]
        TubePresenter[HomeTubeDetailPresenter]
        LogController[HomeLogController]
    end

    subgraph Processing[处理状态层]
        TubeState[HomeTubeProcessState]
        RackState[HomeRackProcessState]
        VolumeConverter[HomeSampleVolumeConverter]
        MonitorLoops[HomeMonitorLoops]
        TempSvc[TemperatureService]
    end

    HomeVM --> DetectionState
    HomeVM --> DetectionCmd
    HomeVM --> PlcCmd
    HomeVM --> PlcGateway
    HomeVM --> Interaction
    HomeVM --> Background
    HomeVM --> Condition
    HomeVM --> LogIngress
    HomeVM --> LogOutput
    HomeVM --> RackPresenter
    HomeVM --> TubePresenter
    HomeVM --> LogController
    HomeVM --> TubeState
    HomeVM --> RackState
    HomeVM --> VolumeConverter
    HomeVM --> MonitorLoops
    HomeVM --> TempSvc
    HomeVM --> Workflow
    HomeVM --> CommMgr

    LogOutput --> Workflow
    LogIngress --> LogController
    PlcGateway --> CommMgr
    PlcCmd --> PlcGateway
    MonitorLoops --> PlcGateway
    TempSvc --> CommMgr
```

## 4. 启动时序图

```mermaid
sequenceDiagram
    participant App as App.OnStartup
    participant CM as CommunicationManager
    participant MW as MainWindow
    participant HV as HomeViewModel
    participant WF as WorkflowEngine
    participant PP as PlcPollingService

    App->>App: 注册全局异常处理
    App->>CM: LoadSettings()
    App->>App: ValidateStartupConfigurations()
    App->>CM: AutoConnect()
    CM->>PP: 保持轮询服务运行
    App->>MW: new MainWindow().Show()
    MW->>HV: 创建首页 DataContext
    HV->>CM: 订阅通信状态与日志
    HV->>WF: ConfigureLogOutput(...)
    HV->>PP: 注册首页核心轮询点
    HV->>HV: 启动报警 档位 工艺模式 料架 温控等后台监控
```

## 5. 运行期协作图

```mermaid
flowchart LR
    User[操作员]
    HomeVM[HomeViewModel]
    PlcCmd[HomePlcCommandCoordinator]
    Workflow[WorkflowEngine]
    Snapshot[WorkflowRuntimeSnapshot]
    ConfigSvc[ConfigService<T>]
    CommMgr[CommunicationManager]
    Tcp[TcpServer]
    Plc[Lx5vPlc]
    Scanner[ScannerProtocolService]
    Balance[BalanceProtocolService]
    LogOutput[HomeLogOutputCoordinator]
    LogController[HomeLogController]
    LogTool[LogTool]

    User -->|初始化 开始 停止 急停| HomeVM
    HomeVM --> PlcCmd
    HomeVM --> Workflow
    HomeVM --> LogController
    HomeVM --> LogOutput

    Workflow --> Snapshot
    Snapshot --> ConfigSvc
    Workflow --> CommMgr
    Workflow --> Scanner
    Workflow --> Balance

    CommMgr --> Plc
    CommMgr --> Tcp

    Workflow -->|流程日志| LogTool
    HomeVM -->|首页日志| LogController
    LogController --> LogTool
```

## 6. 通信与设备拓扑图

```mermaid
flowchart TB
    CommMgr[CommunicationManager]

    subgraph SerialLink[串口链路]
        Polling[PlcPollingService]
        Plc[Lx5vPlc]
        Rs485[Rs485Helper]
    end

    subgraph TcpLink[TCP链路]
        Tcp[TcpServer]
        Session[按 DeviceKey 访问会话]
        ScannerProtocol[ScannerProtocolService]
        BalanceProtocol[BalanceProtocolService]
        ShimadenProtocol[ShimadenSrs11A]
        TempSvc[TemperatureService]
    end

    PlcDevice[PLC]
    ScannerDevice[扫码枪]
    BalanceDevice[天平]
    TempDevice[温控器]

    CommMgr --> Polling
    CommMgr --> Plc
    CommMgr --> Tcp
    CommMgr --> BalanceProtocol
    CommMgr --> ShimadenProtocol

    Polling --> Plc
    Plc --> Rs485
    Rs485 --> PlcDevice

    Tcp --> Session
    Session --> ScannerDevice
    Session --> BalanceDevice
    Session --> TempDevice

    ScannerProtocol --> Tcp
    BalanceProtocol --> Tcp
    TempSvc --> ShimadenProtocol
    TempSvc --> Tcp
```

## 7. 当前架构特点

- 前端采用 `WPF + MVVM`，主窗口以 `TabControl` 承载首页、通讯配置页、设置页。
- 首页 `HomeViewModel` 已拆分为协调、展示、日志、处理状态多个子组件，避免单类承担全部逻辑。
- `CommunicationManager` 作为静态总入口，集中管理 `RS485`、`PLC`、`TCP`、协议服务与通信配置。
- `WorkflowEngine` 负责检测流程状态机，启动时一次性加载 `WorkflowRuntimeSnapshot`，运行中不再频繁热加载配置。
- TCP 设备在接入层通过 `ClientIp + Port` 识别并绑定，在业务层统一通过 `DeviceKey` 访问。
- 首页在构造后立即启动报警、档位、工艺模式、料架、温控等后台监控任务，并在释放时按顺序停止。
- 日志分为首页可见日志与流程结构化日志，最终统一写入本地 `log` 与 `CSV` 文件。

## 8. 核心目录职责

| 目录或文件 | 职责 |
|---|---|
| `App.xaml.cs` | 应用启动、全局异常处理、启动配置校验、退出时统一收口 |
| `MainWindow.xaml` | 主窗口与首页/通讯配置/设置页宿主 |
| `Views/` | 各页面与调试视图 |
| `ViewModels/Home/` | 首页业务编排、日志处理、状态展示与后台监控 |
| `Services/` | 流程引擎、通信总入口、轮询服务、温控服务、配置读写 |
| `Communication/Serial/` | PLC 串口通信与底层传输 |
| `Communication/Tcp/` | TCP 服务端、会话路由、按设备键收发数据 |
| `Protocols/` | 扫码枪、天平、温控器协议封装 |
| `Models/` | 配置模型、业务模型、映射定义 |
| `Config/` | JSON 配置文件 |
| `Logs/` | 运行日志与轨迹导出文件 |

## 9. 建议阅读顺序

1. `App.xaml.cs`
2. `MainWindow.xaml`
3. `ViewModels/Home/HomeViewModel.cs`
4. `Services/CommunicationManager.cs`
5. `Services/WorkflowEngine.cs`
6. `Services/TemperatureService.cs`
7. `Communication/Tcp/TcpServer.cs`
