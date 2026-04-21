using System;
using System.Collections.Generic;

namespace Hitomiso.ONScripterMake;

public static class MessageTranslator
{
    private const string VERSION_STRING = "ONSMake 3.0";

    private static Dictionary<MessageID, string> _translatedMessages = new()
    {
        {MessageID.ERR_U_STUPIT, "You stupid."},
        {MessageID.ERR_UNKNOWN_TOKEN, "Can not identify token '{0}'."},
        {MessageID.ERR_NOT_A_NUMBER, "Numeric value expected."},
        {MessageID.ERR_NOT_A_NUMVAR, "Numeric variable expected."},
        {MessageID.ERR_NOT_A_NAME, "Name expected."},
        {MessageID.ERR_NOT_A_ENUM, "Enum[{0}] expected."},
        {MessageID.ERR_NOT_A_STRING, "String value expected."},
        {MessageID.ERR_NOT_A_STRVAR, "String variable expected."},
		{MessageID.ERR_NOT_A_DIALOG, "Dialog line expected."},

        {MessageID.ERR_MISSING_FIRST_OPERAND, "First operand is missing."},
        {MessageID.ERR_MISSING_SECOND_OPERAND, "Second operand is missing."},
        {MessageID.ERR_INVALID_OPERAND_TYPE, "Invalid operands type."},
        {MessageID.ERR_UNKNOWN_CONDITION_OPERATOR_TYPE, "Unknown condition operator type."},
        {MessageID.ERR_UNEXPECTED_TOKEN, "Unexpected token '{0}'."},
        {MessageID.ERR_EXPRESSIONS_NOT_ALLOWED, "Expressions are not allowed in this context."},
        {MessageID.ERR_CONDITIONS_NOT_ALLOWED, "Conditions are not allowed in this context."},
        {MessageID.ERR_NUMVAR_EXPECTED, "Numeric variable expected."},
        {MessageID.ERR_COMPARE_OPERAND_TYPE_MISMATCH, "Comparing operands' types are not the same."},
        {MessageID.ERR_INVALID_COLOR, "Invalid color. Only 6-digit color codes are accepted."},
        {MessageID.ERR_TOO_FEW_PARAMETERS, "Too few parameters."},
        {MessageID.ERR_TOO_MANY_PARAMETERS, "Too many parameters."},
        {MessageID.ERR_TOO_MANY_TOKEN_CHILDREN, "Too many token children."},
        {MessageID.ERR_ARRAY_WITHOUT_INDEX, "Array without index."},
        {MessageID.ERR_VAR_WITHOUT_NUM, "Using variable without variable number."},
        {MessageID.ERR_NAME_EXPECTED, "Keyword or name expected."},

        {MessageID.ERR_NOT_A_LABEL, "Not a label."},
        {MessageID.ERR_UNKNOWN_DIRECTIVE, "Unknown directive '{0}'."},
        {MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS, "Multiple '{0}' numalias definitions. First one was in {1}."},
        {MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS, "Multiple '{0}' stralias definitions. First one was in {1}."},
        {MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS, "Multiple '{0}' label definitions. First one was in {1}."},
        {MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS, "Multiple '{0}' command definitions. First one was in {1}."},
        {MessageID.ERR_LABEL_NOT_FOUND, "Label '{0}' not found."},
        {MessageID.ERR_NUMALIAS_NOT_DEFINED, "Numalias '{0}' is not defined."},
        {MessageID.ERR_STRALIAS_NOT_DEFINED, "Stralias '{0}' is not defined."},
        {MessageID.ERR_INVALID_BUILT_IN_EFFECT, "Invalid built-in effect number."},
        {MessageID.ERR_EFFECT_MASK_EXPECTED, "Effect mask file expected."},
        {MessageID.ERR_UNEXPECTED_EFFECT_MASK, "Unexpected effect mask."},
        {MessageID.ERR_INVALID_EFFECT_DURATION, "Invalid effect duration (ms)."},

        {MessageID.ERR_DIALOG_BETWEEN_NVL_DIRECTIVES, "Can not use dialog commands while building NVL dialog. Use #end first."},
        {MessageID.ERR_UNKNOWN_MESSAGE_CODE, "Unknown message code '{0}'."},
		{MessageID.ERR_MISSING_MESSAGE_CODE, "Pragma error message code is missing."},
        {MessageID.ERR_UNKNOWN_PRAGMA_CMD, "Unknown pragma command '{0}'."},
        {MessageID.ERR_UNKNOWN_PRAGMA_ERROR_CMD, "Unknown pragma error command '{0}'."},
        {MessageID.ERR_MISSING_DLG_AUTOLABEL_ARGUMENT, "dlg_autolabel needs 1 argument. 0 received."},
        {MessageID.ERR_INVALID_DLG_AUTOLABEL_ARGUMENT, "Invalid dlg_autolabel argument."},
        {MessageID.ERR_USE_N_DIRECTIVE_FIRST, "Use #n to start NVL dialog first."},
        {MessageID.ERR_UNKNOWN_COMMAND, "Unknown command '{0}'"},

        {MessageID.ASK_OVERWRITE_FILE, "File '{0}' already exists. Overwrite?"},
		{MessageID.INFO_NOTE, "{0}"},
        {MessageID.HELP,
            $"{VERSION_STRING}\n" +
            "Script \"build system\" with built-in linter for ONScripter-RU\n" +
            "Freeware. Licensed with GNU GPL v2\n" +
            "by Hitomiso, all lefts reserved.\n" +
            "\n" +
            "Usage:\n" +
            "\tonsmake [options...] -o <output file>\n" +
            "\n" +
            "Options:\n" +
            "-?\n" +
            "--help\n" +
            "\tPrint this message.\n" +
            "-o <output file>\n" +
            "--output <output file>\n" +
            "\tSpecify output file name. Must be set in CLI options or config file.\n" +
            "-c <config file>\n" +
            "--config <config file>\n" +
            "\tSpecify JSON config file.\n" +
            "\tDefault value: ONSMakeConfig.json\n" +
            "\tIf the config file is not found, then all options must be set via the CLI.\n" +
            "-l <list file>\n" +
            "--list <list file>\n" +
            "\tSpecify the file with the list of files to be included into the output script.\n" +
            "\tDefault value: ONSMakeList.txt\n" +
            "-w <directory>\n" +
            "--work-dir <directory>\n" +
            "\tSpecify working directory.\n" +
            "-s (-y|-n)\n" +
            "--silent (-y|-n)\n" +
            "\tDo not print messages to the console. When set, the -y or -n option must also be set.\n" +
            "-y\n" +
            "\tOverwrite output file.\n" +
            "-n\n" +
            "\tDo not overwrite output file.\n" +
            "-j <jobs count>\n" +
            "--jobs <jobs count>\n" +
            "\tSpecify number of threads used for processing files. Uses all available CPU threads by default.\n" +
            "-i <file path>\n" +
            "--input <file path>\n" +
            "\tSpecify files to include in the output script; each should have the -i option.\n" +
            "\tCan be used when the list file does not exist. If there is no --list, then only -i files are included, even if default list file exists.\n" +
            "-r\n" +
            "--raw\n" +
            "\tConcatenate included files without processing.\n" +
            "--ignore-directives\n" +
            "\tDo not execute directives. They won't be deleted or commented.\n" +
            "--no-string-replaces\n" +
            "\tDo not replace strings with patterns from config file.\n" +
            "--no-command-check\n" +
            "\tDo not check output script for command call errors. Parsing and multiple definition errors will still be checked and printed.\n" +
            "--ignore-errors\n" +
            "\tWrite the output file even when there are detected errors in script.\n" +
            "\n" +
            "Note: this program is created specifically for ONScripter-RU (by Umineko Project) and its cool features may not work for other forks of NScripter.\n" +
            "Software provided as-is, no guarantees, etc., etc...\n"
        },
    };

    public static string GetArgumentedString(MessageID id, string[] args)
    {
        if (!_translatedMessages.TryGetValue(id, out string value))
            return "No message found for " + id.ToString();
        return string.Format(value, args);
    }
}
