# C# 注释规范

所有方法必须使用 XML 注释，并严格遵守以下格式：

/// <summary>
/// 功能描述（中文）
/// </summary>
/// By:ChengLei
/// <param name="参数名">参数说明</param>
/// <returns>返回值说明</returns>
/// <remarks>
/// 补充说明（如TCP粘包处理等）
/// </remarks>

规则：
- 必须使用中文,不需要中文句号,描述人性化
- 不允许省略 param / returns（如果有）
- summary 必须描述行为，而不是翻译函数名

所有对象必须使用 XML 注释，并严格遵守以下格式：
/// 作用

规则：
- 必须使用中文,不需要中文句号,描述人性化