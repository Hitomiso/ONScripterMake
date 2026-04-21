namespace Hitomiso.ONScripterMake;

public struct TextPosition
{
	public int Line;
	public int Column;
	
	public TextPosition(int line, int column)
	{
		Line = line;
		Column = column;
	}
}