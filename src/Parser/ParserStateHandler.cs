using System;
using System.Collections.Generic;
using Hitomiso.ONScripterMake.Lexer;

namespace Hitomiso.ONScripterMake.Parser;

#nullable enable
public abstract class ParserStateHandler
{
    /// <summary>
    /// √отовность состо€ни€ к получению и обработке токенов.
    ///  огда false, перед обработкой токенов надо обновить внутренние пол€ состо€ни€ и установить IsReady в true.
    /// </summary>
    public bool IsReady { get; protected set; } = false;
    protected OnsParser _parser;
    protected Dictionary<TokenType, Action<Token>> _tokenHandlers = new();

    public ParserStateHandler(OnsParser parser)
    {
        _parser = parser;
    }

    // @TODO: ѕревратить в bool TryHandleToken
    public virtual void HandleToken(Token token)
    {
        if (!IsReady)
            throw new ApplicationException("State is not marked as ready.");
        if (!_tokenHandlers.ContainsKey(token.Type))
            throw new UnexpectedTokenException(token);
        _tokenHandlers[token.Type](token);
    }

    /// <summary>
    /// «апрещает использование состо€ни€ с устаревшими внутренними данными.
    /// ¬ переопределени€х установите IsReady в false.
    ///  роме Invalidate сделайте метод дл€ установки внутренних полей и IsReady в true, вместо передачи их значений через конструктор. –екомендуетс€ им€ Recycle.
    /// </summary>
    public virtual void Invalidate() { IsReady = false; }

    /// <summary>
    /// ¬ызываетс€, когда состо€ние выше по стеку завершилось и вернуло обработанное значение.
    /// </summary>
    /// <param name="returnValue">¬озвращЄнное состо€нием выше значение.</param>
    public abstract void OnReturn(Token? returnValue);

    /// <summary>
    /// ¬ызываетс€ каскадно по всем состо€ни€м в стеке, когда нужно очистить стек и установить новое начальное состо€ние.
    /// ¬ этом методе нужно финализировать обрабатываемый токен, если он есть, и вернуть его.
    /// Ќикогда не пытайтесь заново сбросить стек состо€ний из этого метода.
    /// </summary>
    /// <param name="returnValue">¬озвращЄнное состо€нием выше значение.</param>
    /// <returns>‘инализированный токен дл€ состо€ни€ ниже по стеку.</returns>
    public abstract Token? OnReset(Token? returnValue);
}
#nullable restore