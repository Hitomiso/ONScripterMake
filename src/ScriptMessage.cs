using System;
using ONSMake.Lexer;
using ONSMake.Processing;

namespace ONSMake;

public record class ScriptMessage
{
	public enum MessageType
	{
		Info,
		Warning,
		Error
	}
	
	public readonly ScriptUnit FileUnit;
	public readonly string SourceLine;

	public readonly TextPosition Position;
	public readonly int Length;
	
	public readonly MessageType Type;
	public readonly MessageID MessageID;
	public readonly string[] MessageArgs;
	
	public ScriptMessage(ScriptUnit fileUnit, TextPosition position, int length, MessageType type, MessageID messageId, string[] args)
	{
		FileUnit = fileUnit;
		SourceLine = fileUnit.InputLines[position.Line];
		
		Position = position;
		Length = length;
		
		Type = type;
		MessageID = messageId;
		MessageArgs = args;
	}
}
