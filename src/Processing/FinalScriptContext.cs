using System;
using System.Collections.Generic;

namespace ONSMake.Processing;

public class FinalScriptContext
{
    public readonly Dictionary<string, (ScriptUnit file, TextPosition pos)> Labels = [];
    public readonly Dictionary<string, (ScriptUnit file, TextPosition pos)> Numaliases = [];
    public readonly Dictionary<string, (ScriptUnit file, TextPosition pos)> Straliases = [];
    public readonly Dictionary<string, (ScriptUnit file, TextPosition pos)> CustomCommands = [];

    public void RegisterDefinitions(IEnumerable<ScriptUnit> processedFiles)
    {
        foreach (ScriptUnit file in processedFiles)
        {
            var makeMultipleDefinitionsError = (Dictionary<string, (ScriptUnit, TextPosition)> savedDefinitions, Dictionary<string, TextPosition> currentDefinitions, string name, MessageID messageId) =>
            {
                (ScriptUnit definedInFile, TextPosition definedInPos) = savedDefinitions[name];
                TextPosition pos = currentDefinitions[name];
                string[] messageArgs = [name, $"{definedInFile.FileName}:{definedInPos.Line + 1}:{definedInPos.Column + 1}"];
                return new ScriptMessage(file, pos, name.Length, ScriptMessage.MessageType.Error, messageId, messageArgs);
            };

            foreach (string label in file.Labels.Keys)
            {
                if (Labels.ContainsKey(label))
                    file.ProcessingMessages.Add(makeMultipleDefinitionsError(Labels, file.Labels, label, MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS));
                else
                    Labels.Add(label, (file, file.Labels[label]));
            }
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
                Labels.Add(labelName, (file, new TextPosition(outputLine.InputLineIndex, 0)));
                autolabelCounters[prefix] = counter;
            }
            foreach (string numalias in file.Numaliases.Keys)
            {
                if (Numaliases.ContainsKey(numalias))
                    file.ProcessingMessages.Add(makeMultipleDefinitionsError(Numaliases, file.Numaliases, numalias, MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS));
                else
                    Numaliases.Add(numalias, (file, file.Numaliases[numalias]));
            }
            foreach (string stralias in file.Straliases.Keys)
            {
                if (Straliases.ContainsKey(stralias))
                    file.ProcessingMessages.Add(makeMultipleDefinitionsError(Straliases, file.Straliases, stralias, MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS));
                else
                    Straliases.Add(stralias, (file, file.Straliases[stralias]));
            }
            foreach (string subroutine in file.Subroutines.Keys)
            {
                if (CustomCommands.ContainsKey(subroutine))
                    file.ProcessingMessages.Add(makeMultipleDefinitionsError(CustomCommands, file.Subroutines, subroutine, MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS));
                else
                    CustomCommands.Add(subroutine, (file, file.Subroutines[subroutine]));
            }
        }

        foreach (var cmdDefinition in CustomCommands)
        {
            string routineLabel = "*" + cmdDefinition.Key;
            if (Labels.TryGetValue(routineLabel, out var def))
            {
                // @TODO: Надо прочитать getparams сразу после метки и выделить параметры команды
                // Ищем первый токен, который не является комментарием
                // Если это не getparam, то кидаем предупреждение
            }
            else
            {
                ScriptMessage labelNotFoundMessage = new
                (
                    cmdDefinition.Value.file, cmdDefinition.Value.pos, cmdDefinition.Key.Length,
                    ScriptMessage.MessageType.Error, MessageID.ERR_LABEL_NOT_FOUND, [routineLabel]
                );
                cmdDefinition.Value.file.ProcessingMessages.Add(labelNotFoundMessage);
            }
        }
    }
}
