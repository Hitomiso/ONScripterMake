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
    public bool PrintHelp;
    public bool Verbose;
    public bool Silent;
    public bool? OverwriteOutputFile;
    public bool Raw;
    public bool CommentDirectives;
    public bool IgnoreDirectives;
    public bool NoStringReplaces;
    public bool NoScriptCheck;
    public bool IgnoreErrors;

    public string? ListFile;
    public List<string> InputFiles = [];
    public string? ConfigFile;
    public string? CommandsFile;
    public string? OutputFile;
    public string? WorkingDirectory;

    public List<(Regex, string)> StringReplacements = [];
	public List<Command> EngineCommands;

    public ProjectConfiguration() { }

    public ProjectConfiguration(string filePath)
    {
        OutputHandler.DefaultPrefix = "Config";

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
                    OutputHandler.PrintError($"Element 'input[{i}]' should be a string.");
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
                    OutputHandler.PrintError($"Elememt 'options[{i}]' should be a string.");
            }
        }

        if (CommandsFile != null)
        {
			if (File.Exists(CommandsFile))
			{
				OutputHandler.DefaultPrefix = "Commands";
				if (TryParseCommandsFile(CommandsFile, out List<Command> engineCommands))
					EngineCommands = engineCommands;
			}
			else
				OutputHandler.PrintError($"File '{CommandsFile}' not found.");
        }
    }

    private static bool TryParseCommandsFile(string commandsFile, out List<Command> engineCommands)
    {
        engineCommands = [];
		uint errorsBeforeParsingCommands = OutputHandler.Errors;
        byte[] fileData = File.ReadAllBytes(commandsFile);
        var root = JsonDocument.Parse(fileData).RootElement;
        if (root.ValueKind != JsonValueKind.Array)
        {
            OutputHandler.PrintError($"Root element of commands file should be an array.");
            return false;
        }

        for (int i = 0; i < root.GetArrayLength(); i++)
        {
            var cmdElement = root[i];
            var cmd = ReadCommand(cmdElement);
            if (cmd != null)
                engineCommands.Add(cmd);
        }
		
		return OutputHandler.Errors == errorsBeforeParsingCommands;
    }

    private static Command? ReadCommand(JsonElement cmdElement)
    {
        if (cmdElement.ValueKind != JsonValueKind.Object)
        {
            OutputHandler.PrintError($"Each command element should be an object.");
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
		foreach(var paramElement in parameters)
        {
            var param = ReadCommandParameter(paramElement);
            if (param != null)
                cmdParams.Add(param);
        }

        return new Command(name, cmdParams.ToArray(), desc);
    }

    private static Parameter? ReadCommandParameter(JsonElement paramElement)
    {
        if (paramElement.ValueKind != JsonValueKind.Object)
        {
            OutputHandler.PrintError($"Each parameter element should be an object.");
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
            OutputHandler.PrintError($"Unknown DataType '{type}'.");
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
				enumValues.Add(element.GetString());
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

        return new Parameter(paramDataType, name, enumValues.ToArray(), desc);
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

    private void ParseReplacesElement(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            OutputHandler.PrintError($"All 'replaces' elements should be ARRAYS of two strings (regex and replacement).");
            return;
        }
        if (element.GetArrayLength() != 2)
        {
            OutputHandler.PrintError($"All 'replaces' elements should be arrays of TWO strings (regex and replacement).");
            return;
        }
        if (element[0].ValueKind != JsonValueKind.String || element[1].ValueKind != JsonValueKind.String)
        {
            OutputHandler.PrintError($"All 'replaces' elements should be arrays of two STRINGS (regex and replacement).");
            return;
        }

        try
        {
            Regex regex = new(element[0].GetString());
            StringReplacements.Add((regex, element[1].GetString()));
        }
        catch (ArgumentException ex)
        {
            OutputHandler.PrintError($"Invalid regex '{element[0].GetString()}': {ex.Message}");
        }
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
            case "-v":
            case "--verbose":
                if (Silent)
                    OutputHandler.PrintError("Can not use verbose and silent modes at the same time.");
                Verbose = true;
                break;
            case "-s":
            case "--silent":
                if (Verbose)
                    OutputHandler.PrintError("Can not use verbose and silent modes at the same time.");
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
                if (CommentDirectives)
                    OutputHandler.PrintError("--raw conflicts with --comment-directives.");
                Raw = true;
                break;
            case "--comment-directives":
                if (Raw)
                    OutputHandler.PrintError("--comment-directives conflicts with --raw.");
                CommentDirectives = true;
                break;
            case "--ignore-directives":
                IgnoreDirectives = true;
                break;
            case "--no-string-replaces":
                NoStringReplaces = true;
                break;
            case "--no-script-check":
                NoScriptCheck = true;
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
}
#nullable restore