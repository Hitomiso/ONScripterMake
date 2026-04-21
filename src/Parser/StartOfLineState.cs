using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class StartOfLineState : ParserStateHandler
{
    public StartOfLineState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Identifier, HandleCommand);
        _tokenHandlers.Add(TokenType.Comment, HandleComment);
        _tokenHandlers.Add(TokenType.JumpPoint, HandleJumpPoint);
        _tokenHandlers.Add(TokenType.Multiply, HandleLabel);
        IsReady = true;
    }

    public void Recycle()
    {
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue)
    {
        if (returnValue != null)
            _parser.RootTokens.Add((Token)returnValue);
        ((CommandState)_parser.ResetToNewState(OnsParserStateType.Command)).Recycle();
    }

    public override Token? OnReset(Token? returnValue)
    {
        return returnValue;
    }

    private void HandleCommand(Token token)
    {
        var commandState = (CommandState)_parser.PushState(OnsParserStateType.Command);
        commandState.Recycle();
        commandState.SetCommand(token);
    }

    private void HandleComment(Token token)
    {
        _parser.RootTokens.Add(token);
    }

    private void HandleJumpPoint(Token token)
    {
        _parser.RootTokens.Add(token);
    }

    private void HandleLabel(Token token)
    {
        ((LabelState)_parser.PushState(OnsParserStateType.Label)).Recycle(token);
    }
}
#nullable restore
