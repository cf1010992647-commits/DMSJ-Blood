using System.Collections.ObjectModel;
using System.Text;
using Blood_Alcohol.Communication.Protocols;
using Blood_Alcohol.Models;
using Blood_Alcohol.ViewModels;

namespace Blood_Alcohol.Tests;

/// <summary>
/// 温控配置与协议行为测试。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 覆盖第四路温控参数接入和按站号组帧的关键行为。
/// </remarks>
public class TemperatureConfigurationTests
{
    /// <summary>
    /// 验证首页条件区不展示预留温控温度。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 预留温控仍参与后台监控和配置 但不应占用首页条件展示位。
    /// </remarks>
    [Fact]
    public void HomeConditionPresenter_Apply_DoesNotIncludeReservedTemperature()
    {
        ObservableCollection<ConditionItemViewModel> conditions = new ObservableCollection<ConditionItemViewModel>();
        ProcessParameterConfig config = new ProcessParameterConfig
        {
            HeatingBoxTemperature = 60.0,
            QuantitativeLoopTemperature = 80.0,
            TransferLineTemperature = 120.0,
            ReservedTemperature = 66.5
        };

        HomeConditionPresenter.Apply(conditions, config);

        Assert.Equal(7, conditions.Count);
        Assert.DoesNotContain(conditions, item => item.Name == "预留温控温度");
        Assert.Equal("10", conditions[3].Value);
    }

    /// <summary>
    /// 验证第四路预留温控温度会参与参数合法性校验。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 超出允许范围时必须返回明确错误信息。
    /// </remarks>
    [Fact]
    public void ProcessParameterConfig_ReservedTemperatureOutOfRange_ReturnsValidationError()
    {
        ProcessParameterConfig config = new ProcessParameterConfig
        {
            ReservedTemperature = 500
        };

        List<string> errors = config.Validate();

        Assert.Contains(errors, error => error.Contains("预留温控温度"));
    }

    /// <summary>
    /// 验证温控站号配置会参与参数合法性校验。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 设置页保存到 JSON 前需要拦住空值和非法站号。
    /// </remarks>
    [Fact]
    public void ProcessParameterConfig_InvalidStation_ReturnsValidationError()
    {
        ProcessParameterConfig config = new ProcessParameterConfig
        {
            HeatingBoxTemperatureStation = "A1"
        };

        List<string> errors = config.Validate();

        Assert.Contains(errors, error => error.Contains("加热箱温控站号"));
    }

    /// <summary>
    /// 验证温控协议会按指定站号生成读取当前温度命令。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 四路温控共用一条连接时 需要靠站号区分不同控制器。
    /// </remarks>
    [Fact]
    public void ShimadenReadPv_WithDifferentStation_UsesStationPrefix()
    {
        ShimadenSrs11A service = new ShimadenSrs11A("04", "1");

        byte[] frame = service.ReadPV();
        string body = Encoding.ASCII.GetString(frame, 1, frame.Length - 3);

        Assert.StartsWith("041R01000", body);
    }
}
