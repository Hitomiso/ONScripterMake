using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;
using ONSMake.Parser;

namespace ONSMake.Linter;

#nullable enable
public partial class OnsLinter
{
	private Result<object, TokenCheckError> CheckCommandCallToken(Token commandToken)
	{
		Result<object, TokenCheckError> checkResult;
		
		string cmdName = commandToken.Content.ToLower();
		if (_scriptContext.CustomCommands.ContainsKey(cmdName))
        {
            // @FIXME: Кастомные команды пока пропускаем
			return MakeCheckOkResult();
        }
		
		if (cmdName.Length > 0 && cmdName[0] == '_')
			cmdName = cmdName[1..];
		if (cmdName == "getparam")
		{
			checkResult = CheckGetParamCommandCall(commandToken);
            if (checkResult.IsError)
                return checkResult;
			return MakeCheckOkResult();
		}
		if (!_defaultCommands.ContainsKey(cmdName))
			return MakeCheckErrorResult(commandToken, MessageID.ERR_UNKNOWN_COMMAND, cmdName);
		
		// Находим и проверяем все перегрузки стандартных команд
		List<Command> cmdDefinitions = _defaultCommands[cmdName];
		for (int i = 0; i < cmdDefinitions.Count; i++)
		{
			checkResult = CheckCommandParameters(commandToken, cmdDefinitions[i]);
			if (!checkResult.IsError)
				return MakeCheckOkResult();
			if (i + 1 >= cmdDefinitions.Count)
				return checkResult;
		}
		return MakeCheckOkResult();
	}
	
	private Result<object, TokenCheckError> CheckGetParamCommandCall(Token getParamCommandToken)
	{
		// Читаем переменные, массивы и указатели на переменные
		foreach (var parameterToken in getParamCommandToken.Children)
		{
			if (parameterToken.Type != TokenType.Variable && parameterToken.Type != TokenType.IndexVariable)
				return MakeCheckErrorResult(parameterToken, MessageID.ERR_NUMVAR_EXPECTED);
			switch (parameterToken.Content)
			{
				case "%":
				case "$":
				case "?":
				case "i%":
					Result<object, TokenCheckError> checkResult = CheckTokenChildren(parameterToken);
					if (checkResult.IsError)
						return checkResult;
					break;
				default:
					return MakeCheckErrorResult(parameterToken, MessageID.ERR_NUMVAR_EXPECTED);
			}
		}
		return MakeCheckOkResult();
	}

    private Result<object, TokenCheckError> CheckCommandParameters(Token commandToken, Command definition)
    {
		Result<object, TokenCheckError> checkResult;
		
		List<Token> commandParameterTokens = [..commandToken.Children];
        for (int i = 0; i < commandParameterTokens.Count; i++)
        {
            // Сравниваем имеющиеся параметры с ожидаемыми
			Token receivedParamToken = commandParameterTokens[i];
			Parameter param;
            if (i >= definition.Parameters.Length)
			{
                if (definition.Parameters.Length == 0 || !definition.Parameters[^1].Repeat)
                {
                    Token potentialCommandToken;
                    if (definition.Parameters.Length == 0 && ARITHMETIC_OPERATORS.Contains(receivedParamToken.Type) && receivedParamToken.Children.Count == 2)
                        potentialCommandToken = receivedParamToken.Children[0];
                    else
                        potentialCommandToken = receivedParamToken;
                    if (potentialCommandToken.Type != TokenType.Identifier)
						return MakeCheckErrorResult(potentialCommandToken, MessageID.ERR_TOO_MANY_PARAMETERS);
                    return MakeCheckErrorResult(potentialCommandToken, MessageID.POTENTIAL_COMMAND);
                }
                param = definition.Parameters[^1];
			}
			else
			{
				param = definition.Parameters[i];
			}
            if (param.Type == DataType.Effect)
            {
                // Effect: NUM[,NUM[,NUM[,STR]]]
                Parameter NUM_PARAM = new(DataType.Num, "num");
                Parameter STR_PARAM = new(DataType.Str, "str");

                Token effectNumToken = receivedParamToken;
                Token? effectDurationToken = null;
                Token? effectMaskToken = null;
                if (i + 1 < commandToken.Children.Count)
                    effectDurationToken = commandToken.Children[i + 1];
                if (i + 2 < commandToken.Children.Count)
                    effectMaskToken = commandToken.Children[i + 2];
                if (i + 3 < commandToken.Children.Count)
					return MakeCheckErrorResult(commandToken.Children[i + 3], MessageID.ERR_TOO_MANY_PARAMETERS);
				checkResult = CheckIsParameterAcceptable(effectNumToken, NUM_PARAM);
                if (checkResult.IsError)
                    return checkResult;
                if (effectNumToken.Type == TokenType.Number)
                {
                    int effectNum = Convert.ToInt32(effectNumToken.Content);
                    if (effectDurationToken != null)
                    {
                        if (effectNum == 1)
							return MakeCheckErrorResult((Token)effectDurationToken, MessageID.ERR_TOO_MANY_PARAMETERS);
                        if ((effectNum < 1 || effectNum > 18) && effectNum != 99)
							return MakeCheckErrorResult(effectNumToken, MessageID.ERR_INVALID_BUILT_IN_EFFECT);
                        bool maskRequired = effectNum == 15 || effectNum == 18 || effectNum == 99;
                        if (maskRequired && effectMaskToken == null)
							return MakeCheckErrorResult((Token)effectDurationToken, MessageID.ERR_EFFECT_MASK_EXPECTED);
                        if (!maskRequired && effectMaskToken != null)
							return MakeCheckErrorResult((Token)effectMaskToken, MessageID.ERR_UNEXPECTED_EFFECT_MASK);
                    }
                }
                if (effectDurationToken != null)
                {
                    if (((Token)effectDurationToken).Type == TokenType.Number && Convert.ToInt32(((Token)effectDurationToken).Content) < 1)
						return MakeCheckErrorResult((Token)effectDurationToken, MessageID.ERR_INVALID_EFFECT_DURATION);
					checkResult = CheckIsParameterAcceptable((Token)effectDurationToken, NUM_PARAM);
                    if (checkResult.IsError)
                        return checkResult;
                }
                if (effectMaskToken != null)
                {
					checkResult = CheckIsParameterAcceptable((Token)effectMaskToken, STR_PARAM);
                    if (checkResult.IsError)
                        return checkResult;
                }
				return MakeCheckOkResult();
            }
            else
            {
				checkResult = CheckIsParameterAcceptable(receivedParamToken, param);
                if (checkResult.IsError)
                    return checkResult;
            }
        }
		return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckIsParameterAcceptable(Token token, Parameter definition)
    {
		Result<object, TokenCheckError> checkResult;
		
        if (ARITHMETIC_OPERATORS.Contains(token.Type))
        {
            if (definition.Type != DataType.Num && definition.Type != DataType.Str)
				return MakeCheckErrorResult(token, MessageID.ERR_EXPRESSIONS_NOT_ALLOWED);
            bool isNumericExpression = definition.Type == DataType.Num;
			checkResult = CheckArithmeticExpression(token, isNumericExpression);
            if (checkResult.IsError)
                return checkResult;
			return MakeCheckOkResult();
        }
        if (COMPARISON_OPERATORS.Contains(token.Type) || LOGICAL_OPERATORS.Contains(token.Type))
        {
            if (definition.Type != DataType.Condition)
				return MakeCheckErrorResult(token, MessageID.ERR_CONDITIONS_NOT_ALLOWED);
			checkResult = CheckLogicExpression(token);
            if (checkResult.IsError)
                return checkResult;
			return MakeCheckOkResult();
        }
        if (definition.Type == DataType.Enum)
        {
            if (definition.EnumValues == null)
                throw new NullReferenceException();
			checkResult = CheckCanConvertToEnum(token, definition.EnumValues);
            if (checkResult.IsError)
                return checkResult;
        }
        else
        {
			checkResult = CheckCanConvert(token, definition.Type);
            if (checkResult.IsError)
                return checkResult;
        }
		return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckArithmeticExpression(Token operatorToken, bool isNumeric)
    {
        if (operatorToken.Children.Count == 0)
			return MakeCheckErrorResult(operatorToken, MessageID.ERR_MISSING_FIRST_OPERAND);
        if (operatorToken.Children.Count == 1)
			return MakeCheckErrorResult(operatorToken, MessageID.ERR_MISSING_SECOND_OPERAND);

        var opA = operatorToken.Children[0];
        var opB = operatorToken.Children[1];
        // Строковые операнды принимает только оператор +
        if (!isNumeric && operatorToken.Type != TokenType.Add)
			return MakeCheckErrorResult(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
		Result<object, TokenCheckError> checkResult = CheckExpressionOperand(opA, isNumeric);
        if (checkResult.IsError)
            return checkResult;
		checkResult = CheckExpressionOperand(opB, isNumeric);
        if (checkResult.IsError)
            return checkResult;
		return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckExpressionOperand(Token operandToken, bool isNumeric)
    {
        if (ARITHMETIC_OPERATORS.Contains(operandToken.Type))
            return CheckArithmeticExpression(operandToken, isNumeric);
		Result<object, TokenCheckError> checkResult = CheckTokenChildren(operandToken);
        if (checkResult.IsError)
            return checkResult;
        if (operandToken.Type == TokenType.Identifier)
        {
            if (isNumeric)
            {
                if (!_scriptContext.Numaliases.ContainsKey(operandToken.Content.ToLower()))
					return MakeCheckErrorResult(operandToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, operandToken.Content);
            }
            else
            {
                if (!_scriptContext.Straliases.ContainsKey(operandToken.Content.ToLower()))
					return MakeCheckErrorResult(operandToken, MessageID.ERR_STRALIAS_NOT_DEFINED, operandToken.Content);
            }
			return MakeCheckOkResult();
        }
        if (isNumeric)
        {
            if (!IsTokenNumeric(operandToken))
				return MakeCheckErrorResult(operandToken, MessageID.ERR_NOT_A_NUMBER);
        }
        else
        {
            if (!IsTokenString(operandToken))
				return MakeCheckErrorResult(operandToken, MessageID.ERR_NOT_A_STRING);
        }
		return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckLogicExpression(Token operatorToken)
    {
		Result<object, TokenCheckError> checkResult;
		
        if (operatorToken.Children.Count == 0)
			return MakeCheckErrorResult(operatorToken, MessageID.ERR_MISSING_FIRST_OPERAND);   
        if (operatorToken.Children.Count == 1)
			return MakeCheckErrorResult(operatorToken, MessageID.ERR_MISSING_SECOND_OPERAND);

        var opA = operatorToken.Children[0];
        var opB = operatorToken.Children[1];
        bool isLogical = LOGICAL_OPERATORS.Contains(operatorToken.Type);
        bool isComparison = COMPARISON_OPERATORS.Contains(operatorToken.Type);
        if (isLogical)
        {
            if (!COMPARISON_OPERATORS.Contains(opA.Type) && !LOGICAL_OPERATORS.Contains(opA.Type))
				return MakeCheckErrorResult(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
            if (!COMPARISON_OPERATORS.Contains(opB.Type) && !LOGICAL_OPERATORS.Contains(opB.Type))
				return MakeCheckErrorResult(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
			checkResult = CheckLogicExpression(opA);
            if (checkResult.IsError)
                return checkResult;
			checkResult = CheckLogicExpression(opB);
            if (checkResult.IsError)
				return checkResult;
        }
        else if (isComparison)
        {
            if (COMPARISON_OPERATORS.Contains(opA.Type) || LOGICAL_OPERATORS.Contains(opA.Type))
				return MakeCheckErrorResult(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
			if (COMPARISON_OPERATORS.Contains(opB.Type) || LOGICAL_OPERATORS.Contains(opB.Type))
				return MakeCheckErrorResult(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
            
			Result<bool, TokenCheckError> opAIsNumericResult = GuessIsOperandNumeric(opA);
			Result<bool, TokenCheckError> opBIsNumericResult = GuessIsOperandNumeric(opB);
            if (opAIsNumericResult.IsError)
                return Result<object, TokenCheckError>.Fail(opAIsNumericResult.Error);
            if (opBIsNumericResult.IsError)
                return Result<object, TokenCheckError>.Fail(opBIsNumericResult.Error);
            if (opAIsNumericResult.Value != opBIsNumericResult.Value)
				return MakeCheckErrorResult(operatorToken, MessageID.ERR_COMPARE_OPERAND_TYPE_MISMATCH);
			
			checkResult = CheckExpressionOperand(opA, opAIsNumericResult.Value);
			if (checkResult.IsError)
				return checkResult;
			checkResult = CheckExpressionOperand(opB, opBIsNumericResult.Value);
			if (checkResult.IsError)
				return checkResult;
        }
        else
        {
			return MakeCheckErrorResult(operatorToken, MessageID.ERR_UNKNOWN_CONDITION_OPERATOR_TYPE);
        }
		return MakeCheckOkResult();
    }

    private Result<bool, TokenCheckError> GuessIsOperandNumeric(Token operandToken)
    {
        if (operandToken.Type == TokenType.Identifier)
        {
            if (_scriptContext.Numaliases.ContainsKey(operandToken.Content.ToLower()))
				return MakeIsNumericOkResult(true);
            if (_scriptContext.Straliases.ContainsKey(operandToken.Content.ToLower()))
				return MakeIsNumericOkResult(false);
			return MakeIsNumericErrorResult(operandToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, operandToken.Content);
        }
        if (!ARITHMETIC_OPERATORS.Contains(operandToken.Type))
			return MakeIsNumericOkResult(IsTokenNumeric(operandToken));

		if (operandToken.Children.Count == 0)
			return MakeIsNumericErrorResult(operandToken, MessageID.ERR_MISSING_FIRST_OPERAND);
		if (operandToken.Children.Count == 1)
			return MakeIsNumericErrorResult(operandToken, MessageID.ERR_MISSING_SECOND_OPERAND);
		var opA = operandToken.Children[0];
		var opB = operandToken.Children[1];
		var opACheckResult = GuessIsOperandNumeric(opA);
		var opBCheckResult = GuessIsOperandNumeric(opB);
		if (opACheckResult.IsError)
			return opACheckResult;
		if (opBCheckResult.IsError)
			return opBCheckResult;
		if (opACheckResult.Value != opBCheckResult.Value)
			return MakeIsNumericErrorResult(operandToken, MessageID.ERR_INVALID_OPERAND_TYPE);
		return opACheckResult;
    }



    private Result<object, TokenCheckError> CheckTokenChildren(Token token)
    {
        if (!_tokenChildrenHandlers.TryGetValue(token.Type, out CheckerDelegate? value))
			return MakeCheckErrorResult(token, MessageID.ERR_U_STUPIT);
        return value(token);
    }

    private Result<object, TokenCheckError> CheckCanConvert(Token token, DataType targetType)
    {
        if (!_canConvertHandlers.TryGetValue(targetType, out CheckerDelegate? value))
			return MakeCheckErrorResult(token, MessageID.ERR_U_STUPIT);
        return value(token);
    }

    private Result<object, TokenCheckError> CheckNoChildren(Token parent)
    {
        if (parent.Children.Count > 0)
			return MakeCheckErrorResult(parent.Children[0], MessageID.ERR_TOO_MANY_TOKEN_CHILDREN);
		return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckVariableOrArrayChildren(Token parent)
    {
        if (parent.Type != TokenType.Variable)
            throw new ArgumentException("Variable or array parent expected.");
        if (parent.Content == "?")
            return CheckArrayChildren(parent);
        else
            return CheckVariableChildren(parent);
    }

    private Result<object, TokenCheckError> CheckVariableChildren(Token parent)
    {
        if (parent.Children.Count != 1)
			return MakeCheckErrorResult(parent, MessageID.ERR_VAR_WITHOUT_NUM);

        var varNumberToken = parent.Children[0];
        if (!IsTokenNumeric(varNumberToken))
			return MakeCheckErrorResult(varNumberToken, MessageID.ERR_NOT_A_NUMBER);
        if (varNumberToken.Type == TokenType.Identifier)
        {
            if (!_scriptContext.Numaliases.ContainsKey(varNumberToken.Content.ToLower()))
				return MakeCheckErrorResult(varNumberToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, varNumberToken.Content);
        }
        else if (varNumberToken.Type == TokenType.Variable)
        {
            if (varNumberToken.Content == "%")
                return CheckVariableChildren(varNumberToken);
            else if (varNumberToken.Content == "?")
                return CheckArrayChildren(varNumberToken);
        }
		return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckArrayChildren(Token parent)
    {
		Result<object, TokenCheckError> checkResult;
		
        if (parent.Children.Count < 2)
			return MakeCheckErrorResult(parent, MessageID.ERR_ARRAY_WITHOUT_INDEX);
        var arrayNumberToken = parent.Children[0];
        if (!IsTokenNumeric(arrayNumberToken))
			return MakeCheckErrorResult(arrayNumberToken, MessageID.ERR_NOT_A_NUMBER);
        if (arrayNumberToken.Type == TokenType.Identifier)
        {
            if (!_scriptContext.Numaliases.ContainsKey(arrayNumberToken.Content.ToLower()))
				return MakeCheckErrorResult(arrayNumberToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, arrayNumberToken.Content);
        }
        else if (arrayNumberToken.Type == TokenType.Variable)
        {
            if (arrayNumberToken.Content == "%")
            {
				checkResult = CheckVariableChildren(arrayNumberToken);
                if (checkResult.IsError)
                    return checkResult;
            }
            else if (arrayNumberToken.Content == "?")
            {
				checkResult = CheckArrayChildren(arrayNumberToken);
                if (checkResult.IsError)
                    return checkResult;
            }
        }

        for (int i = 1; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            // Эти дети уже могут быть числовыми выражениями
            if (ARITHMETIC_OPERATORS.Contains(child.Type))
                return CheckArithmeticExpression(child, isNumeric: true);
			if (COMPARISON_OPERATORS.Contains(child.Type) || LOGICAL_OPERATORS.Contains(child.Type))
				return MakeCheckErrorResult(child, MessageID.ERR_CONDITIONS_NOT_ALLOWED);

			checkResult = CheckTokenChildren(child);
            if (checkResult.IsError)
                return checkResult;
			checkResult = CheckCanConvert(child, DataType.Num);
            if (checkResult.IsError)
                return checkResult;
        }
		return MakeCheckOkResult();
    }
}
#nullable restore
