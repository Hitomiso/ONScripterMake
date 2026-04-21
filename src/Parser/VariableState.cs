using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class VariableState : ParserStateHandler
{
    private Token _varToken;

    public VariableState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Identifier, HandleChild);
        _tokenHandlers.Add(TokenType.Number, HandleChild);
        _tokenHandlers.Add(TokenType.Variable, HandleVariable);
    }

    public void Recycle(Token varToken)
    {
        if (varToken.Type != TokenType.Variable && varToken.Type != TokenType.IndexVariable || varToken.Content == "?")
            throw new UnexpectedTokenException(varToken);
        _varToken = varToken;
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue)
    {
        if (returnValue != null)
            HandleChild((Token)returnValue);
    }

    public override Token? OnReset(Token? returnValue)
    {
        if (returnValue != null)
            _varToken.Children.Add((Token)returnValue);
        return _varToken;
    }

    private void HandleChild(Token token)
    {
        _varToken.Children.Add(token);
        _parser.PopState(_varToken);
    }

    private void HandleVariable(Token token)
    {
        if (token.Content == "%")
        {
            var varHandler = (VariableState)_parser.PushState(OnsParserStateType.Variable);
            varHandler.Recycle(token);
        }
        else if (token.Content == "?")
        {
            var arrayHandler = (ArrayState)_parser.PushState(OnsParserStateType.Array);
            arrayHandler.Recycle(token);
        }
        else
        {
            throw new UnexpectedTokenException(token);
        }
    }
}
#nullable restore
