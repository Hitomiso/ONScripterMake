using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class NormalParametersState : InnerExpressionState
{
    public NormalParametersState(OnsParser parser) : base(parser)
    {
        _tokenHandlers[TokenType.Identifier] = HandleIdentifier;
        _tokenHandlers[TokenType.Multiply] = HandleAsterisk;

        // Остальные обработчики зарегистрированы в родительском классе
        _tokenHandlers.Add(TokenType.IndexVariable, HandleIndexVar);
        _tokenHandlers.Add(TokenType.Comment, HandleComment);
        _tokenHandlers.Add(TokenType.JumpPoint, HandleResetToCommand);
        _tokenHandlers.Add(TokenType.Comma, HandleComma);
        _tokenHandlers.Add(TokenType.Colon, HandleResetToCommand);
        _tokenHandlers.Add(TokenType.Color, HandleColor);
        Recycle();
    }

    public void Recycle(bool isLogical = false)
    {
        base.Recycle(isLogical);
    }

    private void HandleIndexVar(Token token)
    {
        var varHandler = (VariableState)_parser.PushState(OnsParserStateType.Variable);
        varHandler.Recycle(token);
    }

    private void HandleIdentifier(Token token)
    {
        // Id после операнда обрабатываем как команду и выходим из состояния
        if (_isOperatorAwaited && token.Content != "mod")
        {
            var commandHandler = (CommandState)_parser.ResetToNewState(OnsParserStateType.Command);
            commandHandler.Recycle();
            commandHandler.SetCommand(token);
            return;
        }
        HandleOperand(token);
    }

    private void HandleComment(Token token)
    {
        var commandHandler = (CommandState)_parser.ResetToNewState(OnsParserStateType.Command);
        commandHandler.Recycle();
        _parser.RootTokens.Add(token);
    }

    private void HandleComma(Token token)
    {
        _parser.PopState(_parser.CombineTokensIntoTree(_parameterBuffer));
    }

    private void HandleResetToCommand(Token token)
    {
        var commandHandler = (CommandState)_parser.ResetToNewState(OnsParserStateType.Command);
        commandHandler.Recycle();
    }

    private void HandleAsterisk(Token token)
    {
        if (_parameterBuffer.Count == 0)
        {
            var labelHandler = (LabelState)_parser.PushState(OnsParserStateType.Label);
            labelHandler.Recycle(token);
        }
        else
        {
            HandleOperator(token);
        }
    }

    private void HandleColor(Token token)
    {
        _parameterBuffer.Add(token);
    }
}
