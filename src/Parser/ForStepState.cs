using System;
using System.Linq;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

public class ForStepState : NormalParametersState
{
    public ForStepState(OnsParser parser) : base(parser) { }
}
