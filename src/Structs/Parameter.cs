using System;
using System.Linq;
using System.Collections.Generic;

namespace ONSMake;

#nullable enable
public readonly record struct Parameter
{
    public readonly DataType Type;
    public readonly string[]? EnumValues;
	public readonly bool Repeat;

    public Parameter(DataType type, string[]? enumValues = null, bool repeat = false)
    {
        Type = type;
        EnumValues = enumValues;
		Repeat = repeat;
    }
}
#nullable restore