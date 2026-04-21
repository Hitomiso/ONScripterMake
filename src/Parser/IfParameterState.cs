using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class IfParametersState : NormalParametersState
{
    public IfParametersState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Remove(TokenType.Comma);
        Recycle();
    }

    public void Recycle()
    {
        base.Recycle(true);
    }
}
