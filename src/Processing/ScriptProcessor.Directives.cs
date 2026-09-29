using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake;
using ONSMake.Lexer;

namespace ONSMake.Processing;

#nullable enable
public partial class ScriptProcessor
{
    private Result<string[], DirectiveError> NDirective(ScriptUnit unit, string dialog)
    {
		if (string.IsNullOrWhiteSpace(dialog))
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));
		
        string[] outputLines = null;
        if (_nvlIsBuildingDialogue)
        {
            var endDirectiveResult = EndDirective();
			if (endDirectiveResult.IsError)
                return endDirectiveResult;
        }
		
        _nvlIsBuildingDialogue = true;
        _nvlDialogBuilder.Clear();
        _nvlLinesBetweenSection.Clear();
        _nvlLinesBetween.Clear();
		
        _nvlDialogBuilder.Append(dialog);
        return Result<string[], DirectiveError>.Ok(outputLines ?? []);
    }

    private Result<string[], DirectiveError> EDirective(ScriptUnit unit, string dialog)
    {
        if (!_nvlIsBuildingDialogue)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_USE_N_DIRECTIVE_FIRST));
		if (string.IsNullOrWhiteSpace(dialog))
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));

        _nvlDialogBuilder.Append("[#][*]");
        _nvlDialogBuilder.Append(dialog);

        _nvlLinesBetween.Add([.. _nvlLinesBetweenSection]);
        _nvlLinesBetweenSection.Clear();
        return Result<string[], DirectiveError>.Ok([]);
    }

    private Result<string[], DirectiveError> EndDirective()
    {
        if (!_nvlIsBuildingDialogue)
            return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_USE_N_DIRECTIVE_FIRST));

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
        return Result<string[], DirectiveError>.Ok([.. outputLines]);
    }

    private Result<string[], DirectiveError> DlgAutolabelDirective(ScriptUnit unit, List<Token> parameters)
    {
		if (parameters.Count == 0)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_MISSING_DLG_AUTOLABEL_ARGUMENT));
		
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
				return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT));
            }
        }
        else if (param.Type == TokenType.Identifier && param.Content == "off")
        {
            _dialogAutolabelActive = false;
        }
        else
        {
            return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT));
        }
		
        return Result<string[], DirectiveError>.Ok([]);
    }

    private Result<string[], DirectiveError> IncrementalLabelDirective(ScriptUnit unit, List<Token> parameters)
    {
		if (parameters.Count == 0)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));

        string labelPrefix;
		Token param = parameters[0];
        if (param.Type == TokenType.Multiply && parameters.Count > 1)
        {
            Token labelId = parameters[1];
            if (labelId.Type == TokenType.Identifier && labelId.StartColumn == param.StartColumn + 1)
                labelPrefix = "*" + labelId.Content;
            else
				return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NOT_A_LABEL));
        }
        else if (param.Type == TokenType.ParsedLabel)
        {
            labelPrefix = param.Content;
        }
        else
        {
            return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NOT_A_LABEL));
        }

        unit.Autolabels.Add((labelPrefix, unit.OutputLines.Count));
        return Result<string[], DirectiveError>.Ok([string.Empty]);
    }

    private Result<string[], DirectiveError> PragmaDirective(ScriptUnit unit, List<Token> parameters)
    {
		if (parameters.Count == 0)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));
		Token param = parameters[0];
		if (param.Type != TokenType.Identifier)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NAME_EXPECTED));

        switch (param.Content)
        {
            case "error":
                var commandResult = PragmaErrorCommand(parameters);
                if (commandResult.IsError)
                    return Result<string[], DirectiveError>.Fail(commandResult.Error);
                break;
            default:
				return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_UNKNOWN_PRAGMA_CMD, [param.Content]));
        }
        return Result<string[], DirectiveError>.Ok([]);
    }

    private Result<object, DirectiveError> PragmaErrorCommand(List<Token> parameters)
    {
		if (parameters.Count < 2)
            return Result<object, DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));
		Token cmdToken = parameters[1];
		if (cmdToken.Type != TokenType.Identifier)
			return Result<object, DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NAME_EXPECTED));
		
		MessageID? errorMessageId = null;
        if (parameters.Count > 2)
        {
            Token errMsgToken = parameters[2];
            if (errMsgToken.Type != TokenType.Identifier)
                return Result<object, DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NAME_EXPECTED));
            if (MessageID.TryParse(errMsgToken.Content.ToUpper(), out MessageID parsedId))
                errorMessageId = parsedId;
            else
				return Result<object, DirectiveError>.Fail(new DirectiveError(MessageID.ERR_UNKNOWN_MESSAGE_CODE, [errMsgToken.Content]));
		}

        switch (cmdToken.Content)
        {
            case "disable":
                if (errorMessageId == null)
					return Result<object, DirectiveError>.Fail(new DirectiveError(MessageID.ERR_MISSING_MESSAGE_CODE));
                else
                    DisableError((MessageID)errorMessageId);
                break;
            case "restore":
                if (errorMessageId == null)
					RestoreAllErrors();
                else
					EnableError((MessageID)errorMessageId);
                break;
            default:
				return Result<object, DirectiveError>.Fail(new DirectiveError(MessageID.ERR_UNKNOWN_PRAGMA_ERROR_CMD, [cmdToken.Content]));
        }
        return Result<object, DirectiveError>.Ok(null);
    }
	
	private void DisableError(MessageID errorId) => _currentDisabledErrors.Add(errorId);
	private void EnableError(MessageID errorId) => _currentDisabledErrors.Remove(errorId);
	private void RestoreAllErrors() => _currentDisabledErrors.Clear();
}
#nullable restore
