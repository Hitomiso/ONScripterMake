using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;
using System.Net.Http.Headers;

namespace ONSMake.Parser;

#nullable enable
public class ArrayState : ParserStateHandler
{
    private Token _arrayToken;

    public ArrayState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TryHandleIdentifier);
        _tokenHandlers.Add(TokenType.Number, TryHandleArrayNumber);
        _tokenHandlers.Add(TokenType.Variable, TryHandleVariable);
        _tokenHandlers.Add(TokenType.SLeft, TryHandleSquareBraces);
        _tokenHandlers.Add(TokenType.Comment, TryHandleComment);
        _tokenHandlers.Add(TokenType.Comma, TryHandleComma);
    }

    public void TryRecycle(Token arrayToken, out bool success)
    {
        if (!arrayToken.Content.SequenceEqual("?"))
        {
            success = false;
            return;
        }
        _arrayToken = arrayToken;
        IsReady = true;
        success = true;
    }
    
    public override void TryHandleToken(Token token, out bool success)
    {
        if (_tokenHandlers.TryGetValue(token.Type, out _tokenHandlerDelegate? handler))
        {
            handler(token, out success);
            return;
        }
        if (_arrayToken.Children.Count < 2)
        {
            success = false;
            return;
        }
        _context.PopState(_arrayToken);
        _context.PushToken(token, out success);
    }

    public override void OnReturn(Token? returnValue)
    {
        if (returnValue != null)
            _arrayToken.Children.Add((Token)returnValue);
    }

    public override Token? OnReset(Token? returnValue)
    {
        OnReturn(returnValue);
        return _arrayToken;
    }

    private void TryHandleIdentifier(Token token, out bool success)
    {
        if (_arrayToken.Children.Count < 2)
        {
            TryHandleArrayNumber(token, out success);
            return;
        }
        _context.PopState(_arrayToken);
        _context.PushToken(token, out success);
    }

    private void TryHandleArrayNumber(Token token, out bool success)
    {
        if (_arrayToken.Children.Count != 0)
        {
            success = false;
            return;
        }
        _arrayToken.Children.Add(token);
        success = true;
    }

    private void TryHandleVariable(Token token, out bool success)
    {
        if (!token.Content.SequenceEqual("%"))
        {
            success = false;
            return;
        }
        if (_arrayToken.Children.Count != 0)
        {
            success = false;
            return;
        }
        var varHandler = (VariableState)_context.PushState( OnsParserStateType.Variable);
        varHandler.TryRecycle(token, out success);
    }

    private void TryHandleSquareBraces(Token token, out bool success)
    {
        if (_arrayToken.Children.Count == 0)
        {
            success = false;
            return;
        }
        var innerExpressionHandler = (InnerExpressionState)_context.PushState(OnsParserStateType.InnerExpression);
        innerExpressionHandler.Recycle(false, InnerExpressionState.BracketsType.Square);
        success = true;
    }

    private void TryHandleComment(Token token, out bool success)
    {
        if (_arrayToken.Children.Count <= 1)
        {
            success = false;
            return;
        }
        _context.ResetToNewState(OnsParserStateType.Command);
        _context.RootTokens.Add(token);
        success = true;
    }

    private void TryHandleComma(Token token, out bool success)
    {
        if (_arrayToken.Children.Count <= 1)
        {
            success = false;
            return;
        }
        _context.PopState(_arrayToken);
        _context.PushToken(token, out success);
    }
}
#nullable restore
