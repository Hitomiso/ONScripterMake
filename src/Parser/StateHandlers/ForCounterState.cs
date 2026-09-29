using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class ForCounterState : ParserStateHandler
{
    private Token? _variableToken;

    public ForCounterState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Variable, TryHandleVariable);
        _tokenHandlers.Add(TokenType.Equal, TryHandleAssign);
        Recycle();
    }

    public void Recycle()
    {
        _variableToken = null;
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue)
    {
        _variableToken = returnValue;
    }

    public override Token? OnReset(Token? returnValue)
    {
        return returnValue;
    }

    private void TryHandleVariable(Token token, out bool success)
    {
        if (_variableToken != null || token.Content != "%" && token.Content != "?")
        {
            success = false;
            return;
        }
        var varHandler = (VariableState)_context.PushState(OnsParserStateType.Variable);
        varHandler.TryRecycle(token, out success);
    }

    private void TryHandleAssign(Token token, out bool success)
    {
        if (_variableToken == null || token.Content != "=")
        {
            success = false;
            return;
        }
        _context.PopState(_variableToken);
        success = true;
    }
}
#nullable restore
