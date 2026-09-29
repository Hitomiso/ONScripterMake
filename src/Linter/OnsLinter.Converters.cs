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
            string labelName = token.Content;
            if (token.Type == TokenType.String)
                labelName = labelName[1..^1];
            if (!_scriptContext.Labels.ContainsKey(labelName.ToLower()))
                return MakeCheckErrorResult(token, MessageID.ERR_LABEL_NOT_FOUND, labelName);
            return MakeCheckOkResult();
        }
        if (token.Type == TokenType.Variable && token.Content == "$")
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_LABEL);
    }

    private Result<object, TokenCheckError> CheckCanConvertToNum(Token token)
    {
        if (IsTokenNumeric(token))
            return MakeCheckOkResult();
        if (token.Type == TokenType.Identifier)
            return MakeCheckErrorResult(token, MessageID.ERR_NUMALIAS_NOT_DEFINED, token.Content);
        else
            return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_NUMBER);
    }

    private Result<object, TokenCheckError> CheckCanConvertToStr(Token token)
    {
        if (IsTokenString(token))
			return MakeCheckOkResult();
		if (token.Type == TokenType.Identifier)
			return MakeCheckErrorResult(token, MessageID.ERR_STRALIAS_NOT_DEFINED, token.Content);
		else
			return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_STRING);        
    }

    private Result<object, TokenCheckError> CheckCanConvertToNumVar(Token token)
    {
		if (token.Type == TokenType.Variable && (token.Content == "%" || token.Content == "?"))
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_NUMVAR);
    }

    private Result<object, TokenCheckError> CheckCanConvertToStrVar(Token token)
    {
        if (token.Type == TokenType.Variable && token.Content == "$")
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_STRVAR);
    }

    private Result<object, TokenCheckError> CheckCanConvertToColor(Token token)
    {
        if (token.Type == TokenType.Color)
            return MakeCheckOkResult();
        if (token.Type == TokenType.String)
        {
            string content = token.Content[1..^1];
            if (content.Length == 7 && content[0] == '#' && int.TryParse(content[1..7], System.Globalization.NumberStyles.HexNumber, null, out _))
                return MakeCheckOkResult();
        }
        return MakeCheckErrorResult(token, MessageID.ERR_INVALID_COLOR);
    }

    private Result<object, TokenCheckError> CheckCanConvertToEnum(Token token, string[] allowedValues)
    {
        string name = token.Content;
        if (token.Type == TokenType.String)
            name = token.Content[1..^1];
        if ((token.Type == TokenType.Identifier || token.Type == TokenType.String) && allowedValues.Contains(name))
			return MakeCheckOkResult();
        return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_ENUM, string.Join(',', allowedValues));
    }
	
	private Result<object, TokenCheckError> CheckCanConvertToDialog(Token token)
    {
        if (token.Type != TokenType.Dialog)
			return MakeCheckErrorResult(token, MessageID.ERR_NOT_A_DIALOG);
		return MakeCheckOkResult();
        // @TODO: Обработать правильность строки вывода
        // Выделять, проверять и сворачивать теги форматирования
        // Выдавать предупреждение на незакрытые кавычки диалога (во всей реплике)
        // Выдавать предупреждение на двойной пробел (во всей реплике)
    }
}
