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
	private readonly Dictionary<TokenType, Action> TOKEN_CHILDREN_HANDLERS = new()
	{
		{TokenType.Raw, CheckNoChildren},
		{TokenType.Name, CheckNoChildren},
		{TokenType.NumVar, CheckVariable},
		{TokenType.StrVar, CheckVariable},
		{TokenType.Array, CheckArray},
		{TokenType.NumConst, CheckNoChildren},
		{TokenType.StrConst, CheckNoChildren},
		{TokenType.Label, CheckNoChildren},
		{TokenType.Operator, CheckArithmeticOperator},
		{TokenType.ConditionOperator, CheckConditionOperator},
		{TokenType.Color, CheckNoChildren},
	};
	private readonly Dictionary<DataType, Action> CAN_CONVERT_HANDLERS = new ()
	{
		{DataType.Name, CheckCanConvertToName},
		{DataType.Label, CheckCanConvertToLabel},
		{DataType.Num, CheckCanConvertToNum},
		{DataType.Str, CheckCanConvertToStr},
		{DataType.NumVar, CheckCanConvertToNumVar},
		{DataType.StrVar, CheckCanConvertToStrVar},
		{DataType.Color, CheckCanConvertToColor},
		{DataType.Effect, CheckCanConvertToEffect},
		{DataType.Enum, CheckCanConvertToEnum},
		{DataType.Condition, CheckCanConvertToCondition},
	};
	
	private enum ExpressionType
	{
		Num,
		Str
	}
	
	public void CheckMemorizedCommandCalls()
    {
        if (_config.NoScriptCheck)
            return;

        CheckCustomCommandRoutines();
		
		if (_config.EngineCommands == null)
			return;
		
        foreach (var call in _commandCalls)
        {
			string cmdName = call.commandToken.Value;
			if (_customCommands.ContainsKey(cmdName))
				continue; // Кастомные команды пропускаем
			if (cmdName.StartsWith("_"))
				cmdName = cmdName[1..];
			
			try
			{
				var cmdDefinition = _config.EngineCommands.Find(el => el.Name == cmdName);
				if (cmdDefinition == null)
				{
					call.calledAt.Column = call.commandToken.StartColumn + call.commandToken.Value.Length - 1;
					throw new PreprocessException(call.calledAt, call.commandToken.StartColumn, MessageID.ERR_UNKNOWN_COMMAND, cmdName);
				}
				CheckCommandParameters(call.commandToken, cmdDefinition);
			}
			catch (PreprocessException ex)
			{
				OutputHandler.PrintPreprocessException(ex);
			}
        }
    }
	
	private void CheckCustomCommandRoutines()
	{
		foreach (var cmd in _customCommands)
        {
            try
            {
                string routineLabel = "*" + cmd.Key;
                if (!_labels.ContainsKey(routineLabel))
                    throw new PreprocessException(cmd.Value, 0, MessageID.ERR_LABEL_NOT_FOUND, routineLabel);
            }
            catch (PreprocessException ex)
            { 
				if (ex.MessageID == null)
                    OutputHandler.PrintPreprocessException(ex);
                else
                if (!_pragmaDisableAllErrors && !_pragmaDisabledErrors.Contains((MessageID)ex.MessageID))
                    OutputHandler.PrintPreprocessException(ex);
			}
            // @TODO: Надо прочитать getparams сразу после метки, но мне лень
        }
	}
	
	private void CheckCommandParameters(Token commandToken, Command definition)
	{
		// @TODO: Перегрузки стандартных команд
		for (int i = 0; i < commandToken.Children.Count; i++)
		{
			// Сравниваем имеющиеся параметры с ожидаемыми
			if (i >= definition.Parameters.Count)
				throw new PreprocessException(MessageID.ERR_TOO_MANY_PARAMETERS);
			Parameter param = definition.Parameters[i];
			
			try
			{
				IsParameterAcceptable(commandToken.Children[i], definition.Parameters[i]);
				if (!CheckDataType(commandToken.Children[i], param.DataType, out var errorMessageId))
				{
					throw new PreprocessException(MessageID.ERR_);
				}
			}
			catch (PreprocessException ex)
			{
				OutputHandler.PrintPreprocessException(ex);
			}
		}
	}
	
	private void CheckIsParameterAcceptable(Token token, Parameter definition)
	{
		if (token.Type == TokenType.Operator)
		{
			if (definition.Type != DataType.Num && definition.Type != DataType.Str)
				throw new PreprocessException(MessageID.ERR_EXPRESSIONS_NOT_ALLOWED);
			var expressionType = definition.Type == DataType.Num;
			CheckArithmeticExpressionType(token, expressionType);
			return;
		}
		if (token.Type == TokenType.ConditionOperator)
		{
			if (definition.Type != DataType.Condition)
				throw new PreprocessException(MessageID.ERR_CONDITIONS_NOT_ALLOWED);
			CheckLogicExpression(token);
			return;
		}
		CheckTokenChildren(token);
		return CheckCanConvert(token, definition.Type);
	}
	
	private void CheckArithmeticExpressionType(Token operatorToken, bool isNumeric)
	{
		if (operatorToken.Children.Count != 2)
			throw new PreprocessException(MessageID.ERR_INCORRECT_NUM_OF_OPERANDS);
		var opA = operatorToken.Children[0];
		var opB = operatorToken.Children[1];
		// Строковые операнды принимает только оператор +
		if (!isNumeric && operandToken.Value != "+")
			throw new PreprocessException(MessageID.ERR_);
		CheckExpressionOperand(opA, isNumeric);
		CheckExpressionOperand(opB, isNumeric);
	}
	
	private void CheckExpressionOperand(Token operandToken, bool isNumeric)
	{
		if (operandToken.Type == TokenType.Operator)
		{
			CheckArithmeticExpressionType(operandToken, isNumeric);
			return;
		}
		CheckTokenChildren(operandToken);
		if (operandToken.Type == TokenType.Name)
		{
			if (isNumeric)
			{
				if (!_numaliases.ContainsKey(operandToken.Value))
					throw new PreprocessException(MessageID.ERR_NUMALIAS_NOT_DEFINED);
			}
			else
			{
				if (!_straliases.ContainsKey(operandToken.Value))
					throw new PreprocessException(MessageID.ERR_STRALIAS_NOT_DEFINED);
			}
			return;
		}
		if (isNumeric)
		{
			if (!CheckIsNumeric(operandToken))
				throw new PreprocessException(MessageID.ERR_NOT_A_NUMBER);
		}
		else
		{
			if (!CheckIsString(operandToken))
				throw new PreprocessException(MessageID.ERR_NOT_A_STRING);
		}
	}
	
	private void CheckLogicExpression(Token operatorToken)
	{
		// @TODO: Разделать логические операторы и операторы сравнения
	}
	
	private void CheckTokenChildren(Token token)
	{
		if (!TOKEN_CHILDREN_HANDLERS.ContainsKey(token.Type))
			throw new PreprocessException(MessageID.ERR_);
		TOKEN_CHILDREN_HANDLERS[token.Type](token.Children);
	}
	
	private void CheckNoChildren(List<Token> children)
	{
		if (children.Count > 0)
			throw new PreprocessException();
	}
	
	private void CheckVariableChildren(List<Token> children)
	{
		if (children.Count != 1)
			throw new PreprocessException();
		if (!CheckIsNumeric(children[0]))
		{
			// @TODO: Проверить нумалиас
			if (children[0].Type == TokenType.Name)
			{
				if (!_numaliases.ContainsKey(children[0].Value))
					throw new PreprocessException(MessageID.ERR_NUMALIAS_NOT_DEFINED);
			}
			else
			{
				// @TODO: А если это выражение?
				if (!CheckIsNumeric(children[0]))
					// throw new PreprocessException(MessageID.ERR_);
			}
		}
	}
	
	private void CheckArrayChildren(List<Token> children)
	{
		// @TODO: Сделай!
	}
	
	
	
	private void CheckCanConvert(Token token, DataType targetType)
	{
		if (!CAN_CONVERT_HANDLERS.ContainsKey(targetType))
			throw new PreprocessException();
		CAN_CONVERT_HANDLERS[targetType](token);
	}
	
	private bool CheckTokenParameter(Token token, Parameter targetParameter, out MessageID? errorMessageId)
	{
		switch (targetParameter.Type)
		{
			case Name:
				return CheckNameTokenParameter(token, targetParameter, out errorMessageId);
			case Label:
				return CheckLabelTokenParameter(token, targetParameter, out errorMessageId);
			case Num: 
				return CheckNumParameter();
			Str, // var, const or stralias
			case NumVar:
				if (token.Type != TokenType.NumVar)
					throw new PreprocessException();
				break;
			case StrVar:
				if (token.Type != TokenType.StrVar)
					throw new PreprocessException();
				break;
			case Color: // STR const or NAME
			case Effect, // Тут сложно
			case Enum:
				if (token.Type != TokenType.Name)
					throw new PreprocessException();
				if (!targetParameter.EnumValues.Contains(token.Value))
					throw new PreprocessException();
				break;
			case Condition:
				if (token.Type != TokenType.ConditionOperator)
					throw new PreprocessException();
				break;
		}
		
		switch (token.Type)
		{
			case TokenType.Name:
				if (targetType.
				break;
			//case Token
			default:
				throw new NotImplementedException();
		}
	}
	
	private bool CheckNameTokenParameter(Token token, Parameter targetParameter, out MessageID? errorMessageId)
	{
		if (token.Type != TokenType.Name)
		{
			errorMessageId = MessageID.ERR_NOT_A_NAME;
			return false;
		}
		errorMessageId = null;
		return true;
	}
	
	private bool CheckLabelTokenParameter(Token token, Parameter targetParameter, out MessageID? errorMessageId)
	{
		// label, strvar, ?str const?
		switch (token.Type)
		{
			case TokenType.Label:
				return true;
			case TokenType.StrConst:
				// @TODO: Проверить на правильность метки
				throw new NotImplementedException();
				return true;
			case TokenType.StrVar:
				return true;
			default:
				errorMessageId = MessageID.ERR_
				return false;
		}
	}
	
	private bool CheckNumParameter(Token token, Parameter targetParameter, out MessageID? errorMessageId)
	{
		// var, const or numalias
		switch (token.Type)
		{
			case TokenType.NumVar:
			// 
		}
		if (token.Type == TokenType.NumConst)
		{
			// 
		}
		else if (token.Type == TokenType.NumVar)
		{
			// 
		}
		else if (token.Type == TokenType.Name)
		{
			if (!_numaliases.ContainsKey(token.Value))
			{
				errorMessageId = MessageID.ERR_
				return false;
			}
		}
		errorMessageId = MessageID.ERR_
		return false;
	}
}
