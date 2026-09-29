using System;

namespace ONSMake;

#nullable enable
public readonly record struct Result<T, E>
{
	public readonly T Value;
	public readonly E Error;
	public readonly bool IsError;
	
	private Result(T val, E error, bool isError)
	{
		Value = val;
		Error = error;
		IsError = isError;
	}
	
    public static Result<T, E> Ok(T val) => new(val, default, false);
    public static Result<T, E> Fail(E error) => new(default, error, true);
}
#nullable restore