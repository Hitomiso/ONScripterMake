using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Hitomiso.ONScripterMake.Parsing;

namespace Hitomiso.ONScripterMake.Processing;

public partial class CommandCallChecker
{
	private bool CheckCanConvertToName(Token token)
	{
		return token.Type == TokenType.Name;
	}
	
	private bool CheckCanConvertToLabel(Token token)
	{
		// @TODO: Проверять на наличие метки?
		if (token.Type == TokenType.Label)
		return token.Type == TokenType.Label || token.Type == TokenType.StrVar;
	}
	
	private bool CheckCanConvertToNum(Token token)
	{
		if (token.Type == TokenType.Operator)
		{
			throw new NotImplementedException();
		}
		throw new NotImplementedException();
	}
	
	private bool CheckCanConvertToStr(Token token)
	{
		if (token.Type == TokenType.Operator)
		{
			throw new NotImplementedException();
		}
		throw new NotImplementedException();
	}
	
	private bool CheckCanConvertToNumVar(Token token)
	{
		return token.Type == TokenType.NumVar || token.Type == TokenType.Array;
	}
	
	private bool CheckCanConvertToStrVar(Token token)
	{
		return token.Type == TokenType.StrVar;
	}
	
	private bool CheckCanConvertToColor(Token token)
	{
		if (token.Type == TokenType.Color)
			return true;
		return token.Type == TokenType.StrConst 
			&& token.Value.Length == 7 
			&& token.Value[0] == '#' 
			&& int.TryParse(token.Value[1..7], System.Globalization.NumberStyles.HexNumber, null, out _);
	}
	
	private bool CheckCanConvertToEffect(Token token)
	{
		// @TODO: А как?
	}
	
	private bool CheckCanConvertToEnum(Token token, string[] allowedValues)
	{
		return token.Type == TokenType.Name && allowedValues.Contains(token.Value);
	}
	
	private bool CheckCanConvertToCondition(Token token)
	{
		// 
	}
}