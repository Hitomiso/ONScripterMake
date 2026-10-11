using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

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

    public CommandState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TrySetCommand);
        _tokenHandlers.Add(TokenType.Comment, TryHandleComment);
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
            switch (((Token)_commandToken).Content)
            {
                case "notif":
                case "if":
                    var commandHandler = (CommandState)_context.ResetToNewState(OnsParserStateType.Command);
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

    public void TrySetCommand(Token token, out bool success)
    {
        if (token.Type != TokenType.Identifier)
        {
            success = false;
            return;
        }
        if (_commandToken != null)
            throw new ApplicationException($"Command token is already set ({_commandToken}), trying to push {token}.");
        _commandToken = token;
        SetParameterState();
        success = true;
    }

    public void TryHandleComment(Token token, out bool success)
    {
        _context.ResetToNewState(OnsParserStateType.Command);
        _context.RootTokens.Add(token);
        success = true;
    }

    private void SetParameterState()
    {
        if (_commandToken == null)
            return;
        switch (((Token)_commandToken).Content)
        {
            case "notif":
            case "if":
                ((IfParametersState)_context.PushState(OnsParserStateType.IfParameter)).Recycle();
                break;
            case "for":
                PushForParameterState();
                break;
            case "d":
            case "d2":
                ((DialogParameterState)_context.PushState(OnsParserStateType.DialogParameter)).Recycle();
                break;
            default:
                ((NormalParametersState)_context.PushState(OnsParserStateType.NormalParameter)).Recycle();
                break;
        }
    }

    private void PushForParameterState()
    {
        switch (_forLoopParameter)
        {
            case ForLoopParameter.Counter:
                ((ForCounterState)_context.PushState(OnsParserStateType.ForCounter)).Recycle();
                break;
            case ForLoopParameter.FromValue:
                ((ForFromState)_context.PushState(OnsParserStateType.ForFrom)).Recycle();
                break;
            case ForLoopParameter.ToValue:
                ((ForToState)_context.PushState(OnsParserStateType.ForTo)).Recycle();
                break;
            case ForLoopParameter.StepValue:
                ((ForStepState)_context.PushState(OnsParserStateType.ForStep)).Recycle();
                break;
            default:
                throw new ApplicationException($"Handler for loop parameter '{_forLoopParameter}' is unknown.");
        }
    }
}
#nullable restore
