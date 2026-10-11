using ONSMake;
using ONSMake.Lexer;
using System;
using System.Collections.Generic;
using System.Linq;

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

    private Result<string[], DirectiveError> DlgAutolabelDirective(ScriptUnit unit, Token[] parameters)
    {
		if (parameters.Length == 0)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_MISSING_DLG_AUTOLABEL_ARGUMENT));
		
		Token param = parameters[0];
        if (param.Type == TokenType.ParsedLabel)
        {
            _dialogAutolabelActive = true;
            _dialogAutolabelPrefix = param.Content.ToString();
        }
        else if (param.Type == TokenType.Multiply && parameters.Length > 1)
        {
            Token labelId = parameters[1];
            if (labelId.Type == TokenType.Identifier && labelId.StartColumn == param.StartColumn + 1)
            {
                _dialogAutolabelActive = true;
                _dialogAutolabelPrefix = string.Concat("*", labelId.Content);
            }
            else
            {
				return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT));
            }
        }
        else if (param.Type == TokenType.Identifier && param.Content.SequenceEqual("off"))
        {
            _dialogAutolabelActive = false;
        }
        else
        {
            return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT));
        }
		
        return Result<string[], DirectiveError>.Ok([]);
    }

    private Result<string[], DirectiveError> IncrementalLabelDirective(ScriptUnit unit, Token[] parameters)
    {
		if (parameters.Length == 0)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));

        string labelPrefix;
		Token param = parameters[0];
        if (param.Type == TokenType.Multiply && parameters.Length > 1)
        {
            Token labelId = parameters[1];
            if (labelId.Type == TokenType.Identifier && labelId.StartColumn == param.StartColumn + 1)
                labelPrefix = string.Concat("*", labelId.Content);
            else
				return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NOT_A_LABEL));
        }
        else if (param.Type == TokenType.ParsedLabel)
        {
            labelPrefix = param.Content.ToString();
        }
        else
        {
            return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NOT_A_LABEL));
        }

        unit.Autolabels.Add((labelPrefix, unit.OutputLines.Count));
        return Result<string[], DirectiveError>.Ok([string.Empty]);
    }

    private Result<string[], DirectiveError> IgnoreDirective(ScriptUnit unit, Token[] parameters, int lineIndex)
    {
		if (parameters.Length == 0)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_TOO_FEW_PARAMETERS));
		Token param = parameters[0];
		if (param.Type != TokenType.Identifier)
			return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_NAME_EXPECTED));

        if (param.Content.SequenceEqual("off"))
        {
            _currentDisabledErrors.Clear();
            unit.PushListOfIgnoredMessages([], lineIndex);
        }
        else
        {
            if (!MessageID.TryParse(param.Content, true, out MessageID parsedId))
                return Result<string[], DirectiveError>.Fail(new DirectiveError(MessageID.ERR_UNKNOWN_MESSAGE_CODE, param.Content.ToString()));
            _currentDisabledErrors.Add(parsedId);
            unit.PushListOfIgnoredMessages(_currentDisabledErrors.ToArray(), lineIndex);
        }
        return Result<string[], DirectiveError>.Ok([]);
    }
}
#nullable restore
