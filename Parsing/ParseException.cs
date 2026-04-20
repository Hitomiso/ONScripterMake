using System;

namespace Hitomiso.ONScripterMake.Parsing;

public class ParseException : Exception
{
	public Token Token { get; private set; }
	public MessageID MessageID { get; private set; }
	
	public ParseException(Token token, MessageID messageId, params string[] args) : base(MessageTranslator.GetArgumentedString(messageId, args))
	{
		Token = token;
		MessageID = messageId;
	}
}