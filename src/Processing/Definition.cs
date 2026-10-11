namespace ONSMake.Processing;

public readonly record struct Definition
{
	public readonly ScriptUnit FileUnit;
	public readonly TextPosition DefinedAt;
	
	public Definition(ScriptUnit fileUnit, TextPosition definedAt)
	{
		FileUnit = fileUnit;
		DefinedAt = definedAt;
	}
}
