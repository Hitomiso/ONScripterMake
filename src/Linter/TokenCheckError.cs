using System;
using ONSMake;
using ONSMake.Lexer;

namespace ONSMake.Linter;

#nullable enable
public readonly record struct TokenCheckError
{
    public readonly Token? Token;
    public readonly MessageID? MessageID;
    public readonly string[] Args;
    public readonly string MessageText;

    public TokenCheckError(Token? token, MessageID messageId, params string[] args)
    {
        Token = token;
        MessageID = messageId;
        Args = args;
        MessageText = MessageTranslator.GetArgumentedString(messageId, args);
    }
}
#nullable restore
