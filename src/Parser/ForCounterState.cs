using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class ForCounterState : ParserStateHandler
{
    private Token? _variableToken;

    public ForCounterState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Variable, HandleVariable);
        _tokenHandlers.Add(TokenType.Equal, HandleAssign);
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

    private void HandleVariable(Token token)
    {
        if (_variableToken != null)
            throw new UnexpectedTokenException(token);
        if (token.Content != "%" && token.Content != "?")
            throw new UnexpectedTokenException(token);
        var varHandler = (VariableState)_parser.PushState(OnsParserStateType.Variable);
        varHandler.Recycle(token);
    }

    private void HandleAssign(Token token)
    {
        if (_variableToken == null || token.Content != "=")
            throw new UnexpectedTokenException(token);
        _parser.PopState(_variableToken);
    }
}
#nullable restore
