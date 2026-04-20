using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Parsing;

namespace Hitomiso.ONScripterMake.Processing;

public class PreprocessException : Exception
{
    public Line Line { get; private set; }
    public int ErrorStartColumn { get; private set; }
    public MessageID? MessageID { get; private set; }

    public PreprocessException(Line line, int errorStartColumn, string message) : base(message)
    {
        Line = line;
        ErrorStartColumn = errorStartColumn;
    }

    public PreprocessException(Line line, int errorStartColumn, MessageID messageId, params string[] args)
        : base(MessageTranslator.GetArgumentedString(messageId, args))
    {
        Line = line;
        ErrorStartColumn = errorStartColumn;
		MessageID = messageId;
    }
	
	public PreprocessException(Line line, Token token, MessageID messageId, params string[] args)
		: base(MessageTranslator.GetArgumentedString(messageId, args))
	{
		Line = line;
		Line.Column = token.StartColumn + token.Value.Length - 1;
		ErrorStartColumn = token.StartColumn;
		MessageID = messageId;
	}
}