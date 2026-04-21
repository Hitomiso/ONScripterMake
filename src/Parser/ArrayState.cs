using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class ArrayState : ParserStateHandler
{
    private Token _arrayToken;

    public ArrayState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Identifier, HandleIdentifier);
        _tokenHandlers.Add(TokenType.Number, HandleArrayNumber);
        _tokenHandlers.Add(TokenType.Variable, HandleVariable);
        _tokenHandlers.Add(TokenType.SLeft, HandleSquareBraces);
        _tokenHandlers.Add(TokenType.Comment, HandleComment);
        _tokenHandlers.Add(TokenType.Comma, HandleComma);
    }

    public void Recycle(Token arrayToken)
    {
        if (arrayToken.Content != "?")
            throw new UnexpectedTokenException(arrayToken);
        _arrayToken = arrayToken;
        IsReady = true;
    }

    public override void HandleToken(Token token)
    {
        if (!_tokenHandlers.ContainsKey(token.Type))
        {
            if (_arrayToken.Children.Count < 2)
                throw new UnexpectedTokenException(token);
            _parser.PopState(_arrayToken);
            _parser.PushToken(token);
            return;
        }
        _tokenHandlers[token.Type](token);
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

    private void HandleIdentifier(Token token)
    {
        if (_arrayToken.Children.Count < 2)
        {
            HandleArrayNumber(token);
        }
        else
        {
            _parser.PopState(_arrayToken);
            _parser.PushToken(token);
        }
    }

    private void HandleArrayNumber(Token token)
    {
        if (_arrayToken.Children.Count != 0)
            throw new UnexpectedTokenException(token);
        _arrayToken.Children.Add(token);
    }

    private void HandleVariable(Token token)
    {
        if (token.Content != "%")
            throw new UnexpectedTokenException(token);
        if (_arrayToken.Children.Count != 0)
            throw new UnexpectedTokenException(token);
        var varHandler = (VariableState)_parser.PushState( OnsParserStateType.Variable);
        varHandler.Recycle(token);
    }

    private void HandleSquareBraces(Token token)
    {
        if (_arrayToken.Children.Count == 0)
            throw new UnexpectedTokenException(token);
        var innerExpressionHandler = (InnerExpressionState)_parser.PushState(OnsParserStateType.InnerExpression);
        innerExpressionHandler.Recycle(false, InnerExpressionState.BracketsType.Square);
    }

    private void HandleComment(Token token)
    {
        if (_arrayToken.Children.Count <= 1)
            throw new UnexpectedTokenException(token);
        _parser.ResetToNewState(OnsParserStateType.Command);
        _parser.RootTokens.Add(token);
    }

    private void HandleComma(Token token)
    {
        if (_arrayToken.Children.Count <= 1)
            throw new UnexpectedTokenException(token);
        _parser.PopState(_arrayToken);
        _parser.PushToken(token);
    }
}
#nullable restore
