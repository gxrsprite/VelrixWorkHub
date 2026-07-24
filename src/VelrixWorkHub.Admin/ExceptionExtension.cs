using System;
using System.Collections;
using System.Text;

public static class ExceptionExtension
{
	/// <summary>
	/// 手动将一个 Exception 对象及其所有 InnerException 格式化为详细的文本字符串。
	/// </summary>
	/// <param name="ex">要格式化的异常对象。</param>
	/// <param name="includeStackTrace">是否包含堆栈跟踪信息。</param>
	/// <returns>一个详细的、格式化后的异常信息字符串。</returns>
	public static string ToFormatString(this Exception ex, bool includeStackTrace = true)
	{
		if (ex == null)
		{
			return string.Empty;
		}
		StringBuilder stringBuilder = new StringBuilder();
		int indentLevel = 0;
		AppendException(stringBuilder, ex, indentLevel, includeStackTrace);
		return stringBuilder.ToString();
	}

	private static void AppendException(StringBuilder sb, Exception ex, int indentLevel, bool includeStackTrace)
	{
		string value = new string(' ', indentLevel * 2);
		StringBuilder stringBuilder;
		StringBuilder.AppendInterpolatedStringHandler handler;
		if (indentLevel > 0)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder2 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(24, 2, stringBuilder);
			handler.AppendFormatted(value);
			handler.AppendLiteral("---> (Inner Exception #");
			handler.AppendFormatted(indentLevel);
			handler.AppendLiteral(")");
			stringBuilder2.AppendLine(ref handler);
		}
		stringBuilder = sb;
		StringBuilder stringBuilder3 = stringBuilder;
		handler = new StringBuilder.AppendInterpolatedStringHandler(2, 3, stringBuilder);
		handler.AppendFormatted(value);
		handler.AppendFormatted(ex.GetType().FullName);
		handler.AppendLiteral(": ");
		handler.AppendFormatted(ex.Message);
		stringBuilder3.AppendLine(ref handler);
		if (ex.Data.Count > 0)
		{
			stringBuilder = sb;
			StringBuilder stringBuilder4 = stringBuilder;
			handler = new StringBuilder.AppendInterpolatedStringHandler(18, 1, stringBuilder);
			handler.AppendFormatted(value);
			handler.AppendLiteral("  Additional Data:");
			stringBuilder4.AppendLine(ref handler);
			foreach (DictionaryEntry datum in ex.Data)
			{
				stringBuilder = sb;
				StringBuilder stringBuilder5 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(6, 3, stringBuilder);
				handler.AppendFormatted(value);
				handler.AppendLiteral("    ");
				handler.AppendFormatted<object>(datum.Key);
				handler.AppendLiteral(": ");
				handler.AppendFormatted<object>(datum.Value);
				stringBuilder5.AppendLine(ref handler);
			}
		}
		if (includeStackTrace && !string.IsNullOrEmpty(ex.StackTrace))
		{
			string[] array = ex.StackTrace.Split(new string[1] { Environment.NewLine }, StringSplitOptions.None);
			string[] array2 = array;
			foreach (string text in array2)
			{
				stringBuilder = sb;
				StringBuilder stringBuilder6 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(3, 2, stringBuilder);
				handler.AppendFormatted(value);
				handler.AppendLiteral("   ");
				handler.AppendFormatted(text.Trim());
				stringBuilder6.AppendLine(ref handler);
			}
		}
		if (ex.InnerException != null)
		{
			if (includeStackTrace)
			{
				stringBuilder = sb;
				StringBuilder stringBuilder7 = stringBuilder;
				handler = new StringBuilder.AppendInterpolatedStringHandler(45, 1, stringBuilder);
				handler.AppendFormatted(value);
				handler.AppendLiteral("   --- End of inner exception stack trace ---");
				stringBuilder7.AppendLine(ref handler);
			}
			AppendException(sb, ex.InnerException, indentLevel + 1, includeStackTrace);
		}
	}
}
