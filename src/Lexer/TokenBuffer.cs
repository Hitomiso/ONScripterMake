using System;
using System.Buffers;

namespace ONSMake.Lexer;

public struct TokenBuffer : IDisposable
{
    private const int REALLOC_ADDITIONAL_SIZE = 16;

    public int Count { get; private set; } = 0;
    public Token this[int index]
    {
        get
        {
            if (index < 0 || index >= Count)
                throw new IndexOutOfRangeException();
            return _buffer[index];
        }
    }

    private Token[] _buffer;
    
    public TokenBuffer() =>
        _buffer = ArrayPool<Token>.Shared.Rent(REALLOC_ADDITIONAL_SIZE);
    public TokenBuffer(int allocatedSize) =>
        _buffer = ArrayPool<Token>.Shared.Rent(allocatedSize);

    public void Add(Token token)
    {
        if (Count >= _buffer.Length)
        {
            Token[] newBuffer = ArrayPool<Token>.Shared.Rent(_buffer.Length + REALLOC_ADDITIONAL_SIZE);
            Array.Copy(_buffer, newBuffer, Count);
            ArrayPool<Token>.Shared.Return(_buffer);
            _buffer = newBuffer;
        }
        _buffer[Count++] = token;
    }

    public void Clear() =>
        Count = 0;

    public Token[] ToArray()
    {
        Token[] array = new Token[Count];
        Array.Copy(_buffer, array, Count);
        return array;
    }

    public void Dispose() =>
        ArrayPool<Token>.Shared.Return(_buffer);
}
