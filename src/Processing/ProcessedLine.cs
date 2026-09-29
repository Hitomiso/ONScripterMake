using System;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Processing;

public record struct ProcessedLine
{
	public readonly int InputLineIndex;
	public string OutputLine;
	public readonly List<Token> Lexemes;
	public readonly List<Token> Tokens;
	
	public ProcessedLine(int inputLineIndex, string outputLine, List<Token> lexemes, List<Token> tokens)
	{
		InputLineIndex = inputLineIndex;
		OutputLine = outputLine;
		Lexemes = lexemes;
		Tokens = tokens;
	}
}