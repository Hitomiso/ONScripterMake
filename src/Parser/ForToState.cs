using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class ForToState : NormalParametersState
{
    public ForToState(OnsParser parser) : base(parser)
    {
        // Переопределяем чтение ID, чтобы переходить к следующему параметру
        _tokenHandlers[TokenType.Identifier] = HandleName;
    }

    private void HandleName(Token token)
    {
        if (token.Content == "step")
            _parser.PopState(_parser.CombineTokensIntoTree(_parameterBuffer));
        else
            HandleOperand(token);
    }
}