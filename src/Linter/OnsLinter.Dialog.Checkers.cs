using ONSMake.Lexer;
using System;
using System.Buffers;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Text;

namespace ONSMake.Linter;

#nullable enable
public partial class OnsLinter
{
    private static readonly Dictionary<string, Func<ReadOnlySpan<char>, bool>?> FORMAT_TAG_PARAMETER_CHECKERS = new()
    {
        { "italic", null },
        { "i", null },
        { "bold", null },
        { "b", null },
        { "bolditalic", null },
        { "x", null },
        { "underline", null },
        { "u", null },
        { "gradient", CheckDialogTagBool },
        { "g", CheckDialogTagBool },
        { "left", null },
        { "al", null },
        { "center", null},
        { "centre", null },
        { "ac", null },
        { "right", null },
        { "ar", null },
        { "alignment", CheckDialogTagAlign },
        { "a", CheckDialogTagAlign },
        { "nobreak", null },
        { "nobr", null },
        { "font", CheckDialogTagNum },
        { "f", CheckDialogTagNum },
        { "border", CheckDialogTagNum },
        { "borderwidth", CheckDialogTagNum },
        { "o", CheckDialogTagNum },
        { "shadow", CheckDialogTagShadow },
        { "shadowdistance", CheckDialogTagShadow },
        { "s", CheckDialogTagShadow },
        { "y", CheckDialogTagNum },
        { "n", CheckDialogTagNum },
        { "preset", CheckDialogTagNum },
        { "p", CheckDialogTagNum },
        { "fontsize", CheckDialogTagNum },
        { "fontsizeabsolute", CheckDialogTagNum },
        { "size", CheckDialogTagNum },
        { "d", CheckDialogTagNum },
        { "fontsizepercent", CheckDialogTagNum },
        { "fontsizepc", CheckDialogTagNum },
        { "sizepercent", CheckDialogTagNum },
        { "sizepc", CheckDialogTagNum },
        { "e", CheckDialogTagNum },
        { "characterspacing", CheckDialogTagNum },
        { "charspacing", CheckDialogTagNum },
        { "m", CheckDialogTagNum },
        { "ruby", null },
        { "h", null },
        { "loghint", null },
        { "l", null },
        { "width", CheckDialogTagNum },
        { "w", CheckDialogTagNum },
        { "color", CheckDialogTagColor },
        { "colour", CheckDialogTagColor },
        { "c", CheckDialogTagColor },
        { "shadowcolor", CheckDialogTagColor },
        { "shadowcolour", CheckDialogTagColor },
        { "v", CheckDialogTagColor },
        { "bordercolor", CheckDialogTagColor },
        { "bordercolour", CheckDialogTagColor },
        { "r", CheckDialogTagColor }    
    };
    private static readonly HashSet<string> EMPTY_FORMAT_TAGS = [ "parallel", "fit", "j", "t", "n", "0", "qt", "ob", "eb", "os", "es", "-" ];

    private Result<object, TokenCheckError> CheckDialogCommandCall(Token dialogCommandToken, bool isD2)
    {
        Span<char> tagBuffer = stackalloc char[64];
        Span<int> tagColumnsBuffer = stackalloc int[16];
        FormatTagCheckState checkState = new(tagBuffer, tagColumnsBuffer);
        bool isDialogEmpty = true;
        // Если в процессе обработки не произошла серьёзная ошибка, то выводится это сохранённое предупреждение
        TokenCheckError? savedWarning = null;

        for (int i = 0; i < dialogCommandToken.Children.Count; i++)
        {
            Token child = dialogCommandToken.Children[i];
            switch (child.Type)
            {
                case TokenType.Identifier:
                    Result<object, TokenCheckError> commandCheckResult = CheckCommandCallToken(child);
                    if (commandCheckResult.IsError)
                        return Result<object, TokenCheckError>.Fail(commandCheckResult.Error);
                    break;
                case TokenType.Dialog:
                    if (child.Length > 2)
                        isDialogEmpty = false;
                    Result<object, DialogCheckError> tagsCheckResult = CheckDialogTags(child, ref checkState);
                    if (tagsCheckResult.IsError)
                        return MakeCheckErrorResult(dialogCommandToken.OriginalLine, tagsCheckResult.Error);
                    break;
                case TokenType.DialogClickWait:
                case TokenType.DialogPageWait:
                case TokenType.DialogVoiceWait:
                case TokenType.InlineDelayCommand:
                case TokenType.InlineWaitCommand:
                    break;
                case TokenType.DialogSuspend:
                case TokenType.DialogContinueScript:
                    if (!isD2)
                        return MakeCheckErrorResult(child, MessageID.ERR_ONLY_IN_D2);
                    break;
                case TokenType.Comment:
                    savedWarning = new TokenCheckError(child, MessageID.WARN_COMMENT_AFTER_DIALOG);
                    break;
                default:
                    return MakeCheckErrorResult(child, MessageID.ERR_UNEXPECTED_TOKEN);
            }
        }
        if (isDialogEmpty)
            return MakeCheckErrorResult(dialogCommandToken, MessageID.ERR_DIALOG_IS_EMPTY);
        if (checkState.Depth > 0)
            return MakeCheckErrorResult(dialogCommandToken.OriginalLine, checkState.TagStartColumnsBuffer[checkState.Depth - 1], 0, MessageID.ERR_FORMAT_TAG_NOT_CLOSED);

        if (savedWarning.HasValue)
            return Result<object, TokenCheckError>.Fail((TokenCheckError)savedWarning);
        if (checkState.TriggerSpacesWarningAtColumn >= 0)
            return MakeCheckErrorResult(dialogCommandToken.OriginalLine, checkState.TriggerSpacesWarningAtColumn, 1, MessageID.WARN_DIALOG_TOO_MANY_SPACES);
        if (checkState.AreQuotesOpen)
            return MakeCheckErrorResult(dialogCommandToken.OriginalLine, checkState.LastQuotesColumn, 1, MessageID.WARN_DIALOG_QUOTES_NOT_CLOSED);
        return MakeCheckOkResult();
    }

    private Result<object, DialogCheckError> CheckDialogTags(Token dialogToken, ref FormatTagCheckState state)
    {
        for (int i = 1; i < dialogToken.Content.Length - 1; i++)
        {
            char c = dialogToken.Content[i];
            int column = dialogToken.StartColumn + i;
            switch (c)
            {
                case '{':
                    if (!state.IsReadingText)
                        return MakeDialogErrorResult(column, 1, MessageID.ERR_UNEXPECTED_OPEN_FORMAT_TAG);
                    state.StartReadingTagName(column);
                    if (state.Depth >= state.TagStartColumnsBuffer.Length)
                        return MakeDialogErrorResult(column, 1, MessageID.ERR_TOO_MANY_FORMAT_TAGS);
                    state.TagStartColumnsBuffer[state.Depth++] = column;
                    break;
                case '}':
                    state.Depth--;
                    if (state.Depth <= -1)
                        return MakeDialogErrorResult(column, 1, MessageID.ERR_UNEXPECTED_CLOSE_FORMAT_TAG);
                    if (state.IsReadingTagName)
                    {
                        var lookup = EMPTY_FORMAT_TAGS.GetAlternateLookup<ReadOnlySpan<char>>();
                        ReadOnlySpan<char> tagName = state.TagBuffer[..state.TagLength];
                        if (!lookup.Contains(tagName))
                            return MakeDialogErrorResult(column, state.TagLength, MessageID.ERR_UNKNOWN_FORMAT_TAG, state.TagBuffer[..state.TagLength].ToString());
                        if (tagName.SequenceEqual("n"))
                            state.SpacesCount = 0;
                        state.StartReadingText();
                    }
                    else if (state.IsReadingTagParameter)
                    {
                        return MakeDialogErrorResult(column, state.TagLength, MessageID.ERR_TOO_FEW_FORMAT_PARAMETERS);
                    }
                    break;
                case ':':
                    if (state.IsReadingText)
                        break;
                    if (state.IsReadingTagName)
                    {
                        var lookup = FORMAT_TAG_PARAMETER_CHECKERS.GetAlternateLookup<ReadOnlySpan<char>>();
                        if (!lookup.TryGetValue(state.TagBuffer[..state.TagLength], out var parameterChecker))
                            return MakeDialogErrorResult(column, state.TagLength, MessageID.ERR_UNKNOWN_FORMAT_TAG, state.TagBuffer[..state.TagLength].ToString());
                        if (parameterChecker == null)
                            state.StartReadingText();
                        else
                            state.StartReadingTagParameter(parameterChecker, column);
                    }
                    else if (state.IsReadingTagParameter)
                    {
                        if (state.TagParameterChecker == null)
                            throw new NullReferenceException("TagParameterChecker is somehow null.");
                        if (!state.TagParameterChecker(state.TagBuffer[..state.TagLength]))
                            return MakeDialogErrorResult(state.TagStartColumn, state.TagLength, MessageID.ERR_INVALID_FORMAT_PARAMETER, state.TagBuffer[..state.TagLength].ToString());
                        state.StartReadingText();
                    }
                    break;
                default:
                    if (!state.IsReadingText)
                    {
                        if (state.TagLength >= state.TagBuffer.Length)
                            return MakeDialogErrorResult(state.TagStartColumn, state.TagLength, MessageID.ERR_FORMAT_TAG_TOO_LONG);
                        state.TagBuffer[state.TagLength++] = c;
                        break;
                    }
                    switch (c)
                    {
                        case '"':
                            state.AreQuotesOpen = !state.AreQuotesOpen;
                            state.LastQuotesColumn = column;
                            break;
                        case ' ':
                            state.SpacesCount++;
                            if (state.SpacesCount == 2 && state.TriggerSpacesWarningAtColumn == -1)
                                state.TriggerSpacesWarningAtColumn = column;
                            break;
                        default:
                            state.SpacesCount = 0;
                            break;
                    }
                    break;
            }
        }
        return MakeDialogOkResult();
    }

    private static bool CheckDialogTagBool(ReadOnlySpan<char> param) =>
        param.SequenceEqual("1") || param.SequenceEqual("0");
    private static bool CheckDialogTagAlign(ReadOnlySpan<char> param) =>
        param.SequenceEqual("l") || param.SequenceEqual("c") || param.SequenceEqual("r");
    private static bool CheckDialogTagNum(ReadOnlySpan<char> param) =>
        int.TryParse(param, out _);
    private static bool CheckDialogTagShadow(ReadOnlySpan<char> param)
    {
        int commaIdx = param.IndexOf(',');
        if (commaIdx < 0)
            return false;
        return CheckDialogTagNum(param[..commaIdx]) && CheckDialogTagNum(param[(commaIdx + 1)..]);
    }
    private static bool CheckDialogTagColor(ReadOnlySpan<char> param)
    {
        if (param.Length != 6)
            return false;
        foreach (char c in param)
        {
            if (!char.IsDigit(c) && (c < 'a' || c > 'f') && (c < 'A' || c > 'F'))
                return false;
        }
        return true;
    }

    private static Result<object, DialogCheckError> MakeDialogErrorResult(int column, int length, MessageID messageID, params string[] args) =>
        Result<object, DialogCheckError>.Fail(new DialogCheckError(column, length, messageID, args));
    private static Result<object, DialogCheckError> MakeDialogOkResult() =>
        Result<object, DialogCheckError>.Ok(null);

    private static Result<object, TokenCheckError> MakeCheckErrorResult(string originalLine, DialogCheckError dialogError) =>
        MakeCheckErrorResult(originalLine, dialogError.Column, dialogError.Length, dialogError.MessageID, dialogError.Args);
    private static Result<object, TokenCheckError> MakeCheckErrorResult(string originalLine, int column, int length, MessageID messageId, params string[] args)
    {
        Token fictionalToken = new(TokenType.Invalid, originalLine, column, length);
        return MakeCheckErrorResult(fictionalToken, messageId, args);
    }
}
#nullable restore
