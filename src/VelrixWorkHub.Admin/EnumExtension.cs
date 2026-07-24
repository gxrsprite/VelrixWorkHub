using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;

public static class EnumExtension
{
	public static string ToDescription(this Enum item)
	{
		string text = item.ToString();
		return (item.GetType().GetField(text)?.GetCustomAttribute<DescriptionAttribute>(inherit: false))?.Description ?? text;
	}

	public static string ToNameWithDescription(this Enum item)
	{
		string text = item.ToString();
		DescriptionAttribute descriptionAttribute = item.GetType().GetField(text)?.GetCustomAttribute<DescriptionAttribute>(inherit: false);
		return text + ((descriptionAttribute == null || descriptionAttribute.Description.IsNull()) ? "" : ("(" + descriptionAttribute?.Description + ")"));
	}

	public static long ToInt64(this Enum item)
	{
		return Convert.ToInt64(item);
	}

	public static string[] ToStableStringArray<TEnum>(this TEnum value) where TEnum : Enum
	{
		if (typeof(TEnum).GetCustomAttribute<FlagsAttribute>() != null)
		{
			string[] array = (from TEnum v in Enum.GetValues(typeof(TEnum))
				where Convert.ToInt64(value) != 0L && (Convert.ToInt64(value) & Convert.ToInt64(v)) == Convert.ToInt64(v)
				select v.ToString()).ToArray();
			return (array.Length != 0) ? array : new string[1] { Convert.ToInt64(value).ToString() };
		}
		string name = Enum.GetName(typeof(TEnum), value);
		return new string[1] { name ?? Convert.ToInt64(value).ToString() };
	}

	public static List<Dictionary<string, object>> ToList(this Enum value, bool ignoreNull = false)
	{
		Type type = value.GetType();
		if (!type.IsEnum)
		{
			return null;
		}
		return (from Enum x in Enum.GetValues(type)
			where !ignoreNull || !x.ToString().Equals("Null")
			select new Dictionary<string, object>
			{
				["Label"] = x.ToDescription(),
				["Value"] = x
			}).ToList();
	}

	public static List<Dictionary<string, object>> ToList<T>(bool ignoreNull = false)
	{
		Type typeFromHandle = typeof(T);
		if (!typeFromHandle.IsEnum)
		{
			return null;
		}
		return (from Enum x in Enum.GetValues(typeFromHandle)
			where !ignoreNull || !x.ToString().Equals("Null")
			select new Dictionary<string, object>
			{
				["Label"] = x.ToDescription(),
				["Value"] = x
			}).ToList();
	}
}
