using System;
using System.Text;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake;

public class Line
{
    public int Length { get => Content.Length; }

    public string Content
    {
        get => _content;
        set
        {
            _content = value;
            _column = 0;
        }
    }
    public string Filename { get; private set; }
    public int Row { get; private set; }
    public int Column
    {
        get => _column;
        set
        {
            if (value >= Length)
            {
                throw new IndexOutOfRangeException("Col must be less than content's length.");
            }
            _column = value;
        }
    }
    public bool ReachedEnd
    {
        get => _column >= Length;
    }

    private string _content;
    private int _column;

    public Line(string content, string filename, int row, int col = 0)
    {
        _content = content;
        Filename = filename;
        Row = row;
        _column = col;
    }

    public Line(Line line)
    {
        _content = line.Content;
        Filename = line.Filename;
        Row = line.Row;
        _column = line.Column;
    }

    public char PeekChar()
    {
        return Content[Column];
    }
    public char? ReadChar()
    {
        if (ReachedEnd)
            return null;
        return Content[_column++];
    }

    public void SkipPadding()
    {
        while (!ReachedEnd)
        {
            if (!char.IsWhiteSpace(PeekChar()))
                return;
            _column++;
        }
    }

    public string ReadName()
    {
        if (ReachedEnd)
            return "";
        int startCol = Column;
        if (!char.IsAsciiLetter(PeekChar()) && PeekChar() != '_')
            throw new PreprocessException(this, startCol, MessageID.ERR_INVALID_TOKEN_NAME);

        StringBuilder sb = new();
        while (!ReachedEnd)
        {
            if (char.IsAsciiLetterOrDigit(PeekChar()) || PeekChar() == '_')
                sb.Append(ReadChar());
            else
                break;
        }
        // @TODO: проверить как движок реагирует на регистр букв
        return sb.ToString().ToLower();
    }

    public string ReadLabel()
    {
        int startCol = Column;
        if (ReachedEnd)
            throw new PreprocessException(this, startCol, MessageID.ERR_UNEXPECTED_EOL);
        if (ReadChar() != '*')
            throw new PreprocessException(this, startCol, MessageID.ERR_NOT_A_LABEL);

        string name = ReadName();
        if (name.Length == 0)
            throw new PreprocessException(this, startCol, MessageID.ERR_LABEL_WITHOUT_NAME);

        return "*" + name;
    }
	
	public string ReadNumber()
	{
		int startCol = Column;
		if (ReachedEnd)
			throw new PreprocessException(this, startCol, MessageID.ERR_UNEXPECTED_EOL);
		if (!char.IsAsciiDigit(PeekChar()))
			throw new PreprocessException(this, startCol, MessageID.ERR_NOT_A_NUMBER);
		
        StringBuilder sb = new();
        while (!ReachedEnd)
        {
			if (!char.IsAsciiDigit(PeekChar()))
				break;
			sb.Append(ReadChar());
        }
        return sb.ToString();
	}
	
	public string ReadColor()
	{
		int startCol = Column;
		if (ReachedEnd)
			throw new PreprocessException(this, startCol, MessageID.ERR_UNEXPECTED_EOL);
		if (PeekChar() == '#')
			ReadChar();
		string color = ReadHexNumber();
		if (color.Length != 6)
			throw new PreprocessException(this, startCol, MessageID.ERR_INVALID_COLOR);
		return "#" + color;
	}
	
	public string ReadHexNumber()
	{
		if (ReachedEnd)
			throw new PreprocessException(this, Column, MessageID.ERR_UNEXPECTED_EOL);
		
		StringBuilder sb = new();
        while (!ReachedEnd)
        {
			if (!char.IsAsciiHexDigit(PeekChar()))
				break;
			sb.Append(ReadChar());
        }
        return sb.ToString();
	}
	
	public string ReadString()
	{
		int startCol = Column;
		if (ReachedEnd)
			throw new PreprocessException(this, startCol, MessageID.ERR_UNEXPECTED_EOL);
		if (PeekChar() != '"')
			throw new PreprocessException(this, startCol, MessageID.ERR_NOT_A_STRING);
		ReadChar();
		
		StringBuilder sb = new();
        while (!ReachedEnd)
        {
            char c = (char)ReadChar();
            if (c == '"')
				return sb.ToString();
            sb.Append(c);
        }
        throw new PreprocessException(this, startCol, MessageID.ERR_STRING_NOT_CLOSED);
	}

    public string ReadParamsString()
    {
        StringBuilder sb = new();
        int startCol = Column;
        bool insideString = false;

        while (!ReachedEnd)
        {
            char c = PeekChar();
            if (c == '\"')
                insideString = !insideString;
            if (!insideString && (c == ';' || c == ':'))
                break;
            sb.Append(ReadChar());
        }

        if (insideString)
            throw new PreprocessException(this, startCol, MessageID.ERR_STRING_NOT_CLOSED);
        return sb.ToString();
    }

    public string ReadRest()
    {
        if (ReachedEnd)
            return "";
        string rest = _content[_column..];
        _column = Length;
        return rest;
    }

    public string ToFileReference()
    {
        return $"{Filename}:{Row + 1}:{Column + 1}";
    }
}
