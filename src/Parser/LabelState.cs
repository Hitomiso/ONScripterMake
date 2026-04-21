using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class LabelState : ParserStateHandler
{
    private Token _asteriskToken;

    public LabelState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Identifier, HandleName);
    }

    public void Recycle(Token asteriskToken)
    {
        if (asteriskToken.Content != "*")
            throw new UnexpectedTokenException(asteriskToken);
        _asteriskToken = asteriskToken;
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue) { }
    public override Token? OnReset(Token? returnValue) => null;

    private void HandleName(Token token)
    {
        if (token.StartColumn != _asteriskToken.StartColumn + 1)
            throw new UnexpectedTokenException(token);
        Token labelToken = new(TokenType.ParsedLabel, "*" + token.Content, _asteriskToken.StartColumn);
        _parser.PopState(labelToken);
    }
}
#nullable restore
