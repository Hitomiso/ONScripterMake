using System;
using System.Collections.Generic;
using ONSMake.Lexer;

namespace ONSMake.Parser;

#nullable enable
public abstract class ParserStateHandler
{
    /// <summary>
    /// Готовность состояния к получению и обработке токенов.
    /// Когда false, перед обработкой токенов надо обновить внутренние поля состояния и установить IsReady в true.
    /// </summary>
    public bool IsReady { get; protected set; } = false;
    public ParserContext Context
    {
        get => _context;
        set {
            if (value == null)
                throw new NullReferenceException("Can not set Context property to null.");
            _context = value;
        }
    }
    
    protected delegate void _tokenHandlerDelegate(Token token, out bool success);
    protected Dictionary<TokenType, _tokenHandlerDelegate> _tokenHandlers = [];
    protected ParserContext _context;
    
    public ParserStateHandler(ParserContext context)
    {
        _context = context;
    }

    public virtual void TryHandleToken(Token token, out bool success)
    {
        if (!IsReady)
            throw new ApplicationException("State is not marked as ready.");
        if (_tokenHandlers.TryGetValue(token.Type, out _tokenHandlerDelegate? handler))
            handler(token, out success);
        else
            success = false;
    }

    /// <summary>
    /// Запрещает использование состояния с устаревшими внутренними данными.
    /// В переопределениях установить IsReady в false.
    /// Кроме Invalidate сделайте метод для установки внутренних полей и IsReady в true, вместо передачи их значений через Конструктор.
    /// Рекомендуется имя Recycle.
    /// </summary>
    public virtual void Invalidate()
    {
        IsReady = false;
    }

    /// <summary>
    /// Вызывается, когда состояние выше по стеку завершилось и вернуло обработанное значение.
    /// </summary>
    /// <param name="returnValue">Возвращённое состоянием выше значение.</param>
    public abstract void OnReturn(Token? returnValue);

    /// <summary>
    /// Вызывается каскадно по всем состояниям в стеке, когда нужно очистить стек и установить новое начальное состояние.
    /// В этом методе нужно финализировать обрабатываемый токен, если он есть, и вернуть его.
    /// Никогда не пытайтесь заново сбросить стек состояний из этого метода.
    /// </summary>
    /// <param name="returnValue">Возвращённое состоянием выше значение.</param>
    /// <returns>Финализированный токен для состояния ниже по стеку.</returns>
    public abstract Token? OnReset(Token? returnValue);
}
#nullable restore
