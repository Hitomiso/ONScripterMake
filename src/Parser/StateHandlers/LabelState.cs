using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class LabelState : ParserStateHandler
{
    private Token _asteriskToken;

    public LabelState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TryHandleName);
    }

    public void TryRecycle(Token asteriskToken, out bool success)
    {
        if (asteriskToken.Content != "*")
        {
            success = false;
            return;
        }
        _asteriskToken = asteriskToken;
        IsReady = true;
        success = true;
    }

    public override void OnReturn(Token? returnValue) { }
    public override Token? OnReset(Token? returnValue) => null;

    private void TryHandleName(Token token, out bool success)
    {
        if (token.StartColumn != _asteriskToken.StartColumn + 1)
        {
            success = false;
            return;
        }
        Token labelToken = new(TokenType.ParsedLabel, "*" + token.Content, _asteriskToken.StartColumn);
        _context.PopState(labelToken);
        success = true;
    }
}
#nullable restore
