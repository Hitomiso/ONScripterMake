namespace Hitomiso.ONScripterMake.Parsing;

public enum TokenType
{
	Raw,
	Name,
	NumVar,
	StrVar,
	Array,
	NumConst,
	StrConst,
	Label,
	Operator,
	ConditionOperator,
	OpenBracket,
	CloseBracket,
	OpenSquareBracket,
	CloseSquareBracket,
	Color,
	Colon, // Двоеточие
	Comma, // Запятая
	JumpPoint,
	Comment,
	// Сложные типы
	Command,
	Dialog,
	Directive,
}