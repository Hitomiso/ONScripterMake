using ONSMake;
using ONSMake.Lexer;
using ONSMake.Parser;
using System;
using System.Collections.Generic;
using System.Text;

namespace ONSMake.Processing;

#nullable enable
public sealed partial class ScriptProcessor
{
    private readonly ProjectConfiguration _config;
	private delegate Result<string[], DirectiveError> _directiveHandler(ScriptUnit unit, List<Token> parameters);
	private Dictionary<string, _directiveHandler> _directiveHandlers = [];
	private readonly int _createdThreadId = Environment.CurrentManagedThreadId;
	
	private bool _dialogAutolabelActive = false;
    private string _dialogAutolabelPrefix = "";
	
    private List<string[]> _nvlLinesBetween = [];
    private List<string> _nvlLinesBetweenSection = [];
    private StringBuilder _nvlDialogBuilder = new();
    private bool _nvlIsBuildingDialogue = false;
    private bool _nvlIsWritingOutputDialogue = false;
	
	private List<MessageID> _currentDisabledErrors = [];
	
    public ScriptProcessor(ProjectConfiguration config)
    {
		_config = config;
		_directiveHandlers.Add("#dlg_autolabel", DlgAutolabelDirective);
        _directiveHandlers.Add("#incremental_label", IncrementalLabelDirective);
        _directiveHandlers.Add("#pragma", PragmaDirective);
	}
	
	public void ProcessScriptUnit(ScriptUnit unit)
	{
		if (Environment.CurrentManagedThreadId != _createdThreadId)
            throw new InvalidOperationException("Using ScriptProcessor from thread it was not created in is unsafe.");
		
	    for (int i = 0; i < unit.InputLines.Length; i++)
		{
			string line = unit.InputLines[i];

			// Заменяем строки, если есть шаблоны замены
			if (!_config.NoStringReplaces)
			{
				foreach (var pattern in _config.StringReplacements)
					line = pattern.Item1.Replace(line, pattern.Item2);
			}
			
			Result<List<ProcessedLine>, ProcessError> processResult = ProcessLine(unit, line, i);
			if (processResult.IsError)
			{
				if (!_currentDisabledErrors.Contains(processResult.Error.MessageID)) {
					Token token = processResult.Error.Token;
					TextPosition pos = new(i, token.StartColumn);
					var msg = new ScriptMessage(unit, pos, token.Content.Length, ScriptMessage.MessageType.Error, processResult.Error.MessageID, processResult.Error.Args);
					unit.ProcessingMessages.Add(msg);
				}
                unit.OutputLines.Add(new ProcessedLine(i, line, [], []));
            }
            else
            {
                unit.OutputLines.AddRange(processResult.Value);
            }
		}
	}
	
	private Result<List<ProcessedLine>, ProcessError> ProcessLine(ScriptUnit unit, string line, int inputLineIndex)
	{
		Result<List<ProcessedLine>, ProcessError> preprocessResult = PreprocessLine(unit, line, inputLineIndex);
		if (preprocessResult.IsError)
			return preprocessResult;
		
		foreach (ProcessedLine preprocessed in preprocessResult.Value)
		{
            foreach (Token token in preprocessed.Tokens)
			{
                Result<object, ProcessError> registerResult = RegisterDefinitions(unit, token, inputLineIndex);
				if (registerResult.IsError)
					return Result<List<ProcessedLine>, ProcessError>.Fail(registerResult.Error);
            }
        }
		
		return MakeOkResult(preprocessResult.Value);
	}
	
	private Result<List<ProcessedLine>, ProcessError> PreprocessLine(ScriptUnit unit, string line, int inputLineIndex)
	{
		List<ProcessedLine> processed = [];
		Result<List<Token>, LexerError> tokenizingResult = OnsLexer.TokenizeLine(line);
		if (tokenizingResult.IsError)
		{
			string invalidContent = line[tokenizingResult.Error.Column].ToString();
            Token invalidToken = new(TokenType.Invalid, invalidContent, tokenizingResult.Error.Column);
			ProcessError error = new(invalidToken, tokenizingResult.Error.MessageID, [invalidContent]);
            return Result<List<ProcessedLine>, ProcessError>.Fail(error);
        }
		
		// Активный препроцессинг
		if (tokenizingResult.Value.Count > 0 && tokenizingResult.Value[0].Type == TokenType.Directive)
		{
			if (_config.IgnoreDirectives)
				processed.Add(new ProcessedLine(inputLineIndex, line, tokenizingResult.Value, []));
			else
				PreprocessDirective(unit, line, tokenizingResult.Value, inputLineIndex);
		}
		else
		{
			Result<List<Token>, Token> parseResult = OnsParser.ParseTokens(tokenizingResult.Value);
			if (parseResult.IsError)
				return MakeErrorResult(parseResult.Error, MessageID.ERR_UNEXPECTED_TOKEN, [parseResult.Error.ToString()]);
			processed.Add(new ProcessedLine(inputLineIndex, line, tokenizingResult.Value, parseResult.Value));
		}
		
		// Пассивный препроцессинг
		List<ProcessedLine> finalOutputLines = [];
		foreach (ProcessedLine l in processed)
		{
			Result<ProcessedLine[], ProcessError> preprocessResult = PreprocessPassive(unit, l);
		    if (preprocessResult.IsError)
		        return Result<List<ProcessedLine>, ProcessError>.Fail(preprocessResult.Error);
            finalOutputLines.AddRange(preprocessResult.Value);
        }
		return Result<List<ProcessedLine>, ProcessError>.Ok(finalOutputLines);
	}
	
	private Result<ProcessedLine[], ProcessError> PreprocessPassive(ScriptUnit unit, ProcessedLine line)
	{
		Token? dialogToken = null;
		foreach (Token token in line.Tokens)
		{
			if (token.Type != TokenType.Identifier)
				continue;
			string name = token.Content.ToLower();
			if (name == "d" || name == "d2")
			{
				dialogToken = token;
				break;
			}
		}
		
		// Пассивный препроцессинг строк
		if (_nvlIsBuildingDialogue && !_nvlIsWritingOutputDialogue)
		{
			// Запоминаем строки между NVL-директивами
			if (dialogToken != null)
			    return Result<ProcessedLine[], ProcessError>.Fail(new ProcessError((Token)dialogToken, MessageID.ERR_DIALOG_BETWEEN_NVL_DIRECTIVES));
			_nvlLinesBetweenSection.Add(line.OutputLine);
			return Result<ProcessedLine[], ProcessError>.Ok([]);
		}
		else
		{
			if (!_dialogAutolabelActive || dialogToken == null)
                return Result<ProcessedLine[], ProcessError>.Ok([line]);
            // Выставляем автометки для диалогов
            unit.Autolabels.Add((_dialogAutolabelPrefix, unit.OutputLines.Count));
			ProcessedLine autolabelLine = new (line.InputLineIndex, string.Empty, [], []);
			return Result<ProcessedLine[], ProcessError>.Ok([autolabelLine, line]);
		}
	}
	
	private Result<List<ProcessedLine>, ProcessError> PreprocessDirective(ScriptUnit unit, string line, List<Token> tokens, int lineIndex)
	{
        if (tokens.Count == 0)
            throw new ArgumentException("ProcessDirective method should receive at least one token.");
        string name = tokens[0].Content.ToLower();

        var getDialogContent = () => line[(tokens[0].StartColumn + tokens[0].Content.Length)..].Trim();
		Result<string[], DirectiveError> result;
        switch (name)
	    {
			case "#n":
                result = NDirective(unit, getDialogContent());
                break;
            case "#e":
                result = EDirective(unit, getDialogContent());
                break;
            case "#end":
                result = EndDirective();
                break;
			default:
                if (!_directiveHandlers.TryGetValue(name, out _directiveHandler handler))
                    return Result<List<ProcessedLine>, ProcessError>.Fail(new ProcessError(tokens[0], MessageID.ERR_UNKNOWN_DIRECTIVE, name));
				result = handler(unit, tokens[1..]);
                break;
        }
        if (result.IsError)
            return Result<List<ProcessedLine>, ProcessError>.Fail(new ProcessError(tokens[0], result.Error.MessageID, result.Error.Args));

		// Обработка полученных с директивы строк
		List<ProcessedLine> processed = [];
        foreach (string output in result.Value)
        {
			Result<List<ProcessedLine>, ProcessError> processResult = ProcessLine(unit, output, lineIndex);
            if (processResult.IsError)
                return processResult;
            processed.AddRange(processResult.Value);
        }
        return Result<List<ProcessedLine>, ProcessError>.Ok(processed);
    }

    private static Result<object, ProcessError> RegisterDefinitions(ScriptUnit unit, Token token, int inputLineIndex)
	{
		if (token.Type == TokenType.Comment)
		    return RegisterComment(unit, token, inputLineIndex);
		if (token.Type == TokenType.ParsedLabel)
			return RegisterLabel(unit, token, inputLineIndex);
		if (token.Type != TokenType.Identifier)
            return MakeEmptyOkResult();
        
		TextPosition pos = new(inputLineIndex, token.StartColumn);
		switch (token.Content.ToLower())
		{
			case "numalias":
				return RegisterNumalias(unit, token, pos);
			case "stralias":
				return RegisterStralias(unit, token, pos);
			case "defsub":
				return RegisterSubroutine(unit, token, pos);
			default:
				return MakeEmptyOkResult();
		}
	}
	
	private static Result<string, ProcessError> CheckAliasName(Token commandToken)
	{
	    if (commandToken.Children.Count == 0 || commandToken.Children[0].Type != TokenType.Identifier)
			return Result<string, ProcessError>.Fail(new ProcessError(commandToken, MessageID.ERR_TOO_FEW_PARAMETERS));
		return Result<string, ProcessError>.Ok(commandToken.Children[0].Content.ToLower());
	}

    private static Result<object, ProcessError> RegisterComment(ScriptUnit unit, Token token, int lineIndex)
	{
	    int todoIndex = token.Content.IndexOf("TODO:", StringComparison.Ordinal);
		int fixmeIndex = token.Content.IndexOf("FIXME:", StringComparison.Ordinal);
		int noteStartIndex = todoIndex >= 0 ? todoIndex : fixmeIndex;
		if (noteStartIndex >= 0)
		{
			string messageText = token.Content[noteStartIndex..];
			TextPosition messagePos = new(lineIndex, token.StartColumn);
			unit.ProcessingMessages.Add(new ScriptMessage(unit, messagePos, token.Content.Length, ScriptMessage.MessageType.Info, MessageID.INFO_NOTE, [messageText]));
		}
		return MakeEmptyOkResult();
	}

    private static Result<object, ProcessError> RegisterLabel(ScriptUnit unit, Token token, int lineIndex)
	{
	    string labelName = token.Content.ToLower();
		if (unit.Labels.TryGetValue(labelName, out TextPosition defPos))
		{
            string firstDefinition = $"{unit.FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
	        return MakeEmptyErrorResult(token, MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS, labelName, firstDefinition);
		}
		unit.Labels.Add(labelName, new TextPosition(lineIndex, token.StartColumn));
		return MakeEmptyOkResult();
	}

    private static Result<object, ProcessError> RegisterNumalias(ScriptUnit unit, Token commandToken, TextPosition pos)
    {
        Result<string, ProcessError> nameCheckResult = CheckAliasName(commandToken);
		if (nameCheckResult.IsError)
			return Result<object, ProcessError>.Fail(nameCheckResult.Error);
		string name = nameCheckResult.Value;
		if (unit.Numaliases.TryGetValue(name, out TextPosition defPos))
		{
            string firstDefinition = $"{unit.FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
			return MakeEmptyErrorResult(commandToken, MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS, name, firstDefinition);
		}
		unit.Numaliases.Add(name, pos);
	    return MakeEmptyOkResult();
    }

    private static Result<object, ProcessError> RegisterStralias(ScriptUnit unit, Token commandToken, TextPosition pos)
    {
        Result<string, ProcessError> nameCheckResult = CheckAliasName(commandToken);
	    if (nameCheckResult.IsError)
			return Result<object, ProcessError>.Fail(nameCheckResult.Error);
		string name = nameCheckResult.Value;
		if (unit.Straliases.TryGetValue(name, out TextPosition defPos))
		{
            string firstDefinition = $"{unit.FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
			return MakeEmptyErrorResult(commandToken, MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS, name, firstDefinition);
		}
		unit.Straliases.Add(name, pos);
		return MakeEmptyOkResult();
    }

    private static Result<object, ProcessError> RegisterSubroutine(ScriptUnit unit, Token commandToken, TextPosition pos)
    {
        Result<string, ProcessError> nameCheckResult = CheckAliasName(commandToken);
		if (nameCheckResult.IsError)
			return Result<object, ProcessError>.Fail(nameCheckResult.Error);
		string name = nameCheckResult.Value;
		if (unit.Subroutines.TryGetValue(name, out TextPosition defPos))
		{
            string firstDefinition = $"{unit.FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
			return MakeEmptyErrorResult(commandToken, MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS, name, firstDefinition);
		}
		unit.Subroutines.Add(name, pos);
		return MakeEmptyOkResult();
    }
	
	
	private static Result<List<ProcessedLine>, ProcessError> MakeErrorResult(Token token, MessageID messageId, params string[] args) => 
	    Result<List<ProcessedLine>, ProcessError>.Fail(new ProcessError(token, messageId, args));
    private static Result<List<ProcessedLine>, ProcessError> MakeOkResult(List<ProcessedLine> lines) =>
		Result<List<ProcessedLine>, ProcessError>.Ok(lines);

    private static Result<object, ProcessError> MakeEmptyErrorResult(Token token, MessageID messageId, params string[] args) =>
	    Result<object, ProcessError>.Fail(new ProcessError(token, messageId, args));
	private static Result<object, ProcessError> MakeEmptyOkResult() =>
	    Result<object, ProcessError>.Ok(null);
}
#nullable restore
