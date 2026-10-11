using System;

namespace ONSMake.Linter;

public ref struct FormatTagCheckState
{
    public enum State
    {
        Name,
        Parameter,
        Text
    }

    public readonly bool IsReadingTagName => ReadingState == State.Name;
    public readonly bool IsReadingTagParameter => ReadingState == State.Parameter;
    public readonly bool IsReadingText => ReadingState == State.Text;

    public int Depth = 0;
    public Span<int> TagStartColumnsBuffer;

    public State ReadingState = State.Text;
    public bool AreQuotesOpen = false;
    public int LastQuotesColumn = -1;
    public int SpacesCount = 0;
    public int TriggerSpacesWarningAtColumn = -1;

    public Span<char> TagBuffer;
    public int TagLength = 0;
    public int TagStartColumn { get; private set; }

    public Func<ReadOnlySpan<char>, bool>? TagParameterChecker = null;

    public FormatTagCheckState(Span<char> tagBuffer, Span<int> columnsBuffer)
    {
        TagBuffer = tagBuffer;
        TagStartColumnsBuffer = columnsBuffer;
    }

    public void StartReadingTagName(int tagStartColumn)
    {
        ReadingState = State.Name;
        TagLength = 0;
        TagStartColumn = tagStartColumn;
    }

    public void StartReadingTagParameter(Func<ReadOnlySpan<char>, bool> parameterChecker, int parameterStartColumn)
    {
        ReadingState = State.Parameter;
        TagParameterChecker = parameterChecker ?? throw new ArgumentNullException("Parameter checker is null.");
        TagLength = 0;
        TagStartColumn = parameterStartColumn + 1;
    }

    public void StartReadingText()
    {
        ReadingState = State.Text;
        TagLength = 0;
    }
}
