using System;
using System.Collections.Generic;

namespace Hitomiso.ONScripterMake.Parsing;

public class Token
{
	public TokenType Type;
	public string Value;
	public int StartColumn;
	public List<Token> Children = [];
	
	public Token(TokenType type, string value, int startColumn)
	{
		Type = type;
		Value = value;
		StartColumn = startColumn;
	}
}