using ONSMake;

namespace ONSMake.Processing;

public readonly record struct DirectiveError
{
	public readonly MessageID MessageID;
	public readonly string[] Args;
	
	public DirectiveError(MessageID messageId, params string[] args)
	{
		MessageID = messageId;
		Args = args ?? [];
	}
}
