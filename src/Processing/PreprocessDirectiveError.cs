namespace Hitomiso.ONScripterMake.Processing;

internal class PreprocessDirectiveError
{
    public readonly MessageID MessageID;
    public readonly string[] Args;

    public PreprocessDirectiveError(MessageID messageID, params string[] args)
    {
        MessageID = messageID;
        Args = args;
    }
}
