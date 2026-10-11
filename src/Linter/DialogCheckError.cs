using ONSMake;

namespace ONSMake.Linter;

public readonly record struct DialogCheckError
{
    public readonly int Column;
    public readonly int Length;
    public readonly MessageID MessageID;
    public readonly string[] Args;

    public DialogCheckError(int column, int length, MessageID messageId, params string[] args)
    {
        Column = column;
        Length = length;
        MessageID = messageId;
        Args = args;
    }
}
