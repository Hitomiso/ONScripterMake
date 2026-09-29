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
	
    public static Result<List<Token>, Token> ParseTokens(List<Token> tokens)
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
					return Result<List<Token>, Token>.Fail(token);
			}
		}
		catch (Exception ex)
		{
			OutputHandler.PrintParsingFailure(tokens, context.RootTokens, ex);
            return Result<List<Token>, Token>.Fail(tokens.First());
		}
		((StartOfLineState)context.ResetToNewState(OnsParserStateType.StartOfLine)).Recycle();
        List<Token> parsedTokens = [.. context.RootTokens];
        s_contextPool.Add(context);
        return Result<List<Token>, Token>.Ok(parsedTokens);
    }
}
#nullable restore
