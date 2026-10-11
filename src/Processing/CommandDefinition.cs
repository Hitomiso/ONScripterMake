namespace ONSMake.Processing;

public record class CommandDefinition
{
	public readonly string Name;
	public readonly ScriptUnit FileUnit;
	public readonly TextPosition DefinedAt;
	public DataType[] ParameterTypes;
	
	public CommandDefinition(string name, ScriptUnit fileUnit, TextPosition definedAt, DataType[] parameterTypes)
	{
		Name = name;
		FileUnit = fileUnit;
		DefinedAt = definedAt;
		ParameterTypes = parameterTypes;
	}
}
