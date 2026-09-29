using System;
using System.Linq;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

public class ForStepState : NormalParametersState
{
    public ForStepState(ParserContext context) : base(context) { }
}
