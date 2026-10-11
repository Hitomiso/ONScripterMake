using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class ForFromState : InnerExpressionState
{
    public ForFromState(ParserContext context) : base(context)
    {
        // Переопределяем чтение ID, чтобы переходить к следующему параметру
        _tokenHandlers[TokenType.Identifier] = TryHandleName;
        Recycle(false);
    }

    public void Recycle()
    {
        Recycle(false);
    }

    private void TryHandleName(Token token, out bool success)
    {
        if (!token.Content.SequenceEqual("to"))
        {
            TryHandleOperand(token, out success);
            return;
        }

        Result<Token?, Token> combineResult = _context.CombineTokensIntoTree(_parameterBuffer);
        if (combineResult.IsError)
        {
            success = false;
            return;
        }
        _context.PopState(combineResult.Value);
        success = true;
    }
}
#nullable restore
