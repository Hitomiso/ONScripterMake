using System;
using System.Linq;
using System.Net.Http.Headers;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Parser;

namespace Hitomiso.ONScripterMake.Linter;

#nullable enable
public partial class OnsLinter
{
	private bool CheckCanConvertToName(Token token, out TokenCheckError? error)
    {
        if (token.Type != TokenType.Identifier)
        {
            error = new TokenCheckError(token, MessageID.ERR_NOT_A_NAME);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckCanConvertToLabel(Token token, out TokenCheckError? error)
    {
        if (token.Type == TokenType.ParsedLabel || token.Type == TokenType.String)
        {
            string labelName = token.Content;
            if (token.Type == TokenType.String)
                labelName = labelName[1..^1];
            if (!_scriptContext.LabelDefinitions.ContainsKey(labelName))
            {
                error = new TokenCheckError(token, MessageID.ERR_LABEL_NOT_FOUND, labelName);
                return false;
            }
            error = null;
            return true;
        }
        if (token.Type != TokenType.Variable || token.Content != "$")
        {
            error = new TokenCheckError(token, MessageID.ERR_NOT_A_LABEL);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckCanConvertToNum(Token token, out TokenCheckError? error)
    {
        if (!IsTokenNumeric(token))
        {
            if (token.Type == TokenType.Identifier)
                error = new TokenCheckError(token, MessageID.ERR_NUMALIAS_NOT_DEFINED, token.Content);
            else
                error = new TokenCheckError(token, MessageID.ERR_NOT_A_NUMBER);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckCanConvertToStr(Token token, out TokenCheckError? error)
    {
        if (!IsTokenString(token))
        {
            if (token.Type == TokenType.Identifier)
                error = new TokenCheckError(token, MessageID.ERR_STRALIAS_NOT_DEFINED, token.Content);
            else
                error = new TokenCheckError(token, MessageID.ERR_NOT_A_STRING);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckCanConvertToNumVar(Token token, out TokenCheckError? error)
    {
		if (token.Type != TokenType.Variable || (token.Content != "%" && token.Content != "?"))
        {
            error = new TokenCheckError(token, MessageID.ERR_NOT_A_NUMVAR);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckCanConvertToStrVar(Token token, out TokenCheckError? error)
    {
        if (token.Type != TokenType.Variable || token.Content != "$")
        {
            error = new TokenCheckError(token, MessageID.ERR_NOT_A_STRVAR);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckCanConvertToColor(Token token, out TokenCheckError? error)
    {
        if (token.Type == TokenType.Color)
        {
            error = null;
            return true;
        }
        else if (token.Type == TokenType.String)
        {
            string content = token.Content[1..^1];
            if (content.Length == 7 && content[0] == '#' && int.TryParse(content[1..7], System.Globalization.NumberStyles.HexNumber, null, out _))
            {
                error = null;
                return true;
            }
        }
        error = new TokenCheckError(token, MessageID.ERR_INVALID_COLOR);
        return false;
    }

    private bool CheckCanConvertToEnum(Token token, string[] allowedValues, out TokenCheckError? error)
    {
        string name = token.Content;
        if (token.Type == TokenType.String)
            name = token.Content[1..^1];
        if ((token.Type != TokenType.Identifier && token.Type != TokenType.String) || !allowedValues.Contains(name))
        {
            error = new TokenCheckError(token, MessageID.ERR_NOT_A_ENUM, string.Join(',', allowedValues));
            return false;
        }
        error = null;
        return true;
    }
	
	private bool CheckCanConvertToDialog(Token token, out TokenCheckError? error)
    {
        if (token.Type != TokenType.Dialog)
        {
            error = new TokenCheckError(token, MessageID.ERR_NOT_A_DIALOG);
            return false;
        }
        error = null;
        return true;
		// @TODO: Обработать правильность строки вывода
    }
}
#nullable restore
