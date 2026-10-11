using System;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Processing;

public record struct ProcessedLine
{
	public readonly int InputLineIndex;
	public string OutputLine;
	public readonly Token[] Lexemes;
	public readonly Token[] Tokens;
	
	public ProcessedLine(int inputLineIndex, string outputLine, Token[] lexemes, Token[] tokens)
	{
		InputLineIndex = inputLineIndex;
		OutputLine = outputLine;
		Lexemes = lexemes;
		Tokens = tokens;
	}
}