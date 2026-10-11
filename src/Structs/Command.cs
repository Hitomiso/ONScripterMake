using System;

namespace ONSMake;

#nullable enable
public class Command
{
    public string Name;
    public Parameter[] Parameters;

    public Command(string name, Parameter[] parameters)
    {
        Name = name;
        Parameters = parameters;
    }
}
#nullable restore