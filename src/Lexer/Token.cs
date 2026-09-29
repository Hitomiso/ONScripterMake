using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ONSMake.Lexer;

public readonly record struct Token
{
    public readonly TokenType Type;
    public readonly string Content;
    public readonly int StartColumn;
    public readonly List<Token> Children = [];

    public Token(TokenType type, string content, int startColumn)
    {
        if (content == null)
            throw new NullReferenceException();
        Type = type;
        Content = content;
        StartColumn = startColumn;
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