using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ONSMake.Parser;

#nullable enable
public sealed class OnsParserStatePool
{
    private static readonly Dictionary<Type, OnsParserStateType> STATE_TYPE_CLASSES = new()
    {
        { typeof(StartOfLineState), OnsParserStateType.StartOfLine },
        { typeof(CommandState), OnsParserStateType.Command },
        { typeof(VariableState), OnsParserStateType.Variable },
        { typeof(ArrayState), OnsParserStateType.Array },
        { typeof(LabelState), OnsParserStateType.Label },
        { typeof(InnerExpressionState), OnsParserStateType.InnerExpression },
        { typeof(NormalParametersState), OnsParserStateType.NormalParameter },
        { typeof(IfParametersState), OnsParserStateType.IfParameter },
        { typeof(ForCounterState), OnsParserStateType.ForCounter },
        { typeof(ForFromState), OnsParserStateType.ForFrom },
        { typeof(ForToState), OnsParserStateType.ForTo },
        { typeof(ForStepState), OnsParserStateType.ForStep },
        { typeof(DialogParameterState), OnsParserStateType.DialogParameter },
        { typeof(InlineCommandState), OnsParserStateType.InlineCommand },
        { typeof(InlineCommandParameterState), OnsParserStateType.InlineCommandParameter },
    };

    private readonly ConcurrentDictionary<OnsParserStateType, ConcurrentBag<ParserStateHandler>> _stateBuckets = [];

    public ParserStateHandler GetStateByType(ParserContext context, OnsParserStateType stateType)
    {
        ParserStateHandler handler;
		if (!GetBucketByType(stateType).TryTake(out handler))
			handler = CreateStateHandler(context, stateType);
		handler.Context = context;
        return handler;
    }

    public void StoreUsedState(ParserStateHandler stateHandler)
    {
        if (!STATE_TYPE_CLASSES.TryGetValue(stateHandler.GetType(), out OnsParserStateType stateType))
            throw new ApplicationException($"Unregistered state type for handler '{stateHandler.GetType().FullName}'.");

        ConcurrentBag<ParserStateHandler> bucket = GetBucketByType(stateType);
        stateHandler.Invalidate();
        bucket.Add(stateHandler);
    }

    private ConcurrentBag<ParserStateHandler> GetBucketByType(OnsParserStateType stateType) =>
		_stateBuckets.GetOrAdd(stateType, _ => new ConcurrentBag<ParserStateHandler>());

    private ParserStateHandler CreateStateHandler(ParserContext context, OnsParserStateType stateType)
    {
        foreach (var pair in STATE_TYPE_CLASSES)
        {
            if (pair.Value == stateType)
            {
                ParserStateHandler? handler = (ParserStateHandler?)Activator.CreateInstance(pair.Key, context);
                if (handler == null)
                    throw new NullReferenceException();
                return handler;
            }
        }
        throw new ApplicationException($"Unregistered state handler class for type '{stateType}'.");
    }
}
#nullable restore
