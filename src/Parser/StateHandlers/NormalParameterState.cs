using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class NormalParametersState : InnerExpressionState
{
    public NormalParametersState(ParserContext context) : base(context)
    {
        _tokenHandlers[TokenType.Identifier] = TryHandleIdentifier;
        _tokenHandlers[TokenType.Multiply] = TryHandleAsterisk;

        // Остальные обработчики зарегистрированы в родительском классе
        _tokenHandlers.Add(TokenType.IndexVariable, TryHandleIndexVar);
        _tokenHandlers.Add(TokenType.Comment, TryHandleComment);
        _tokenHandlers.Add(TokenType.JumpPoint, TryHandleResetToCommand);
        _tokenHandlers.Add(TokenType.Comma, TryHandleComma);
        _tokenHandlers.Add(TokenType.Colon, TryHandleResetToCommand);
        _tokenHandlers.Add(TokenType.Color, TryHandleColor);
        Recycle();
    }

    public void Recycle(bool isLogical = false)
    {
        base.Recycle(isLogical);
    }

    private void TryHandleIndexVar(Token token, out bool success)
    {
        var varHandler = (VariableState)_context.PushState(OnsParserStateType.Variable);
        varHandler.TryRecycle(token, out success);
    }

    private void TryHandleIdentifier(Token token, out bool success)
    {
        // Id после операнда обрабатываем как команду и выходим из состояния
        if (_isOperatorAwaited && !token.Content.SequenceEqual("mod"))
        {
            var commandHandler = (CommandState)_context.ResetToNewState(OnsParserStateType.Command);
            commandHandler.Recycle();
            commandHandler.TrySetCommand(token, out success);
            return;
        }
        TryHandleOperand(token, out success);
    }

    private void TryHandleComment(Token token, out bool success)
    {
        ((CommandState)_context.ResetToNewState(OnsParserStateType.Command)).Recycle();
        _context.RootTokens.Add(token);
        success = true;
    }

    private void TryHandleComma(Token token, out bool success)
    {
        Result<Token?, Token> combineResult = _context.CombineTokensIntoTree(_parameterBuffer);
        if (combineResult.IsError)
        {
            success = false;
            return;
        }
        _context.PopState(combineResult.Value);
        success = true;
    }

    private void TryHandleResetToCommand(Token token, out bool success)
    {
        var commandHandler = (CommandState)_context.ResetToNewState(OnsParserStateType.Command);
        commandHandler.Recycle();
        success = true;
    }

    private void TryHandleAsterisk(Token token, out bool success)
    {
        if (_parameterBuffer.Count == 0)
        {
            var labelHandler = (LabelState)_context.PushState(OnsParserStateType.Label);
            labelHandler.TryRecycle(token, out success);
        }
        else
        {
            TryHandleOperator(token, out success);
        }
    }

    private void TryHandleColor(Token token, out bool success)
    {
        _parameterBuffer.Add(token);
        success = true;
    }
}
#nullable restore
