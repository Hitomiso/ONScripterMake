using System;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake;

public class SourceCodeMessage
{
	public enum MessageType
	{
		Info,
		Warning,
		Error
	}
	
	public readonly string FileName;
	public readonly string SourceLine;

	public readonly TextPosition Position;
	public readonly int Length;
	
	public readonly MessageType Type;
	public readonly MessageID MessageID;
	public readonly string[] MessageArgs;
	
	public SourceCodeMessage(string fileName, string sourceLine, TextPosition position, int length, MessageType type, MessageID messageId, params string[] args)
	{
		FileName = fileName;
		SourceLine = sourceLine;
		
		Position = position;
		Length = length;
		
		Type = type;
		MessageID = messageId;
		MessageArgs = args;
	}
	
	public SourceCodeMessage(SourceFileProcessor processedFile, TextPosition position, int length, MessageType type, MessageID messageId, string[] args)
	{
		FileName = processedFile.FileName;
		SourceLine = processedFile.InputLines[position.Line];
		
		Position = position;
		Length = length;
		
		Type = type;
		MessageID = messageId;
		MessageArgs = args;
	}
}
