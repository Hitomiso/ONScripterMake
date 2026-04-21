using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.IO;
using System.Text.RegularExpressions;

namespace Hitomiso.ONScripterMake;

#nullable enable
public class ProjectConfiguration
{
    private const string CONFIG_MESSAGE_PREFIX = "config";
    private const string COMMANDS_MESSAGE_PREFIX = "engine commands";

    public bool PrintHelp;
    public bool Silent;
    public bool? OverwriteOutputFile;
    public bool Raw;
    public bool IgnoreDirectives;
    public bool NoStringReplaces;
    public bool NoCommandCheck;
    public bool IgnoreErrors;

    public int? JobsCount;
    public string? ListFile;
    public List<string> InputFiles = [];
    public string? ConfigFile;
    public string? CommandsFile;
    public string? OutputFile;
    public string? WorkingDirectory;

    public List<(Regex, string)> StringReplacements = [];
	public Dictionary<string, List<Command>> EngineCommands = [];

    public ProjectConfiguration() { }

    public ProjectConfiguration(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        JsonDocument doc = JsonDocument.Parse(fileData);
        var root = doc.RootElement;
        ListFile = ReadStringProperty(root, "list");
        OutputFile = ReadStringProperty(root, "output");
        CommandsFile = ReadStringProperty(root, "commands");

        var inputsArr = ReadArrayProperty(root, "inputs");
        if (inputsArr != null)
        {
            for (int i = 0; i < inputsArr.Length; i++)
            {
                var prop = inputsArr[i];
                if (prop.ValueKind == JsonValueKind.String)
                    InputFiles.Add(prop.GetString());
                else
                    OutputHandler.PrintError($"Element 'input[{i}]' should be a string.", CONFIG_MESSAGE_PREFIX);
            }
        }

        var replacesArr = ReadArrayProperty(root, "replaces");
        if (replacesArr != null)
        {
            for (int i = 0; i < replacesArr.Length; i++)
            {
                var prop = replacesArr[i];
                ParseReplacesElement(prop);
            }
        }

        var flagsArr = ReadArrayProperty(root, "options");
        if (flagsArr != null)
        {
            for (int i = 0; i < flagsArr.Length; i++)
            {
                var prop = flagsArr[i];
                if (prop.ValueKind == JsonValueKind.String)
                    TryApplySimpleOption(prop.GetString());
                else
                    OutputHandler.PrintError($"Elememt 'options[{i}]' should be a string.", CONFIG_MESSAGE_PREFIX);
            }
        }

        if (CommandsFile != null)
        {
            if (File.Exists(CommandsFile))
            {
                if (TryParseCommandsFile(CommandsFile, out Dictionary<string, List<Command>> engineCommands))
                    EngineCommands = engineCommands;
            }
            else
                OutputHandler.PrintError($"File '{CommandsFile}' not found.", CONFIG_MESSAGE_PREFIX);
        }
    }

    public bool ParseArguments(string[] args)
    {
        if (args == null)
            return true;
        uint errorsBeforeParsing = OutputHandler.Errors;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-l":
                case "--list":
                    ListFile = args.ElementAtOrDefault(++i);
                    if (ListFile == null)
                        OutputHandler.PrintError("List option has no value. Specify list file's path.");
                    break;
                case "-i":
                case "--input":
                    string? file = args.ElementAtOrDefault(++i);
                    if (file == null)
                        OutputHandler.PrintError("Input option has no value. Specify input file's path.");
                    else
                        InputFiles.Add(file);
                    break;
                case "-c":
                case "--config":
                    ConfigFile = args.ElementAtOrDefault(++i);
                    if (ConfigFile == null)
                        OutputHandler.PrintError("Config option has no value. Specify config file's path.");
                    break;
                case "-o":
                case "--output":
                    OutputFile = args.ElementAtOrDefault(++i);
                    if (OutputFile == null)
                        OutputHandler.PrintError("Output option has no value. Specify output file's path.");
                    break;
                case "-w":
                case "--work-dir":
                    WorkingDirectory = args.ElementAtOrDefault(++i);
                    if (WorkingDirectory == null)
                        OutputHandler.PrintError("Working directory option has no value. Specify working directory's path.");
                    break;
                case "-j":
                case "--jobs":
                    string? countArgument = args.ElementAtOrDefault(++i);
                    if (countArgument == null)
                        OutputHandler.PrintError("Jobs option has no value. Specify jobs count.");
                    if (int.TryParse(countArgument, out int count) && count > 0)
                        JobsCount = count;
                    else
                        OutputHandler.PrintError("Jobs count must be a natural number.");
                    break;
                default:
                    TryApplySimpleOption(args[i]);
                    break;
            }
        }
        if (Silent && OverwriteOutputFile == null)
            OutputHandler.PrintError("-y or -n must be specified in silent mode.");

        return OutputHandler.Errors == errorsBeforeParsing;
    }

    public bool TryApplySimpleOption(string option)
    {
        uint errorsBeforeApplying = OutputHandler.Errors;

        switch (option)
        {
            case "-?":
            case "--help":
                PrintHelp = true;
                break;
            case "-s":
            case "--silent":
                Silent = true;
                break;
            case "-y":
                if (OverwriteOutputFile == false)
                    OutputHandler.PrintError("Can not use -y when you already said -n.");
                OverwriteOutputFile = true;
                break;
            case "-n":
                if (OverwriteOutputFile == true)
                    OutputHandler.PrintError("Can not use -n when you already said -y.");
                OverwriteOutputFile = false;
                break;
            case "-r":
            case "--raw":
                Raw = true;
                break;
            case "--ignore-directives":
                IgnoreDirectives = true;
                break;
            case "--no-string-replaces":
                NoStringReplaces = true;
                break;
            case "--no-command-check":
                NoCommandCheck = true;
                break;
            case "--ignore-errors":
                IgnoreErrors = true;
                break;
            default:
                OutputHandler.PrintError($"Unknown option '{option}'.");
                break;
        }

        return OutputHandler.Errors == errorsBeforeApplying;
    }

    private void ParseReplacesElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            OutputHandler.PrintError($"All 'replaces' elements should be ARRAYS of two strings (regex and replacement).", CONFIG_MESSAGE_PREFIX);
            return;
        }
        if (element.GetArrayLength() != 2)
        {
            OutputHandler.PrintError($"All 'replaces' elements should be arrays of TWO strings (regex and replacement).", CONFIG_MESSAGE_PREFIX);
            return;
        }
        if (element[0].ValueKind != JsonValueKind.String || element[1].ValueKind != JsonValueKind.String)
        {
            OutputHandler.PrintError($"All 'replaces' elements should be arrays of two STRINGS (regex and replacement).", CONFIG_MESSAGE_PREFIX);
            return;
        }

        try
        {
            string? regexString = element[0].GetString();
            string? replacement = element[1].GetString();
            if (regexString == null || replacement == null)
                throw new NullReferenceException();
            Regex regex = new(regexString);
            StringReplacements.Add((regex, replacement));
        }
        catch (ArgumentException ex)
        {
            OutputHandler.PrintError($"Invalid regex '{element[0].GetString()}': {ex.Message}", CONFIG_MESSAGE_PREFIX);
        }
    }

    private static bool TryParseCommandsFile(string commandsFile, out Dictionary<string, List<Command>> engineCommands)
    {
        engineCommands = [];
        uint errorsBeforeParsingCommands = OutputHandler.Errors;
        byte[] fileData = File.ReadAllBytes(commandsFile);
        var root = JsonDocument.Parse(fileData).RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            OutputHandler.PrintError($"Root element of commands file should be an array.", COMMANDS_MESSAGE_PREFIX);
            return false;
        }

        for (int i = 0; i < root.GetArrayLength(); i++)
        {
            var cmdElement = root[i];
            var cmd = ReadCommand(cmdElement);
            if (cmd != null)
			{
				if (!engineCommands.ContainsKey(cmd.Name))
					engineCommands.Add(cmd.Name, new List<Command>());
				engineCommands[cmd.Name].Add(cmd);
			}
        }

        return OutputHandler.Errors == errorsBeforeParsingCommands;
    }

    private static Command? ReadCommand(JsonElement cmdElement)
    {
        if (cmdElement.ValueKind != JsonValueKind.Object)
        {
            OutputHandler.PrintError($"Each command element should be an object.", COMMANDS_MESSAGE_PREFIX);
            return null;
        }

        var name = ReadStringProperty(cmdElement, "name");
        if (name == null)
            return null;
        var desc = ReadStringProperty(cmdElement, "desc");
        var parameters = ReadArrayProperty(cmdElement, "params");
        if (parameters == null)
            return null;

        List<Parameter> cmdParams = [];
        foreach (var paramElement in parameters)
        {
			if (cmdParams.Count > 0 && cmdParams[^1].Repeat)
			{
				OutputHandler.PrintError($"Each command may have only one repeated parameter and only as the last parameter.", COMMANDS_MESSAGE_PREFIX);
				return null;
			}
            var param = ReadCommandParameter(paramElement);
            if (param != null)
                cmdParams.Add(param);
        }

        return new Command(name, [.. cmdParams], desc);
    }

    private static Parameter? ReadCommandParameter(JsonElement paramElement)
    {
        if (paramElement.ValueKind != JsonValueKind.Object)
        {
            OutputHandler.PrintError($"Each parameter element should be an object.", COMMANDS_MESSAGE_PREFIX);
            return null;
        }

        var name = ReadStringProperty(paramElement, "name");
        if (name == null)
            return null;
        var type = ReadStringProperty(paramElement, "type");
        if (type == null)
            return null;
        var desc = ReadStringProperty(paramElement, "desc");
        DataType paramDataType;
        if (!DataType.TryParse(type, out paramDataType))
        {
            OutputHandler.PrintError($"Unknown DataType '{type}'.", COMMANDS_MESSAGE_PREFIX);
            return null;
        }

        List<string> enumValues = [];
        if (paramDataType == DataType.Enum)
        {
            var valueElements = ReadArrayProperty(paramElement, "values");
            if (valueElements == null)
            {
                OutputHandler.PrintError("Enum parameter should have 'values' array of accepted values.");
                return null;
            }

            foreach (var element in valueElements)
            {
                if (element.ValueKind != JsonValueKind.String)
                {
                    OutputHandler.PrintError("Enum parameter's 'values' array should contain only strings.");
                    return null;
                }
                string? value = element.GetString();
                if (value == null)
                    throw new NullReferenceException();
                enumValues.Add(value);
            }
        }
        else
        {
            if (paramElement.TryGetProperty("values", out _))
            {
                OutputHandler.PrintError("Not Enum parameters should not have 'values' property.");
                return null;
            }
        }
		bool repeatValue = false;
		if (paramElement.TryGetProperty("repeat", out var repeatProperty))
		{
			repeatValue = repeatProperty.GetBoolean();
		}

        return new Parameter(paramDataType, name, enumValues.ToArray(), desc, repeatValue);
    }

    private static string? ReadStringProperty(JsonElement parent, string subpropertyName)
    {
        if (parent.TryGetProperty(subpropertyName, out var subproperty))
        {
            if (subproperty.ValueKind == JsonValueKind.String)
                return subproperty.GetString();
            else
                OutputHandler.PrintError($"Property '{subpropertyName}' should be a string.");
        }
        return null;
    }

    private static JsonElement[]? ReadArrayProperty(JsonElement parent, string subpropertyName)
    {
        if (parent.TryGetProperty(subpropertyName, out var subproperty))
        {
            if (subproperty.ValueKind == JsonValueKind.Array)
            {
                int len = subproperty.GetArrayLength();
                var output = new JsonElement[len];
                for (int i = 0; i < len; i++)
                    output[i] = subproperty[i];
                return output;
            }
            else
                OutputHandler.PrintError($"Property '{subpropertyName}' should be an array.");
        }
        return null;
    }    
}
#nullable restore
