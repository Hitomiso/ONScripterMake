using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Hitomiso.ONScripterMake;

#nullable enable
public class Command
{
    public string Name;
    public Parameter[] Parameters;
    public string? Description;

    public Command(string name, Parameter[] parameters, string? description = null)
    {
        Name = name;
        Parameters = parameters;
        Description = description;
    }
}

public class Parameter
{
    public DataType Type;
    public string Name;
    public string[]? EnumValues;
    public string? Description;
	
    public Parameter(DataType type, string name, string[]? enumValues = null, string? description = null)
    {
        Type = type;
        Name = name;
        EnumValues = enumValues;
        Description = description;
    }
}

public enum DataType
{
    Name,
    Label,
    Num,
    Str,
    NumVar,
    StrVar,
    Color,
    Effect,
    Enum,
    Condition
}
#nullable restore