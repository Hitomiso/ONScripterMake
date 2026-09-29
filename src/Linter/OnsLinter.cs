using System;
using System.Collections.Generic;
using ONSMake;
using ONSMake.Lexer;
using ONSMake.Parser;
using ONSMake.Processing;

namespace ONSMake.Linter;

public partial class OnsLinter
{
    private delegate Result<object, TokenCheckError> CheckerDelegate(Token token);

    private Dictionary<TokenType, CheckerDelegate> _tokenChildrenHandlers = [];
    private Dictionary<DataType, CheckerDelegate> _canConvertHandlers = [];
    private Dictionary<string, List<Command>> _defaultCommands = [];
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
    
    public OnsLinter(Dictionary<string, List<Command>> defaultCommands, FinalScriptContext scriptContext)
    {
        _defaultCommands = defaultCommands;
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

    public void LintUnit(ScriptUnit fileUnit)
    {
        foreach (ProcessedLine line in fileUnit.OutputLines)
        {
            try
            {
                LintLine(fileUnit, line);
            }
            catch (Exception ex)
            {
                OutputHandler.PrintLintingFailure(fileUnit, line, ex);
            }
        }
    }

    public void LintLine(ScriptUnit fileUnit, ProcessedLine line)
    {
        bool needReparse;
        List<Token> rootTokens = line.Tokens;
        do
        {
            needReparse = false;
            foreach (Token token in rootTokens)
            {
                if (token.Type != TokenType.Identifier)
                    continue;
                Result<object, TokenCheckError> checkResult = CheckCommandCallToken(token);
                if (!checkResult.IsError)
                    return;
                
                if (checkResult.Error.Token == null || checkResult.Error.MessageID == null)
                    throw new ApplicationException("Incorrect error returned.");
                if (checkResult.Error.MessageID != MessageID.POTENTIAL_COMMAND)
                {
                    AddErrorToFile(fileUnit, line.InputLineIndex, checkResult.Error.Token.Value, (MessageID)checkResult.Error.MessageID, checkResult.Error.Args);
                    return;
                }

                // Ищем проблемную лексему
                int commandTokenIndex = line.Lexemes.FindIndex(lexeme => lexeme.StartColumn == checkResult.Error.Token.Value.StartColumn);
                if (commandTokenIndex == -1)
                    throw new ArgumentException("Can not find potential command lexeme index.");

                // Репарсим строку
                List<Token> potentialCommandTokens = line.Lexemes[commandTokenIndex..];
                foreach (Token tokenToClear in potentialCommandTokens)
                    tokenToClear.Children.Clear();
                Result<List<Token>, Token> reparseResult = OnsParser.ParseTokens(potentialCommandTokens);
                if (reparseResult.IsError)
                {
                    AddErrorToFile(fileUnit, line.InputLineIndex, reparseResult.Error, MessageID.ERR_UNEXPECTED_TOKEN, [reparseResult.Error.ToString()]);
                    return;
                }
                rootTokens = reparseResult.Value;
                needReparse = true;
            }
        }
        while (needReparse);
    }

    private static void AddErrorToFile(ScriptUnit fileUnit, int line, Token errorToken, MessageID messageId, string[] args)
    {
        TextPosition pos = new(line, errorToken.StartColumn);
        ScriptMessage msg = new(fileUnit, pos, errorToken.Content.Length, ScriptMessage.MessageType.Error, messageId, args);
        fileUnit.ProcessingMessages.Add(msg);
    }

    private bool IsTokenNumeric(Token token)
    {
        switch (token.Type)
        {
            case TokenType.Identifier:
                return _scriptContext.Numaliases.ContainsKey(token.Content.ToLower());
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
                return _scriptContext.Straliases.ContainsKey(token.Content.ToLower());
            case TokenType.String:
                return true;
            case TokenType.Variable:
                return token.Content == "$";
            default:
                return false;
        }
    }
	
	private static Result<object, TokenCheckError> MakeCheckErrorResult(Token token, MessageID messageID, params string[] args) =>
		Result<object, TokenCheckError>.Fail(new TokenCheckError(token, messageID, args));
	private static Result<object, TokenCheckError> MakeCheckOkResult() =>
		Result<object, TokenCheckError>.Ok(null);
	
	private static Result<bool, TokenCheckError> MakeIsNumericErrorResult(Token token, MessageID messageID, params string[] args) =>
		Result<bool, TokenCheckError>.Fail(new TokenCheckError(token, messageID, args));
	private static Result<bool, TokenCheckError> MakeIsNumericOkResult(bool isNumeric) =>
		Result<bool, TokenCheckError>.Ok(isNumeric);
}
