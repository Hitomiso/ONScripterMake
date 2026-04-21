using System;
using System.IO;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Parser;

namespace Hitomiso.ONScripterMake.Processing;

// @TODO: Разделить обработчик и результат обработки
#nullable enable
public sealed partial class SourceFileProcessor
{
	public readonly string FileName;
    public List<(string prefix, int outputLineIndex)> Autolabels { get; private set; } = [];
    public Dictionary<string, TextPosition> DefinedLabels { get; private set; } = [];
	public Dictionary<string, TextPosition> DefinedNumaliases { get; private set; } = [];
	public Dictionary<string, TextPosition> DefinedStraliases { get; private set; } = [];
	public Dictionary<string, TextPosition> DefinedSubroutines { get; private set; } = [];
	public Stack<(int lineIndex, MessageID[] disabledErrors)> DisabledErrorsHistory { get; private set; } = [];
	public List<SourceCodeMessage> ProcessingMessages { get; private set; } = [];
	
	public readonly string[] InputLines;
	public List<ProcessedLine> OutputLines { get; private set; } = [];
	
	private readonly ProjectConfiguration _config;
	private readonly OnsParser _parser;
	private delegate string[] _directiveHandler(List<Token> parameters, out PreprocessDirectiveError? error);
	private Dictionary<string, _directiveHandler> _directiveHandlers = [];
	
	private bool _dialogAutolabelActive = false;
    private string _dialogAutolabelPrefix = "";
	
    private List<string[]> _nvlLinesBetween = [];
    private List<string> _nvlLinesBetweenSection = [];
    private StringBuilder _nvlDialogBuilder = new();
    private bool _nvlIsBuildingDialogue = false;
    private bool _nvlIsWritingOutputDialogue = false;
	
	private List<MessageID> _currentDisabledErrors = [];
	private int _currentLineIndex = 0;

    public SourceFileProcessor(string fileName, ProjectConfiguration config, OnsParser parser)
    {
		FileName = fileName;
		InputLines = File.ReadAllLines(fileName);
		_config = config;
		_parser = parser;
		
		_directiveHandlers.Add("#dlg_autolabel", DlgAutolabelDirective);
        _directiveHandlers.Add("#incremental_label", IncrementalLabelDirective);
        _directiveHandlers.Add("#pragma", PragmaDirective);
		
		for (int i = 0; i < InputLines.Length; i++)
		{
			_currentLineIndex = i;
			
			string line = InputLines[i];
			// Заменяем строки, если есть шаблоны замены
			if (!_config.NoStringReplaces)
			{
				foreach (var pattern in _config.StringReplacements)
					line = pattern.Item1.Replace(line, pattern.Item2);
			}
			
			try
			{
				var processOutput = ProcessLine(line, i, out ProcessError? error);
				if (error != null)
				{
                    TextPosition pos = new(i, error.Token.StartColumn);
                    var msg = new SourceCodeMessage(this, pos, error.Token.Content.Length, SourceCodeMessage.MessageType.Error, error.MessageID, error.Args);
                    ProcessingMessages.Add(msg);
                    OutputLines.Add(new ProcessedLine(i, line, [], []));
                }
                OutputLines.AddRange(processOutput);
			}
			catch (InvalidTokenException ex)
			{
				TextPosition pos = new(i, ex.Column);
				int length;
				if (line[ex.Column] == '`' || line[ex.Column] == '"')
				{
					length = line.Length - ex.Column;
				}
				else
				{
					int endIndex = line.IndexOf(' ', ex.Column);
					length = (endIndex >= 0 ? endIndex : line.Length) - ex.Column;
                }
				var msg = new SourceCodeMessage(this, pos, length, SourceCodeMessage.MessageType.Error, MessageID.ERR_UNKNOWN_TOKEN, [line.Substring(ex.Column, length)]);
				ProcessingMessages.Add(msg);
				OutputLines.Add(new ProcessedLine(i, line, [], [])); // Повторяющийся код!
			}
			catch (UnexpectedTokenException ex)
			{
				TextPosition pos = new(i, ex.Token.StartColumn);
				int length = ex.Token.Content.Length;
				var msg = new SourceCodeMessage(this, pos, length, SourceCodeMessage.MessageType.Error, MessageID.ERR_UNEXPECTED_TOKEN, [ex.Token.ToString()]);
				ProcessingMessages.Add(msg);
				OutputLines.Add(new ProcessedLine(i, line, [], [])); // Повторяющийся код!
			}
		}
	}
	
	private List<ProcessedLine> ProcessLine(string line, int inputLineIndex, out ProcessError? error)
	{
		List<ProcessedLine> processed = PreprocessLine(line, inputLineIndex, out error);
		if (error != null)
			return [];

        for (int i = 0; i < processed.Count; i++)
		{
            foreach (Token token in processed[i].Tokens)
			{
                HandleDefinitionTokens(token, inputLineIndex, out error);
				if (error != null)
					return [];
            }
        }
		
		return processed;
	}
	
	private List<ProcessedLine> PreprocessLine(string line, int inputLineIndex, out ProcessError? error)
	{
		List<ProcessedLine> processed = [];
		List<Token> lexemes = OnsLexer.TokenizeLine(line);
		
		// Активный препроцессинг
		if (lexemes.Count > 0 && lexemes[0].Type == TokenType.Directive)
		{
			if (_config.IgnoreDirectives)
			{
				processed.Add(new ProcessedLine(inputLineIndex, line, lexemes, []));
            }
			else
			{
				string[] directiveOutput;
				string directiveParam;
				PreprocessDirectiveError? directiveError = null;
				error = null;
                switch (lexemes[0].Content.ToLower())
				{
					case "#n":
                        directiveParam = line.Substring(lexemes[0].StartColumn + lexemes[0].Content.Length).Trim();
                        directiveOutput = NDirective(directiveParam, out directiveError);
                        break;
                    case "#e":
                        directiveParam = line.Substring(lexemes[0].StartColumn + lexemes[0].Content.Length).Trim();
                        directiveOutput = EDirective(directiveParam, out directiveError);
                        break;
                    case "#end":
                        directiveParam = line.Substring(lexemes[0].StartColumn + lexemes[0].Content.Length).Trim();
                        directiveOutput = EndDirective(out directiveError);
                        break;
					default:
                        directiveOutput = ProcessDirective(lexemes, out error);
                        break;
                }
				if (directiveError != null)
					error = new ProcessError(lexemes[0], directiveError.MessageID, directiveError.Args);
                if (error != null)
                    return [];
                foreach (string output in directiveOutput)
                {
                    var processOutput = ProcessLine(output, inputLineIndex, out error);
                    if (error != null)
                        return [];
                    processed.AddRange(processOutput);
                }
            }
		}
		else
		{
			_parser.ParseTokens(lexemes);
            List<Token> tokens = [.. _parser.RootTokens];
			processed.Add(new ProcessedLine(inputLineIndex, line, lexemes, tokens));
		}
		
		// Пассивный препроцессинг
		List<ProcessedLine> finalOutputLines = [];
		foreach (ProcessedLine l in processed)
		{
            finalOutputLines.AddRange(PreprocessPassive(l, out error));
			if (error != null)
				return [];
        }
		error = null;
		return finalOutputLines;
	}
	
	private ProcessedLine[] PreprocessPassive(ProcessedLine line, out ProcessError? error)
	{
		bool hasDialogCmd = false;
		Token? dialogToken = null;
		foreach (Token token in line.Tokens)
		{
			if (token.Type != TokenType.Identifier)
				continue;
			string name = token.Content.ToLower();
			if (name == "d" || name == "d2")
			{
				hasDialogCmd = true;
				dialogToken = token;
				break;
			}
		}
		
		// Пассивный препроцессинг строк
		if (_nvlIsBuildingDialogue && !_nvlIsWritingOutputDialogue)
		{
			// Запоминаем строки между NVL-директивами
			if (hasDialogCmd)
			{
				error = new ProcessError(dialogToken, MessageID.ERR_DIALOG_BETWEEN_NVL_DIRECTIVES);
				return [];
            }
			_nvlLinesBetweenSection.Add(line.OutputLine);
			error = null;
			return [];
		}
		else
		{
			if (!_dialogAutolabelActive || !hasDialogCmd)
			{
				error = null;
                return [line];
            }
            // Выставляем автометки для диалогов
            Autolabels.Add((_dialogAutolabelPrefix, OutputLines.Count));
			ProcessedLine autolabelLine = new (line.InputLineIndex, string.Empty, [], []);
			error = null;
			return [autolabelLine, line];
		}
	}
	
	private string[] ProcessDirective(List<Token> directiveTokens, out ProcessError? error)
	{
		if (directiveTokens.Count == 0)
			throw new ArgumentException("ProcessDirective method should receive at least one token.");
		string name = directiveTokens[0].Content;
		if (!_directiveHandlers.ContainsKey(name))
		{
			error = new ProcessError(directiveTokens[0], MessageID.ERR_UNKNOWN_DIRECTIVE, name);
			return [];
        }
        var directiveOutput = _directiveHandlers[name](directiveTokens[1..], out PreprocessDirectiveError? directiveError);
		if (directiveError != null)
		{
			error = new ProcessError(directiveTokens[0], directiveError.MessageID, directiveError.Args);
			return [];
		}
		error = null;
        return directiveOutput;
    }
	
	private void HandleDefinitionTokens(Token token, int inputLineIndex, out ProcessError? error)
	{
		if (token.Type == TokenType.Comment)
		{
			int todoIndex = token.Content.IndexOf("TODO:");
			int fixmeIndex = token.Content.IndexOf("FIXME:");
			int noteStartIndex = todoIndex >= 0 ? todoIndex : fixmeIndex;
			if (noteStartIndex >= 0)
			{
				string messageText = token.Content[noteStartIndex..];
				TextPosition messagePos = new(inputLineIndex, token.StartColumn);
				ProcessingMessages.Add(new SourceCodeMessage(this, messagePos, token.Content.Length, SourceCodeMessage.MessageType.Info, MessageID.INFO_NOTE, [messageText]));
			}
		}
		
		if (token.Type == TokenType.ParsedLabel)
		{
			string labelName = token.Content.ToLower();
			if (DefinedLabels.TryGetValue(labelName, out TextPosition defPos))
			{
                string firstDefinition = $"{FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
				error = new ProcessError(token, MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS, labelName, firstDefinition);
				return;
			}
			
			DefinedLabels.Add(labelName, new TextPosition(inputLineIndex, token.StartColumn));
			error = null;
			return;
		}
		
		if (token.Type != TokenType.Identifier)
		{
			error = null;
            return;
        }
		var getDefinedName = (Token commandToken, out ProcessError? error) =>
		{
			if (commandToken.Children.Count == 0 || commandToken.Children[0].Type != TokenType.Identifier)
			{
				error = new ProcessError(commandToken, MessageID.ERR_TOO_FEW_PARAMETERS);
				return "";
            }
			error = null;
			return commandToken.Children[0].Content.ToLower();
		};
		string name;
		TextPosition pos = new(inputLineIndex, token.StartColumn);
		switch (token.Content.ToLower())
		{
			case "numalias":
				name = getDefinedName(token, out error);
				if (error != null)
					return;
				if (DefinedNumaliases.ContainsKey(name))
				{
					TextPosition defPos = DefinedNumaliases[name];
					string firstDefinition = $"{FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
					error = new ProcessError(token, MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS, name, firstDefinition);
					return;
				}
				DefinedNumaliases.Add(name, pos);
				break;
			case "stralias":
				name = getDefinedName(token, out error);
				if (error != null)
					return;
				if (DefinedStraliases.ContainsKey(name))
				{
					TextPosition defPos = DefinedStraliases[name];
					string firstDefinition = $"{FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
					error = new ProcessError(token, MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS, name, firstDefinition);
					return;
				}
				DefinedStraliases.Add(name, pos);
				break;
			case "defsub":
				name = getDefinedName(token, out error);
				if (error != null)
					return;
				if (DefinedSubroutines.ContainsKey(name))
				{
					TextPosition defPos = DefinedSubroutines[name];
					string firstDefinition = $"{FileName}:{defPos.Line + 1}:{defPos.Column + 1}";
					error = new ProcessError(token, MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS, name, firstDefinition);
					return;
				}
				DefinedSubroutines.Add(name, pos);
				break;
		}
		error = null;
	}
}
#nullable restore
