using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class VariableState : ParserStateHandler
{
    private Token _varToken;

    public VariableState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TryHandleChild);
        _tokenHandlers.Add(TokenType.Number, TryHandleChild);
        _tokenHandlers.Add(TokenType.Variable, TryHandleVariable);
    }

    public void TryRecycle(Token varToken, out bool success)
    {
        if (varToken.Type != TokenType.Variable && varToken.Type != TokenType.IndexVariable || varToken.Content.SequenceEqual("?"))
        {
            success = false;
            return;
        }
        _varToken = varToken;
        IsReady = true;
        success = true;
    }

    public override void OnReturn(Token? returnValue)
    {
        if (returnValue != null)
            TryHandleChild((Token)returnValue, out _);
    }

    public override Token? OnReset(Token? returnValue)
    {
        if (returnValue != null)
            _varToken.Children.Add((Token)returnValue);
        return _varToken;
    }

    private void TryHandleChild(Token token, out bool success)
    {
        _varToken.Children.Add(token);
        _context.PopState(_varToken);
        success = true;
    }

    private void TryHandleVariable(Token token, out bool success)
    {
        if (token.Content.SequenceEqual("%"))
        {
            var varHandler = (VariableState)_context.PushState(OnsParserStateType.Variable);
            varHandler.TryRecycle(token, out success);
        }
        else if (token.Content.SequenceEqual("?"))
        {
            var arrayHandler = (ArrayState)_context.PushState(OnsParserStateType.Array);
            arrayHandler.TryRecycle(token, out success);
        }
        else
        {
            success = false;
        }
    }
}
#nullable restore
