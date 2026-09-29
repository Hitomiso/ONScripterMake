using System;
using ONSMake;

namespace ONSMake.Lexer;

#nullable enable
public readonly record struct DetectedToken
{
    public readonly TokenType TokenType;
    public readonly int TokenLength;
    
    public DetectedToken(TokenType type, int length)
    {
        TokenType = type;
        TokenLength = length;
    }
}
#nullable restore
