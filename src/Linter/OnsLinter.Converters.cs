using System;
using System.Linq;
using ONSMake.Lexer;

namespace ONSMake.Linter;

public partial class OnsLinter
{
	private Result<object, TokenCheckError> CheckCanConvertToName(Token token)
    {
        if (token.Type != TokenType.Identifier)
            return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_NAME);
        return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckCanConvertToLabel(Token token)
    {
        if (token.Type == TokenType.ParsedLabel || token.Type == TokenType.String)
        {
            string labelName;
            if (token.Type == TokenType.String)
                labelName = token.Content[1..^1].ToString().ToLower();
            else
                labelName = token.Content.ToString().ToLower();
            if (!_scriptContext.Labels.ContainsKey(labelName))
                return MakeCheckErrorResult(token, MessageID.ERR_LABEL_NOT_FOUND, labelName);
            return MakeCheckOkResult();
        }
        if (token.Type == TokenType.Variable && token.Content.SequenceEqual("$"))
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_LABEL);
    }

    private Result<object, TokenCheckError> CheckCanConvertToNum(Token token)
    {
        Result<bool, TokenCheckError> isNumericResult = GuessIsOperandNumeric(token);
        if (isNumericResult.IsError)
            return Result<object, TokenCheckError>.Fail(isNumericResult.Error);
        if (!isNumericResult.Value)
            return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_NUMBER);
        return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckCanConvertToStr(Token token)
    {
        Result<bool, TokenCheckError> isStringResult = GuessIsOperandString(token);
        if (isStringResult.IsError)
            return Result<object, TokenCheckError>.Fail(isStringResult.Error);
        if (!isStringResult.Value)
            return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_STRING);
        return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckCanConvertToNumVar(Token token)
    {
		if (token.Type == TokenType.Variable && (token.Content.SequenceEqual("%") || token.Content.SequenceEqual("?")))
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_NUMVAR);
    }

    private Result<object, TokenCheckError> CheckCanConvertToStrVar(Token token)
    {
        if (token.Type == TokenType.Variable && token.Content.SequenceEqual("$"))
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_STRVAR);
    }

    private Result<object, TokenCheckError> CheckCanConvertToColor(Token token)
    {
        if (token.Type == TokenType.Color)
            return MakeCheckOkResult();
        if (token.Type == TokenType.String)
        {
            ReadOnlySpan<char> content = token.Content[1..^1];
            if (content.Length == 7 && content[0] == '#' && int.TryParse(content[1..7], System.Globalization.NumberStyles.HexNumber, null, out _))
                return MakeCheckOkResult();
        }
        return MakeCheckErrorResult(token, MessageID.ERR_INVALID_COLOR);
    }

    private Result<object, TokenCheckError> CheckCanConvertToEnum(Token token, string[] allowedValues)
    {
        string name;
        if (token.Type == TokenType.String)
            name = token.Content[1..^1].ToString();
        else
            name = token.Content.ToString();
        if ((token.Type == TokenType.Identifier || token.Type == TokenType.String) && allowedValues.Contains(name))
            return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_ENUM, string.Join(',', allowedValues));
    }
	
	private Result<object, TokenCheckError> CheckCanConvertToDialog(Token token)
    {
        if (token.Type != TokenType.Dialog)
			return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_DIALOG);
		return MakeCheckOkResult();
    }
}
