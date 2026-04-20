using System;
using System.Collections.Generic;
using System.Linq;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake.Parsing;

public static partial class Parser
{
	private static readonly Dictionary<char, TokenType> SINGLE_CHAR_TOKENS = new ()
	{
		{'%', TokenType.NumVar},
		{'$', TokenType.StrVar},
		{'?', TokenType.Array},
		{':', TokenType.Colon},
		{',', TokenType.Comma},
		{'(', TokenType.OpenBracket},
		{')', TokenType.CloseBracket},
		{'[', TokenType.OpenSquareBracket},
		{']', TokenType.CloseSquareBracket},
		{'~', TokenType.JumpPoint},
		{'+', TokenType.Operator},
		{'-', TokenType.Operator},
		{'*', TokenType.Operator},
		{'/', TokenType.Operator},
		{'>', TokenType.ConditionOperator},
		{'<', TokenType.ConditionOperator},
		{'=', TokenType.ConditionOperator},
		{'&', TokenType.ConditionOperator},
		{'|', TokenType.ConditionOperator},
	};
	private static readonly Dictionary<string, TokenType> DOUBLE_CHAR_TOKENS = new ()
	{
		{"&&", TokenType.ConditionOperator},
		{"||", TokenType.ConditionOperator},
		{">=", TokenType.ConditionOperator},
		{"<=", TokenType.ConditionOperator},
		{"==", TokenType.ConditionOperator},
		{"!=", TokenType.ConditionOperator},
		{"<>", TokenType.ConditionOperator},
	};
	private static readonly Dictionary<string, int> OPERATORS_PRIORITY = new ()
	{
		{"|", 1},
		{"||", 1},
		{"&", 2},
		{"&&", 2},
		{">", 3},
		{"<", 3},
		{"=", 3},
		{">=", 3},
		{"<=", 3},
		{"==", 3},
		{"!=", 3},
		{"<>", 3},
		{"+", 4},
		{"-", 4},
		{"*", 5},
		{"/", 5},
		{"mod", 5},
	};
	private static readonly TokenType[] ALLOWED_AT_START_OF_LINE_TOKENS = 
	[
		TokenType.Label,
		TokenType.JumpPoint,
		TokenType.Comment,
		TokenType.Name,
		TokenType.Command,
		TokenType.Dialog,
		TokenType.Directive,
	];
	private static readonly TokenType[] COMMAND_TOKENS = 
	[
		TokenType.Name,
		TokenType.Command,
		TokenType.Dialog,
		TokenType.Directive,
	];
	private static readonly string[] NVL_DIRECTIVES = ["n", "e", "end"];
		
	private class ParseState
	{
		/*private readonly Dictionary<CommandType, Action<Token>> PARAMETER_PUSH_HANDLERS = new ()
		{
			{CommandType.Regular, PushRegularParameter},
			{CommandType.Branch, PushBranchParameter},
			{CommandType.ForLoop, PushForLoopParameter},
			{CommandType.Dialog, PushDialogParameter},
			{CommandType.Directive, PushDirectiveParameter}
		};*/
		/*private readonly Dictionary<CommandType, Action> PARAMETER_SAVE_HANDLERS = new ()
		{
			{CommandType.Regular, SaveRegularParameter},
			{CommandType.Branch, SaveBranchParameter},
			{CommandType.ForLoop, SaveRegularParameter},
		};*/

		private readonly TokenType[] TOKENS_NOT_BEFORE_COMMAND = 
		[
			TokenType.NumVar,
			TokenType.StrVar,
			TokenType.Array,
			TokenType.OpenBracket,
			TokenType.OpenSquareBracket,
			TokenType.Operator,
			TokenType.ConditionOperator
		];
		
		public bool IsCommandExpected { get; private set; } = true;
		public Token LastToken { get; private set; } = null;
		public Token CommandToken = null;
		public CommandType? CurrentCommandType { get; private set; }
		public List<Token> ParameterBuffer = [];
		
		private ForLoopParameter _currentLoopParameter;
		private List<Token> _distinctTokens = [];
		
		public enum CommandType
		{
			Regular,
			Branch,
			ForLoop,
			Dialog,
			Directive
		}
		
		private enum ForLoopParameter
		{
			Counter,
			FromValue,
			ToValue,
			StepValue
		}
		
		// @TODO: Отрефакторить этот ужас
		public void PushToken(Token token)
		{
			// Первые токены строки и токены-команды должны проверяться раздельно
			if (LastToken == null)
			{
				if (!Enumerable.Contains(ALLOWED_AT_START_OF_LINE_TOKENS, token.Type))
					throw new ParseException(token, MessageID.ERR_INVALID_START_OF_LINE);
				if (token.Type == TokenType.Label)
				{
					_distinctTokens.Add(token);
					LastToken = token;
					return;
				}
			}	
			
			if (CurrentCommandType != CommandType.Dialog)
			{
				switch (token.Type)
				{
					case TokenType.Comment:
						SaveParameter();
						_distinctTokens.Add(token);
						LastToken = token;
						return;
					case TokenType.Colon:
					case TokenType.JumpPoint:
						SaveParameter();
						IsCommandExpected = true;
						LastToken = token;
						return;
				}
			}

			if (IsCommandExpected)
			{
				if (token.Type != TokenType.Comment && !COMMAND_TOKENS.Contains(token.Type))
					throw new ParseException(token, MessageID.ERR_COMMAND_EXPECTED);
				UpdateCurrentCommandType(token);
				CommandToken = token;
				_distinctTokens.Add(token);
				IsCommandExpected = false;
				LastToken = token;
				return;
			}	

			switch (CurrentCommandType)
			{
				case CommandType.Regular:
					PushRegularParameter(token);
					break;
				case CommandType.Branch:
					PushBranchParameter(token);
					break;
				case CommandType.ForLoop:
					PushForLoopParameter(token);
					break;
				case CommandType.Dialog:
					ParameterBuffer.Add(token);
					break;
				case CommandType.Directive:
					CommandToken.Children.Add(token);
					break;
			}
			LastToken = token;
		}
		
		public List<Token> FinalizeDistinctTokens()
		{
			if (ParameterBuffer.Count > 0)
				SaveParameter();
			return _distinctTokens;
		}

		private void UpdateCurrentCommandType(Token token)
		{
			if (token.Type == TokenType.Directive)
			{
				CurrentCommandType = CommandType.Directive;
				return;
			}

			switch (token.Value)
			{
				case "d":
				case "d2":
					CurrentCommandType = CommandType.Dialog;
					break;
				case "if":
				case "notif":
					CurrentCommandType = CommandType.Branch;
					break;
				case "for":
					CurrentCommandType = CommandType.ForLoop;
					_currentLoopParameter = ForLoopParameter.Counter;
					break;
				default:
					CurrentCommandType = CommandType.Regular;
					break;
			}
		}
		
		private void SaveParameter()
		{
			if (ParameterBuffer.Count == 0)
				return;
			Token token;
			if (CurrentCommandType == CommandType.Branch)
				token = ParseLogicExpression(ref ParameterBuffer);
			else
				token = ParseArithmeticExpression(ref ParameterBuffer);
			if (ParameterBuffer.Count > 1)
				throw new ParseException(ParameterBuffer[1], MessageID.ERR_UNEXPECTED_TOKEN);
			CommandToken.Children.Add(token);
			ParameterBuffer.Clear();
		}
		
		private void PushRegularParameter(Token token)
		{
			if (token.Type == TokenType.Comma)
				SaveParameter();
			else if (!ConvertNameTokenIntoCommand(token))
				ParameterBuffer.Add(token);
		}
		
		private void PushBranchParameter(Token token)
		{
			// Переводим name токен в команду, когда он стоит после не-оператора
			if (!ConvertNameTokenIntoCommand(token))
				ParameterBuffer.Add(token);
		}
		
		private bool ConvertNameTokenIntoCommand(Token token)
		{
			if (ParameterBuffer.Count == 0 || token.Type != TokenType.Name)
				return false;
			if (TOKENS_NOT_BEFORE_COMMAND.Contains(ParameterBuffer[^1].Type))
				return false;
			
			// Превращаем в команду, заносим в _distinctTokens, сохраняем параметр условия
			SaveParameter();
			if (token.Value == "d" || token.Value == "d2")
				token.Type = TokenType.Dialog;
			else
				token.Type = TokenType.Command;
			UpdateCurrentCommandType(token);
			_distinctTokens.Add(token);
			CommandToken = token;
			IsCommandExpected = false;
			return true;
		}
		
		private void PushForLoopParameter(Token token)
		{
			if (CommandToken.Children.Count == 0 && ParameterBuffer.Count == 0 && token.Type != TokenType.NumVar && token.Type != TokenType.Array)
				throw new ParseException(token, MessageID.ERR_NUMVAR_EXPECTED);
			
			// for %VAR=NUM to NUM [step NUM]
			if (_currentLoopParameter == ForLoopParameter.Counter && token.Type == TokenType.ConditionOperator && token.Value == "=")
			{
				SaveParameter();
				if (CommandToken.Children[0].Type != TokenType.NumVar && CommandToken.Children[0].Type != TokenType.Array)
					throw new ParseException(CommandToken.Children[0], MessageID.ERR_NUMVAR_EXPECTED);
				_currentLoopParameter = ForLoopParameter.FromValue;
			}
			else if (_currentLoopParameter == ForLoopParameter.FromValue && token.Type == TokenType.Name && token.Value == "to")
			{
				SaveParameter();
				_currentLoopParameter = ForLoopParameter.ToValue;
			}
			else if (_currentLoopParameter == ForLoopParameter.ToValue && token.Type == TokenType.Name && token.Value == "step")
			{
				SaveParameter();
				_currentLoopParameter = ForLoopParameter.StepValue;
			}
			else
			{
				ParameterBuffer.Add(token);
			}
			
			// @TODO: Потом надо валидировать количество этих параметров
		}
	}
	
	public static Token[] ParseLine(Line line)
	{
		try
		{
			var state = new ParseState();
			while (!line.ReachedEnd)
			{
				Token token = null;
				if (state.CurrentCommandType != null && state.CurrentCommandType == ParseState.CommandType.Dialog)
					token = ReadNextDialogToken(line, state);
				else
					token = ReadNextToken(line, state);
				if (token == null)
					continue;
				if (token.Type == TokenType.Name)
				{
					if (state.IsCommandExpected)
						token.Type = TokenType.Command;
					else if (token.Value == "mod")
						token.Type = TokenType.Operator;
				}
				state.PushToken(token);
			}
		
			return state.FinalizeDistinctTokens().ToArray();
		}
		catch (ParseException ex)
		{
			line.Column = ex.Token.StartColumn + ex.Token.Value.Length - 1;
			// @TODO: Вставить обработку ParseException в FileProcessor
			throw new PreprocessException(line, ex.Token.StartColumn, ex.Message);
		}
	}
	
	private static Token ReadNextDialogToken(Line line, ParseState state)
	{
		// @TODO: Сделать получше
		int startCol = line.Column;
		return new Token(TokenType.Raw, line.ReadRest(), startCol);
	}
	
	private static Token? ReadNextToken(Line line, ParseState state)
	{
		line.SkipPadding();
		if (line.ReachedEnd)
			return null;
		int startColumn = line.Column;
		char c = line.PeekChar();
		
		if (char.IsAsciiLetter(c) || c == '_')
			return new Token(TokenType.Name, line.ReadName(), startColumn);
		if (char.IsAsciiDigit(c))
			return new Token(TokenType.NumConst, line.ReadNumber(), startColumn);
		
		switch (c)
		{
			case '"':
				return new Token(TokenType.StrConst, line.ReadString(), startColumn);
			case '*':
				if (state.LastToken == null || state.ParameterBuffer.Count == 0)
					return new Token(TokenType.Label, line.ReadLabel(), startColumn);
				else
					return new Token(TokenType.Operator, line.ReadChar().ToString(), startColumn);
			case '#':
				if (state.LastToken != null)
					return new Token(TokenType.Color, line.ReadColor(), startColumn);
				line.ReadChar();
				string directiveName;
				if (line.PeekChar() == '*')
					directiveName = "incremental_label";
				else
					directiveName = line.ReadName();
				Token directiveToken = new Token(TokenType.Directive, directiveName, startColumn);
				// Для NVl директив в качестве аргумента передаём всю строку до конца
				if (NVL_DIRECTIVES.Contains(directiveName))
				{
					startColumn = line.Column;
					Token dialog = new Token(TokenType.Raw, line.ReadRest(), startColumn);
					directiveToken.Children.Add(dialog);
				}
				return directiveToken;
			case ';':
				return new Token(TokenType.Comment, line.ReadRest(), startColumn);
			default:
				// Определяем операторы из 2-х символов
				c = (char)line.ReadChar();
				if (!line.ReachedEnd)
				{
					string dc = $"{c}{line.PeekChar()}";
					if (DOUBLE_CHAR_TOKENS.ContainsKey(dc))
						return new Token(DOUBLE_CHAR_TOKENS[dc], $"{c}{line.ReadChar()}", startColumn);
				}
				if (SINGLE_CHAR_TOKENS.ContainsKey(c))
					return new Token(SINGLE_CHAR_TOKENS[c], c.ToString(), startColumn);
				break;
		}
		throw new PreprocessException(line, startColumn, MessageID.ERR_UNKNOWN_TOKEN);
	}
}
