using System;
using System.Linq;
using ONSMake.Lexer;

namespace ONSMake.Parser;

internal class InlineCommandState : ParserStateHandler
{
    private static string[] BANNED_INLINE_COMMANDS = [ "notif", "if", "for", "d", "d2", "gosub", "goto", "getparam" ];

    private Token? _commandToken;

    public InlineCommandState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TrySetCommand);
        _tokenHandlers.Add(TokenType.InlineDelayCommand, TrySetCommand);
        _tokenHandlers.Add(TokenType.InlineWaitCommand, TrySetCommand);
        Recycle();
    }

    public void Recycle()
    {
        _commandToken = null;
        IsReady = true;
    }

    public override void OnReturn(Token? returnToken)
    {
        if (_commandToken != null && ((Token)_commandToken).Children.Count > 0 && returnToken == null)
            throw new ArgumentException("Returned parameter token is null.");
        if (_commandToken != null && returnToken != null)
        {
            if (returnToken.Value.Type == TokenType.SRight)
            {
                _context.PopState(_commandToken);
                return;
            }
            ((Token)_commandToken).Children.Add((Token)returnToken);
        }
        SetParameterState();
    }

    public override Token? OnReset(Token? returnToken)
    {
        if (_commandToken == null)
            return null;
        if (returnToken != null)
            ((Token)_commandToken).Children.Add((Token)returnToken);
        return _commandToken;
    }

    private void TrySetCommand(Token token, out bool success)
    {
        foreach (string banned in BANNED_INLINE_COMMANDS)
        {
            if (token.Content.SequenceEqual(banned))
            {
                success = false;
                return;
            }
        }
        if (_commandToken != null)
            throw new ApplicationException($"Command token is already set ({_commandToken}), trying to push {token}.");
        _commandToken = token;
        SetParameterState();
        success = true;
    }

    private void SetParameterState()
    {
        if (_commandToken == null)
            return;
        ((InlineCommandParameterState)_context.PushState(OnsParserStateType.InlineCommandParameter)).Recycle();
    }
}
