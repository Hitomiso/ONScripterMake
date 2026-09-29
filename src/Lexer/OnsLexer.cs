using System;
using System.Collections.Generic;
using ONSMake;

namespace ONSMake.Lexer;

#nullable enable
public static class OnsLexer
{
    private static readonly LexemeRule[] _rules =
    [
        new LexemeRule("`", lineSegment => {
            int length = lineSegment[1..].IndexOf("`", StringComparison.Ordinal) + 2; // Включаем обе кавычки
            if (length == 1)
                return Result<DetectedToken, MessageID>.Fail(MessageID.ERR_QUOTES_NOT_CLOSED);
            return Result<DetectedToken, MessageID>.Ok(new (TokenType.Dialog, length));
        }),
        new LexemeRule("\"", lineSegment => {
            int length = lineSegment[1..].IndexOf("\"", StringComparison.Ordinal) + 2; // Включаем обе кавычки
            if (length == 1)
                return Result<DetectedToken, MessageID>.Fail(MessageID.ERR_QUOTES_NOT_CLOSED);
            return Result<DetectedToken, MessageID>.Ok(new (TokenType.String, length));
        }),
        new LexemeRule(";", lineSegment => Result<DetectedToken, MessageID>.Ok(new (TokenType.Comment, lineSegment.Length))),
        new LexemeRule("~", TokenType.JumpPoint),
        new LexemeRule("i%", TokenType.IndexVariable),
        new LexemeRule(lineSegment => {
            if (lineSegment.Length == 0)
                return null;
            char c = lineSegment[0];
            if ((c < 'A' || c > 'Z') && (c < 'a' || c > 'z') && c != '_')
                return null;
            int length = 1;
            while (length < lineSegment.Length && (char.IsLetterOrDigit(lineSegment[length]) || lineSegment[length] == '_'))
                length++;
            return Result<DetectedToken, MessageID>.Ok(new (TokenType.Identifier, length));
        }),
        new LexemeRule(lineSegment => {
            int length = 0;
            while (length < lineSegment.Length && lineSegment[length] >= '0' && lineSegment[length] <= '9')
                length++;
            if (length == 0)
                return null;
            return Result<DetectedToken, MessageID>.Ok(new (TokenType.Number, length));
        }),
        new LexemeRule("$", TokenType.Variable),
        new LexemeRule("%", TokenType.Variable),
        new LexemeRule("?", TokenType.Variable),
        new LexemeRule("!w", TokenType.InlineWaitCommand),
        new LexemeRule("!d", TokenType.InlineDelayCommand),
        new LexemeRule("!=", TokenType.NotEqual),
        new LexemeRule("#", lineSegment => {
            if (lineSegment.Length < 7)
                return null;
            for (int i = 1; i <= 6; i++)
            {
                char h = lineSegment[i];
                bool isHex = (h >= '0' && h <= '9') || (h >= 'A' && h <= 'F') || (h >= 'a' && h <= 'f');
                if (!isHex)
                    return null;
            }
            return Result<DetectedToken, MessageID>.Ok(new (TokenType.Color, 7));
        }),
        new LexemeRule("[@]", TokenType.DialogClickWait),
        new LexemeRule("[\\]", TokenType.DialogPageWait),
        new LexemeRule("[#]", TokenType.DialogContinueScript),
        new LexemeRule("[*]", TokenType.DialogSuspend),
        new LexemeRule("[|]", TokenType.DialogVoiceWait),
        new LexemeRule("(", TokenType.Left),
        new LexemeRule(")", TokenType.Right),
        new LexemeRule("[", TokenType.SLeft),
        new LexemeRule("]", TokenType.SRight),
        new LexemeRule(",", TokenType.Comma),
        new LexemeRule(":", TokenType.Colon),
        new LexemeRule("+", TokenType.Add),
        new LexemeRule("-", TokenType.Subtract),
        new LexemeRule("*", TokenType.Multiply),
        new LexemeRule("/", TokenType.Divide),
        new LexemeRule("<=", TokenType.LessOrEqual),
        new LexemeRule("<>", TokenType.NotEqual),
        new LexemeRule("<", TokenType.Less),
        new LexemeRule(">=", TokenType.GreaterOrEqual),
        new LexemeRule(">", TokenType.Greater),
        new LexemeRule("==", TokenType.Equal),
        new LexemeRule("=", TokenType.Equal),
        new LexemeRule("||", TokenType.Or),
        new LexemeRule("|", TokenType.Or),
        new LexemeRule("&&", TokenType.And),
        new LexemeRule("&", TokenType.And)
    ];
    
    public static Result<List<Token>, LexerError> TokenizeLine(ReadOnlySpan<char> lineSpan)
    {
        List<Token> tokens = [];
        int col = 0;

        while (col < lineSpan.Length)
        {
            // Пропуск пробельных символов
            while (col < lineSpan.Length && char.IsWhiteSpace(lineSpan[col]))
                col++;
            if (col >= lineSpan.Length)
                break;
            ReadOnlySpan<char> lineSegment = lineSpan[col..];
            
            // Проверяем, если токен похож на директиву
            if (tokens.Count == 0 && lineSegment[0] == '#')
            {
                if (lineSegment.Length > 1 && lineSegment[1] == '*')
                {
                    tokens.Add(new Token(TokenType.Directive, "#incremental_label", col));
                    col++;
                    continue;
                }
                int length = 1;
                while (length < lineSegment.Length && (char.IsLetterOrDigit(lineSegment[length]) || lineSegment[length] == '_'))
                    length++;
                if (length > 1)
                {
                    tokens.Add(new Token(TokenType.Directive, lineSegment[..length].ToString(), col));
                    col += length;
                    continue;
                }
            }
            
            Token? token = null;
            foreach (LexemeRule rule in _rules)
            {
                Result<DetectedToken, MessageID>? checkResult = rule.CheckSegmentForLexeme(lineSegment);
                if (checkResult == null)
                    continue;
                if (checkResult.Value.IsError)
                {
                    LexerError error = new(checkResult.Value.Error, col);
                    return Result<List<Token>, LexerError>.Fail(error);
                }
                string content = lineSegment[..checkResult.Value.Value.TokenLength].ToString();
                token = new Token(checkResult.Value.Value.TokenType, content, col);
                tokens.Add((Token)token);
                col += checkResult.Value.Value.TokenLength;
                break;
            }
            if (token == null)
            {
                LexerError error = new(MessageID.ERR_INVALID_TOKEN, col);
                return Result<List<Token>, LexerError>.Fail(error);
            }
        }

        return Result<List<Token>, LexerError>.Ok(tokens);
    }
}
#nullable restore
