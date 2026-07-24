using System;
using System.Collections.Generic;
using Newtonsoft.Json;

public record ApiResult
{
	[JsonProperty("code")]
	public int Code { get; protected set; }

	[JsonProperty("message")]
	public string Message { get; protected set; }

	[JsonProperty("data")]
	public object Data { get; protected set; }

	public static ApiResult Success => new ApiResult(0, "成功");

	public static ApiResult Error => new ApiResult(5001, "发生错误");

	public static ApiResult DataNotFound => new ApiResult(5002, "数据不存在");

	public static ApiResult RequireLogin => new ApiResult(8888, "未登陆或登陆失效");

	public static ApiResult NoPermission => new ApiResult(8001, "没有权限");

	protected ApiResult()
	{
	}

	private ApiResult(int code)
	{
		Code = code;
	}

	private ApiResult(string message)
	{
		Message = message;
	}

	private ApiResult(int code, string message)
		: this(code)
	{
		Message = message;
	}

	public ApiResult SetCode(int value)
	{
		Code = value;
		return this;
	}

	public ApiResult SetCode(Enum value)
	{
		Code = Convert.ToInt32(value);
		Message = value.ToString();
		return this;
	}

	public ApiResult SetMessage(string value)
	{
		Message = value;
		return this;
	}

	public ApiResult<T> SetData<T>(T data)
	{
		if (typeof(T) == typeof(object))
		{
			Data = data;
			return this as ApiResult<T>;
		}
		ApiResult<T> apiResult = new ApiResult<T>
		{
			Code = Code,
			Message = Message,
			Data = data
		};
		((ApiResult)apiResult).Data = data;
		return apiResult;
	}

	public ApiResult<OffsetListDto<T>> SetDataOffsetList<T>(long? offset, IEnumerable<T> list)
	{
		OffsetListDto<T> data = new OffsetListDto<T>(offset, list);
		ApiResult<OffsetListDto<T>> apiResult = new ApiResult<OffsetListDto<T>>
		{
			Code = Code,
			Message = Message,
			Data = data
		};
		((ApiResult)apiResult).Data = data;
		return apiResult;
	}
}
[JsonObject(/*Could not decode attribute arguments.*/)]
public record ApiResult<T> : ApiResult
{
	[JsonProperty("data")]
	public new T Data { get; internal set; }
}
