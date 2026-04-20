using System;
using System.Collections.Generic;
using System.Linq;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake.Parsing;

public static partial class Parser
{
	private static bool CheckIsNumeric(Token token)
	{
		switch (token.Type)
		{
			case TokenType.Name:
			case TokenType.NumConst:
			case TokenType.NumVar:
			case TokenType.Array:
				return true;
			default:
				return false;
		}
	}
	
	private static bool CheckIsString(Token token)
	{
		switch (token.Type)
		{
			case TokenType.Name:
			case TokenType.StrConst:
			case TokenType.StrVar:
				return true;
			default:
				return false;
		}
	}
	
	private static void SquishVariables(ref List<Token> tokens)
	{
		for (int i = 0; i < tokens.Count; i++)
		{
			var token = tokens[i];
			switch (token.Type)
			{
				case TokenType.Array:
				case TokenType.NumVar:
					if (token.Children.Count == 0)
						tokens.Insert(i, ParseNumeric(tokens, i));
					break;
				case TokenType.StrVar:
					if (token.Children.Count == 0)
						tokens.Insert(i, ParseString(tokens, i));
					break;
				case TokenType.OpenBracket:
					List<Token> bracketsContent = ReadRoundBrackets(tokens, i);
					tokens.Insert(i, ParseLogicExpression(bracketsContent));
					break;
				case TokenType.CloseBracket:
				case TokenType.OpenSquareBracket:
				case TokenType.CloseSquareBracket:
					throw new ParseException(token, MessageID.ERR_UNEXPECTED_BRACKET);
			}
		}
	}
	
	private static Token ParseNumeric(List<Token> tokens, int startIndex)
	{
		var token = PopTokenFromList(tokens, startIndex);
		switch (token.Type)
		{
			case TokenType.Name:
			case TokenType.NumConst:
				return token;
			case TokenType.NumVar:
				var varNumber = ParseNumeric(tokens, startIndex);
				token.Children.Add(varNumber);
				return token;
			case TokenType.Array:
				var arrNumber = ParseNumeric(tokens, startIndex);
				token.Children.Add(arrNumber);
				if (tokens[startIndex].Type != TokenType.OpenSquareBracket)
					throw new ParseException(token, MessageID.ERR_OPENING_BRACKET_EXPECTED);
				do
				{
					List<Token> bracketTokens = ReadSquareBrackets(tokens, startIndex);
					var arrIndex = ParseArithmeticExpression(bracketTokens);
					token.Children.Add(arrIndex);
					if (startIndex >= tokens.Count)
						break;
				}
				while (tokens[startIndex].Type == TokenType.OpenSquareBracket);				
				return token;
			default:
				throw new ParseException(token, MessageID.ERR_NOT_A_NUMBER);
		}
	}
	
	private static Token ParseString(List<Token> tokens, int startIndex)
	{
		var token = PopTokenFromList(tokens, startIndex);
		switch (token.Type)
		{
			case TokenType.Name:
			case TokenType.StrConst:
				return token;
			case TokenType.StrVar:
				var varNumber = ParseNumeric(tokens, startIndex);
				token.Children.Add(varNumber);
				return token;
			default:
				throw new ParseException(token, MessageID.ERR_NOT_A_STRING);
		}
	}
	
	private static List<Token> ReadRoundBrackets(List<Token> tokens, int index) => 
		ReadBrackets(tokens, index, TokenType.OpenBracket, TokenType.CloseBracket);
	
	private static List<Token> ReadSquareBrackets(List<Token> tokens, int index) =>
		ReadBrackets(tokens, index, TokenType.OpenSquareBracket, TokenType.CloseSquareBracket);
	
	private static List<Token> ReadBrackets(List<Token> tokens, int index, TokenType openingType, TokenType closingType)
	{
		if (tokens[index].Type != openingType)
			throw new ParseException(tokens[index], MessageID.ERR_OPENING_BRACKET_EXPECTED);
		
		List<Token> bracketsContent = [];
		int depth = 0;
		Token token = null;
		while (index < tokens.Count)
		{
			token = PopTokenFromList(tokens, index);
			if (token.Type == openingType)
				depth++;
			else if (token.Type == closingType)
				depth--;
			if (depth == 0)
				return bracketsContent;
			else if (depth > 1 || token.Type != openingType)
				bracketsContent.Add(token);
		}
		throw new ParseException(token, MessageID.ERR_CLOSING_BRACKET_EXPECTED);
	}
	
	private static Token PopTokenFromList(List<Token> tokens, int index)
	{
		var token = tokens[index];
		tokens.RemoveAt(index);
		return token;
	}
	
	private static Token ParseArithmeticExpression(List<Token> tokens)
	{
		SquishVariables(ref tokens);
		
		Token operatorToken;
		if (tokens.Count > 0 && tokens[0].Type == TokenType.Operator)
		{
			operatorToken = tokens[0];
			if (operatorToken.Value == "-")
			{
				// Вставляем 2 токена вместо 1 минуса
				tokens[0] = new Token(TokenType.NumConst, "-1", operatorToken.StartColumn);
				tokens.Insert(1, new Token(TokenType.Operator, "*", operatorToken.StartColumn));
			}
			else if (tokens.Count >= 2 && operatorToken.Value == "*" && tokens[1].Type == TokenType.Name)
			{
				tokens[1].Value = "*" + tokens[1].Value;
				tokens[1].Type = TokenType.Label;
				tokens.RemoveAt(0);
			}
		}
		
		tokens = BuildOperatorTree(tokens, TokenType.Operator);
		
		if (tokens.Count == 0)
			throw new NotImplementedException();
		if (tokens.Count > 1)
			throw new ParseException();
		return PopTokenFromList(tokens, 0);
	}
	
	private static Token ParseLogicExpression(List<Token> tokens)
	{
		// Сначала выполняем арифметику между логическими операторами, потом сами логические операторы
		List<Token> logicExpression = [];
		List<Token> arithmeticExpression = [];
		foreach (Token token in tokens)
		{
			if (token.Type == TokenType.ConditionOperator)
			{
				Token expr = ParseArithmeticExpression(ref arithmeticExpression);
				logicExpression.Add(expr);
				logicExpression.Add(token);
				arithmeticExpression.Clear();
			}
			else
				arithmeticExpression.Add(token);
		}
		if (arithmeticExpression.Count > 0) {
			Token expr = ParseArithmeticExpression(ref arithmeticExpression);
			logicExpression.Add(expr);
			arithmeticExpression.Clear();
		}
		
		logicExpression = BuildOperatorTree(logicExpression, TokenType.ConditionOperator);
		
		// (Может, здесь можно найти унарные операторы?)
		// @TODO: Унарные операторы можно записать к условным и проверять так же, как минус, чтобы перед ним не было операнда
		if (logicExpression.Count == 0)
			throw new NotImplementedException();
		if (logicExpression.Count > 1)
			throw new ParseException(tokens[1], MessageID.ERR_);
		return PopTokenFromList(logicExpression, 0);
	}
	
	private static List<Token> BuildOperatorTree(List<Token> tokens, TokenType targetOperatorType)
	{
		// tokens = [..tokens];
		int opIndex = FindHighestPriorityOperatorIndex(tokens, targetOperatorType);
		while (opIndex >= 0)
		{
			// Если бинарный оператор найден, добавляем ему операнды
			Token operatorToken = tokens[opIndex];
			// @FIXME: Указывается неправильная ошибка
			// test.txt:17:27 error: First operand is missing.
			// if %wewe + 5 < (0 - (-8)) * 2 / mov $wewe,"ewe"
			//                           ^
			if (opIndex + 1 >= tokens.Count)
				throw new ParseException(tokens[opIndex], MessageID.ERR_MISSING_SECOND_OPERAND);
			if (opIndex - 1 < 0)
				throw new ParseException(tokens[opIndex], MessageID.ERR_MISSING_FIRST_OPERAND);
			
			var operandB = PopTokenFromList(tokens, opIndex + 1);
			var operandA = PopTokenFromList(tokens, opIndex - 1);
			if (operandA.Type == TokenType.Operator && operandA.Children.Count == 0)
				throw new ParseException(operandA, MessageID.ERR_MISSING_FIRST_OPERAND);
			if (operandB.Type == TokenType.Operator && operandB.Children.Count == 0)
				throw new ParseException(operandB, MessageID.ERR_MISSING_FIRST_OPERAND);
			
			operatorToken.Children.Add(operandA);
			operatorToken.Children.Add(operandB);
			opIndex = FindHighestPriorityOperatorIndex(tokens, targetOperatorType);
		}
		return tokens;
	}
	
	private static int FindHighestPriorityOperatorIndex(List<Token> tokens, TokenType targetOperatorType)
	{
		int opPriority = -1;
		int opIndex = -1;
		for (int i = 0; i < tokens.Count; i++)
		{
			// @TODO: Находить унарные операторы (можно выше)
			var token = tokens[i];
			if (token.Type != targetOperatorType)
				continue;
			if (!OPERATORS_PRIORITY.ContainsKey(token.Value))
				throw new ParseException(token, MessageID.ERR_UNKNOWN_OPERATOR_PRIORITY, token.Value);
			if (OPERATORS_PRIORITY[token.Value] > opPriority)
			{
				if (token.Children.Count > 0)
					continue;
				opIndex = i;
				opPriority = OPERATORS_PRIORITY[token.Value];
			}
		}
		return opIndex;
	}
}