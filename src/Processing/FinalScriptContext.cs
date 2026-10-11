using System;
using System.Collections.Generic;

namespace ONSMake.Processing;

public class FinalScriptContext
{
    public readonly Dictionary<string, Definition> Labels = [];
    public readonly Dictionary<string, Definition> Numaliases = [];
    public readonly Dictionary<string, Definition> Straliases = [];
    public readonly Dictionary<string, CommandDefinition> CustomCommands = [];

    public void RegisterDefinitions(IEnumerable<ScriptUnit> processedFiles)
    {
        foreach (ScriptUnit file in processedFiles)
        {
            RegisterLabels(file);
			RegisterAutolabels(file);
            RegisterNumaliases(file);
			RegisterStraliases(file);
			RegisterSubroutines(file);
        }

        foreach (var cmdPair in CustomCommands)
            RegisterCustomCommandParameters(cmdPair.Key, cmdPair.Value);
    }
	
	private ScriptMessage MakeMultipleDefinitionsError(ScriptUnit fileUnit, Dictionary<string, Definition> savedDefinitions, Dictionary<string, TextPosition> currentDefinitions, string name, MessageID messageId)
	{
		Definition lastDefinition = savedDefinitions[name];
		TextPosition newPos = currentDefinitions[name];
		string[] messageArgs = [name, $"{lastDefinition.FileUnit.FileName}:{lastDefinition.DefinedAt.Line + 1}:{lastDefinition.DefinedAt.Column + 1}"];
		return new ScriptMessage(fileUnit, newPos, name.Length, ScriptMessage.MessageType.Error, messageId, messageArgs);
	}
	
	private void RegisterLabels(ScriptUnit file)
	{
		foreach (string label in file.Labels.Keys)
		{
			if (Labels.ContainsKey(label))
				file.AddMessage(MakeMultipleDefinitionsError(file, Labels, file.Labels, label, MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS));
			else
				Labels.Add(label, new Definition(file, file.Labels[label]));
		}
	}
	
	private void RegisterAutolabels(ScriptUnit file)
	{
		Dictionary<string, int> autolabelCounters = [];
		foreach (var autolabel in file.Autolabels)
		{
			string prefix = autolabel.prefix;
			int counter;
			if (autolabelCounters.TryGetValue(prefix, out int value))
			{
				counter = value;
			}
			else
			{
				counter = 0;
				autolabelCounters.Add(prefix, counter);
			}

			ProcessedLine outputLine = file.OutputLines[autolabel.outputLineIndex];

			while (Labels.ContainsKey(prefix + (++counter).ToString())) { }
			string labelName = prefix + counter.ToString();
			outputLine.OutputLine = labelName;
			file.OutputLines[autolabel.outputLineIndex] = outputLine;
			Labels.Add(labelName, new Definition(file, new TextPosition(outputLine.InputLineIndex, 0)));
			autolabelCounters[prefix] = counter;
		}
	}
	
	private void RegisterNumaliases(ScriptUnit file)
	{
		foreach (string numalias in file.Numaliases.Keys)
		{
			if (Numaliases.ContainsKey(numalias))
				file.AddMessage(MakeMultipleDefinitionsError(file, Numaliases, file.Numaliases, numalias, MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS));
			else
				Numaliases.Add(numalias, new Definition(file, file.Numaliases[numalias]));
		}
	}
	
	private void RegisterStraliases(ScriptUnit file)
	{
		foreach (string stralias in file.Straliases.Keys)
		{
			if (Straliases.ContainsKey(stralias))
				file.AddMessage(MakeMultipleDefinitionsError(file, Straliases, file.Straliases, stralias, MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS));
			else
				Straliases.Add(stralias, new Definition(file, file.Straliases[stralias]));
		}
	}
	
	private void RegisterSubroutines(ScriptUnit file)
	{
		foreach (string name in file.Subroutines.Keys)
		{
			if (CustomCommands.ContainsKey(name))
			{
				CommandDefinition last = CustomCommands[name];
				TextPosition pos = file.Subroutines[name];
				string[] messageArgs = [name, $"{last.FileUnit.FileName}:{last.DefinedAt.Line + 1}:{last.DefinedAt.Column + 1}"];
				ScriptMessage msg = new (file, pos, name.Length, ScriptMessage.MessageType.Error, MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS, messageArgs);
				file.AddMessage(msg);
			}
			else
			{
				CommandDefinition cmd = new(name, file, file.Subroutines[name], new DataType[0]);
				CustomCommands.Add(name, cmd);
			}
		}
	}
	
	private void RegisterCustomCommandParameters(string name, CommandDefinition cmdDefinition)
	{
		string routineLabel = "*" + name;
		if (!Labels.TryGetValue(routineLabel, out Definition label))
		{
			ScriptMessage labelNotFoundMessage = new
			(
				cmdDefinition.FileUnit, cmdDefinition.DefinedAt, name.Length,
				ScriptMessage.MessageType.Error, MessageID.ERR_LABEL_NOT_FOUND, [routineLabel]
			);
			cmdDefinition.FileUnit.AddMessage(labelNotFoundMessage);
			return;
		}
		
		if (!label.FileUnit.SubroutineParameters.TryGetValue(routineLabel, out DataType[] parameterTypes))
			return;
		cmdDefinition.ParameterTypes = parameterTypes;
	}
}
