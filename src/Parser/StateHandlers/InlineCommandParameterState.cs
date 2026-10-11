using ONSMake.Lexer;
using System;

namespace ONSMake.Parser;

internal class InlineCommandParameterState : InnerExpressionState
{
    public InlineCommandParameterState(ParserContext context) : base(context)
    {
        _tokenHandlers[TokenType.Multiply] = TryHandleAsterisk;
        _tokenHandlers[TokenType.SRight] = TryHandleSRight;
        _tokenHandlers.Add(TokenType.Color, TryHandleColor);
        _tokenHandlers[TokenType.Variable] = TryHandleVariable;
        Recycle();
    }

    public void Recycle() => base.Recycle(isLogical: false);

    private void TryHandleAsterisk(Token token, out bool success)
    {
        success = PopParameter();
    }

    private void TryHandleColor(Token token, out bool success)
    {
        _parameterBuffer.Add(token);
        success = true;
    }

    private void TryHandleSRight(Token token, out bool success)
    {
        success = PopParameter();
        if (!success)
            return;
        _context.PopState(token);
    }

    private new void TryHandleVariable(Token token, out bool success)
    {
        // Массивы внутри диалогов обрабатываются с ошибкой
        if (token.Content.SequenceEqual("?"))
        {
            success = false;
            return;
        }
        base.TryHandleVariable(token, out success);
    }

    private bool PopParameter()
    {
        Result<Token?, Token> combineResult = _context.CombineTokensIntoTree(_parameterBuffer);
        if (combineResult.IsError)
            return false;
        _context.PopState(combineResult.Value);
        return true;
    }
}
