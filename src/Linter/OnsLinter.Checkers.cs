using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;
using ONSMake.Parser;
using ONSMake.Processing;
using System.Text;

namespace ONSMake.Linter;

#nullable enable
public partial class OnsLinter
{
	private Result<object, TokenCheckError> CheckCommandCallToken(Token commandToken)
	{
		bool isExplicitlyBuiltIn = false;
        string cmdName;
        if (commandToken.Content.Length > 0 && commandToken.Content[0] == '_')
        {
            cmdName = commandToken.Content[1..].ToString();
            isExplicitlyBuiltIn = true;
        }
        else
        {
            cmdName = commandToken.Content.ToString();
        }

		if (!isExplicitlyBuiltIn && _scriptContext.CustomCommands.TryGetValue(cmdName, out CommandDefinition? definition))
			return CheckCustomCommandParameters(commandToken, definition);

        Result<object, TokenCheckError> checkResult;
        switch (cmdName)
        {
            case "getparam":
                checkResult = CheckGetParamCommandCall(commandToken);
                if (checkResult.IsError)
                    return checkResult;
                return MakeCheckOkResult();
            case "d":
            case "d2":
                checkResult = CheckDialogCommandCall(commandToken, cmdName == "d2");
                if (checkResult.IsError)
                    return checkResult;
                return MakeCheckOkResult();
        }
		if (!_defaultCommands.TryGetValue(cmdName, out List<Command>? cmdDefinitions))
			return MakeCheckErrorResult(commandToken, MessageID.ERR_UNKNOWN_COMMAND, cmdName);
		
		// Находим и проверяем все перегрузки стандартной команды
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
        for (int i = 0; i < getParamCommandToken.Children.Count; i++)
        {
            Token parameterToken = getParamCommandToken.Children[i];
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

        //List<Token> commandParameterTokens = [..commandToken.Children];
        //for (int i = 0; i < commandParameterTokens.Count; i++)
        for (int i = 0; i < commandToken.Children.Count; i++)
        {
            // Сравниваем имеющиеся параметры с ожидаемыми
            //Token receivedParamToken = commandParameterTokens[i];
            Token receivedParamToken = commandToken.Children[i];
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
                    return MakeCheckErrorResult(potentialCommandToken, MessageID._POTENTIAL_COMMAND);
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
                Parameter NUM_PARAM = new(DataType.Num);
                Parameter STR_PARAM = new(DataType.Str);

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
                    int effectNum = int.Parse(effectNumToken.Content);
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
                    if (((Token)effectDurationToken).Type == TokenType.Number && int.Parse(((Token)effectDurationToken).Content) < 1)
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

	private Result<object, TokenCheckError> CheckCustomCommandParameters(Token commandToken, CommandDefinition definition)
	{
		if (commandToken.Children.Count > definition.ParameterTypes.Length)
			return MakeCheckErrorResult(commandToken.Children[definition.ParameterTypes.Length], MessageID.ERR_TOO_MANY_PARAMETERS);
		
		Parameter NUM_PARAM = new(DataType.Num);
        Parameter STR_PARAM = new(DataType.Str);
		Parameter NUMVAR_PARAM = new(DataType.NumVar);
		Parameter STRVAR_PARAM = new(DataType.StrVar);
		Parameter selectedParam;
		Result<object, TokenCheckError> checkResult;
		
		for (int i = 0; i < definition.ParameterTypes.Length; i++)
		{
			DataType parameterType = definition.ParameterTypes[i];
			if (i >= commandToken.Children.Count)
			{
				if (parameterType == DataType.AnyVar)
					return MakeCheckErrorResult(commandToken, MessageID.ERR_TOO_FEW_PARAMETERS);
				continue;
			}
			Token token = commandToken.Children[i];
			switch (parameterType)
			{
				case DataType.Num:
					selectedParam = NUM_PARAM;
					break;
				case DataType.Str:
					selectedParam = STR_PARAM;
					break;
				case DataType.AnyVar:
                    Result<bool, TokenCheckError> isNumericResult = GuessIsOperandNumeric(token);
                    if (isNumericResult.IsError)
                        return Result<object, TokenCheckError>.Fail(isNumericResult.Error);
                    if (isNumericResult.Value)
						selectedParam = NUMVAR_PARAM;
					else
						selectedParam = STRVAR_PARAM;
					break;
				default:
					return MakeCheckErrorResult(token, MessageID.ERR_U_STUPIT);
			}
			
			checkResult = CheckIsParameterAcceptable(token, selectedParam);
			if (checkResult.IsError)
				return checkResult;
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

        if (isNumeric)
        {
            Result<bool, TokenCheckError> isNumericResult = GuessIsOperandNumeric(operandToken);
            if (isNumericResult.IsError)
                return Result<object, TokenCheckError>.Fail(isNumericResult.Error);
            if (!isNumericResult.Value)
                return MakeCheckErrorResult(operandToken, MessageID.ERR_NOT_A_NUMBER);
        }
        else
        {
            Result<bool, TokenCheckError> isStringResult = GuessIsOperandString(operandToken);
            if (isStringResult.IsError)
                return Result<object, TokenCheckError>.Fail(isStringResult.Error);
            if (!isStringResult.Value)
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
        if (ARITHMETIC_OPERATORS.Contains(operandToken.Type))
        {
            if (operandToken.Children.Count == 0)
                return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_MISSING_FIRST_OPERAND);
            if (operandToken.Children.Count == 1)
                return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_MISSING_SECOND_OPERAND);
            var opA = operandToken.Children[0];
            var opB = operandToken.Children[1];
            var opACheckResult = GuessIsOperandNumeric(opA);
            var opBCheckResult = GuessIsOperandNumeric(opB);
            if (opACheckResult.IsError)
                return opACheckResult;
            if (opBCheckResult.IsError)
                return opBCheckResult;
            if (opACheckResult.Value != opBCheckResult.Value)
                return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_INVALID_OPERAND_TYPE);
            return opACheckResult;
        }

        switch (operandToken.Type)
        {
            case TokenType.Identifier:
                string name = operandToken.Content.ToString();
                if (_scriptContext.Numaliases.ContainsKey(name))
                    return MakeTypeGuessOkResult(true);
                if (_scriptContext.Straliases.ContainsKey(name))
                    return MakeTypeGuessOkResult(false);
                return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, name);
            case TokenType.Number:
                return MakeTypeGuessOkResult(true);
            case TokenType.Variable:
                Result<object, TokenCheckError> checkResult = CheckTokenChildren(operandToken);
                if (checkResult.IsError)
                    return MakeTypeGuessErrorResult(checkResult.Error);
                bool isStringVar = operandToken.Content.SequenceEqual("$");
                return MakeTypeGuessOkResult(!isStringVar);
        }
        return MakeTypeGuessOkResult(false);
    }

    private Result<bool, TokenCheckError> GuessIsOperandString(Token operandToken)
    {
        switch (operandToken.Type)
        {
            case TokenType.Add:
                if (operandToken.Children.Count == 0)
                    return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_MISSING_FIRST_OPERAND);
                if (operandToken.Children.Count == 1)
                    return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_MISSING_SECOND_OPERAND);
                var opA = operandToken.Children[0];
                var opB = operandToken.Children[1];
                var opACheckResult = GuessIsOperandString(opA);
                var opBCheckResult = GuessIsOperandString(opB);
                if (opACheckResult.IsError)
                    return opACheckResult;
                if (opBCheckResult.IsError)
                    return opBCheckResult;
                if (opACheckResult.Value != opBCheckResult.Value)
                    return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_INVALID_OPERAND_TYPE);
                return opACheckResult;
            case TokenType.Identifier:
                string name = operandToken.Content.ToString();
                if (_scriptContext.Straliases.ContainsKey(name))
                    return MakeTypeGuessOkResult(true);
                if (_scriptContext.Numaliases.ContainsKey(name))
                    return MakeTypeGuessOkResult(false);
                return MakeTypeGuessErrorResult(operandToken, MessageID.ERR_STRALIAS_NOT_DEFINED, name);
            case TokenType.String:
                return MakeTypeGuessOkResult(true);
            case TokenType.Variable:
                Result<object, TokenCheckError> checkResult = CheckTokenChildren(operandToken);
                if (checkResult.IsError)
                    return MakeTypeGuessErrorResult(checkResult.Error);
                bool isStringVar = operandToken.Content.SequenceEqual("$");
                return MakeTypeGuessOkResult(isStringVar);
        }
        return MakeTypeGuessOkResult(false);
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
        if (parent.Content.SequenceEqual("?"))
            return CheckArrayChildren(parent);
        else
            return CheckVariableChildren(parent);
    }

    private Result<object, TokenCheckError> CheckVariableChildren(Token parent)
    {
        if (parent.Children.Count != 1)
			return MakeCheckErrorResult(parent, MessageID.ERR_VAR_WITHOUT_NUM);
        var varNumberToken = parent.Children[0];

        switch (varNumberToken.Type)
        {
            case TokenType.Identifier:
            case TokenType.Variable:
            case TokenType.Number:
                Result<bool, TokenCheckError> isNumericResult = GuessIsOperandNumeric(varNumberToken);
                if (isNumericResult.IsError)
                    return Result<object, TokenCheckError>.Fail(isNumericResult.Error);
                if (!isNumericResult.Value)
                    return MakeCheckErrorResult(varNumberToken, MessageID.ERR_NOT_A_NUMBER);
                break;
            default:
                return MakeCheckErrorResult(varNumberToken, MessageID.ERR_NOT_A_NUMBER);
        }
        return MakeCheckOkResult();
    }

    private Result<object, TokenCheckError> CheckArrayChildren(Token parent)
    {
		Result<object, TokenCheckError> checkResult;
		
        if (parent.Children.Count < 2)
			return MakeCheckErrorResult(parent, MessageID.ERR_ARRAY_WITHOUT_INDEX);
        var arrayNumberToken = parent.Children[0];

        switch (arrayNumberToken.Type)
        {
            case TokenType.Identifier:
            case TokenType.Variable:
            case TokenType.Number:
                Result<bool, TokenCheckError> isNumericResult = GuessIsOperandNumeric(arrayNumberToken);
                if (isNumericResult.IsError)
                    return Result<object, TokenCheckError>.Fail(isNumericResult.Error);
                if (!isNumericResult.Value)
                    return MakeCheckErrorResult(arrayNumberToken, MessageID.ERR_NOT_A_NUMBER);
                break;
            default:
                return MakeCheckErrorResult(arrayNumberToken, MessageID.ERR_NOT_A_NUMBER);
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
