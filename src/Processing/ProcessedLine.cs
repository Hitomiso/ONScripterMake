using System;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Parser;

namespace Hitomiso.ONScripterMake.Processing;

public struct ProcessedLine
{
	public int InputLineIndex;
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