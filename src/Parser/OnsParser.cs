using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake;
using ONSMake.Lexer;
using System.Collections.Concurrent;

namespace ONSMake.Parser;

#nullable enable
public static class OnsParser
{
	private static readonly ConcurrentBag<ParserContext> s_contextPool = [];
	
    public static Result<Token[], Token> ParseTokens(Token[] tokens)
    {
        if (!s_contextPool.TryTake(out ParserContext context))
            context = new ParserContext();
        context.ResetContext();
        try
		{
			foreach (var token in tokens)
			{
				context.PushToken(token, out bool success);
				if (!success)
					return Result<Token[], Token>.Fail(token);
			}
		}
		catch (Exception ex)
		{
			OutputHandler.PrintParsingFailure(tokens, context.RootTokens.ToArray(), ex);
            return Result<Token[], Token>.Fail(tokens.First());
		}
		((StartOfLineState)context.ResetToNewState(OnsParserStateType.StartOfLine)).Recycle();
        Token[] parsedTokens = context.RootTokens.ToArray();
        s_contextPool.Add(context);
        return Result<Token[], Token>.Ok(parsedTokens);
    }
}
#nullable restore
