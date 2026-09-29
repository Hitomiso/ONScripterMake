using System;
using ONSMake.Lexer;

namespace ONSMake.Parser;

// @TODO: Реализовать нормальную обработку диалогов в токены
#nullable enable
public class DialogParameterState : ParserStateHandler
{
    public DialogParameterState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Comment, TryHandleStub);
        _tokenHandlers.Add(TokenType.JumpPoint, TryHandleStub);
        _tokenHandlers.Add(TokenType.Identifier, TryHandleStub);
        _tokenHandlers.Add(TokenType.Number, TryHandleStub);
        _tokenHandlers.Add(TokenType.String, TryHandleStub);

        _tokenHandlers.Add(TokenType.Color, TryHandleStub);
        _tokenHandlers.Add(TokenType.Dialog, TryHandleStub);
        _tokenHandlers.Add(TokenType.InlineWaitCommand, TryHandleStub);
        _tokenHandlers.Add(TokenType.InlineDelayCommand, TryHandleStub);
        _tokenHandlers.Add(TokenType.DialogClickWait, TryHandleStub);
        _tokenHandlers.Add(TokenType.DialogPageWait, TryHandleStub);
        _tokenHandlers.Add(TokenType.DialogContinueScript, TryHandleStub);
        _tokenHandlers.Add(TokenType.DialogSuspend, TryHandleStub);
        _tokenHandlers.Add(TokenType.DialogVoiceWait, TryHandleStub);
        _tokenHandlers.Add(TokenType.Left, TryHandleStub);
        _tokenHandlers.Add(TokenType.Right, TryHandleStub);
        _tokenHandlers.Add(TokenType.SLeft, TryHandleStub);

        _tokenHandlers.Add(TokenType.SRight, TryHandleStub);
        _tokenHandlers.Add(TokenType.Variable, TryHandleStub);
        _tokenHandlers.Add(TokenType.IndexVariable, TryHandleStub);
        _tokenHandlers.Add(TokenType.Comma, TryHandleStub);
        _tokenHandlers.Add(TokenType.Colon, TryHandleStub);

        _tokenHandlers.Add(TokenType.Add, TryHandleStub);
        _tokenHandlers.Add(TokenType.Subtract, TryHandleStub);
        _tokenHandlers.Add(TokenType.Multiply, TryHandleStub);
        _tokenHandlers.Add(TokenType.Divide, TryHandleStub);
        _tokenHandlers.Add(TokenType.Less, TryHandleStub);

        _tokenHandlers.Add(TokenType.LessOrEqual, TryHandleStub);
        _tokenHandlers.Add(TokenType.Equal, TryHandleStub);
        _tokenHandlers.Add(TokenType.NotEqual, TryHandleStub);
        _tokenHandlers.Add(TokenType.GreaterOrEqual, TryHandleStub);
        _tokenHandlers.Add(TokenType.Greater, TryHandleStub);

        _tokenHandlers.Add(TokenType.Or, TryHandleStub);
        _tokenHandlers.Add(TokenType.And, TryHandleStub);
        Recycle();
    }

    public void Recycle()
    {
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue) { }

    public override Token? OnReset(Token? returnValue) => null;

    private void TryHandleStub(Token token, out bool success) => success = true;
}
#nullable restore
