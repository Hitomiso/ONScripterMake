using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

public class IfParametersState : NormalParametersState
{
    public IfParametersState(ParserContext context) : base(context)
    {
        _tokenHandlers.Remove(TokenType.Comma);
        Recycle();
    }

    public void Recycle()
    {
        base.Recycle(true);
    }
}
