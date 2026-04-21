using System;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class UnexpectedTokenError
{
    public readonly Token Token;

    public UnexpectedTokenError(Token token)
    {
        Token = token;
    }
}
