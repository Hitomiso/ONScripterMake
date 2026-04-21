using System;

namespace Hitomiso.ONScripterMake.Lexer;

public class InvalidTokenException : Exception
{
    public readonly string Line;
    public readonly int Column;

    public InvalidTokenException(string line, int column)
    {
        Line = line;
        Column = column;
    }
}