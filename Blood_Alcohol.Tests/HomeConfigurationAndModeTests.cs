using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Blood_Alcohol.Models;
using Blood_Alcohol.Services;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Tests;

/// <summary>
/// 首页配置保存与档位同步测试。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 覆盖参数页保存校验和 PLC 档位实时同步两类容易在现场失真的逻辑。
/// </remarks>
public class HomeConfigurationAndModeTests
{
    /// <summary>
    /// 验证四路温控站号配置不允许重复。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 一位站号和两位站号表示同一设备时也必须识别为重复，避免后台监控重复打到同一台温控。
    /// </remarks>
    [Fact]
    public void ProcessParameterConfig_DuplicateTemperatureStations_ReturnsValidationError()
    {
        ProcessParameterConfig config = new ProcessParameterConfig
        {
            HeatingBoxTemperatureStation = "01",
            QuantitativeLoopTemperatureStation = "1"
        };

        List<string> errors = config.Validate();

        Assert.Contains(errors, error => error.Contains("不能使用相同站号", StringComparison.Ordinal));
    }

    /// <summary>
    /// 验证参数页保存时会拦截非法配置且不会落盘。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 通过真实执行 SaveConfigCommand 确认保存链路已经接入配置校验，而不是只测配置对象本身。
    /// </remarks>
    [Fact]
    public async Task ParameterConfigViewModel_SaveConfig_InvalidValue_DoesNotPersist()
    {
        string configPath = GetProcessParameterConfigPath();
        ConfigBackupSnapshot backup = await BackupConfigFileAsync(configPath);

        try
        {
            DeleteConfigFile(configPath);

            ParameterConfigViewModel viewModel = new ParameterConfigViewModel
            {
                HeatingBoxTemperature = 500
            };

            viewModel.SaveConfigCommand.Execute(null);

            Assert.Contains("保存失败", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Contains("加热箱温度", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.False(File.Exists(configPath));
        }
        finally
        {
            await RestoreConfigFileAsync(configPath, backup);
        }
    }

    /// <summary>
    /// 验证参数页保存成功时会把单数字站号规范化为两位文本后落盘。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 用于确认保存逻辑既保留合法输入的自动补零体验，又不会再静默吞掉非法配置。
    /// </remarks>
    [Fact]
    public async Task ParameterConfigViewModel_SaveConfig_ValidStation_NormalizesAndPersists()
    {
        string configPath = GetProcessParameterConfigPath();
        ConfigBackupSnapshot backup = await BackupConfigFileAsync(configPath);

        try
        {
            DeleteConfigFile(configPath);

            ParameterConfigViewModel viewModel = new ParameterConfigViewModel
            {
                HeatingBoxTemperatureStation = "4",
                QuantitativeLoopTemperatureStation = "12",
                TransferLineTemperatureStation = "23",
                ReservedTemperatureStation = "34"
            };

            viewModel.SaveConfigCommand.Execute(null);

            ProcessParameterConfig savedConfig = new ConfigService<ProcessParameterConfig>("ProcessParameterConfig.json").Load();

            Assert.Contains("参数配置已保存", viewModel.StatusMessage, StringComparison.Ordinal);
            Assert.Equal("04", savedConfig.HeatingBoxTemperatureStation);
            Assert.Equal("12", savedConfig.QuantitativeLoopTemperatureStation);
            Assert.Equal("23", savedConfig.TransferLineTemperatureStation);
            Assert.Equal("34", savedConfig.ReservedTemperatureStation);
        }
        finally
        {
            await RestoreConfigFileAsync(configPath, backup);
        }
    }

    /// <summary>
    /// 验证档位监控循环会按 PLC 的 M10 变化持续同步软件档位。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回异步测试任务。</returns>
    /// <remarks>
    /// 先模拟手动再模拟自动，确认监控循环不会只信按钮点击后的内存状态。
    /// </remarks>
    [Fact]
    public async Task RunOperationModeMonitorAsync_WhenPlcModeChanges_UpdatesSoftwareMode()
    {
        Queue<HomePlcBoolReadResult> reads = new Queue<HomePlcBoolReadResult>(
            new[]
            {
                new HomePlcBoolReadResult(true, false, string.Empty),
                new HomePlcBoolReadResult(true, true, string.Empty)
            });
        List<OperationMode> appliedModes = new List<OperationMode>();
        List<string> logs = new List<string>();
        using CancellationTokenSource cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));

        Task monitorTask = HomeMonitorLoops.RunOperationModeMonitorAsync(
            new HomeOperationModeMonitorContext
            {
                PollInterval = TimeSpan.FromMilliseconds(5),
                IsPlcConnected = () => true,
                ReadAutoModeAsync = token => Task.FromResult(reads.Count > 0 ? reads.Dequeue() : new HomePlcBoolReadResult(true, true, string.Empty)),
                RunOnUiThread = action => action(),
                SetOperationMode = mode =>
                {
                    appliedModes.Add(mode);
                    if (appliedModes.Count >= 2)
                    {
                        cts.Cancel();
                    }
                },
                AddLog = (level, source, kind, message) => logs.Add(message)
            },
            cts.Token);

        await monitorTask;

        Assert.Equal(new[] { OperationMode.Manual, OperationMode.Auto }, appliedModes);
        Assert.DoesNotContain(logs, message => message.Contains("档位同步失败", StringComparison.Ordinal));
    }

    /// <summary>
    /// 获取测试配置文件路径。
    /// </summary>
    /// By:ChengLei
    /// <returns>返回测试运行目录下的流程参数配置文件绝对路径。</returns>
    /// <remarks>
    /// 参数页视图模型固定把配置写到 AppDomain.BaseDirectory 下的 Config 目录，测试需要直接备份该文件。
    /// </remarks>
    private static string GetProcessParameterConfigPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Config", "ProcessParameterConfig.json");
    }

    /// <summary>
    /// 备份测试前的配置文件内容。
    /// </summary>
    /// By:ChengLei
    /// <param name="path">待备份配置文件路径。</param>
    /// <returns>返回配置文件备份快照。</returns>
    /// <remarks>
    /// 用于在测试结束后恢复现场，避免自测污染后续运行或其他测试。
    /// </remarks>
    private static async Task<ConfigBackupSnapshot> BackupConfigFileAsync(string path)
    {
        if (!File.Exists(path))
        {
            return new ConfigBackupSnapshot(false, string.Empty);
        }

        string content = await File.ReadAllTextAsync(path);
        return new ConfigBackupSnapshot(true, content);
    }

    /// <summary>
    /// 恢复测试前的配置文件内容。
    /// </summary>
    /// By:ChengLei
    /// <param name="path">需要恢复的配置文件路径。</param>
    /// <param name="backup">测试前保存的配置文件备份快照。</param>
    /// <returns>返回异步恢复任务。</returns>
    /// <remarks>
    /// 若测试前不存在配置文件，则恢复阶段会删除测试期间新建的文件。
    /// </remarks>
    private static async Task RestoreConfigFileAsync(string path, ConfigBackupSnapshot backup)
    {
        if (!backup.Existed)
        {
            DeleteConfigFile(path);
            return;
        }

        string? directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(path, backup.Content);
    }

    /// <summary>
    /// 删除测试用配置文件。
    /// </summary>
    /// By:ChengLei
    /// <param name="path">需要删除的配置文件路径。</param>
    /// <remarks>
    /// 只删除测试运行目录下的流程参数配置文件，不处理其他路径。
    /// </remarks>
    private static void DeleteConfigFile(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

/// <summary>
/// 作用
/// 测试配置文件备份快照
internal readonly record struct ConfigBackupSnapshot(bool Existed, string Content);
