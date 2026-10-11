namespace ONSMake.Parser;

public enum OnsParserStateType
{
	StartOfLine,
	Command,
	Variable,
	Array,
	Label,
	InnerExpression,
	NormalParameter,
	IfParameter,
	ForCounter,
	ForFrom,
	ForTo,
	ForStep,
	DialogParameter,
	InlineCommand,
	InlineCommandParameter
}
