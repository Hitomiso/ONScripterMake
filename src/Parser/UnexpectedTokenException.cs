using System;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class UnexpectedTokenException : Exception
{
	public readonly Token Token;
	
    public UnexpectedTokenException(Token token) : base(MessageTranslator.GetArgumentedString(MessageID.ERR_UNEXPECTED_TOKEN, [token.ToString()]))
    {
        Token = token;
    }
}