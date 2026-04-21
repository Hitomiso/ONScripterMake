using System;
using System.Linq;
using System.Collections.Generic;

namespace Hitomiso.ONScripterMake;

#nullable enable
public class Parameter
{
    public DataType Type;
    public string Name;
    public string[]? EnumValues;
    public string? Description;
	public bool Repeat;

    public Parameter(DataType type, string name, string[]? enumValues = null, string? description = null, bool repeat = false)
    {
        Type = type;
        Name = name;
        EnumValues = enumValues;
        Description = description;
		Repeat = repeat;
    }
}
#nullable restore