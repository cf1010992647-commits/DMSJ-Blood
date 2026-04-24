using Blood_Alcohol.Communication.Serial;
using Blood_Alcohol.Models;
using Blood_Alcohol.Protocols;
using Blood_Alcohol.Services;
using Blood_Alcohol.ViewModels;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Text;

namespace Blood_Alcohol.Tests;

/// <summary>
/// 全链路通信模拟测试。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 模拟 PLC 扫码枪 天平全部联机，验证初始化 开始 流程执行与停止整条链路可以跑通。
/// </remarks>
public class FullCommunicationSimulationTests
{
    /// <summary>
    /// 验证在全部通信已连接的模拟环境下可以从头到尾跑通一次完整流程。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 覆盖初始化参数下发 开始脉冲 扫码 八个称重步骤 重量转Z 以及停止脉冲。
    /// </remarks>
    [Fact]
    public async Task SimulatedCommunications_FullWorkflow_RunsEndToEnd()
    {
        await using FullWorkflowSimulationContext context = new FullWorkflowSimulationContext();

        await context.RunAsync();
    }

    /// <summary>
    /// 验证扫码枪长时间无数据时会记录扫码超时失败日志。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 保留扫码枪 TCP 已连接但未返回条码的场景，用于确认流程不会静默卡住。
    /// </remarks>
    [Fact]
    public async Task SimulatedCommunications_NoScanData_RecordsScanTimeoutFailure()
    {
        await using FullWorkflowSimulationContext context = new FullWorkflowSimulationContext();

        await context.RunScanTimeoutAsync();
    }

    /// <summary>
    /// 验证称重步骤等不到 PLC 确认位时会记录步骤超时失败日志。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 先完成扫码与天平清零，再在首个称重步骤故意不置位 OK 信号，确认流程能明确报错。
    /// </remarks>
    [Fact]
    public async Task SimulatedCommunications_WeightOkNotRaised_RecordsWeightStepTimeoutFailure()
    {
        await using FullWorkflowSimulationContext context = new FullWorkflowSimulationContext();

        await context.RunFirstWeightOkTimeoutAsync();
    }

    /// <summary>
    /// 全流程模拟上下文。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 负责准备配置 注入假 PLC 连接 TCP 设备并按顺序驱动完整业务流程。
    /// </remarks>
    private sealed class FullWorkflowSimulationContext : IAsyncDisposable
    {
        private readonly WorkflowSignalConfig _signals = new WorkflowSignalConfig
        {
            SignalWaitTimeoutSeconds = 5,
            ZAbsolutePositionScale = 100
        };

        private readonly ProcessParameterConfig _parameters = new ProcessParameterConfig
        {
            ZDropNeedleRiseSlowSpeed = 1000,
            PipetteAspirateDelay100ms = 1,
            PipetteDispenseDelay100ms = 1,
            TubeShakeHomeDelay100ms = 1,
            TubeShakeWorkDelay100ms = 1,
            TubeShakeTargetCount = 3,
            HeadspaceShakeHomeDelay100ms = 1,
            HeadspaceShakeWorkDelay100ms = 1,
            HeadspaceShakeTargetCount = 3,
            ButanolAspirateDelay100ms = 1,
            ButanolDispenseDelay100ms = 1,
            SampleBottlePressureTime100ms = 2,
            QuantitativeLoopBalanceTime100ms = 2,
            InjectionTime100ms = 1
        };

        private readonly WeightToZCalibrationConfig _weightToZ = new WeightToZCalibrationConfig
        {
            HasCoefficient = true,
            ZPerWeight = 1.0,
            HasMicroliterCoefficient = true,
            MicroliterPerWeight = 1000
        };

        private readonly FakeLx5vPlcTransport _plcTransport = new FakeLx5vPlcTransport();
        private readonly ConcurrentQueue<WorkflowEngine.WorkflowLogMessage> _workflowLogs = new ConcurrentQueue<WorkflowEngine.WorkflowLogMessage>();
        private readonly List<string> _homeLogs = new List<string>();
        private readonly BalanceProtocolService _balanceProtocol = new BalanceProtocolService();
        private readonly int _scannerPort = ReserveTcpPort();
        private readonly int _balancePort = ReserveTcpPort();
        private readonly string _scannerDeviceKey = "扫码枪";
        private readonly string _balanceDeviceKey = "天平";

        private WorkflowEngine? _engine;
        private string _currentBatchNo = string.Empty;
        private HomeDetectionStateCoordinator? _detectionState;
        private HomePlcCommandCoordinator? _plcCoordinator;
        private HomeDetectionCommandCoordinator? _detectionCoordinator;
        private object? _deviceRegistry;
        private object? _originalPlc;
        private CommunicationSettings? _originalSettings;
        private TcpClient? _scannerClient;
        private TcpClient? _balanceClient;
        private Task? _balanceLoopTask;
        private CancellationTokenSource? _balanceLoopCts;
        private int _balanceAllCommandCount;

        /// <summary>
        /// 运行一次完整的全流程模拟。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步运行任务。</returns>
        /// <remarks>
        /// 由外层测试调用，内部依次完成环境准备 初始化 开始 流程驱动 校验与停止。
        /// </remarks>
        public async Task RunAsync()
        {
            await PrepareAndStartDetectionAsync();
            Assert.True(_plcTransport.GetWriteCount(5) >= 2);

            Queue<byte[]> balanceResponses = new Queue<byte[]>(
                new[]
                {
                    BuildBalanceAllResponse(111, 3),
                    BuildBalanceAllResponse(222, 3),
                    BuildBalanceAllResponse(333, 3),
                    BuildBalanceAllResponse(445, 3),
                    BuildBalanceAllResponse(555, 3),
                    BuildBalanceAllResponse(666, 3),
                    BuildBalanceAllResponse(777, 3),
                    BuildBalanceAllResponse(888, 3)
                });

            StartBalanceLoop(balanceResponses);
            await DriveWorkflowAsync();

            int zRaw = CombineInt32(_plcTransport.GetRegister(_signals.ZAbsolutePositionLowRegister), _plcTransport.GetRegister((ushort)(_signals.ZAbsolutePositionLowRegister + 1)));
            Assert.Equal(45, zRaw);
            Assert.Contains(_homeLogs, log => log.Contains("开始检测", StringComparison.Ordinal));
            await StopDetectionAsync();

            Assert.NotNull(_detectionState);
            Assert.False(_detectionState.IsDetectionStarted);
            Assert.True(_plcTransport.GetWriteCount(900) >= 2);
            Assert.Contains(GetWorkflowLogs(), log => log.EventName == "Z坐标下发");
            Assert.Contains(GetWorkflowLogs(), log => log.EventName == "清零完成");
            Assert.Equal(8, Volatile.Read(ref _balanceAllCommandCount));
            Assert.Equal(8, GetWorkflowLogs().Count(log => log.EventName == "步骤确认"));
            Assert.Equal(2, GetWorkflowLogs().Count(log => log.EventName == "称重完成"));
        }

        /// <summary>
        /// 运行扫码超时失败场景。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步运行任务。</returns>
        /// <remarks>
        /// 启动完整模拟环境后只置位扫码允许信号，不发送扫码内容，等待流程记录超时日志。
        /// </remarks>
        public async Task RunScanTimeoutAsync()
        {
            await PrepareAndStartDetectionAsync();

            _plcTransport.SetCoil(_signals.AllowScanCoil, true);
            WorkflowEngine.WorkflowLogMessage timeoutLog = await WaitForWorkflowLogAsync(
                log => log.Message.Contains("scan处理失败：TCP接收超时", StringComparison.Ordinal),
                TimeSpan.FromSeconds(12));

            Assert.Equal("错误", timeoutLog.LevelText);
            Assert.DoesNotContain(GetWorkflowLogs(), log => log.EventName == "扫码成功");
            Assert.Equal(0, Volatile.Read(ref _balanceAllCommandCount));

            await StopDetectionAsync();
        }

        /// <summary>
        /// 运行首个称重步骤确认超时失败场景。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步运行任务。</returns>
        /// <remarks>
        /// 完成扫码和天平清零后，只让天平返回一次重量，不置位步骤 OK 信号，等待流程记录超时日志。
        /// </remarks>
        public async Task RunFirstWeightOkTimeoutAsync()
        {
            await PrepareAndStartDetectionAsync();

            StartBalanceLoop(new Queue<byte[]>(new[]
            {
                BuildBalanceAllResponse(111, 3)
            }));

            await PerformScanAndZeroAsync();

            _plcTransport.SetCoil(_signals.AllowHs1PlaceWeightCoil, true);
            await WaitForBalanceCommandCountAsync(1, TimeSpan.FromSeconds(5));

            WorkflowEngine.WorkflowLogMessage timeoutLog = await WaitForWorkflowLogAsync(
                log => log.Message.Contains("hs1_place_weight处理失败：等待 顶空1放置OK", StringComparison.Ordinal),
                TimeSpan.FromSeconds(7));

            Assert.Equal("错误", timeoutLog.LevelText);
            Assert.Equal(1, Volatile.Read(ref _balanceAllCommandCount));
            Assert.DoesNotContain(
                GetWorkflowLogs(),
                log => log.EventName == "步骤确认" && string.Equals(log.ProcessName, "顶空1放置", StringComparison.Ordinal));

            await StopDetectionAsync();
        }

        /// <summary>
        /// 释放模拟上下文占用的静态资源与 TCP 连接。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步释放任务。</returns>
        /// <remarks>
        /// 由测试结束时调用，确保不会污染后续测试环境。
        /// </remarks>
        public async ValueTask DisposeAsync()
        {
            if (_balanceLoopCts != null)
            {
                _balanceLoopCts.Cancel();
            }

            if (_balanceLoopTask != null)
            {
                try
                {
                    await _balanceLoopTask.ConfigureAwait(false);
                }
                catch
                {
                }
            }

            if (_engine != null)
            {
                try
                {
                    await _engine.StopAsync().ConfigureAwait(false);
                }
                catch
                {
                }
            }

            _scannerClient?.Close();
            _balanceClient?.Close();
            CommunicationManager.TcpServer.Stop();

            if (_deviceRegistry != null && _originalPlc != null)
            {
                SetAutoPropertyBackingField(_deviceRegistry, "Plc", _originalPlc);
            }

            if (_originalSettings != null)
            {
                CommunicationManager.Settings = _originalSettings;
                CommunicationManager.ConfigureTcpDeviceMappings();
            }
        }

        /// <summary>
        /// 准备流程运行所需配置文件。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 RunAsync 在启动前调用，确保 WorkflowEngine.Start 可以加载到有效配置。
        /// </remarks>
        private void PrepareConfigs()
        {
            new ConfigService<WorkflowSignalConfig>("WorkflowSignalConfig.json").Save(_signals);
            new ConfigService<ProcessParameterConfig>("ProcessParameterConfig.json").Save(_parameters);
            new ConfigService<WeightToZCalibrationConfig>("WeightToZCalibrationConfig.json").Save(_weightToZ);
        }

        /// <summary>
        /// 用内存版 PLC 替换 CommunicationManager 当前 PLC 实例。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 RunAsync 调用，使 HomePlcGateway 与 WorkflowEngine 继续走原有静态入口。
        /// </remarks>
        private void ReplaceCommunicationManagerPlc()
        {
            FieldInfo devicesField = typeof(CommunicationManager).GetField("_devices", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(nameof(CommunicationManager), "_devices");

            _deviceRegistry = devicesField.GetValue(null) ?? throw new InvalidOperationException("未找到通信设备注册表。");
            _originalPlc = GetAutoPropertyBackingField(_deviceRegistry, "Plc");
            _originalSettings = CommunicationManager.Settings;

            Lx5vPlc fakePlc = new Lx5vPlc(new Blood_Alcohol.Communication.Serial.Rs485Helper(), transport: _plcTransport);
            SetAutoPropertyBackingField(_deviceRegistry, "Plc", fakePlc);
        }

        /// <summary>
        /// 准备环境并启动检测流程。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步启动任务。</returns>
        /// <remarks>
        /// 供成功链路与失败链路测试复用，统一完成配置准备、通信替换、初始化和开始检测。
        /// </remarks>
        private async Task PrepareAndStartDetectionAsync()
        {
            PrepareConfigs();
            ReplaceCommunicationManagerPlc();
            await StartTcpDevicesAsync();
            SeedPlcInitialState();

            _engine = new WorkflowEngine();
            _engine.ConfigureLogOutput(new Blood_Alcohol.Logs.LogTool(), () => _currentBatchNo);
            _engine.OnLogGenerated += log => _workflowLogs.Enqueue(log);

            _detectionState = new HomeDetectionStateCoordinator();
            HomePlcGateway gateway = new HomePlcGateway(CommunicationManager.PlcAccessLock, TimeSpan.FromMilliseconds(100));
            _plcCoordinator = new HomePlcCommandCoordinator(
                gateway,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromMilliseconds(20));
            _detectionCoordinator = new HomeDetectionCommandCoordinator();

            await _detectionCoordinator.InitializeAsync(new HomeInitializeCommandContext
            {
                DetectionState = _detectionState,
                LoadProcessParameterConfig = () => _parameters,
                ApplyConditions = _ => { },
                InitializePlcAsync = (config, onReadError) => _plcCoordinator.InitializeAsync(config, onReadError),
                AddLog = AddHomeLog,
                InvalidateCommands = static () => { }
            });

            Assert.True(_plcTransport.GetCoil(14));
            Assert.Equal((ushort)_parameters.ZDropNeedleRiseSlowSpeed, _plcTransport.GetRegister(6000));

            await _detectionCoordinator.StartAsync(new HomeStartCommandContext
            {
                DetectionState = _detectionState,
                SelectedTubeCount = 1,
                SelectedHeadspaceCount = 2,
                IsPlcConnected = static () => true,
                ResetStartCommandLowAsync = async () => await EnsureCommandSucceededAsync(_plcCoordinator.EnsureStartCommandLowAsync()),
                TryStartAsync = _plcCoordinator.TryStartAsync,
                SetAlarmActive = _ => { },
                ClearTubeProcessRuntimeState = static () => { },
                AllocateNextBatchNo = () => "批次_001",
                SetCurrentBatchNo = batchNo => _currentBatchNo = batchNo,
                RefreshDetectionState = static () => { },
                StartWorkflow = _engine.Start,
                StartTubeCountSync = static () => { },
                AddLog = AddHomeLog,
                InvalidateCommands = static () => { }
            });

            Assert.True(_detectionState.IsDetectionStarted);
        }

        /// <summary>
        /// 停止当前检测流程。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步停止任务。</returns>
        /// <remarks>
        /// 供所有模拟场景收尾复用，统一走首页停止命令语义。
        /// </remarks>
        private async Task StopDetectionAsync()
        {
            if (_detectionCoordinator == null || _detectionState == null || _plcCoordinator == null || _engine == null)
            {
                return;
            }

            await _detectionCoordinator.StopAsync(new HomeStopCommandContext
            {
                DetectionState = _detectionState,
                RefreshDetectionState = static () => { },
                StopTubeCountSyncAsync = static () => Task.CompletedTask,
                ClearTubeProcessRuntimeState = static () => { },
                ClearRackProcessStates = static () => { },
                StopWorkflowAsync = () => _engine.StopAsync(),
                SendStopAsync = async () => await EnsureCommandSucceededAsync(_plcCoordinator.SendStopAsync()),
                AddLog = AddHomeLog
            });
        }

        /// <summary>
        /// 启动 TCP 服务并连接扫码枪与天平模拟客户端。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步启动任务。</returns>
        /// <remarks>
        /// 由 RunAsync 调用，保留项目按 ClientIp 加源端口绑定逻辑设备身份的真实行为。
        /// </remarks>
        private async Task StartTcpDevicesAsync()
        {
            CommunicationManager.TcpServer.Stop();
            CommunicationManager.Settings = new CommunicationSettings
            {
                ComPort = "COM1",
                BaudRate = 9600,
                TcpPort = 20108,
                TcpIP = IPAddress.Loopback.ToString(),
                TcpDevices = new List<TcpDeviceMapping>
                {
                    new TcpDeviceMapping
                    {
                        Port = _scannerPort,
                        DeviceType = "扫码枪",
                        DeviceKey = _scannerDeviceKey,
                        ClientIp = IPAddress.Loopback.ToString()
                    },
                    new TcpDeviceMapping
                    {
                        Port = _balancePort,
                        DeviceType = "天平",
                        DeviceKey = _balanceDeviceKey,
                        ClientIp = IPAddress.Loopback.ToString()
                    }
                }
            };
            CommunicationManager.ConfigureTcpDeviceMappings();
            CommunicationManager.TcpServer.Start(0);

            _scannerClient = await ConnectFromLocalPortAsync(CommunicationManager.TcpServer.ListeningPort, _scannerPort);
            _balanceClient = await ConnectFromLocalPortAsync(CommunicationManager.TcpServer.ListeningPort, _balancePort);
            await WaitForDeviceAsync(CommunicationManager.TcpServer, _scannerDeviceKey, expected: true);
            await WaitForDeviceAsync(CommunicationManager.TcpServer, _balanceDeviceKey, expected: true);
        }

        /// <summary>
        /// 设置流程启动前的 PLC 初始状态。
        /// </summary>
        /// By:ChengLei
        /// <remarks>
        /// 由 RunAsync 调用，保证初始化前置位和开始前置位具备确定的初始值。
        /// </remarks>
        private void SeedPlcInitialState()
        {
            _plcTransport.SetCoil(2, false);
            _plcTransport.SetCoil(10, true);
            _plcTransport.SetCoil(14, false);
            _plcTransport.SetCoil(490, true);
            _plcTransport.SetCoil(491, false);
            _plcTransport.SetCoil(492, false);
            _plcTransport.SetCoil(493, false);
        }

        /// <summary>
        /// 启动天平模拟读写循环。
        /// </summary>
        /// By:ChengLei
        /// <param name="responses">按步骤返回的称重回包队列。</param>
        /// <remarks>
        /// 收到读重量命令时依次回放预设回包，收到清零命令时不返回数据以覆盖项目当前容错路径。
        /// </remarks>
        private void StartBalanceLoop(Queue<byte[]> responses)
        {
            if (_balanceClient == null)
            {
                throw new InvalidOperationException("天平客户端未连接。");
            }

            _balanceLoopCts = new CancellationTokenSource();
            _balanceLoopTask = Task.Run(async () =>
            {
                byte[] buffer = new byte[128];
                NetworkStream stream = _balanceClient.GetStream();

                while (!_balanceLoopCts.IsCancellationRequested)
                {
                    int length;
                    try
                    {
                        length = await stream.ReadAsync(buffer, _balanceLoopCts.Token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        break;
                    }

                    if (length == 0)
                    {
                        break;
                    }

                    byte[] command = new byte[length];
                    Array.Copy(buffer, command, length);
                    if (command.SequenceEqual(_balanceProtocol.GetAllCommand()))
                    {
                        Interlocked.Increment(ref _balanceAllCommandCount);

                        if (responses.Count == 0)
                        {
                            throw new InvalidOperationException("天平模拟回包已耗尽。");
                        }

                        byte[] response = responses.Dequeue();
                        await stream.WriteAsync(response, _balanceLoopCts.Token).ConfigureAwait(false);
                    }
                }
            });
        }

        /// <summary>
        /// 按真实流程顺序驱动扫码与全部称重步骤。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回异步驱动任务。</returns>
        /// <remarks>
        /// 由 RunAsync 调用，通过线圈脉冲和 TCP 回包触发 WorkflowEngine 完成整批流程。
        /// </remarks>
        private async Task DriveWorkflowAsync()
        {
            if (_scannerClient == null)
            {
                throw new InvalidOperationException("扫码枪客户端未连接。");
            }

            await PerformScanAndZeroAsync();

            await RunWeightStepAsync(_signals.AllowHs1PlaceWeightCoil, _signals.Hs1PlaceWeightOkCoil, 1, "顶空1放置");
            await RunWeightStepAsync(_signals.AllowHs2PlaceWeightCoil, _signals.Hs2PlaceWeightOkCoil, 2, "顶空2放置");
            await RunWeightStepAsync(_signals.AllowTubePlaceWeightCoil, _signals.TubePlaceWeightOkCoil, 3, "采血管放置");
            await RunWeightStepAsync(_signals.AllowTubeAfterAspirateWeightCoil, _signals.TubeAfterAspirateWeightOkCoil, 4, "采血管吸液后");
            await RunWeightStepAsync(_signals.AllowHs1AfterBloodWeightCoil, _signals.Hs1AfterBloodWeightOkCoil, 5, "顶空1加血液后");
            await RunWeightStepAsync(_signals.AllowHs2AfterBloodWeightCoil, _signals.Hs2AfterBloodWeightOkCoil, 6, "顶空2加血液后");
            await RunWeightStepAsync(_signals.AllowHs1AfterButanolWeightCoil, _signals.Hs1AfterButanolWeightOkCoil, 7, "顶空1加叔丁醇后");
            await RunWeightStepAsync(_signals.AllowHs2AfterButanolWeightCoil, _signals.Hs2AfterButanolWeightOkCoil, 8, "顶空2加叔丁醇后");
        }

        /// <summary>
        /// 执行扫码与天平清零阶段。
        /// </summary>
        /// By:ChengLei
        /// <param name="code">要发送的模拟条码。</param>
        /// <returns>返回异步执行任务。</returns>
        /// <remarks>
        /// 供成功链路和称重失败链路复用，保证流程已经越过扫码入口。
        /// </remarks>
        private async Task PerformScanAndZeroAsync(string code = "BC001")
        {
            if (_scannerClient == null)
            {
                throw new InvalidOperationException("扫码枪客户端未连接。");
            }

            await Task.Delay(200);

            _plcTransport.SetCoil(_signals.AllowScanCoil, true);
            await Task.Delay(100);
            await _scannerClient.GetStream().WriteAsync(Encoding.ASCII.GetBytes(code));
            await WaitForWorkflowLogAsync(log => log.EventName == "扫码成功", TimeSpan.FromSeconds(5));
            _plcTransport.SetCoil(_signals.ScanOkCoil, true);
            await WaitForWorkflowLogAsync(log => log.EventName == "清零完成", TimeSpan.FromSeconds(5));
            _plcTransport.SetCoil(_signals.AllowScanCoil, false);
            _plcTransport.SetCoil(_signals.ScanOkCoil, false);
        }

        /// <summary>
        /// 驱动单个称重步骤完成。
        /// </summary>
        /// By:ChengLei
        /// <param name="allowCoil">允称重线圈地址。</param>
        /// <param name="okCoil">步骤确认线圈地址。</param>
        /// <param name="expectedBalanceCommandCount">期望天平读重量命令累计次数。</param>
        /// <param name="processName">流程工序名称。</param>
        /// <returns>返回异步驱动任务。</returns>
        /// <remarks>
        /// 先置位允称重，再等待天平收到对应次数的读命令，最后置位 OK 信号完成步骤确认。
        /// </remarks>
        private async Task RunWeightStepAsync(ushort allowCoil, ushort okCoil, int expectedBalanceCommandCount, string processName)
        {
            _plcTransport.SetCoil(allowCoil, true);
            await WaitForBalanceCommandCountAsync(expectedBalanceCommandCount, TimeSpan.FromSeconds(5));

            _plcTransport.SetCoil(okCoil, true);
            await WaitForWorkflowLogAsync(
                log => log.EventName == "步骤确认" && string.Equals(log.ProcessName, processName, StringComparison.Ordinal),
                TimeSpan.FromSeconds(5));

            _plcTransport.SetCoil(allowCoil, false);
            _plcTransport.SetCoil(okCoil, false);
        }

        /// <summary>
        /// 等待天平读重量命令累计次数达到目标值。
        /// </summary>
        /// By:ChengLei
        /// <param name="expectedCount">期望累计次数。</param>
        /// <param name="timeout">等待超时时间。</param>
        /// <returns>返回异步等待任务。</returns>
        /// <remarks>
        /// 由称重步骤驱动流程调用，用于替代普通称重步骤缺少中间日志的问题。
        /// </remarks>
        private async Task WaitForBalanceCommandCountAsync(int expectedCount, TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                if (Volatile.Read(ref _balanceAllCommandCount) >= expectedCount)
                {
                    return;
                }

                await Task.Delay(20);
            }

            throw new TimeoutException("等待天平读重量命令超时。");
        }

        /// <summary>
        /// 等待出现符合条件的流程日志。
        /// </summary>
        /// By:ChengLei
        /// <param name="predicate">日志匹配条件。</param>
        /// <param name="timeout">等待超时时间。</param>
        /// <returns>返回匹配到的流程日志。</returns>
        /// <remarks>
        /// 由流程驱动步骤调用，用日志作为流程推进的稳定同步点。
        /// </remarks>
        private async Task<WorkflowEngine.WorkflowLogMessage> WaitForWorkflowLogAsync(
            Predicate<WorkflowEngine.WorkflowLogMessage> predicate,
            TimeSpan timeout)
        {
            DateTime deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                foreach (WorkflowEngine.WorkflowLogMessage log in GetWorkflowLogs())
                {
                    if (predicate(log))
                    {
                        return log;
                    }
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("等待流程日志超时。");
        }

        /// <summary>
        /// 获取当前已收集的流程日志快照。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回流程日志列表快照。</returns>
        /// <remarks>
        /// 由断言与同步等待流程调用，避免直接暴露并发队列。
        /// </remarks>
        private List<WorkflowEngine.WorkflowLogMessage> GetWorkflowLogs()
        {
            return _workflowLogs.ToList();
        }

        /// <summary>
        /// 追加一条首页日志文本。
        /// </summary>
        /// By:ChengLei
        /// <param name="level">日志级别。</param>
        /// <param name="source">日志来源。</param>
        /// <param name="kind">日志类别。</param>
        /// <param name="message">日志文本。</param>
        /// <remarks>
        /// 由首页命令协调器上下文调用，方便最终断言开始与停止流程是否被触发。
        /// </remarks>
        private void AddHomeLog(HomeLogLevel level, HomeLogSource source, HomeLogKind kind, string message)
        {
            _homeLogs.Add($"[{level}][{source}][{kind}] {message}");
        }

        /// <summary>
        /// 确保 PLC 命令执行结果为成功。
        /// </summary>
        /// By:ChengLei
        /// <param name="resultTask">待等待的 PLC 命令结果任务。</param>
        /// <returns>返回异步等待任务。</returns>
        /// <remarks>
        /// 由开始前复位与停止脉冲包装调用，失败时直接抛出异常中断测试。
        /// </remarks>
        private static async Task EnsureCommandSucceededAsync(Task<HomeCommandResult> resultTask)
        {
            HomeCommandResult result = await resultTask.ConfigureAwait(false);
            if (!result.Success)
            {
                throw new InvalidOperationException(result.Error);
            }
        }

        /// <summary>
        /// 构造天平全量回包。
        /// </summary>
        /// By:ChengLei
        /// <param name="rawWeight">原始整数重量。</param>
        /// <param name="dot">小数位数。</param>
        /// <returns>返回带 CRC 的天平回包。</returns>
        /// <remarks>
        /// 用于给天平模拟客户端生成与生产协议一致的读重量回包。
        /// </remarks>
        private static byte[] BuildBalanceAllResponse(int rawWeight, int dot)
        {
            byte[] frame = new byte[13];
            frame[0] = 0x01;
            frame[1] = 0x03;
            frame[2] = 0x08;
            frame[3] = (byte)((rawWeight >> 24) & 0xFF);
            frame[4] = (byte)((rawWeight >> 16) & 0xFF);
            frame[5] = (byte)((rawWeight >> 8) & 0xFF);
            frame[6] = (byte)(rawWeight & 0xFF);
            frame[7] = 0x00;
            frame[8] = (byte)dot;
            frame[9] = 0x00;
            frame[10] = 0x00;

            ushort crc = ComputeModbusCrc(frame, frame.Length - 2);
            frame[11] = (byte)(crc & 0xFF);
            frame[12] = (byte)((crc >> 8) & 0xFF);
            return frame;
        }

        /// <summary>
        /// 计算 Modbus CRC16。
        /// </summary>
        /// By:ChengLei
        /// <param name="data">待计算的数据。</param>
        /// <param name="length">参与计算的字节数。</param>
        /// <returns>返回 CRC16 结果。</returns>
        /// <remarks>
        /// 由天平模拟回包构造流程调用。
        /// </remarks>
        private static ushort ComputeModbusCrc(byte[] data, int length)
        {
            ushort crc = 0xFFFF;
            for (int i = 0; i < length; i++)
            {
                crc ^= data[i];
                for (int bit = 0; bit < 8; bit++)
                {
                    bool lsb = (crc & 0x0001) != 0;
                    crc >>= 1;
                    if (lsb)
                    {
                        crc ^= 0xA001;
                    }
                }
            }

            return crc;
        }

        /// <summary>
        /// 合并两个 16 位寄存器为 32 位整数。
        /// </summary>
        /// By:ChengLei
        /// <param name="lowWord">低 16 位。</param>
        /// <param name="highWord">高 16 位。</param>
        /// <returns>返回合并后的 32 位整数。</returns>
        /// <remarks>
        /// 由重量转 Z 结果断言调用。
        /// </remarks>
        private static int CombineInt32(ushort lowWord, ushort highWord)
        {
            return (highWord << 16) | lowWord;
        }

        /// <summary>
        /// 连接到当前 TCP 服务端并固定客户端本地端口。
        /// </summary>
        /// By:ChengLei
        /// <param name="serverPort">服务端监听端口。</param>
        /// <param name="localPort">客户端本地源端口。</param>
        /// <returns>返回已连接客户端。</returns>
        /// <remarks>
        /// 用于复用项目真实的 ClientIp 加源端口设备身份绑定逻辑。
        /// </remarks>
        private static async Task<TcpClient> ConnectFromLocalPortAsync(int serverPort, int localPort)
        {
            TcpClient client = new TcpClient(AddressFamily.InterNetwork);
            client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
            client.Client.Bind(new IPEndPoint(IPAddress.Loopback, localPort));
            await client.ConnectAsync(IPAddress.Loopback, serverPort);
            return client;
        }

        /// <summary>
        /// 等待指定逻辑设备达到期望连接状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="server">TCP 服务端实例。</param>
        /// <param name="deviceKey">逻辑设备键。</param>
        /// <param name="expected">期望连接状态。</param>
        /// <returns>返回等待任务。</returns>
        /// <remarks>
        /// 由连接模拟设备后调用，给服务端异步接收循环预留绑定时间。
        /// </remarks>
        private static async Task WaitForDeviceAsync(global::TcpServer server, string deviceKey, bool expected)
        {
            for (int i = 0; i < 40; i++)
            {
                if (server.IsDeviceConnected(deviceKey) == expected)
                {
                    return;
                }

                await Task.Delay(50);
            }

            Assert.Equal(expected, server.IsDeviceConnected(deviceKey));
        }

        /// <summary>
        /// 预留一个可用于本地绑定的 TCP 端口。
        /// </summary>
        /// By:ChengLei
        /// <returns>返回可用端口号。</returns>
        /// <remarks>
        /// 由模拟扫码枪和天平客户端创建前调用。
        /// </remarks>
        private static int ReserveTcpPort()
        {
            TcpListener listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }

        /// <summary>
        /// 读取自动属性的私有后备字段。
        /// </summary>
        /// By:ChengLei
        /// <param name="target">目标对象。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <returns>返回属性后备字段当前值。</returns>
        /// <remarks>
        /// 由 CommunicationManager 设备注册表替换流程调用。
        /// </remarks>
        private static object GetAutoPropertyBackingField(object target, string propertyName)
        {
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(target.GetType().Name, propertyName);
            return field.GetValue(target) ?? throw new InvalidOperationException($"字段 {propertyName} 当前为空。");
        }

        /// <summary>
        /// 写入自动属性的私有后备字段。
        /// </summary>
        /// By:ChengLei
        /// <param name="target">目标对象。</param>
        /// <param name="propertyName">属性名称。</param>
        /// <param name="value">要写入的字段值。</param>
        /// <remarks>
        /// 由 CommunicationManager 静态设备实例替换与恢复流程调用。
        /// </remarks>
        private static void SetAutoPropertyBackingField(object target, string propertyName, object value)
        {
            FieldInfo field = target.GetType().GetField($"<{propertyName}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new MissingFieldException(target.GetType().Name, propertyName);
            field.SetValue(target, value);
        }
    }

    /// <summary>
    /// 内存版 PLC 传输实现。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 通过内存字典模拟 M 位与 D 寄存器读写，并记录命令写入次数用于断言。
    /// </remarks>
    private sealed class FakeLx5vPlcTransport : ILx5vPlcTransport
    {
        private readonly object _syncRoot = new object();
        private readonly Dictionary<ushort, bool> _coils = new Dictionary<ushort, bool>();
        private readonly Dictionary<ushort, ushort> _registers = new Dictionary<ushort, ushort>();
        private readonly Dictionary<ushort, int> _coilWriteCount = new Dictionary<ushort, int>();

        /// <summary>
        /// 读取保持寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="startAddress">起始寄存器地址。</param>
        /// <param name="length">读取寄存器数量。</param>
        /// <returns>返回寄存器数组。</returns>
        /// <remarks>
        /// 由 Lx5vPlc 通过注入接口调用，测试中忽略从站地址差异。
        /// </remarks>
        public Task<ushort[]> ReadHoldingRegistersAsync(byte slaveAddress, ushort startAddress, ushort length)
        {
            ushort[] values = new ushort[length];
            lock (_syncRoot)
            {
                for (int i = 0; i < length; i++)
                {
                    values[i] = _registers.TryGetValue((ushort)(startAddress + i), out ushort value) ? value : (ushort)0;
                }
            }

            return Task.FromResult(values);
        }

        /// <summary>
        /// 读取线圈状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="startAddress">起始线圈地址。</param>
        /// <param name="length">读取线圈数量。</param>
        /// <returns>返回线圈状态数组。</returns>
        /// <remarks>
        /// 由 WorkflowEngine 轮询触发位和等待 OK 位时调用。
        /// </remarks>
        public Task<bool[]> ReadCoilsAsync(byte slaveAddress, ushort startAddress, ushort length)
        {
            bool[] values = new bool[length];
            lock (_syncRoot)
            {
                for (int i = 0; i < length; i++)
                {
                    values[i] = _coils.TryGetValue((ushort)(startAddress + i), out bool value) && value;
                }
            }

            return Task.FromResult(values);
        }

        /// <summary>
        /// 写入单个线圈。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="address">线圈地址。</param>
        /// <param name="value">目标线圈值。</param>
        /// <returns>返回写入任务。</returns>
        /// <remarks>
        /// 除了更新线圈状态外，还会对初始化完成位做最小模拟。
        /// </remarks>
        public Task WriteSingleCoilAsync(byte slaveAddress, ushort address, bool value)
        {
            lock (_syncRoot)
            {
                _coils[address] = value;
                _coilWriteCount[address] = _coilWriteCount.TryGetValue(address, out int count) ? count + 1 : 1;
            }

            if (address == 13 && value)
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(50);
                    SetCoil(14, true);
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 写入单个保持寄存器。
        /// </summary>
        /// By:ChengLei
        /// <param name="slaveAddress">PLC 从站地址。</param>
        /// <param name="address">寄存器地址。</param>
        /// <param name="value">寄存器值。</param>
        /// <returns>返回写入任务。</returns>
        /// <remarks>
        /// 由初始化参数下发与 Z 坐标写入流程调用。
        /// </remarks>
        public Task WriteSingleRegisterAsync(byte slaveAddress, ushort address, ushort value)
        {
            lock (_syncRoot)
            {
                _registers[address] = value;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// 设置指定线圈状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">线圈地址。</param>
        /// <param name="value">目标线圈值。</param>
        /// <remarks>
        /// 由测试驱动流程时直接设置允许位和 OK 位。
        /// </remarks>
        public void SetCoil(ushort address, bool value)
        {
            lock (_syncRoot)
            {
                _coils[address] = value;
            }
        }

        /// <summary>
        /// 获取指定线圈状态。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">线圈地址。</param>
        /// <returns>返回当前线圈值。</returns>
        /// <remarks>
        /// 由断言初始化完成位和其他状态时调用。
        /// </remarks>
        public bool GetCoil(ushort address)
        {
            lock (_syncRoot)
            {
                return _coils.TryGetValue(address, out bool value) && value;
            }
        }

        /// <summary>
        /// 获取指定寄存器值。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">寄存器地址。</param>
        /// <returns>返回当前寄存器值。</returns>
        /// <remarks>
        /// 由初始化参数和 Z 坐标写入结果断言调用。
        /// </remarks>
        public ushort GetRegister(ushort address)
        {
            lock (_syncRoot)
            {
                return _registers.TryGetValue(address, out ushort value) ? value : (ushort)0;
            }
        }

        /// <summary>
        /// 获取指定线圈被写入的次数。
        /// </summary>
        /// By:ChengLei
        /// <param name="address">线圈地址。</param>
        /// <returns>返回写入次数。</returns>
        /// <remarks>
        /// 由开始脉冲和停止脉冲断言调用。
        /// </remarks>
        public int GetWriteCount(ushort address)
        {
            lock (_syncRoot)
            {
                return _coilWriteCount.TryGetValue(address, out int count) ? count : 0;
            }
        }
    }
}
