using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Processing;

internal class ProcessError
{
    public readonly Token Token;
    public readonly MessageID MessageID;
    public readonly string[] Args;

    public ProcessError(Token token, MessageID messageID, params string[] args)
    {
        Token = token;
        MessageID = messageID;
        Args = args;
    }
}
