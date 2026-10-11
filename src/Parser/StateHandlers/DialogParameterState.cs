using System;
using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public class DialogParameterState : ParserStateHandler
{
    public DialogParameterState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Dialog, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.InlineWaitCommand, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.InlineDelayCommand, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.DialogClickWait, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.DialogPageWait, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.DialogContinueScript, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.DialogSuspend, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.DialogVoiceWait, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.Comment, TryHandleDialogPart);
        _tokenHandlers.Add(TokenType.SLeft, TryHandleInlineCommand);
        Recycle();
    }

    public void Recycle() => 
        IsReady = true;

    public override void OnReturn(Token? returnValue) => 
        _context.PopState(returnValue);

    public override Token? OnReset(Token? returnValue) => null;

    private void TryHandleDialogPart(Token token, out bool success)
    {
        _context.PopState(token);
        success = true;
    }

    private void TryHandleInlineCommand(Token token, out bool success)
    {
        ((InlineCommandState)_context.PushState(OnsParserStateType.InlineCommand)).Recycle();
        success = true;
    }
}
#nullable restore
