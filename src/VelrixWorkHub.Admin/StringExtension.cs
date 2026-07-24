using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using BootstrapBlazor.Components;

public static class StringExtension
{
	private static readonly string _chars = "0123456789";

	private static readonly char[] _constant = new char[62]
	{
		'0', '1', '2', '3', '4', '5', '6', '7', '8', '9',
		'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
		'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
		'u', 'v', 'w', 'x', 'y', 'z', 'A', 'B', 'C', 'D',
		'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M', 'N',
		'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X',
		'Y', 'Z'
	};

	/// <summary>
	/// 判断字符串是否为Null、空
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static bool IsNull(this string s)
	{
		return string.IsNullOrWhiteSpace(s);
	}

	public static string IsNull(this string s1, string s2)
	{
		return string.IsNullOrWhiteSpace(s1) ? s2 : s1;
	}

	/// <summary>
	/// 判断字符串是否不为Null、空
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static bool NotNull(this string s)
	{
		return !string.IsNullOrWhiteSpace(s);
	}

	/// <summary>
	/// 与字符串进行比较，忽略大小写
	/// </summary>
	/// <param name="s"></param>
	/// <param name="value"></param>
	/// <returns></returns>
	public static bool EqualsIgnoreCase(this string s, string value)
	{
		return s.Equals(value, StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 首字母转小写
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static string FirstCharToLower(this string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return s.First().ToString().ToLower() + s.Substring(1);
	}

	/// <summary>
	/// 首字母转大写
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static string FirstCharToUpper(this string s)
	{
		if (string.IsNullOrEmpty(s))
		{
			return s;
		}
		return s.First().ToString().ToUpper() + s.Substring(1);
	}

	/// <summary>
	/// 转为Base64，UTF-8格式
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static string ToBase64(this string s)
	{
		return s.ToBase64(Encoding.UTF8);
	}

	/// <summary>
	/// 转为Base64
	/// </summary>
	/// <param name="s"></param>
	/// <param name="encoding">编码</param>
	/// <returns></returns>
	public static string ToBase64(this string s, Encoding encoding)
	{
		if (s.IsNull())
		{
			return string.Empty;
		}
		byte[] bytes = encoding.GetBytes(s);
		return Convert.ToBase64String(bytes);
	}

	/// <summary>
	/// 转换为16进制
	/// </summary>
	/// <param name="bytes"></param>
	/// <param name="lowerCase">是否小写</param>
	/// <returns></returns>
	public static string ToHex(this byte[] bytes, bool lowerCase = true)
	{
		if (bytes == null)
		{
			return null;
		}
		StringBuilder stringBuilder = new StringBuilder();
		string text = (lowerCase ? "x2" : "X2");
		for (int i = 0; i < bytes.Length; i++)
		{
			stringBuilder.Append(bytes[i].ToString(text));
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// 16进制转字节数组
	/// </summary>
	/// <param name="s"></param>
	/// <returns></returns>
	public static byte[] HexToBytes(this string s)
	{
		if (s.IsNull())
		{
			return null;
		}
		byte[] array = new byte[s.Length / 2];
		for (int i = 0; i < s.Length / 2; i++)
		{
			int num = Convert.ToInt32(s.Substring(i * 2, 2), 16);
			array[i] = (byte)num;
		}
		return array;
	}

	public static string ToPath(this string s)
	{
		if (s.IsNull())
		{
			return string.Empty;
		}
		return s.Replace("\\", "/");
	}

	public static string ToMask(this string s)
	{
		if (s.IsNull())
		{
			return string.Empty;
		}
		return Regex.Replace(Regex.Replace(Regex.Replace(s, "(\\d{3})\\d{4}(\\d{4})", (Match m) => m.Groups[1]?.Value + "****" + m.Groups[2]?.Value), "(?<=.{2})[^@]+(?=.{2}@)", "****"), "(\\d{1,3})\\.\\d{1,3}\\.\\d{1,3}\\.\\d{1,3}", (Match m) => m.Groups[1]?.Value + ".*.*." + m.Groups[4]?.Value);
	}

	/// <summary>
	/// 生成随机字符串，默认32位
	/// </summary>
	/// <param name="length">随机数长度</param>
	/// <returns></returns>
	public static string GenerateRandom(this string _, int length = 32)
	{
		StringBuilder stringBuilder = new StringBuilder();
		Random random = new Random();
		for (int i = 0; i < length; i++)
		{
			stringBuilder.Append(_constant[random.Next(_constant.Length)]);
		}
		return stringBuilder.ToString();
	}

	/// <summary>
	/// 生成随机6位数
	/// </summary>
	/// <param name="length"></param>
	/// <returns></returns>
	public static string GenerateRandomNumber(this string _, int length = 6)
	{
		Random random = new Random();
		return new string((from s in Enumerable.Repeat(_chars, length)
			select s[random.Next(s.Length)]).ToArray());
	}

	/// <summary>
	/// 是否为图片
	/// </summary>
	/// <returns></returns>
	public static bool IsImage(this string path)
	{
		string text = ".gif|.jpg|.jpeg|.png";
		return text.Contains(Path.GetExtension(path) ?? "", StringComparison.OrdinalIgnoreCase);
	}

	/// <summary>
	/// 是否为图片
	/// </summary>
	/// <returns></returns>
	public static bool IsImage(this UploadFile file)
	{
		string text = ".gif|.jpg|.jpeg|.png";
		return text.Contains(file.GetExtension() ?? "", StringComparison.OrdinalIgnoreCase);
	}
}
