using ONSMake;
using System;
using System.Collections.Generic;
using System.IO;

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
	public readonly List<ScriptMessage> ProcessingMessages = [];
	
	public readonly string[] InputLines;
	public readonly List<ProcessedLine> OutputLines = [];
	
	public ScriptUnit(string fileName)
	{
	    FileName = fileName;
	    InputLines = File.ReadAllLines(fileName);
	}
}
#nullable restore
