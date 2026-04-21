using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Hitomiso.ONScripterMake.Processing;

public class FinalScriptContext
{
    public Dictionary<string, (SourceFileProcessor file, TextPosition pos)> LabelDefinitions = [];
    public Dictionary<string, (SourceFileProcessor file, TextPosition pos)> NumaliasDefinitions = [];
    public Dictionary<string, (SourceFileProcessor file, TextPosition pos)> StraliasDefinitions = [];
    public Dictionary<string, (SourceFileProcessor file, TextPosition pos)> CustomCommands = [];

    public SourceCodeMessage[] RegisterDefinitions(IEnumerable<SourceFileProcessor> processedFiles)
    {
        List<SourceCodeMessage> messages = [];

        foreach (SourceFileProcessor file in processedFiles)
        {
            var makeMultipleDefinitionsError = (Dictionary<string, (SourceFileProcessor, TextPosition)> savedDefinitions, Dictionary<string, TextPosition> currentDefinitions, string name, MessageID messageId) =>
            {
                (SourceFileProcessor definedInFile, TextPosition definedInPos) = savedDefinitions[name];
                TextPosition pos = currentDefinitions[name];
                string[] messageArgs = [name, $"{definedInFile.FileName}:{definedInPos.Line + 1}:{definedInPos.Column + 1}"];
                return new SourceCodeMessage(file, pos, name.Length, SourceCodeMessage.MessageType.Error, messageId, messageArgs);
            };

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

                while (LabelDefinitions.ContainsKey(prefix + (++counter).ToString())) { }
                string labelName = prefix + counter.ToString();
                outputLine.OutputLine = labelName;
                file.OutputLines[autolabel.outputLineIndex] = outputLine;
                LabelDefinitions.Add(labelName, (file, new TextPosition(outputLine.InputLineIndex, 0)));
                autolabelCounters[prefix] = counter;
            }
            foreach (string label in file.DefinedLabels.Keys)
            {
                if (LabelDefinitions.ContainsKey(label))
                    messages.Add(makeMultipleDefinitionsError(LabelDefinitions, file.DefinedLabels, label, MessageID.ERR_MULTIPLE_LABEL_DEFINITIONS));
                else
                    LabelDefinitions.Add(label, (file, file.DefinedLabels[label]));
            }
            foreach (string numalias in file.DefinedNumaliases.Keys)
            {
                if (NumaliasDefinitions.ContainsKey(numalias))
                    messages.Add(makeMultipleDefinitionsError(NumaliasDefinitions, file.DefinedNumaliases, numalias, MessageID.ERR_MULTIPLE_NUMALIAS_DEFINITIONS));
                else
                    NumaliasDefinitions.Add(numalias, (file, file.DefinedNumaliases[numalias]));
            }
            foreach (string stralias in file.DefinedStraliases.Keys)
            {
                if (StraliasDefinitions.ContainsKey(stralias))
                    messages.Add(makeMultipleDefinitionsError(StraliasDefinitions, file.DefinedStraliases, stralias, MessageID.ERR_MULTIPLE_STRALIAS_DEFINITIONS));
                else
                    StraliasDefinitions.Add(stralias, (file, file.DefinedStraliases[stralias]));
            }
            foreach (string subroutine in file.DefinedSubroutines.Keys)
            {
                if (CustomCommands.ContainsKey(subroutine))
                    messages.Add(makeMultipleDefinitionsError(CustomCommands, file.DefinedSubroutines, subroutine, MessageID.ERR_MULTIPLE_COMMAND_DEFINITIONS));
                else
                    CustomCommands.Add(subroutine, (file, file.DefinedSubroutines[subroutine]));
            }
        }

        foreach (var cmdDefinition in CustomCommands)
        {
            string routineLabel = "*" + cmdDefinition.Key;
            if (!LabelDefinitions.ContainsKey(routineLabel))
            {
                SourceCodeMessage labelNotFoundMessage = new
                (
                    cmdDefinition.Value.file,
                    cmdDefinition.Value.pos,
                    cmdDefinition.Key.Length,
                    SourceCodeMessage.MessageType.Error,
                    MessageID.ERR_LABEL_NOT_FOUND,
                    [routineLabel]
                );
                messages.Add(labelNotFoundMessage);
            }
            // @TODO: Надо прочитать getparams сразу после метки и выделить параметры команды
        }
        return [.. messages];
    }

    //
}
