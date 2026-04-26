using System;
using System.Linq;
using System.Collections.Generic;

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
#nullable restore