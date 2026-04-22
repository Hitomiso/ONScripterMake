using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Hitomiso.ONScripterMake.Parsing;

namespace Hitomiso.ONScripterMake.Processing;

public partial class ScriptProcessor
{
    private ProjectConfiguration _config;

    private Dictionary<string, Func<Token, string[]>> _directiveHandlers = [];

    private bool _dialogAutolabelActive = false;
    private string _dialogAutolabelPrefix = "";
    private int _dialogAutolabelValue = 1;

    private List<string[]> _nvlLinesBetween = [];
    private List<string> _nvlLinesBetweenSection = [];
    private StringBuilder _nvlDialogBuilder = new();
    private bool _nvlIsBuildingDialogue = false;
    private bool _nvlIsWritingOutputDialogue = false;

    private bool _pragmaDisableAllErrors = false;
    private List<MessageID> _pragmaDisabledErrors = [];

    private Dictionary<string, Line> _straliases = [];
    private Dictionary<string, Line> _numaliases = [];
    private Dictionary<string, Line> _labels = [];
    private Dictionary<string, Line> _customCommands = [];

    private List<(Token commandToken, Line calledAt)> _commandCalls = [];

    public ScriptProcessor(ProjectConfiguration config)
    {
        _directiveHandlers.Add("dlg_autolabel", DlgAutolabelDirective);
        _directiveHandlers.Add("incremental_label", IncrementalLabelDirective);
        _directiveHandlers.Add("n", NDirective);
        _directiveHandlers.Add("e", EDirective);
        _directiveHandlers.Add("end", EndDirective);
        _directiveHandlers.Add("pragma", PragmaDirective);

        _config = config;
    }
	
    public string[] ProcessFile(string filename)
    {
        List<string> output = [];
        string[] lines = File.ReadAllLines(filename);
        string[] processOutput;
		Line line = null;
        for (int row = 0; row < lines.Count(); row++)
        {
			string lineContent = lines[row];
            try
            {
				// Заменяем строки, если есть шаблоны замены
				if (!_config.NoStringReplaces)
					foreach (var pattern in _config.StringReplacements)
						lineContent = pattern.Item1.Replace(lineContent, pattern.Item2);
				
				// Парсим и обрабатываем строки
				line = new(lineContent, filename, row);
				_nvlIsWritingOutputDialogue = false;
				string[] processedLines = ProcessLine(line);
				output.AddRange(processedLines);
				
				// Выводим подробную информацию, если есть
				if (_config.Verbose)
                {
                    Console.WriteLine("i< " + lineContent);
                    foreach (string s in processedLines)
                        Console.WriteLine("o> " + s);
                }
            }
			catch (DirectiveParameterException ex)
			{
				line.Column = ex.Token.StartColumn + ex.Token.Value.Length - 1;
				var ppEx = new PreprocessException(line, ex.Token.StartColumn, ex.Message);
				
				output.Add(lineContent);
                if (ex.MessageID == null)
                    OutputHandler.PrintPreprocessException(ppEx);
                else
                if (!_pragmaDisableAllErrors && !_pragmaDisabledErrors.Contains((MessageID)ex.MessageID))
                    OutputHandler.PrintPreprocessException(ppEx);
			}
            catch (PreprocessException ex)
            {
                output.Add(lineContent);
                if (ex.MessageID == null)
                    OutputHandler.PrintPreprocessException(ex);
                else
                if (!_pragmaDisableAllErrors && !_pragmaDisabledErrors.Contains((MessageID)ex.MessageID))
                    OutputHandler.PrintPreprocessException(ex);
            }
			catch (Exception ex)
			{
				OutputHandler.PrintError(ex.ToString(), lineContent);
			}
        }
        return output.ToArray();
    }
		
	private string[] ProcessLine(Line line)
	{
		var tokens = Parser.ParseLine(line);
		bool hasDialogCmd = false;
		
		foreach (var token in tokens)
		{
			if (token.Type == TokenType.Dialog)
				hasDialogCmd = true;
			if (token.Type == TokenType.Directive)
			{
				if (!_config.IgnoreDirectives)
				{
					// @TODO: Удалить, когда всё заработает
					// PrintTokens(token.Children);
					
					List<string> outputLines = [];
					if (_config.CommentDirectives)
						outputLines.Add(";" + line.Content);
					outputLines.AddRange(ProcessDirectiveToken(line, token));
					// Внутри ProcessDirective мы уже вызвали ProcessLine на каждую выходную строку директивы
					return outputLines.ToArray();
				}
			}
			else
				ProcessToken(line, token);
		}
		
		// Пассивный препроцессинг строк
		if (_nvlIsBuildingDialogue && !_nvlIsWritingOutputDialogue)
		{
			// Запоминаем строки между NVL-директивами
			if (hasDialogCmd)
				throw new PreprocessException(line, tokens[0].StartColumn, MessageID.ERR_DIALOG_BETWEEN_NVL_DIRECTIVES);
			_nvlLinesBetweenSection.Add(line.Content);
			return [];
		}
		else
		{
			if (!_dialogAutolabelActive || !hasDialogCmd)
				return [line.Content];
			// Выставляем автометки для диалогов
			while (_labels.ContainsKey(_dialogAutolabelPrefix + _dialogAutolabelValue.ToString()))
				_dialogAutolabelValue++;
			string labelName = _dialogAutolabelPrefix + _dialogAutolabelValue.ToString();
			_labels.Add(labelName, line);
			return [labelName, line.Content];			
		}
	}

    private void ProcessToken(Line line, Token token)
    {
		switch (token.Type)
		{
			case TokenType.Label:
				if (_labels.ContainsKey(token.Value))
					throw new PreprocessException(line, token.StartColumn, MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS, token.Value, _labels[token.Value].ToFileReference());
				_labels.Add(token.Value, line);
				break;
			case TokenType.JumpPoint:
				break;
			case TokenType.Comment:
				int todoIndex = token.Value.IndexOf("TODO:");
				int fixmeIndex = token.Value.IndexOf("FIXME:");
				if (todoIndex >= 0)
					OutputHandler.PrintInfo(token.Value[todoIndex..], line.ToFileReference());
				else if (fixmeIndex >= 0)
					OutputHandler.PrintInfo(token.Value[fixmeIndex..], line.ToFileReference());
				break;
			case TokenType.Name:
			case TokenType.Command:
			case TokenType.Dialog:
				ProcessCommand(token, line);
				_commandCalls.Add((token, line));
				break;
		}
    }

    private string[] ProcessDirectiveToken(Line line, Token token)
    {
        if (!_directiveHandlers.ContainsKey(token.Value))
            throw new PreprocessException(line, token.StartColumn, MessageID.ERR_UNKNOWN_DIRECTIVE, token.Value);

        List<string> processed = [];
        string[] handled = _directiveHandlers[token.Value](token);
        if (handled != null)
        {
            foreach (string handledLine in handled)
            {
                Line subline = new(line);
                subline.Content = handledLine;
				processed.AddRange(ProcessLine(subline));
            }
        }

        return processed.ToArray();
    }

	// Если переделать исключения в ParseException, то можно избавиться от Line в параметрах. Почти.
    private void ProcessCommand(Token token, Line line)
    {
		string param;
		switch (token.Value)
		{
			case "defsub":
				if (token.Children.Count == 0)
					throw new PreprocessException(line, token, MessageID.ERR_TOO_FEW_PARAMETERS);
				param = token.Children[0].Value;
				if (_customCommands.ContainsKey(param))
					throw new PreprocessException(line, token.Children[0], MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS, param, _customCommands[param].ToFileReference());
				_customCommands.Add(param, line);
				break;
			case "numalias":
				if (token.Children.Count == 0)
					throw new PreprocessException(line, token, MessageID.ERR_TOO_FEW_PARAMETERS);
				param = token.Children[0].Value;
				if (_numaliases.ContainsKey(param))
					throw new PreprocessException(line, token.Children[0], MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS, param, _numaliases[param].ToFileReference());
				// if (_straliases.ContainsKey(param))
					// throw new PreprocessException(line, token.Children[0], MessageID.ERR_STRALIAS_TO_NUMALIAS_REDEFINITION, param, _straliases[param].ToFileReference());
				_numaliases.Add(param, line);
				break;
			case "stralias":
				if (token.Children.Count == 0)
					throw new PreprocessException(line, token, MessageID.ERR_TOO_FEW_PARAMETERS);
				param = token.Children[0].Value;
				if (_straliases.ContainsKey(param))
					throw new PreprocessException(line, token.Children[0], MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS, param, _straliases[param].ToFileReference());
				// if (_numaliases.ContainsKey(param))
					// throw new PreprocessException(line, token.Children[0], MessageID.ERR_NUMALIAS_TO_STRALIAS_REDEFINITION, param, _numaliases[param].ToFileReference());
				_straliases.Add(param, line);
				break;
			case "d":
			case "d2":
				// @TODO: Проверить строку вывода на правильность
				break;
		}
    }
	
	
	
	private void PrintTokens(IEnumerable<Token> tokens, int depth = 0)
	{
		StringBuilder padding = new();
		padding.Append('~', depth);
		foreach (var token in tokens)
		{
			Console.Write(padding);
			Console.WriteLine(token.Value);
			PrintTokens(token.Children, depth + 1);
		}
	}
}
