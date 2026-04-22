using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Hitomiso.ONScripterMake.Parsing;

namespace Hitomiso.ONScripterMake.Processing;

public class CommandCallChecker
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
	
	public void CheckMemorizedCommandCalls()
    {
        if (_config.NoScriptCheck)
            return;

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
            // Надо прочитать getparams сразу после метки, но мне лень
        }
		
		if (_config.EngineCommands == null)
			return;
		// HashSet<string> defaultCommandNames = [];
		// foreach (Command cmd in _config.EngineCommands)
			// defaultCommandNames.Add(cmd.Name);
        foreach (var call in _commandCalls)
        {
			string cmdName = call.commandToken.Value;
			if (_customCommands.ContainsKey(cmdName))
				continue; // Пропускаем пока кастомные команды
			if (cmdName.StartsWith("_"))
				cmdName = cmdName[1..];
			
			var cmdDefinition = _config.EngineCommands.Find(el => el.Name == cmdName);
			// Выкидываем ошибку несуществующей команды
			if (cmdDefinition == null)
			{
				call.calledAt.Column = call.commandToken.StartColumn + call.commandToken.Value.Length - 1;
				var ex = new PreprocessException(call.calledAt, call.commandToken.StartColumn, MessageID.ERR_UNKNOWN_COMMAND, cmdName);
				OutputHandler.PrintPreprocessException(ex);
				continue;
			}
			
			try
			{
				CheckCommandParameters(call.commandToken, cmdDefinition);
			}
			catch (PreprocessException ex)
			{
				OutputHandler.PrintPreprocessException(ex);
			}
        }
    }
	
	private void CheckCommandParameters(Token commandToken, Command definition)
	{
		// @TODO: Проверить параметры стандартной команды
		// @TODO: Перегрузки стандартных команд
		for (int i = 0; i < commandToken.Children.Count; i++)
		{
			if (i >= definition.Parameters.Count)
				throw new PreprocessException();
			CheckTokenChildren(commandToken.Children[i]);
			Parameter param = definition.Parameters[i];
			
			if (!CheckDataType(commandToken.Children[i], param.DataType, out var errorMessageId))
			{
				throw new PreprocessException();
			}
		}
	}
	
	private bool CheckTokenChildren(Token token, DataType expectedDataType)
	{
		if (!TOKEN_CHILDREN_HANDLERS.ContainsKey(token.Type))
			throw new PreprocessException();
		TOKEN_CHILDREN_HANDLERS[token.Type](token.Children);
	}
	
	private void CheckNoChildren(List<Token> children)
	{
		if (children.Count > 0)
			throw new PreprocessException();
	}
	
	private void CheckVariable(List<Token> children)
	{
		if (children.Count > 1)
			throw new PreprocessException();
		if (!CheckIsNumeric(children[0]))
		{
			// @TODO: Проверить нумалиас
			if (children[0].Type == TokenType.Name)
				
		}
	}
	
	private void CheckArray(List<Token> children)
	{
		// 
	}
	
	private void CheckArithmeticOperator(List<Token> children)
	{
		// @TODO: Может выдать разные типы
	}
	
	private void CheckConditionOperator(List<Token> children)
	{
		// @TODO: Разделять логические операторы и операторы сравнения
	}
	
	
	// private void Check
	
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
