using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Parser;

namespace Hitomiso.ONScripterMake.Linter;

#nullable enable
public partial class OnsLinter
{
	private bool CheckCommandCallToken(Token commandToken, out TokenCheckError? error)
	{
		string cmdName = commandToken.Content.ToLower();
		if (_scriptContext.CustomCommands.ContainsKey(cmdName))
        {
            // @FIXME: Кастомные команды пока пропускаем
            error = null;
            return true;
        }
		
		if (cmdName.StartsWith("_"))
			cmdName = cmdName[1..];
		if (cmdName == "getparam")
		{
            if (!CheckGetParamCommandCall(commandToken, out error))
                return false;
			error = null;
            return true;
		}
		if (!_defaultCommands.ContainsKey(cmdName))
        {
            error = new TokenCheckError(commandToken, MessageID.ERR_UNKNOWN_COMMAND, cmdName);
            return false;
        }
		
		// Находим и проверяем все перегрузки стандартных команд
		List<Command> cmdDefinitions = _defaultCommands[cmdName];
		for (int i = 0; i < cmdDefinitions.Count; i++)
		{
            if (CheckCommandParameters(commandToken, cmdDefinitions[i], out error))
            {
                error = null; // Всё хорошо. Прекращаем читать дргуие перегрузки команды
                return true;
            }
            else if (i + 1 >= cmdDefinitions.Count)
            {
                return false;
            }
		}
        error = null;
        return true;
	}
	
	private bool CheckGetParamCommandCall(Token getParamCommandToken, out TokenCheckError? error)
	{
		// Читаем переменные, массивы и указатели на переменные
		foreach (var parameterToken in getParamCommandToken.Children)
		{
			if (parameterToken.Type == TokenType.Variable || parameterToken.Type == TokenType.IndexVariable)
			{
				switch (parameterToken.Content)
				{
					case "%":
					case "$":
					case "?":
					case "i%":
						if (!CheckTokenChildren(parameterToken, out error))
                            return false;
						break;
					default:
						error = new TokenCheckError(parameterToken, MessageID.ERR_NUMVAR_EXPECTED);
                        return false;
				}
			}
			else
			{
				error = new TokenCheckError(parameterToken, MessageID.ERR_NUMVAR_EXPECTED);
                return false;
			}
		}
        error = null;
        return true;
	}

    private bool CheckCommandParameters(Token commandToken, Command definition, out TokenCheckError? error)
    {
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
                    {
                        error = new TokenCheckError(potentialCommandToken, MessageID.ERR_TOO_MANY_PARAMETERS);
                        return false;
                    }
                    throw new PotentialCommandInvocationException(potentialCommandToken);
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
                Token? effectDurationToken = commandToken.Children.ElementAtOrDefault(i + 1);
                Token? effectMaskToken = commandToken.Children.ElementAtOrDefault(i + 2);
                if (i + 3 < commandToken.Children.Count)
                {
                    error = new TokenCheckError(commandToken.Children[i + 3], MessageID.ERR_TOO_MANY_PARAMETERS);
                    return false;
                }

                if (!CheckIsParameterAcceptable(effectNumToken, NUM_PARAM, out error))
                    return false;
                if (effectNumToken.Type == TokenType.Number)
                {
                    int effectNum = Convert.ToInt32(effectNumToken.Content);
                    if (effectDurationToken != null)
                    {
                        if (effectNum == 1)
                        {
                            error = new TokenCheckError(effectDurationToken, MessageID.ERR_TOO_MANY_PARAMETERS);
                            return false;
                        }
                        if ((effectNum < 1 || effectNum > 18) && effectNum != 99)
                        {
                            error = new TokenCheckError(effectNumToken, MessageID.ERR_INVALID_BUILT_IN_EFFECT);
                            return false;
                        }
                        bool maskRequired = effectNum == 15 || effectNum == 18 || effectNum == 99;
                        if (maskRequired && effectMaskToken == null)
                        {
                            error = new TokenCheckError(effectDurationToken, MessageID.ERR_EFFECT_MASK_EXPECTED);
                            return false;
                        }
                        if (!maskRequired && effectMaskToken != null)
                        {
                            error = new TokenCheckError(effectMaskToken, MessageID.ERR_UNEXPECTED_EFFECT_MASK);
                            return false;
                        }
                    }
                }
                if (effectDurationToken != null)
                {
                    if ((effectDurationToken).Type == TokenType.Number && Convert.ToInt32((effectDurationToken).Content) < 1)
                    {
                        error = new TokenCheckError(effectDurationToken, MessageID.ERR_INVALID_EFFECT_DURATION);
                        return false;
                    }
                    if (!CheckIsParameterAcceptable(effectDurationToken, NUM_PARAM, out error))
                        return false;
                }
                if (effectMaskToken != null)
                {
                    if (!CheckIsParameterAcceptable(effectMaskToken, STR_PARAM, out error))
                        return false;
                }
                error = null;
                return true;
            }
            else
            {
                if (!CheckIsParameterAcceptable(receivedParamToken, param, out error))
                    return false;
            }
        }
        error = null;
        return true;
    }

    private bool CheckIsParameterAcceptable(Token token, Parameter definition, out TokenCheckError? error)
    {
        if (ARITHMETIC_OPERATORS.Contains(token.Type))
        {
            if (definition.Type != DataType.Num && definition.Type != DataType.Str)
            {
                error = new TokenCheckError(token, MessageID.ERR_EXPRESSIONS_NOT_ALLOWED);
                return false;
            }
            bool isNumericExpression = definition.Type == DataType.Num;
            if (!CheckArithmeticExpression(token, isNumericExpression, out error))
                return false;
            error = null;
            return true;
        }
        if (COMPARISON_OPERATORS.Contains(token.Type) || LOGICAL_OPERATORS.Contains(token.Type))
        {
            if (definition.Type != DataType.Condition)
            {
                error = new TokenCheckError(token, MessageID.ERR_CONDITIONS_NOT_ALLOWED);
                return false;
            }
            if (!CheckLogicExpression(token, out error))
                return false;
            error = null;
            return true;
        }
        if (definition.Type == DataType.Enum)
        {
            if (definition.EnumValues == null)
                throw new NullReferenceException();
            if (!CheckCanConvertToEnum(token, definition.EnumValues, out error))
                return false;
        }
        else
        {
            if (!CheckCanConvert(token, definition.Type, out error))
                return false;
        }
        error = null;
        return true;
    }

    private bool CheckArithmeticExpression(Token operatorToken, bool isNumeric, out TokenCheckError? error)
    {
        if (operatorToken.Children.Count == 0)
        {
            error = new TokenCheckError(operatorToken, MessageID.ERR_MISSING_FIRST_OPERAND);
            return false;
        }
        if (operatorToken.Children.Count == 1)
        {
            error = new TokenCheckError(operatorToken, MessageID.ERR_MISSING_SECOND_OPERAND);
            return false;
        }

        var opA = operatorToken.Children[0];
        var opB = operatorToken.Children[1];
        // Строковые операнды принимает только оператор +
        if (!isNumeric && operatorToken.Type != TokenType.Add)
        {
            error = new TokenCheckError(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
            return false;
        }
        if (!CheckExpressionOperand(opA, isNumeric, out error))
            return false;
        if (!CheckExpressionOperand(opB, isNumeric, out error))
            return false;
        error = null;
        return true;
    }

    private bool CheckExpressionOperand(Token operandToken, bool isNumeric, out TokenCheckError? error)
    {
        if (ARITHMETIC_OPERATORS.Contains(operandToken.Type))
            return CheckArithmeticExpression(operandToken, isNumeric, out error);
        if (!CheckTokenChildren(operandToken, out error))
            return false;
        if (operandToken.Type == TokenType.Identifier)
        {
            if (isNumeric)
            {
                if (!_scriptContext.NumaliasDefinitions.ContainsKey(operandToken.Content))
                {
                    error = new TokenCheckError(operandToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, operandToken.Content);
                    return false;
                }
            }
            else
            {
                if (!_scriptContext.StraliasDefinitions.ContainsKey(operandToken.Content))
                {
                    error = new TokenCheckError(operandToken, MessageID.ERR_STRALIAS_NOT_DEFINED, operandToken.Content);
                    return false;
                }
            }
            error = null;
            return true;
        }
        if (isNumeric)
        {
            if (!IsTokenNumeric(operandToken))
            {
                error = new TokenCheckError(operandToken, MessageID.ERR_NOT_A_NUMBER);
                return false;
            }
        }
        else
        {
            if (!IsTokenString(operandToken))
            {
                error = new TokenCheckError(operandToken, MessageID.ERR_NOT_A_STRING);
                return false;
            }
        }
        error = null;
        return true;
    }

    private bool CheckLogicExpression(Token operatorToken, out TokenCheckError? error)
    {
        if (operatorToken.Children.Count == 0)
        {
            error = new TokenCheckError(operatorToken, MessageID.ERR_MISSING_FIRST_OPERAND);
            return false;
        }   
        if (operatorToken.Children.Count == 1)
        {
            error = new TokenCheckError(operatorToken, MessageID.ERR_MISSING_SECOND_OPERAND);
            return false;
        }

        var opA = operatorToken.Children[0];
        var opB = operatorToken.Children[1];
        bool isLogical = LOGICAL_OPERATORS.Contains(operatorToken.Type);
        bool isComparison = COMPARISON_OPERATORS.Contains(operatorToken.Type);
        if (isLogical)
        {
            if (!COMPARISON_OPERATORS.Contains(opA.Type) && !LOGICAL_OPERATORS.Contains(opA.Type))
            {
                error = new TokenCheckError(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
                return false;
            }
            if (!COMPARISON_OPERATORS.Contains(opB.Type) && !LOGICAL_OPERATORS.Contains(opB.Type))
            {
                error = new TokenCheckError(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
                return false;
            }
            if (!CheckLogicExpression(opA, out error))
                return false;
            if (!CheckLogicExpression(opB, out error))
                return false;
        }
        else if (isComparison)
        {
            if (COMPARISON_OPERATORS.Contains(opA.Type) || LOGICAL_OPERATORS.Contains(opA.Type))
            {
                error = new TokenCheckError(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
                return false;
            }
			if (COMPARISON_OPERATORS.Contains(opB.Type) || LOGICAL_OPERATORS.Contains(opB.Type))
            {
                error = new TokenCheckError(operatorToken, MessageID.ERR_INVALID_OPERAND_TYPE);
                return false;
            }
            
            if (!GuessIsOperandNumeric(opA, out bool isOpANumeric, out error))
                return false;
            if (!GuessIsOperandNumeric(opB, out bool isOpBNumeric, out error))
                return false;
            if (isOpANumeric != isOpBNumeric)
            {
                error = new TokenCheckError(operatorToken, MessageID.ERR_COMPARE_OPERAND_TYPE_MISMATCH);
                return false;
            }
            if (!CheckExpressionOperand(opA, isOpANumeric, out error))
                return false;
            if (!CheckExpressionOperand(opB, isOpBNumeric, out error))
                return false;
        }
        else
        {
            error = new TokenCheckError(operatorToken, MessageID.ERR_UNKNOWN_CONDITION_OPERATOR_TYPE);
            return false;
        }
        error = null;
        return true;
    }

    private bool GuessIsOperandNumeric(Token operandToken, out bool isNumeric, out TokenCheckError? error)
    {
        if (operandToken.Type == TokenType.Identifier)
        {
            if (_scriptContext.NumaliasDefinitions.ContainsKey(operandToken.Content))
            {
                isNumeric = true;
                error = null;
                return true;
            }    
            if (_scriptContext.StraliasDefinitions.ContainsKey(operandToken.Content))
            {
                isNumeric = false;
                error = null;
                return true;
            }
            isNumeric = false;
            error = new TokenCheckError(operandToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, operandToken.Content);
            return false;
        }
        isNumeric = false;
        if (ARITHMETIC_OPERATORS.Contains(operandToken.Type))
		{
			if (operandToken.Children.Count == 0)
            {
                error = new TokenCheckError(operandToken, MessageID.ERR_MISSING_FIRST_OPERAND);
                return false;
            }
			if (operandToken.Children.Count == 1)
            {
                error = new TokenCheckError(operandToken, MessageID.ERR_MISSING_SECOND_OPERAND);
                return false;
            }   
			var opA = operandToken.Children[0];
			var opB = operandToken.Children[1];
            if (!GuessIsOperandNumeric(opA, out bool isOpANumeric, out error))
                return false;
            if (!GuessIsOperandNumeric(opB, out bool isOpBNumeric, out error))
                return false;
			if (isOpANumeric != isOpBNumeric)
            {
                error = new TokenCheckError(operandToken, MessageID.ERR_INVALID_OPERAND_TYPE);
                return false;
            }
            isNumeric = isOpANumeric;
            error = null;
            return true;
		}
        isNumeric = IsTokenNumeric(operandToken);
        error = null;
        return true;
    }



    private bool CheckTokenChildren(Token token, out TokenCheckError? error)
    {
        if (!_tokenChildrenHandlers.ContainsKey(token.Type))
        {
            error = new TokenCheckError(token, MessageID.ERR_U_STUPIT);
            return false;
        }
        return _tokenChildrenHandlers[token.Type](token, out error);
    }

    private bool CheckCanConvert(Token token, DataType targetType, out TokenCheckError? error)
    {
        if (!_canConvertHandlers.ContainsKey(targetType))
        {
            error = new TokenCheckError(token, MessageID.ERR_U_STUPIT);
            return false;
        }
        return _canConvertHandlers[targetType](token, out error);
    }

    private bool CheckNoChildren(Token parent, out TokenCheckError? error)
    {
        if (parent.Children.Count > 0)
        {
            error = new TokenCheckError(parent.Children[0], MessageID.ERR_TOO_MANY_TOKEN_CHILDREN);
            return false;
        }
        error = null;
        return true;
    }

    private bool CheckVariableOrArrayChildren(Token parent, out TokenCheckError? error)
    {
        if (parent.Type != TokenType.Variable)
            throw new ArgumentException("Variable or array parent expected.");
        if (parent.Content == "?")
            return CheckArrayChildren(parent, out error);
        else
            return CheckVariableChildren(parent, out error);
    }

    private bool CheckVariableChildren(Token parent, out TokenCheckError? error)
    {
        if (parent.Children.Count != 1)
        {
            error = new TokenCheckError(parent, MessageID.ERR_VAR_WITHOUT_NUM);
            return false;
        }

        var varNumberToken = parent.Children[0];
        if (!IsTokenNumeric(varNumberToken))
        {
            error = new TokenCheckError(varNumberToken, MessageID.ERR_NOT_A_NUMBER);
            return false;
        }
        if (varNumberToken.Type == TokenType.Identifier)
        {
            if (!_scriptContext.NumaliasDefinitions.ContainsKey(varNumberToken.Content))
            {
                error = new TokenCheckError(varNumberToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, varNumberToken.Content);
                return false;
            }
        }
        else if (varNumberToken.Type == TokenType.Variable)
        {
            if (varNumberToken.Content == "%")
                return CheckVariableChildren(varNumberToken, out error);
            else if (varNumberToken.Content == "?")
                return CheckArrayChildren(varNumberToken, out error);
        }
        error = null;
        return true;
    }

    private bool CheckArrayChildren(Token parent, out TokenCheckError? error)
    {
        if (parent.Children.Count < 2)
        {
            error = new TokenCheckError(parent, MessageID.ERR_ARRAY_WITHOUT_INDEX);
            return false;
        }
        var arrayNumberToken = parent.Children[0];
        if (!IsTokenNumeric(arrayNumberToken))
        {
            error = new TokenCheckError(arrayNumberToken, MessageID.ERR_NOT_A_NUMBER);
            return false;
        }
        if (arrayNumberToken.Type == TokenType.Identifier)
        {
            if (!_scriptContext.NumaliasDefinitions.ContainsKey(arrayNumberToken.Content.ToLower()))
            {
                error = new TokenCheckError(arrayNumberToken, MessageID.ERR_NUMALIAS_NOT_DEFINED, arrayNumberToken.Content);
                return false;
            }
        }
        else if (arrayNumberToken.Type == TokenType.Variable)
        {
            if (arrayNumberToken.Content == "%")
            {
                if (!CheckVariableChildren(arrayNumberToken, out error))
                    return false;
            }
            else if (arrayNumberToken.Content == "?")
            {
                if (!CheckArrayChildren(arrayNumberToken, out error))
                    return false;
            }
        }

        for (int i = 1; i < parent.Children.Count; i++)
        {
            var child = parent.Children[i];
            // Эти дети уже могут быть числовыми выражениями
            if (ARITHMETIC_OPERATORS.Contains(child.Type))
                return CheckArithmeticExpression(child, isNumeric: true, out error);
			if (COMPARISON_OPERATORS.Contains(child.Type) || LOGICAL_OPERATORS.Contains(child.Type))
            {
                error = new TokenCheckError(child, MessageID.ERR_CONDITIONS_NOT_ALLOWED);
                return false;
            }

            if (!CheckTokenChildren(child, out error))
                return false;
            if (!CheckCanConvert(child, DataType.Num, out error))
                return false;
        }
        error = null;
        return true;
    }
}
#nullable restore
