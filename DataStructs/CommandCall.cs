using System;
using System.Linq;
using System.Collections.Generic;

namespace Hitomiso.ONScripterMake;

public struct CommandCall
{
    public Line CalledAt;
    public string CommandName;
    public string Parameters;

    public CommandCall(Line calledAt, string commandName, string parameters)
    {
        CalledAt = calledAt;
        CommandName = commandName;
        Parameters = parameters;
    }
}
