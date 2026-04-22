using System;
using System.Collections.Generic;

namespace Hitomiso.ONScripterMake;

public static class MessageTranslator
{
    private static Dictionary<MessageID, string> _translatedMessages = new()
    {
		{MessageID.ERR_U_STUPIT, "You stupid."},
		{MessageID.ERR_UNKNOWN_TOKEN, "Can not identify token."},
		{MessageID.ERR_INVALID_TOKEN_NAME, "Token names must start with an ascii letter or an underscore."},
		{MessageID.ERR_INVALID_START_OF_LINE, "Invalid start of line."},
		{MessageID.ERR_COMMAND_EXPECTED, "Command expected here, but found something else."},
		{MessageID.ERR_COMMANDS_NOT_ALLOWED, "Commands are not allowed in this context."},
		{MessageID.ERR_UNEXPECTED_BRACKET, "This bracket was not expected here."},
		{MessageID.ERR_NOT_A_NUMBER, "Numeric value expected."},
		{MessageID.ERR_NOT_A_NAME, "Name expected."},
		{MessageID.ERR_NOT_A_STRING, "String value expected."},
		{MessageID.ERR_UNKNOWN_OPERATOR_PRIORITY, "Can not find priority of a '{0}' operator."},
		{MessageID.ERR_UNEXPECTED_EOL, "Unexpected end of line."},
		{MessageID.ERR_OPENING_BRACKET_EXPECTED, "Opening bracket expected."},
		{MessageID.ERR_CLOSING_BRACKET_EXPECTED, "Closing bracket expected."},
		{MessageID.ERR_MISSING_FIRST_OPERAND, "First operand is missing."},
		{MessageID.ERR_MISSING_SECOND_OPERAND, "Second operand is missing."},
		{MessageID.ERR_UNEXPECTED_TOKEN, "Unexpected token."},
		{MessageID.ERR_CONDITIONS_NOT_ALLOWED, "Conditions are not allowed in this context."},
		{MessageID.ERR_NUMVAR_EXPECTED, "Numeric variable expected."},
		{MessageID.ERR_MISSING_LOOP_FROM, "Initial counter value is missing."},
		{MessageID.ERR_MISSING_LOOP_TO, "Final counter value (to) is missing."},
		{MessageID.ERR_MISSING_LOOP_STEP, "Counter step is missing."},
		{MessageID.ERR_INVALID_COLOR, "Invalid color. Only 6-digit color codes are accepted."},
		{MessageID.ERR_INVALID_CONDITION, "Condition must have comparison or/and logical operators."},
		{MessageID.ERR_TOO_FEW_PARAMETERS, "Too few parameters."},
		{MessageID.ERR_NAME_EXPECTED, "Keyword or name expected."},
		
        {MessageID.ERR_NOT_A_LABEL, "Not a label."},
        {MessageID.ERR_LABEL_WITHOUT_NAME, "Labels without names are not allowed."},
        {MessageID.ERR_UNKNOWN_DIRECTIVE, "Unknown directive '{0}'."},
        {MessageID.ERR_DIRECTIVES_NOT_ALLOWED, "Directives are not allowed in this context. Use them only on a new line."},
        {MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS, "Multiple '{0}' numalias definitions. First one was in {1}."},
        {MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS, "Multiple '{0}' stralias definitions. First one was in {1}."},
        {MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS, "Multiple '{0}' label definitions. First one was in {1}."},
        {MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS, "Multiple '{0}' command definitions. First one was in {1}."},
        {MessageID.ERR_STRING_NOT_CLOSED, "String literal is not closed. Please count your quotes."},
        {MessageID.ERR_LABEL_NOT_FOUND, "Label '{0}' not found."},
        {MessageID.ERR_DIALOG_BETWEEN_NVL_DIRECTIVES, "Can not use dialog commands while building NVL dialog. Use #end first."},
        {MessageID.ERR_UNKNOWN_MESSAGE_CODE, "Unknown message code '{0}'."},
        {MessageID.ERR_UNKNOWN_PRAGMA_CMD, "Unknown pragma command '{0}'."},
        {MessageID.ERR_UNKNOWN_PRAGMA_ERROR_CMD, "Unknown pragma error command '{0}'."},
        {MessageID.ERR_MISSING_DLG_AUTOLABEL_ARGUMENT, "dlg_autolabel needs 1 argument. 0 received."},
        {MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT, "Invalid dlg_autolabel argument."},
        {MessageID.ERR_USE_N_DIRECTIVE_FIRST, "Use #n to start NVL dialog first."},
		{MessageID.ERR_UNKNOWN_COMMAND, "Unknown command '{0}'"},

        {MessageID.ASK_OVERWRITE_FILE, "File '{0}' already exists. Overwrite?"},
    };

    public static void LoadLanguagePack(string filePath)
    {
        throw new NotImplementedException();
    }

    public static string GetArgumentedString(MessageID id, string[] args)
    {
        if (!_translatedMessages.ContainsKey(id))
            return string.Empty;
        return string.Format(_translatedMessages[id], args);
    }
}
