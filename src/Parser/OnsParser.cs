using Hitomiso.ONScripterMake;
using Hitomiso.ONScripterMake.Lexer;
using System;
using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Text;
using System.Threading.Tasks;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public enum OnsParserStateType
{
	StartOfLine,
	Command,
	Variable,
	Array,
	Label,
	InnerExpression,
	NormalParameter,
	IfParameter,
	ForCounter,
	ForFrom,
	ForTo,
	ForStep,
	DialogParameter
}

public sealed class OnsParserStatePool
{
	private readonly Dictionary<OnsParserStateType, Type> STATE_TYPE_CLASSES = new()
	{
		{ OnsParserStateType.StartOfLine, typeof(StartOfLineState) },
        { OnsParserStateType.Command, typeof(CommandState) },
        { OnsParserStateType.Variable, typeof(VariableState) },
        { OnsParserStateType.Array, typeof(ArrayState) },
        { OnsParserStateType.Label, typeof(LabelState) },
        { OnsParserStateType.InnerExpression, typeof(InnerExpressionState) },
        { OnsParserStateType.NormalParameter, typeof(NormalParametersState) },
        { OnsParserStateType.IfParameter, typeof(IfParametersState) },
        { OnsParserStateType.ForCounter, typeof(ForCounterState) },
        { OnsParserStateType.ForFrom, typeof(ForFromState) },
        { OnsParserStateType.ForTo, typeof(ForToState) },
        { OnsParserStateType.ForStep, typeof(ForStepState) },
        { OnsParserStateType.DialogParameter, typeof(DialogParameterState) },
    };

    private Dictionary<OnsParserStateType, Stack<ParserStateHandler>> _parserStatesPool = [];
	private OnsParser _parser;

	public OnsParserStatePool(OnsParser parser)
	{
		_parser = parser;
	}

    public ParserStateHandler GetStateByType(OnsParserStateType stateType)
	{
        Stack<ParserStateHandler> statePool = GetPoolByType(stateType);

        ParserStateHandler stateHandler;
		if (statePool.Count == 0)
			stateHandler = CreateStateHandler(stateType);
		else
			stateHandler = statePool.Pop();
		return stateHandler;
    }

	public void StoreUsedState(ParserStateHandler stateHandler)
	{
		OnsParserStateType? stateType = null;
		foreach (var pair in STATE_TYPE_CLASSES)
		{
			if (pair.Value.FullName == stateHandler.GetType().FullName)
			{
				stateType = pair.Key;
				break;
			}
		}
		if (stateType == null)
			throw new ApplicationException($"Unregistered state type for handler '{stateHandler.GetType().FullName}'.");

        Stack<ParserStateHandler> statePool = GetPoolByType((OnsParserStateType)stateType);
		stateHandler.Invalidate();
		statePool.Push(stateHandler);
    }

	private Stack<ParserStateHandler> GetPoolByType(OnsParserStateType stateType)
	{
        Stack<ParserStateHandler> statePool;
        if (_parserStatesPool.ContainsKey(stateType))
        {
            return _parserStatesPool[stateType];
        }
        else
        {
            statePool = new Stack<ParserStateHandler>();
            _parserStatesPool.Add(stateType, statePool);
			return statePool;
        }
    }

	private ParserStateHandler CreateStateHandler(OnsParserStateType stateType)
	{
		if (!STATE_TYPE_CLASSES.ContainsKey(stateType))
            throw new ApplicationException($"Unregistered state handler class for type '{stateType}'.");
		ParserStateHandler? handler = (ParserStateHandler?)Activator.CreateInstance(STATE_TYPE_CLASSES[stateType], _parser);
		if (handler == null)
			throw new NullReferenceException();
        return handler;
    }
}

public sealed class OnsParser
{
	private readonly Dictionary<TokenType, int> OPERATORS_PRIORITY = new()
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
	
	public int StackCount { get => _stateStack.Count; }

    public List<Token> RootTokens = [];
    private Stack<ParserStateHandler> _stateStack = [];
	private OnsParserStatePool _statePool;

    public OnsParser()
    {
		_statePool = new OnsParserStatePool(this);
        PushState(OnsParserStateType.StartOfLine);
    }

	public ParserStateHandler PushState(OnsParserStateType type)
	{
		ParserStateHandler handler = _statePool.GetStateByType(type);
        _stateStack.Push(handler);
		return handler;
	}

    public void PopState(Token? returnValue)
    {
		ParserStateHandler oldState = _stateStack.Pop();
		_statePool.StoreUsedState(oldState);
        _stateStack.Peek().OnReturn(returnValue);
    }
	
	public int CountStack() => _stateStack.Count;

    public ParserStateHandler ResetToNewState(OnsParserStateType stateType)
    {
        Token? returnValue = null;
        while (_stateStack.Count > 0)
        {
			// FIXME: Почему-то вылазит ошибка о пустом стеке, когда он не пустой
            ParserStateHandler handler = _stateStack.Pop();
            returnValue = handler.OnReset(returnValue);
			_statePool.StoreUsedState(handler);
        }
		if (returnValue != null)
			RootTokens.Add(returnValue);

		ParserStateHandler newState = _statePool.GetStateByType(stateType);
		_stateStack.Push(newState);
		return newState;
    }

    public void ParseTokens(List<Token> tokens)
    {
		lock (_stateStack)
		{
            HardResetStateStack();
            RootTokens.Clear();
            try
            {
                foreach (var token in tokens)
                    PushToken(token);
            }
            catch (UnexpectedTokenException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Line tokens:");
                Console.ResetColor();
                foreach (var token in tokens)
                    Console.WriteLine(token);

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Parsed root tokens:");
                Console.ResetColor();
                foreach (Token token in RootTokens)
                    Console.WriteLine(token.TreeToString());

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("State stack:");
                Console.ResetColor();
                foreach (ParserStateHandler state in _stateStack)
                    Console.WriteLine(state.GetType().Name);

                Console.ForegroundColor = ConsoleColor.Blue;
                Console.WriteLine("Exception:");
                Console.ResetColor();
                Console.WriteLine(ex);
            }
			// Здесь обязаны, в отличие от начала метода, очищат стек правильно
			((StartOfLineState)ResetToNewState(OnsParserStateType.StartOfLine)).Recycle();
        }
    }

	/// <summary>
	/// Вызывать только из классов состояний. Обрабатывает переданный токен.
	/// </summary>
	/// <param name="token">Токен для обработки и добавления в дерево</param>
    public void PushToken(Token token)
    {
        _stateStack.Peek().HandleToken(token);
    }

    public Token? CombineTokensIntoTree(List<Token> tokensInParameter)
    {
		if (tokensInParameter.Count == 0)
			return null;
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
		{
			throw new UnexpectedTokenException(tokens[1]); // Выражение было сложено неправильно
		}
		return tokens[0];
    }
	
	private void HardResetStateStack()
	{
		while (_stateStack.Count > 0)
		{
			ParserStateHandler handler = _stateStack.Pop();
            handler.Invalidate();
            _statePool.StoreUsedState(handler);
        }
        StartOfLineState startState = (StartOfLineState)_statePool.GetStateByType(OnsParserStateType.StartOfLine);
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