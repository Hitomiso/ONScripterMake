using ONSMake;

namespace ONSMake.Processing;

public readonly record struct DirectiveError
{
	public readonly MessageID MessageID;
	public readonly string[] Args;
	
	public DirectiveError(MessageID messageId, string[] args = null)
	{
		MessageID = messageId;
		Args = args ?? [];
	}
}
