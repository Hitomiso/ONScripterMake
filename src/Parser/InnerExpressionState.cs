using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public class InnerExpressionState : ParserStateHandler
{
    private readonly TokenType[] CONDITION_OPERATORS =
    [
        TokenType.Less,
        TokenType.LessOrEqual,
        TokenType.Equal,
        TokenType.NotEqual,
        TokenType.GreaterOrEqual,
        TokenType.Greater,
        TokenType.Or,
        TokenType.And
    ];

    public enum BracketsType
    {
        None,
        Round,
        Square
    }
    private BracketsType _bracketsType;

    protected bool _isOperatorAwaited;
    protected bool _isLogical;
    protected List<Token> _parameterBuffer = [];

    public InnerExpressionState(OnsParser parser) : base(parser)
    {
        _tokenHandlers.Add(TokenType.Identifier, HandleOperand);
        _tokenHandlers.Add(TokenType.Number, HandleOperand);
        _tokenHandlers.Add(TokenType.String, HandleOperand);
        _tokenHandlers.Add(TokenType.Variable, HandleVariable);
        _tokenHandlers.Add(TokenType.Left, HandleOpeningBrackets);
        _tokenHandlers.Add(TokenType.Right, HandleClosingBrackets);
        _tokenHandlers.Add(TokenType.SRight, HandleClosingBrackets);

        _tokenHandlers.Add(TokenType.Add, HandleOperator);
        _tokenHandlers.Add(TokenType.Subtract, HandleOperator);
        _tokenHandlers.Add(TokenType.Multiply, HandleOperator);
        _tokenHandlers.Add(TokenType.Divide, HandleOperator);

        _tokenHandlers.Add(TokenType.Less, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.LessOrEqual, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.Equal, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.NotEqual, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.GreaterOrEqual, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.Greater, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.Or, HandleConditionOperator);
        _tokenHandlers.Add(TokenType.And, HandleConditionOperator);
    }

    public void Recycle(bool isLogical, BracketsType bracketsType = BracketsType.None)
    {
        _isLogical = isLogical;
        _bracketsType = bracketsType;
        _isOperatorAwaited = false;
        _parameterBuffer.Clear();
        IsReady = true;
    }

    public override void OnReturn(Token? returnValue)
    {
        if (returnValue != null)
        {
            _parameterBuffer.Add((Token)returnValue);
            _isOperatorAwaited = true;
        }
    }

    public override Token? OnReset(Token? returnValue)
    {
        OnReturn(returnValue);
        return _parser.CombineTokensIntoTree(_parameterBuffer);
    }

    protected void HandleOperand(Token token)
    {
        // Обрабатываем словесные операторы
        if (token.Type == TokenType.Identifier && token.Content == "mod")
        {
            Token moduloToken = new(TokenType.ParsedModulo, token.Content, token.StartColumn);
            HandleOperator(moduloToken);
            return;
        }

        // Обрабатываем отрицательное число как бинарную операцию, если можно
        if (_isOperatorAwaited && token.Type == TokenType.Number && token.Content[0] == '-')
        {
            var minusToken = new Token(TokenType.Subtract, "-", token.StartColumn);
            var numberToken = new Token(TokenType.Number, token.Content[1..], token.StartColumn + 1);
            _parameterBuffer.Add(minusToken);
            _parameterBuffer.Add(numberToken);
            return;
        }

        if (_isOperatorAwaited)
            throw new UnexpectedTokenException(token);
        _parameterBuffer.Add(token);
        _isOperatorAwaited = true;
    }

    protected void HandleVariable(Token token)
    {
        if (token.Content == "?")
        {
            var arrayHandler = (ArrayState)_parser.PushState(OnsParserStateType.Array);
            arrayHandler.Recycle(token);
        }
        else
        {
            var varHandler = (VariableState)_parser.PushState(OnsParserStateType.Variable);
            varHandler.Recycle(token);
        }
    }

    protected void HandleOperator(Token token)
    {
        // Обрабатываем унарный минус
        if (token.Type == TokenType.Subtract && (_parameterBuffer.Count == 0 || CONDITION_OPERATORS.Contains(_parameterBuffer[^1].Type)))
        {
            var zeroToken = new Token(TokenType.Number, "0", token.StartColumn);
            _parameterBuffer.Add(zeroToken);
            _parameterBuffer.Add(token);
            _isOperatorAwaited = false;
            return;
        }

        if (!_isOperatorAwaited)
        {
            // @FIXME: Скорее всего состояние переменной возвращает переменную, но в функции возврата флаг операнд/оператор не меняется
            // Или же что-то другое, но из-за состояния переменной
            throw new UnexpectedTokenException(token);
        }
        _parameterBuffer.Add(token);
        _isOperatorAwaited = false;
    }

    protected void HandleConditionOperator(Token token)
    {
        if (!_isLogical)
            throw new UnexpectedTokenException(token);
        HandleOperator(token);
    }

    protected void HandleOpeningBrackets(Token token)
    {
        if (token.Type != TokenType.Left)
            throw new UnexpectedTokenException(token);
        var innerExpressionHandler = (InnerExpressionState)_parser.PushState(OnsParserStateType.InnerExpression);
        innerExpressionHandler.Recycle(_isLogical, BracketsType.Round);
    }

    protected void HandleClosingBrackets(Token token)
    {
        if (token.Type == TokenType.Right && _bracketsType == BracketsType.Round)
            _parser.PopState(_parser.CombineTokensIntoTree(_parameterBuffer));
        else if (token.Type == TokenType.SRight && _bracketsType == BracketsType.Square)
            _parser.PopState(_parser.CombineTokensIntoTree(_parameterBuffer));
        else
            throw new UnexpectedTokenException(token);
    }
}
#nullable restore
