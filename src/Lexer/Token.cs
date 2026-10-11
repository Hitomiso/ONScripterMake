using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ONSMake.Lexer;

public readonly record struct Token
{
    public readonly ReadOnlySpan<char> Content =>
        _cachedLoweredContent ?? OriginalLine.AsSpan().Slice(StartColumn, Length);

    public readonly TokenType Type;
    public readonly string OriginalLine;
    public readonly int StartColumn;
    public readonly int Length;
    public readonly List<Token> Children = [];
    
    private readonly char[] _cachedLoweredContent;

    public Token(TokenType type, string originalLine, int startColumn, int length)
    {
        OriginalLine = originalLine ?? throw new NullReferenceException();
        Type = type;
        StartColumn = startColumn;
        Length = length;

        if (type != TokenType.Identifier)
            return;
        foreach (char c in Content)
        {
            if (char.IsUpper(c))
            {
                _cachedLoweredContent = new char[length];
                OriginalLine.AsSpan().Slice(StartColumn, Length).ToLowerInvariant(_cachedLoweredContent);
                break;
            }
        }
    }
    
	public override string ToString() => $"{Type}({Content})";

    public string TreeToString()
    {
        StringBuilder sb = new();
        sb.AppendLine(ToString());

        for (int i = 0; i < Children.Count; i++)
        {
            var child = Children[i];
            string[] lines = child.ToString().Split('\n');
            if (i + 1 < Children.Count)
                sb.AppendLine("├─" + lines[0]);
            else
                sb.AppendLine("└─" + lines[0]);
            foreach (string line in lines.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                if (i + 1 < Children.Count)
                    sb.AppendLine("│ " + line);
                else
                    sb.AppendLine("  " + line);
            }
        }

        return sb.ToString();
    }
}