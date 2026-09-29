using ONSMake;

namespace ONSMake.Lexer;

public readonly record struct LexerError
{
	public readonly MessageID MessageID;
	public readonly int Column;
	
	public LexerError(MessageID messageId, int column)
	{
		MessageID = messageId;
		Column = column;
	}
}