using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Processing;

#nullable enable
public partial class SourceFileProcessor
{
    private string[] NDirective(string dialog, out PreprocessDirectiveError? error)
    {
		if (string.IsNullOrWhiteSpace(dialog))
        {
            error = new PreprocessDirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS);
            return [];
        }
		
        string[] outputLines = null;
        if (_nvlIsBuildingDialogue)
        {
            outputLines = EndDirective(out error);
            if (error != null)
                return [];
        }
		
        _nvlIsBuildingDialogue = true;
        _nvlDialogBuilder.Clear();
        _nvlLinesBetweenSection.Clear();
        _nvlLinesBetween.Clear();
		
        _nvlDialogBuilder.Append(dialog);
        error = null;
        return outputLines ?? [];
    }

    private string[] EDirective(string dialog, out PreprocessDirectiveError? error)
    {
        if (!_nvlIsBuildingDialogue)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_USE_N_DIRECTIVE_FIRST);
            return [];
        }
		if (string.IsNullOrWhiteSpace(dialog))
        {
            error = new PreprocessDirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS);
            return [];
        }

        _nvlDialogBuilder.Append("[#][*]");
        _nvlDialogBuilder.Append(dialog);

        _nvlLinesBetween.Add([.. _nvlLinesBetweenSection]);
        _nvlLinesBetweenSection.Clear();
        error = null;
        return [];
    }

    private string[] EndDirective(out PreprocessDirectiveError? error)
    {
        if (!_nvlIsBuildingDialogue)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_USE_N_DIRECTIVE_FIRST);
            return [];
        }

        List<string> outputLines = [];
        bool isD2 = _nvlLinesBetween.Count > 0;
        if (isD2)
        {
            outputLines.Add("d2 " + _nvlDialogBuilder.ToString());
            for (int i = 0; i < _nvlLinesBetween.Count; i++)
            {
                outputLines.Add("wait_on_d " + i.ToString());
                outputLines.AddRange(_nvlLinesBetween[i]);
                outputLines.Add("d_continue");
            }
            outputLines.Add("wait_on_d -1");
        }
        else
            outputLines.Add("d " + _nvlDialogBuilder.ToString());
        outputLines.AddRange(_nvlLinesBetweenSection);

        _nvlIsBuildingDialogue = false;
        _nvlIsWritingOutputDialogue = true;
        error = null;
        return [.. outputLines];
    }

    private string[] DlgAutolabelDirective(List<Token> parameters, out PreprocessDirectiveError? error)
    {
		if (parameters.Count == 0)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_MISSING_DLG_AUTOLABEL_ARGUMENT);
            return [];
        }
		
		Token param = parameters[0];
        if (param.Type == TokenType.ParsedLabel)
        {
            _dialogAutolabelActive = true;
            _dialogAutolabelPrefix = param.Content;
        }
        else if (param.Type == TokenType.Multiply && parameters.Count > 1)
        {
            Token labelId = parameters[1];
            if (labelId.Type == TokenType.Identifier && labelId.StartColumn == param.StartColumn + 1)
            {
                _dialogAutolabelActive = true;
                _dialogAutolabelPrefix = "*" + labelId.Content;
            }
            else
            {
                error = new PreprocessDirectiveError(MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT);
                return [];
            }
        }
        else if (param.Type == TokenType.Identifier && param.Content == "off")
        {
            _dialogAutolabelActive = false;
        }
        else
        {
            error = new PreprocessDirectiveError(MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT);
            return [];
        }
		
        error = null;
        return [];
    }

    private string[] IncrementalLabelDirective(List<Token> parameters, out PreprocessDirectiveError? error)
    {
		if (parameters.Count == 0)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS);
            return [];
        }
        string labelPrefix;
		Token param = parameters[0];
        if (param.Type == TokenType.Multiply && parameters.Count > 1)
        {
            Token labelId = parameters[1];
            if (labelId.Type == TokenType.Identifier && labelId.StartColumn == param.StartColumn + 1)
            {
                labelPrefix = "*" + labelId.Content;
            }
            else
            {
                error = new PreprocessDirectiveError(MessageID.ERR_NOT_A_LABEL);
                return [];
            }
        }
        else if (param.Type == TokenType.ParsedLabel)
        {
            labelPrefix = param.Content;
        }
        else
        {
            error = new PreprocessDirectiveError(MessageID.ERR_NOT_A_LABEL);
            return [];
        }

        Autolabels.Add((labelPrefix, OutputLines.Count));
        error = null;
        return [string.Empty];
    }

    private string[] PragmaDirective(List<Token> parameters, out PreprocessDirectiveError? error)
    {
		if (parameters.Count == 0)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS);
            return [];
        }
		Token param = parameters[0];
		if (param.Type != TokenType.Identifier)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_NAME_EXPECTED);
            return [];
        }

        switch (param.Content)
        {
            case "error":
                PragmaErrorCommand(parameters, out error);
                if (error != null)
                    return [];
                break;
            default:
                error = new PreprocessDirectiveError(MessageID.ERR_UNKNOWN_PRAGMA_CMD, param.Content);
                return [];
        }
        error = null;
        return [];
    }

    private void PragmaErrorCommand(List<Token> parameters, out PreprocessDirectiveError? error)
    {
		if (parameters.Count < 2)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS);
            return;
        }
		Token cmdToken = parameters[1];
		if (cmdToken.Type != TokenType.Identifier)
        {
            error = new PreprocessDirectiveError(MessageID.ERR_NAME_EXPECTED);
            return;
        }
		
		MessageID? errorMessageId = null;
        if (parameters.Count > 2)
        {
            Token errMsgToken = parameters[2];
            if (errMsgToken.Type != TokenType.Identifier)
            {
                error = new PreprocessDirectiveError(MessageID.ERR_NAME_EXPECTED);
                return;
            }
            if (MessageID.TryParse(errMsgToken.Content.ToUpper(), out MessageID parsedId))
            {
                errorMessageId = parsedId;
            }
            else
            {
                error = new PreprocessDirectiveError(MessageID.ERR_UNKNOWN_MESSAGE_CODE, errMsgToken.Content);
                return;
            }
		}

        switch (cmdToken.Content)
        {
            case "disable":
                if (errorMessageId == null)
                {
                    error = new PreprocessDirectiveError(MessageID.ERR_MISSING_MESSAGE_CODE);
                    return;
                }
                else
                {
                    DisableError((MessageID)errorMessageId);
                }
                break;
            case "restore":
                if (errorMessageId == null)
					RestoreAllErrors();
                else
					EnableError((MessageID)errorMessageId);
                break;
            default:
                error = new PreprocessDirectiveError(MessageID.ERR_UNKNOWN_PRAGMA_ERROR_CMD, cmdToken.Content);
                return;
        }
        error = null;
    }
	
	private void DisableError(MessageID errorId)
	{
		_currentDisabledErrors.Add(errorId);
		DisabledErrorsHistory.Push((_currentLineIndex, _currentDisabledErrors.ToArray()));
	}
	
	private void EnableError(MessageID errorId)
	{
		_currentDisabledErrors.Remove(errorId);
		DisabledErrorsHistory.Push((_currentLineIndex, _currentDisabledErrors.ToArray()));
	}
	
	private void RestoreAllErrors()
	{
		_currentDisabledErrors.Clear();
		DisabledErrorsHistory.Push((_currentLineIndex, _currentDisabledErrors.ToArray()));
	}
}
#nullable restore
