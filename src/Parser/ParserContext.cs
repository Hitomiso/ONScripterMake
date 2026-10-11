using ONSMake;
using ONSMake.Lexer;
using System;
using System.Collections.Generic;

namespace ONSMake.Parser;

#nullable enable
public class ParserContext
{
	private static readonly Dictionary<TokenType, int> OPERATORS_PRIORITY = new()
	{
		{TokenType.Or, 1},
		{TokenType.And, 2},
		{TokenType.Less, 3},
		{TokenType.LessOrEqual, 3},
		{TokenType.Equal, 3},
		{TokenType.NotEqual, 3},
		{TokenType.GreaterOrEqual, 3},
		{TokenType.Greater, 3},
		{TokenType.Add, 4},
		{TokenType.Subtract, 4},
		{TokenType.Multiply, 5},
		{TokenType.Divide, 5},
		{TokenType.ParsedModulo, 5},
	};

    public readonly List<Token> RootTokens = [];
    private readonly Stack<ParserStateHandler> _stateStack = [];
    private readonly OnsParserStatePool _statePool = new();

    public void ResetContext()
	{
        RootTokens.Clear();
		HardResetStateStack();
        ((StartOfLineState)PushState(OnsParserStateType.StartOfLine)).Recycle();
    }
    
    public ParserStateHandler PushState(OnsParserStateType type)
	{
		ParserStateHandler handler = _statePool.GetStateByType(this, type);
        _stateStack.Push(handler);
		return handler;
	}

    public void PopState(Token? returnValue)
    {
		ParserStateHandler oldState = _stateStack.Pop();
		_statePool.StoreUsedState(oldState);
		if (_stateStack.Count == 0)
		{
			if (returnValue != null)
				RootTokens.Add((Token)returnValue);
			((CommandState)PushState(OnsParserStateType.Command)).Recycle();
		}
		else
		{
			_stateStack.Peek().OnReturn(returnValue);
		}
    }
	
	public int CountStack() => _stateStack.Count;

    public ParserStateHandler ResetToNewState(OnsParserStateType stateType)
    {
        Token? returnValue = null;
        while (_stateStack.Count > 0)
        {
            ParserStateHandler handler = _stateStack.Pop();
            returnValue = handler.OnReset(returnValue);
			_statePool.StoreUsedState(handler);
        }
		if (returnValue != null)
			RootTokens.Add((Token)returnValue);

		ParserStateHandler newState = _statePool.GetStateByType(this, stateType);
		_stateStack.Push(newState);
		return newState;
    }

	/// <summary>
	/// Вызывать только из классов состояний. Обрабатывает переданный токен.
	/// </summary>
	/// <param name="token">Токен для обработки и добавления в дерево</param>
	/// <param name="success">Если равен false, то переданный токен не ожидался, парсинг привёл к ошибке.</param>
    public void PushToken(Token token, out bool success)
    {
        _stateStack.Peek().TryHandleToken(token, out success);
    }

    public Result<Token?, Token> CombineTokensIntoTree(List<Token> tokensInParameter)
    {
		if (tokensInParameter.Count == 0)
			return Result<Token?, Token>.Ok(null);
        List<Token> tokens = [.. tokensInParameter];
		
		// Не учитывает унарные операторы, которые пока не поддерживаются :(
		int opIdx = -1;
		do
		{
			opIdx = GetMaxPriorityOperatorIndex(tokens);
			if (opIdx == -1)
				break;
			if (opIdx == 0 || opIdx >= tokens.Count - 1)
				throw new NotImplementedException("Unary operators are not supported.");
			tokens[opIdx].Children.Add(tokens[opIdx - 1]);
			tokens[opIdx].Children.Add(tokens[opIdx + 1]);
			tokens.RemoveAt(opIdx - 1);
			tokens.RemoveAt(opIdx);
		}
		while (opIdx >= 0);
		
		if (tokens.Count == 0 || tokens.Count > 1)
			return Result<Token?, Token>.Fail(tokens[1]); // Выражение было сложено неправильно
		return Result<Token?, Token>.Ok(tokens[0]);
    }
	
	private void HardResetStateStack()
	{
		while (_stateStack.Count > 0)
		{
			ParserStateHandler handler = _stateStack.Pop();
            handler.Invalidate();
            _statePool.StoreUsedState(handler);
        }
        StartOfLineState startState = (StartOfLineState)_statePool.GetStateByType(this, OnsParserStateType.StartOfLine);
        startState.Recycle();
        _stateStack.Push(startState);
    }

	private int GetMaxPriorityOperatorIndex(List<Token> tokens)
	{
		int maxPriorityOperatorIndex = -1;
		int maxPriority = -1;
		for (int i = 0; i < tokens.Count; i++)
		{
			Token token = tokens[i];
			if (token.Children.Count > 0)
				continue;
			if (!OPERATORS_PRIORITY.ContainsKey(token.Type))
				continue;
			int priority = OPERATORS_PRIORITY[token.Type];
			if (priority > maxPriority)
			{
				maxPriorityOperatorIndex = i;
				maxPriority = priority;
			}
		}
		return maxPriorityOperatorIndex;
	}
}
#nullable restore
