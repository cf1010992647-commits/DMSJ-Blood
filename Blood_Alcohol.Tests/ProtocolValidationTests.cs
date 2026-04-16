using Blood_Alcohol.Communication.Protocols;
using Blood_Alcohol.Protocols;

namespace Blood_Alcohol.Tests;

/// <summary>
/// 协议层严格校验测试。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 验证好帧可以解析，坏帧会明确失败。
/// </remarks>
public class ProtocolValidationTests
{
    /// <summary>
    /// 验证天平好帧通过 CRC 与字段校验。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 使用一次性读取重量响应帧，原始重量 123，小数位 3。
    /// </remarks>
    [Fact]
    public void BalanceReadWeight_ValidFrame_ReturnsWeight()
    {
        BalanceProtocolService service = new BalanceProtocolService();
        byte[] frame = new byte[]
        {
            0x01, 0x03, 0x08, 0x00, 0x00, 0x00, 0x7B, 0x00, 0x03, 0x00, 0x00, 0x81, 0xDD
        };

        double weight = service.ReadWeight(frame);

        Assert.Equal(0.123d, weight, precision: 3);
    }

    /// <summary>
    /// 验证天平坏 CRC 帧会失败。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 仅篡改 CRC 字节，其他字段保持正确。
    /// </remarks>
    [Fact]
    public void BalanceReadWeight_InvalidCrc_Throws()
    {
        BalanceProtocolService service = new BalanceProtocolService();
        byte[] frame = new byte[]
        {
            0x01, 0x03, 0x08, 0x00, 0x00, 0x00, 0x7B, 0x00, 0x03, 0x00, 0x00, 0x00, 0x00
        };

        Exception ex = Assert.Throws<Exception>(() => service.ReadWeight(frame));

        Assert.Contains("CRC", ex.Message);
    }

    /// <summary>
    /// 验证扫码结果会清洗不可见字符。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 空字符、回车和换行不会进入最终条码。
    /// </remarks>
    [Fact]
    public void ScannerParseCode_RemovesInvisibleCharacters()
    {
        ScannerProtocolService service = new ScannerProtocolService();

        string code = service.ParseCode(new byte[] { 0x00, 0x42, 0x41, 0x0D, 0x32, 0x30, 0x0A, 0x31 });

        Assert.Equal("BA201", code);
    }

    /// <summary>
    /// 验证扫码清洗后为空会失败。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 只包含不可见字符的扫码数据不能进入业务流程。
    /// </remarks>
    [Fact]
    public void ScannerParseCode_BlankAfterSanitize_Throws()
    {
        ScannerProtocolService service = new ScannerProtocolService();

        Assert.Throws<Exception>(() => service.ParseCode(new byte[] { 0x00, 0x0D, 0x0A, 0x09 }));
    }

    /// <summary>
    /// 验证温控好帧可以解析温度。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// 帧体保留现有逗号后四位十六进制温度值解析规则。
    /// </remarks>
    [Fact]
    public void ShimadenParseTemperature_ValidFrame_ReturnsTemperature()
    {
        ShimadenSrs11A service = new ShimadenSrs11A("01", "1");
        byte[] frame = BuildShimadenFrame("011R01000,00FA");

        double temperature = service.ParseTemperature(frame);

        Assert.Equal(25.0d, temperature, precision: 1);
    }

    /// <summary>
    /// 验证温控坏帧头会失败。
    /// </summary>
    /// By:ChengLei
    /// <remarks>
    /// STX 错误时必须明确抛出解析异常。
    /// </remarks>
    [Fact]
    public void ShimadenParseTemperature_InvalidStx_Throws()
    {
        ShimadenSrs11A service = new ShimadenSrs11A("01", "1");
        byte[] frame = BuildShimadenFrame("011R01000,00FA");
        frame[0] = 0x00;

        Exception ex = Assert.Throws<Exception>(() => service.ParseTemperature(frame));

        Assert.Contains("帧头", ex.Message);
    }

    /// <summary>
    /// 构造温控测试响应帧。
    /// </summary>
    /// By:ChengLei
    /// <param name="body">响应帧体。</param>
    /// <returns>返回完整温控响应帧。</returns>
    /// <remarks>
    /// 测试用工具方法，按 STX + body + ETX + CR 生成帧。
    /// </remarks>
    private static byte[] BuildShimadenFrame(string body)
    {
        byte[] bodyBytes = System.Text.Encoding.ASCII.GetBytes(body);
        byte[] frame = new byte[bodyBytes.Length + 3];
        frame[0] = 0x02;
        Array.Copy(bodyBytes, 0, frame, 1, bodyBytes.Length);
        frame[^2] = 0x03;
        frame[^1] = 0x0D;
        return frame;
    }
}
