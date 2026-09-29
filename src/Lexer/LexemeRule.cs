using System;

namespace ONSMake.Lexer;

#nullable enable
public readonly record struct LexemeRule
{
    private readonly string? Pattern;
    private readonly TokenType? _type;
    private readonly Func<ReadOnlySpan<char>, Result<DetectedToken, MessageID>?>? _checkerFunc;
    
    public LexemeRule(string pattern, TokenType type)
    {
        Pattern = pattern;
        _type = type;
        _checkerFunc = null;
    }
    public LexemeRule(string pattern, Func<ReadOnlySpan<char>, Result<DetectedToken, MessageID>?> checker)
    {
        Pattern = pattern;
        _type = null;
        _checkerFunc = checker;
    }
    public LexemeRule(Func<ReadOnlySpan<char>, Result<DetectedToken, MessageID>?> checker)
    {
        Pattern = null;
        _type = null;
        _checkerFunc = checker;
    }
    
    public Result<DetectedToken, MessageID>? CheckSegmentForLexeme(ReadOnlySpan<char> lineSegment)
    {
        if (Pattern != null && !lineSegment.StartsWith(Pattern, StringComparison.Ordinal))
            return null;
        if (_checkerFunc != null)
            return _checkerFunc(lineSegment);
        if (_type == null)
            return null;
        if (Pattern == null)
            return null;
        return Result<DetectedToken, MessageID>.Ok(new DetectedToken((TokenType)_type, Pattern.Length));
    }
}
#nullable restore
