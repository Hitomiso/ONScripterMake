using System;
using Hitomiso.ONScripterMake;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Linter;

#nullable enable
public class TokenCheckError
{
    public Token? Token { get; set; }
    public MessageID? MessageID { get; private set; }
    public string[] Args { get; private set; }
    public string MessageText { get; private set; }

    public TokenCheckError(Token? token, MessageID messageId, params string[] args)
    {
        Token = token;
        MessageID = messageId;
        Args = args;
        MessageText = MessageTranslator.GetArgumentedString(messageId, args);
    }
}
#nullable restore
