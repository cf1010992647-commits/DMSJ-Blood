using System;
using Blood_Alcohol.Services;

namespace Blood_Alcohol.ViewModels;

/// <summary>
/// 首页日志文本解析器。
/// </summary>
/// By:ChengLei
/// <remarks>
/// 由首页把流程日志文本映射为首页日志枚举和扫码内容。
/// </remarks>
internal static class HomeLogParser
{
	/// <summary>
	/// 从流程日志文本提取条码内容。
	/// </summary>
	/// By:ChengLei
	/// <param name="message">日志消息文本。</param>
	/// <returns>返回提取到的条码文本，未命中时返回空。</returns>
	/// <remarks>
	/// 由首页流程日志映射场景调用。
	/// </remarks>
	public static string? ExtractScanCode(string message)
	{
		if (string.IsNullOrWhiteSpace(message))
		{
			return null;
		}

		const string prefix = "扫码成功：";
		int start = message.IndexOf(prefix, StringComparison.Ordinal);
		if (start < 0)
		{
			return null;
		}

		int contentStart = start + prefix.Length;
		int contentEnd = message.IndexOf('，', contentStart);
		string value = contentEnd > contentStart
			? message.Substring(contentStart, contentEnd - contentStart)
			: message.Substring(contentStart);

		value = value.Trim();
		return string.IsNullOrWhiteSpace(value) ? null : value;
	}

	/// <summary>
	/// 将日志级别文本解析为首页日志级别。
	/// </summary>
	/// By:ChengLei
	/// <param name="levelText">日志级别文本。</param>
	/// <returns>返回首页日志级别。</returns>
	/// <remarks>
	/// 由通信日志和流程日志映射调用。
	/// </remarks>
	public static HomeLogLevel ParseLevel(string levelText)
	{
		if (!string.IsNullOrWhiteSpace(levelText))
		{
			if (levelText.Contains("错误") || levelText.Contains("閿欒"))
			{
				return HomeLogLevel.Error;
			}

			if (levelText.Contains("警告") || levelText.Contains("璀﹀憡"))
			{
				return HomeLogLevel.Warning;
			}
		}

		return HomeLogLevel.Info;
	}

	/// <summary>
	/// 将日志类型文本解析为首页日志类型。
	/// </summary>
	/// By:ChengLei
	/// <param name="kindText">日志类型文本。</param>
	/// <returns>返回首页日志类型。</returns>
	/// <remarks>
	/// 由流程日志映射调用。
	/// </remarks>
	public static HomeLogKind ParseKind(string kindText)
	{
		if (!string.IsNullOrWhiteSpace(kindText)
			&& (kindText.Contains("检测") || kindText.Contains("妫€娴")))
		{
			return HomeLogKind.Detection;
		}

		return HomeLogKind.Operation;
	}
}
