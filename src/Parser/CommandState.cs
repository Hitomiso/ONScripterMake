using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class CommandState : ParserStateHandler
{
    private enum ForLoopParameter
    {
        Counter,
        FromValue,
        ToValue,
        StepValue
    }

    private Token? _commandToken;
    private ForLoopParameter _forLoopParameter;

    public CommandState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Identifier, SetCommand);
        _tokenHandlers.Add(TokenType.Comment, HandleComment);
        Recycle();
    }

    public void Recycle()
    {
        _commandToken = null;
        _forLoopParameter = ForLoopParameter.Counter;
        IsReady = true;
    }

    public override void OnReturn(Token? token)
    {
        if (_commandToken != null && ((Token)_commandToken).Children.Count > 0 && token == null)
            throw new ArgumentException("Returned parameter token is null.");
        if (_commandToken != null && token != null)
        {
            ((Token)_commandToken).Children.Add((Token)token);
            switch (((Token)_commandToken).Content.ToLower())
            {
                case "notif":
                case "if":
                    var commandHandler = (CommandState)_parser.ResetToNewState(OnsParserStateType.Command);
                    commandHandler.Recycle();
                    return;
                case "for":
                    // Увеличиваем тип текущего параметра
                    if (_forLoopParameter != ForLoopParameter.StepValue)
                        _forLoopParameter++;
                    break;
            }
        }
        SetParameterState();
    }

    public override Token? OnReset(Token? token)
    {
        if (_commandToken == null)
            return null;
        if (token != null)
            ((Token)_commandToken).Children.Add((Token)token);
        return _commandToken;
    }

    public void SetCommand(Token token)
    {
        if (token.Type != TokenType.Identifier)
            throw new UnexpectedTokenException(token);
        if (_commandToken != null)
            throw new ApplicationException($"Command token is already set ({_commandToken}), trying to push {token}.");
        _commandToken = token;
        SetParameterState();
    }

    public void HandleComment(Token token)
    {
        _parser.ResetToNewState(OnsParserStateType.Command);
        _parser.RootTokens.Add(token);
    }

    private void SetParameterState()
    {
        if (_commandToken == null)
            return;
        switch (((Token)_commandToken).Content.ToLower())
        {
            case "notif":
            case "if":
                ((IfParametersState)_parser.PushState(OnsParserStateType.IfParameter)).Recycle();
                break;
            case "for":
                PushForParameterState();
                break;
            case "d":
            case "d2":
                ((DialogParameterState)_parser.PushState(OnsParserStateType.DialogParameter)).Recycle();
                break;
            default:
                ((NormalParametersState)_parser.PushState(OnsParserStateType.NormalParameter)).Recycle();
                break;
        }
    }

    private void PushForParameterState()
    {
        switch (_forLoopParameter)
        {
            case ForLoopParameter.Counter:
                ((ForCounterState)_parser.PushState(OnsParserStateType.ForCounter)).Recycle();
                break;
            case ForLoopParameter.FromValue:
                ((ForFromState)_parser.PushState(OnsParserStateType.ForFrom)).Recycle();
                break;
            case ForLoopParameter.ToValue:
                ((ForToState)_parser.PushState(OnsParserStateType.ForTo)).Recycle();
                break;
            case ForLoopParameter.StepValue:
                ((ForStepState)_parser.PushState(OnsParserStateType.ForStep)).Recycle();
                break;
            default:
                throw new ApplicationException($"Handler for loop parameter '{_forLoopParameter}' is unknown.");
        }
    }
}
#nullable restore
