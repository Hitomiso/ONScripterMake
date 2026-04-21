using System;
using System.Collections.Generic;
using Hitomiso.ONScripterMake;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Parser;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake.Linter;

#nullable enable
public partial class OnsLinter
{
    private delegate bool CheckerDelegate(Token token, out TokenCheckError? error);

    private Dictionary<TokenType, CheckerDelegate> _tokenChildrenHandlers = [];
    private Dictionary<DataType, CheckerDelegate> _canConvertHandlers = [];
    private Dictionary<string, List<Command>> _defaultCommands = [];
    private readonly OnsParser _parser = new();
    private readonly FinalScriptContext _scriptContext;

    private readonly TokenType[] ARITHMETIC_OPERATORS =
    [
        TokenType.Add,
        TokenType.Subtract,
        TokenType.Multiply,
        TokenType.Divide,
        TokenType.ParsedModulo,
    ];
    private readonly TokenType[] LOGICAL_OPERATORS =
    [
        TokenType.Or,
        TokenType.And,
    ];
    private readonly TokenType[] COMPARISON_OPERATORS =
    [
        TokenType.Less,
        TokenType.LessOrEqual,
        TokenType.Equal,
        TokenType.NotEqual,
        TokenType.GreaterOrEqual,
        TokenType.Greater,
    ];

    private enum ExpressionType
    {
        Num,
        Str
    }
     
    private class PotentialCommandInvocationException : Exception
    {
        public readonly Token PotentialCommandToken;

        public PotentialCommandInvocationException(Token potentialCommandToken)
        {
            PotentialCommandToken = potentialCommandToken;
        }
    }

    public OnsLinter(Dictionary<string, List<Command>> defaultCommands, FinalScriptContext scriptContext)
    {
        _defaultCommands = new Dictionary<string, List<Command>>(defaultCommands);
        _scriptContext = scriptContext;

        _tokenChildrenHandlers.Add(TokenType.Identifier, CheckNoChildren);
        _tokenChildrenHandlers.Add(TokenType.Variable, CheckVariableOrArrayChildren);
        _tokenChildrenHandlers.Add(TokenType.IndexVariable, CheckVariableChildren);
        _tokenChildrenHandlers.Add(TokenType.Number, CheckNoChildren);
        _tokenChildrenHandlers.Add(TokenType.String, CheckNoChildren);
        _tokenChildrenHandlers.Add(TokenType.ParsedLabel, CheckNoChildren);
        _tokenChildrenHandlers.Add(TokenType.Color, CheckNoChildren);

        _canConvertHandlers.Add(DataType.Name, CheckCanConvertToName);
        _canConvertHandlers.Add(DataType.Label, CheckCanConvertToLabel);
        _canConvertHandlers.Add(DataType.Num, CheckCanConvertToNum);
        _canConvertHandlers.Add(DataType.Str, CheckCanConvertToStr);
        _canConvertHandlers.Add(DataType.NumVar, CheckCanConvertToNumVar);
        _canConvertHandlers.Add(DataType.StrVar, CheckCanConvertToStrVar);
        _canConvertHandlers.Add(DataType.Color, CheckCanConvertToColor);
        _canConvertHandlers.Add(DataType.Dialog, CheckCanConvertToDialog);
    }

    public SourceCodeMessage[] LintFile(SourceFileProcessor file) // Должен быть потокобезопасным
    {
        List<SourceCodeMessage> messages = [];

        // Выполняем для каждой команды в каждой строке
        foreach (ProcessedLine line in file.OutputLines)
        {
            List<Token> rootTokens = line.Tokens;
        lintingRootTokens:
            try
            {
                foreach (Token token in rootTokens)
                {
                    if (token.Type != TokenType.Identifier)
                        continue;
                    if (!CheckCommandCallToken(token, out TokenCheckError? error))
                    {
                        if (error == null || error.Token == null || error.MessageID == null)
                            throw new ApplicationException("Incorrect error returned.");
                        TextPosition pos = new(line.InputLineIndex, error.Token.StartColumn);
                        messages.Add(new SourceCodeMessage(file, pos, error.Token.Content.Length, SourceCodeMessage.MessageType.Error, error.MessageID.Value, error.Args));
                    }
                }
            }
            catch (PotentialCommandInvocationException ex)
            {
                // Ищем проблемную лексему
                int commandTokenIndex = -1;
                for (int i = 0; i < line.Lexemes.Count; i++) // @FIXME: Кто-то крадёт токены из Lexemes. Их меньше, чем должно быть
                {
                    if (line.Lexemes[i].StartColumn == ex.PotentialCommandToken.StartColumn)
                    {
                        commandTokenIndex = i;
                        break;
                    }
                }
                if (commandTokenIndex == -1)
                    throw new ArgumentException(); // Не получилось найти проблемную лексему в строке

                // Репарсим строку
                try
                {
                    List<Token> tokens = line.Lexemes[commandTokenIndex..];
                    foreach (Token token in tokens)
                        token.Children.Clear();
                    _parser.ParseTokens(tokens);
                    rootTokens = [.. _parser.RootTokens];
                    goto lintingRootTokens;
                }
                catch (UnexpectedTokenException unexpectedTokenEx)
                {
                    TextPosition pos = new(line.InputLineIndex, unexpectedTokenEx.Token.StartColumn);
                    int length = unexpectedTokenEx.Token.Content.Length;
                    var msg = new SourceCodeMessage(file, pos, length, SourceCodeMessage.MessageType.Error, MessageID.ERR_UNEXPECTED_TOKEN, [unexpectedTokenEx.Token.ToString()]);
                    messages.Add(msg);
                }
            }
            catch (Exception ex)
            {
                OutputHandler.PrintError(ex.Message);
            }
        }

        return [.. messages];
    }

    private bool IsTokenNumeric(Token token)
    {
        switch (token.Type)
        {
            case TokenType.Identifier:
                return _scriptContext.NumaliasDefinitions.ContainsKey(token.Content.ToLower());
            case TokenType.Number:
                return true;
            case TokenType.Variable:
                return token.Content != "$";
            default:
                return false;
        }
    }

    private bool IsTokenString(Token token)
    {
        switch (token.Type)
        {
            case TokenType.Identifier:
                return _scriptContext.StraliasDefinitions.ContainsKey(token.Content);
            case TokenType.String:
                return true;
            case TokenType.Variable:
                return token.Content == "$";
            default:
                return false;
        }
    }
}
#nullable restore