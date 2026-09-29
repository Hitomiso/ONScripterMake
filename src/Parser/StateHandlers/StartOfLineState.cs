using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class StartOfLineState : ParserStateHandler
{
    public StartOfLineState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TryHandleCommand);
        _tokenHandlers.Add(TokenType.Comment, TryHandleComment);
        _tokenHandlers.Add(TokenType.JumpPoint, TryHandleJumpPoint);
        _tokenHandlers.Add(TokenType.Multiply, TryHandleLabel);
        IsReady = true;
    }

    public void Recycle()
    {
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue)
    {
        if (returnValue != null)
            _context.RootTokens.Add((Token)returnValue);
        ((CommandState)_context.ResetToNewState(OnsParserStateType.Command)).Recycle();
    }

    public override Token? OnReset(Token? returnValue)
    {
        return returnValue;
    }

    private void TryHandleCommand(Token token, out bool success)
    {
        var commandState = (CommandState)_context.PushState(OnsParserStateType.Command);
        commandState.Recycle();
        commandState.TrySetCommand(token, out success);
    }

    private void TryHandleComment(Token token, out bool success)
    {
        _context.RootTokens.Add(token);
        success = true;
    }

    private void TryHandleJumpPoint(Token token, out bool success)
    {
        _context.RootTokens.Add(token);
        success = true;
    }

    private void TryHandleLabel(Token token, out bool success)
    {
        ((LabelState)_context.PushState(OnsParserStateType.Label)).TryRecycle(token, out success);
    }
}
#nullable restore
