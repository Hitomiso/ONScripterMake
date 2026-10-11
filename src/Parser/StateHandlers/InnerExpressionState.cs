using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

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

    public InnerExpressionState(ParserContext context) : base(context)
    {
        _tokenHandlers.Add(TokenType.Identifier, TryHandleOperand);
        _tokenHandlers.Add(TokenType.Number, TryHandleOperand);
        _tokenHandlers.Add(TokenType.String, TryHandleOperand);
        _tokenHandlers.Add(TokenType.Variable, TryHandleVariable);
        _tokenHandlers.Add(TokenType.Left, TryHandleOpeningBrackets);
        _tokenHandlers.Add(TokenType.Right, TryHandleClosingBrackets);
        _tokenHandlers.Add(TokenType.SRight, TryHandleClosingBrackets);

        _tokenHandlers.Add(TokenType.Add, TryHandleOperator);
        _tokenHandlers.Add(TokenType.Subtract, TryHandleOperator);
        _tokenHandlers.Add(TokenType.Multiply, TryHandleOperator);
        _tokenHandlers.Add(TokenType.Divide, TryHandleOperator);

        _tokenHandlers.Add(TokenType.Less, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.LessOrEqual, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.Equal, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.NotEqual, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.GreaterOrEqual, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.Greater, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.Or, TryHandleConditionOperator);
        _tokenHandlers.Add(TokenType.And, TryHandleConditionOperator);
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
        if (returnValue == null)
            return;
        _parameterBuffer.Add((Token)returnValue);
        _isOperatorAwaited = true;
    }

    public override Token? OnReset(Token? returnValue)
    {
        OnReturn(returnValue);
        Result<Token?, Token> combineResult = _context.CombineTokensIntoTree(_parameterBuffer);
        if (combineResult.IsError)
            return null;
        else
            return combineResult.Value;
    }

    protected void TryHandleOperand(Token token, out bool success)
    {
        // Обрабатываем словесные операторы
        if (token.Type == TokenType.Identifier && token.Content.SequenceEqual("mod"))
        {
            Token moduloToken = new(TokenType.ParsedModulo, token.OriginalLine, token.StartColumn, token.Length);
            TryHandleOperator(moduloToken, out success);
            return;
        }

        // Обрабатываем отрицательное число как бинарную операцию, если можно
        if (_isOperatorAwaited && token.Type == TokenType.Number && token.Content[0] == '-')
        {
            var minusToken = new Token(TokenType.Subtract, token.OriginalLine, token.StartColumn, 1);
            var numberToken = new Token(TokenType.Number, token.OriginalLine, token.StartColumn + 1, token.Length - 1);
            _parameterBuffer.Add(minusToken);
            _parameterBuffer.Add(numberToken);
            success = true;
            return;
        }

        if (_isOperatorAwaited)
        {
            success = false;
            return;
        }
        _parameterBuffer.Add(token);
        _isOperatorAwaited = true;
        success = true;
    }

    protected void TryHandleVariable(Token token, out bool success)
    {
        if (token.Content.SequenceEqual("?"))
        {
            var arrayHandler = (ArrayState)_context.PushState(OnsParserStateType.Array);
            arrayHandler.TryRecycle(token, out success);
        }
        else
        {
            var varHandler = (VariableState)_context.PushState(OnsParserStateType.Variable);
            varHandler.TryRecycle(token, out success);
        }
    }

    protected void TryHandleOperator(Token token, out bool success)
    {
        // Обрабатываем унарный минус
        if (token.Type == TokenType.Subtract && (_parameterBuffer.Count == 0 || CONDITION_OPERATORS.Contains(_parameterBuffer[^1].Type)))
        {
            var zeroToken = new Token(TokenType.Number, "0", 0, 1);
            _parameterBuffer.Add(zeroToken);
            _parameterBuffer.Add(token);
            _isOperatorAwaited = false;
            success = true;
            return;
        }

        if (!_isOperatorAwaited)
        {
            success = false;
            return;
        }
        _parameterBuffer.Add(token);
        _isOperatorAwaited = false;
        success = true;
    }

    protected void TryHandleConditionOperator(Token token, out bool success)
    {
        if (_isLogical)
            TryHandleOperator(token, out success);
        else
            success = false;
    }

    protected void TryHandleOpeningBrackets(Token token, out bool success)
    {
        if (token.Type != TokenType.Left)
        {
            success = false;
            return;
        }
        var innerExpressionHandler = (InnerExpressionState)_context.PushState(OnsParserStateType.InnerExpression);
        innerExpressionHandler.Recycle(_isLogical, BracketsType.Round);
        success = true;
    }

    protected void TryHandleClosingBrackets(Token token, out bool success)
    {
        bool areBracketsValid = (token.Type == TokenType.Right && _bracketsType == BracketsType.Round) || 
            (token.Type == TokenType.SRight && _bracketsType == BracketsType.Square);
        success = false;
        if (!areBracketsValid)
            return;
        Result<Token?, Token> combineResult = _context.CombineTokensIntoTree(_parameterBuffer);
        if (combineResult.IsError)
            return;
        _context.PopState(combineResult.Value);
        success = true;
    }
}
#nullable restore
