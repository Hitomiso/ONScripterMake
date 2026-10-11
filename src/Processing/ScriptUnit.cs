using ONSMake;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;

namespace ONSMake.Processing;

#nullable enable
public sealed class ScriptUnit
{
	public readonly string FileName;
    public readonly List<(string prefix, int outputLineIndex)> Autolabels = [];
    public readonly Dictionary<string, TextPosition> Labels = [];
	public readonly Dictionary<string, TextPosition> Numaliases = [];
	public readonly Dictionary<string, TextPosition> Straliases = [];
	public readonly Dictionary<string, TextPosition> Subroutines = [];
	public readonly Dictionary<string, DataType[]> SubroutineParameters = [];
    public ReadOnlyCollection<ScriptMessage> Messages => _processingMessages.AsReadOnly();

    public readonly string[] InputLines;
	public readonly List<ProcessedLine> OutputLines = [];

    private readonly record struct IgnoredMessagesEntry(MessageID[] MessageIDs, int FromLine) { }
    private readonly List<ScriptMessage> _processingMessages = [];
    private readonly Stack<IgnoredMessagesEntry> _ignoredMessagesHistory = [];

    public ScriptUnit(string fileName)
	{
	    FileName = fileName;
	    InputLines = File.ReadAllLines(fileName);
	}

	public void AddMessage(ScriptMessage message)
	{
		var disabled = _ignoredMessagesHistory.FirstOrDefault(pair => pair.FromLine < message.Position.Line);
		if (disabled.MessageIDs != null && disabled.MessageIDs.Contains(message.MessageID))
			return;
        _processingMessages.Add(message);
    }

	public void PushListOfIgnoredMessages(MessageID[] ignored, int fromLine)
	{
		if (_ignoredMessagesHistory.Count > 0)
		{
			if (fromLine < _ignoredMessagesHistory.Peek().FromLine)
				throw new ArgumentException("New ignored messages entry is before the last entry.");
        }
        _ignoredMessagesHistory.Push(new IgnoredMessagesEntry(ignored, fromLine));
	}
}
#nullable restore
