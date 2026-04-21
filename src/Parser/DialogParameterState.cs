using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

// @TODO: Реализовать нормальную обработку диалогов в токены
#nullable enable
public class DialogParameterState : ParserStateHandler
{
    public DialogParameterState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Comment, HandleStub);
        _tokenHandlers.Add(TokenType.JumpPoint, HandleStub);
        _tokenHandlers.Add(TokenType.Identifier, HandleStub);
        _tokenHandlers.Add(TokenType.Number, HandleStub);
        _tokenHandlers.Add(TokenType.String, HandleStub);

        _tokenHandlers.Add(TokenType.Color, HandleStub);
        _tokenHandlers.Add(TokenType.Dialog, HandleStub);
        _tokenHandlers.Add(TokenType.InlineWaitCommand, HandleStub);
        _tokenHandlers.Add(TokenType.InlineDelayCommand, HandleStub);
        _tokenHandlers.Add(TokenType.DialogClickWait, HandleStub);
        _tokenHandlers.Add(TokenType.DialogPageWait, HandleStub);
        _tokenHandlers.Add(TokenType.DialogContinueScript, HandleStub);
        _tokenHandlers.Add(TokenType.DialogSuspend, HandleStub);
        _tokenHandlers.Add(TokenType.DialogVoiceWait, HandleStub);
        _tokenHandlers.Add(TokenType.Left, HandleStub);
        _tokenHandlers.Add(TokenType.Right, HandleStub);
        _tokenHandlers.Add(TokenType.SLeft, HandleStub);

        _tokenHandlers.Add(TokenType.SRight, HandleStub);
        _tokenHandlers.Add(TokenType.Variable, HandleStub);
        _tokenHandlers.Add(TokenType.IndexVariable, HandleStub);
        _tokenHandlers.Add(TokenType.Comma, HandleStub);
        _tokenHandlers.Add(TokenType.Colon, HandleStub);

        _tokenHandlers.Add(TokenType.Add, HandleStub);
        _tokenHandlers.Add(TokenType.Subtract, HandleStub);
        _tokenHandlers.Add(TokenType.Multiply, HandleStub);
        _tokenHandlers.Add(TokenType.Divide, HandleStub);
        _tokenHandlers.Add(TokenType.Less, HandleStub);

        _tokenHandlers.Add(TokenType.LessOrEqual, HandleStub);
        _tokenHandlers.Add(TokenType.Equal, HandleStub);
        _tokenHandlers.Add(TokenType.NotEqual, HandleStub);
        _tokenHandlers.Add(TokenType.GreaterOrEqual, HandleStub);
        _tokenHandlers.Add(TokenType.Greater, HandleStub);

        _tokenHandlers.Add(TokenType.Or, HandleStub);
        _tokenHandlers.Add(TokenType.And, HandleStub);
        Recycle();
    }

    public void Recycle()
    {
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue) { }

    public override Token? OnReset(Token? returnValue) => null;

    private void HandleStub(Token token) { }
}
#nullable restore
