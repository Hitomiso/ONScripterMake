using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Parsing;

namespace Hitomiso.ONScripterMake.Processing;

public class DirectiveParameterException : Exception
{
	public Token Token;
	public MessageID MessageID { get; private set; }
	
	// @TODO: Доработать до нормального состояния, добавить аргументы сообщения, базовый конструктор
	public DirectiveParameterException(Token token, MessageID messageId, params string[] args) 
		: base(MessageTranslator.GetArgumentedString(messageId, args))
	{
		Token = token;
		MessageID = messageId;
	}
}

public partial class ScriptProcessor
{
    private string[] NDirective(Token directiveToken)
    {
		if (directiveToken.Children.Count == 0)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_TOO_FEW_PARAMETERS);
		
        string[] outputLines = null;
        if (_nvlIsBuildingDialogue)
            outputLines = EndDirective(directiveToken);
		
        _nvlIsBuildingDialogue = true;
        _nvlDialogBuilder.Clear();
        _nvlLinesBetweenSection.Clear();
        _nvlLinesBetween.Clear();
		
        _nvlDialogBuilder.Append(directiveToken.Children[0].Value);
        return outputLines ?? [];
    }

    private string[] EDirective(Token directiveToken)
    {
        if (!_nvlIsBuildingDialogue)
            throw new DirectiveParameterException(directiveToken, MessageID.ERR_USE_N_DIRECTIVE_FIRST);
		if (directiveToken.Children.Count == 0)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_TOO_FEW_PARAMETERS);

        _nvlDialogBuilder.Append("[#][*]");
        _nvlDialogBuilder.Append(directiveToken.Children[0].Value);

        _nvlLinesBetween.Add(_nvlLinesBetweenSection.ToArray());
        _nvlLinesBetweenSection.Clear();
        return [];
    }

    private string[] EndDirective(Token directiveToken)
    {
        if (!_nvlIsBuildingDialogue)
            throw new DirectiveParameterException(directiveToken, MessageID.ERR_USE_N_DIRECTIVE_FIRST);

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
        return outputLines.ToArray();
    }

    private string[] DlgAutolabelDirective(Token directiveToken)
    {
		if (directiveToken.Children.Count == 0)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_MISSING_DLG_AUTOLABEL_ARGUMENT);

		Token param = directiveToken.Children[0];
        if (param.Type == TokenType.Label)
        {
            _dialogAutolabelActive = true;
            _dialogAutolabelPrefix = param.Value;
            _dialogAutolabelValue = 1;
        }
        else if (param.Type == TokenType.Name && param.Value == "off")
            _dialogAutolabelActive = false;
        else
            throw new DirectiveParameterException(param, MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT);

        return [];
    }

    private string[] IncrementalLabelDirective(Token directiveToken)
    {
		if (directiveToken.Children.Count == 0)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_TOO_FEW_PARAMETERS);
		Token param = directiveToken.Children[0];
		if (param.Type != TokenType.Label)
			throw new DirectiveParameterException(param, MessageID.ERR_NOT_A_LABEL);
		
        while (_labels.ContainsKey(param.Value + _dialogAutolabelValue.ToString()))
            _dialogAutolabelValue++;
        return [param.Value + _dialogAutolabelValue.ToString()];
    }

    private string[] PragmaDirective(Token directiveToken)
    {
		if (directiveToken.Children.Count == 0)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_TOO_FEW_PARAMETERS);
		Token param = directiveToken.Children[0];
		if (param.Type != TokenType.Name)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_NAME_EXPECTED);

        switch (param.Value)
        {
            case "error":
                PragmaErrorCommand(directiveToken);
                break;
            default:
                throw new DirectiveParameterException(param, MessageID.ERR_UNKNOWN_PRAGMA_CMD, param.Value);
        }
        return [];
    }

    private void PragmaErrorCommand(Token directiveToken)
    {
		if (directiveToken.Children.Count < 2)
			throw new DirectiveParameterException(directiveToken, MessageID.ERR_TOO_FEW_PARAMETERS);
		Token cmdToken = directiveToken.Children[1];
		if (cmdToken.Type != TokenType.Name)
			throw new DirectiveParameterException(cmdToken, MessageID.ERR_NAME_EXPECTED);
		
		MessageID? errorMessageId = null;
		if (directiveToken.Children.Count > 2)
		{
			Token errMsgToken = directiveToken.Children[2];
			if (errMsgToken.Type != TokenType.Name)
				throw new DirectiveParameterException(errMsgToken, MessageID.ERR_NAME_EXPECTED);
			errMsgToken.Value = errMsgToken.Value.ToUpper();
			if (MessageID.TryParse(errMsgToken.Value, out MessageID parsedId))
				errorMessageId = parsedId;
			else
                throw new DirectiveParameterException(errMsgToken, MessageID.ERR_UNKNOWN_MESSAGE_CODE, errMsgToken.Value);
		}

        switch (cmdToken.Value)
        {
            case "disable":
                if (errorMessageId == null)
                    _pragmaDisableAllErrors = true;
                else
                    _pragmaDisabledErrors.Add((MessageID)errorMessageId);
                break;
            case "restore":
                if (errorMessageId == null)
                {
                    _pragmaDisableAllErrors = false;
                    _pragmaDisabledErrors.Clear();
                }
                else
                    _pragmaDisabledErrors.Remove((MessageID)errorMessageId);
                break;
            default:
                throw new DirectiveParameterException(cmdToken, MessageID.ERR_UNKNOWN_PRAGMA_ERROR_CMD, cmdToken.Value);
        }
    }
}
