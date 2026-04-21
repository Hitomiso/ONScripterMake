using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class ForFromState : InnerExpressionState
{
    public ForFromState(OnsParser parser) : base(parser)
    {
        // Переопределяем чтение ID, чтобы переходить к следующему параметру
        _tokenHandlers[TokenType.Identifier] = HandleName;
        Recycle(false);
    }

    public void Recycle()
    {
        Recycle(false);
    }

    private void HandleName(Token token)
    {
        if (token.Content == "to")
            _parser.PopState(_parser.CombineTokensIntoTree(_parameterBuffer));
        else
            HandleOperand(token);
    }
}
