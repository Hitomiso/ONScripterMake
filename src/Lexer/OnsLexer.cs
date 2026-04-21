using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Text;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Hitomiso.ONScripterMake.Lexer;

#nullable enable
public static class OnsLexer
{
    /*
    private static (string regex, TokenType type)[] ONSRU_TOKENS =
    [
        ("`.*?`", TokenType.Dialog),
        ("\".*?\"", TokenType.String),
        (";.*", TokenType.Comment),
        ("~", TokenType.JumpPoint),
        (@"i%", TokenType.IndexVariable),
        (@"[A-Za-z_]\w*", TokenType.Identifier),
        (@"\d+", TokenType.Number),
        (@"\$|\%|\?", TokenType.Variable),
        (@"\!w", TokenType.InlineWaitCommand),
        (@"\!d", TokenType.InlineDelayCommand),
        ("#[A-Fa-f0-9]{6}", TokenType.Color),
        (@"\[\@\]", TokenType.DialogClickWait),
        (@"\[\\\]", TokenType.DialogPageWait),
        (@"\[\#\]", TokenType.DialogContinueScript),
        (@"\[\*\]", TokenType.DialogSuspend),
        (@"\[\|\]", TokenType.DialogVoiceWait),
        (@"#\w+", TokenType.Directive),
        (@"\(", TokenType.Left),
        (@"\)", TokenType.Right),
        (@"\[", TokenType.SLeft),
        (@"\]", TokenType.SRight),
        (@"\,", TokenType.Comma),
        (":", TokenType.Colon),
        (@"\+", TokenType.Add),
        (@"\-", TokenType.Subtract),
        (@"\*", TokenType.Multiply),
        (@"\/", TokenType.Divide),
        ("<=", TokenType.LessOrEqual),
        (">=", TokenType.GreaterOrEqual),
        ("==?", TokenType.Equal),
        (@"<>|\!=", TokenType.NotEqual),
        ("<", TokenType.Less),
        (">", TokenType.Greater),
        (@"\|\|?", TokenType.Or),
        ("&&?", TokenType.And),
    ];
    */

    public static List<Token> TokenizeLine(string line)
    {
        List<Token> tokens = [];
        int length = line.Length;
        int col = 0;

        while (col < length)
        {
            // Пропуск пробельных символов
            while (col < length && char.IsWhiteSpace(line[col]))
                col++;
            if (col >= length)
                break;

            char c = line[col];
            Token? token = null;

            // Диалог: `...`
            if (c == '`')
            {
                int end = line.IndexOf('`', col + 1);
                if (end >= 0)
                {
                    token = new Token(TokenType.Dialog, line.Substring(col, end - col + 1), col);
                    col = end + 1;
                    tokens.Add(token);
                    continue;
                }
            }
            
            // Строка: "..."
            if (c == '"')
            {
                int end = line.IndexOf('"', col + 1);
                if (end >= 0)
                {
                    token = new Token(TokenType.String, line.Substring(col, end - col + 1), col);
                    col = end + 1;
                    tokens.Add(token);
                    continue;
                }
            }

            // Комментарий: ; до конца строки
            if (c == ';')
            {
                token = new Token(TokenType.Comment, line.Substring(col), col);
                tokens.Add(token);
                break; // Выход из цикла, так как вся оставшаяся строка — комментарий
            }

            // Точка перехода: ~
            if (c == '~')
            {
                token = new Token(TokenType.JumpPoint, "~", col);
                col++;
                tokens.Add(token);
                continue;
            }

            // Индексная переменная: i%
            if (c == 'i' && col + 1 < length && line[col + 1] == '%')
            {
                token = new Token(TokenType.IndexVariable, "i%", col);
                col += 2;
                tokens.Add(token);
                continue;
            }

            // Идентификатор: [A-Za-z_][\w]*
            if ((c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z') || c == '_')
            {
                int end = col + 1;
                while (end < length && (char.IsLetterOrDigit(line[end]) || line[end] == '_'))
                    end++;
                string content = line.Substring(col, end - col).ToLower();
                token = new Token(TokenType.Identifier, content, col);
                col = end;
                tokens.Add(token);
                continue;
            }

            // Число: \d+
            if (c >= '0' && c <= '9')
            {
                int end = col + 1;
                while (end < length && line[end] >= '0' && line[end] <= '9')
                    end++;
                string content = line.Substring(col, end - col);
                token = new Token(TokenType.Number, content, col);
                col = end;
                tokens.Add(token);
                continue;
            }

            // Переменная: $ % ?
            if (c == '$' || c == '%' || c == '?')
            {
                token = new Token(TokenType.Variable, line[col].ToString(), col);
                col++;
                tokens.Add(token);
                continue;
            }
            
            // Команды: !w, !d и оператор !=
            if (c == '!')
            {
                if (col + 1 < length)
                {
                    char next = line[col + 1];
                    if (next == 'w')
                    {
                        token = new Token(TokenType.InlineWaitCommand, "!w", col);
                        col += 2;
                        tokens.Add(token);
                        continue;
                    }
                    if (next == 'd')
                    {
                        token = new Token(TokenType.InlineDelayCommand, "!d", col);
                        col += 2;
                        tokens.Add(token);
                        continue;
                    }
                    if (next == '=')
                    {
                        token = new Token(TokenType.NotEqual, "!=", col);
                        col += 2;
                        tokens.Add(token);
                        continue;
                    }
                }
                // Одиночный ! без пары – нет токена, уходим ниже к ошибке
            }

            // Цвет: #[A-Fa-f0-9]{6} или директива: #\w+
            if (c == '#')
            {
                // Проверяем на цвет
                if (col + 6 < length)
                {
                    bool isHex = true;
                    for (int i = 1; i <= 6; i++)
                    {
                        char h = line[col + i];
                        if (!((h >= '0' && h <= '9') || (h >= 'A' && h <= 'F') || (h >= 'a' && h <= 'f')))
                        {
                            isHex = false;
                            break;
                        }
                    }
                    if (isHex)
                    {
                        token = new Token(TokenType.Color, line.Substring(col, 7), col);
                        col += 7;
                        tokens.Add(token);
                        continue;
                    }
                }

                // Если не цвет, пробуем директиву
                if (col + 1 < length && line[col + 1] == '*')
                {
                    token = new Token(TokenType.Directive, "#incremental_label", col);
                    col++;
                    tokens.Add(token);
                    continue;
                }
                int dirEnd = col + 1;
                while (dirEnd < length && (char.IsLetterOrDigit(line[dirEnd]) || line[dirEnd] == '_'))
                    dirEnd++;
                if (dirEnd > col + 1)
                {
                    token = new Token(TokenType.Directive, line.Substring(col, dirEnd - col), col);
                    col = dirEnd;
                    tokens.Add(token);
                    continue;
                }
                // Одиночная # без продолжения – нет токена
            }

            // Управляющие токены диалога: [@], [\], [#], [*], [|]
            if (c == '[')
            {
                if (col + 2 < length)
                {
                    string seq = line.Substring(col, 3);
                    TokenType? type = seq switch
                    {
                        "[@]" => TokenType.DialogClickWait,
                        "[\\]" => TokenType.DialogPageWait,
                        "[#]" => TokenType.DialogContinueScript,
                        "[*]" => TokenType.DialogSuspend,
                        "[|]" => TokenType.DialogVoiceWait,
                        _ => null
                    };
                    if (type.HasValue)
                    {
                        token = new Token(type.Value, seq, col);
                        col += 3;
                        tokens.Add(token);
                        continue;
                    }
                }
                // Если не спец-токен, то одиночная левая квадратная скобка
                token = new Token(TokenType.SLeft, "[", col);
                col++;
                tokens.Add(token);
                continue;
            }

            // Одиночные символы и составные операторы
            switch (c)
            {
                case '(': token = new Token(TokenType.Left, "(", col); col++; break;
                case ')': token = new Token(TokenType.Right, ")", col); col++; break;
                case ']': token = new Token(TokenType.SRight, "]", col); col++; break;
                case ',': token = new Token(TokenType.Comma, ",", col); col++; break;
                case ':': token = new Token(TokenType.Colon, ":", col); col++; break;
                case '+': token = new Token(TokenType.Add, "+", col); col++; break;
                case '-': token = new Token(TokenType.Subtract, "-", col); col++; break;
                case '*': token = new Token(TokenType.Multiply, "*", col); col++; break;
                case '/': token = new Token(TokenType.Divide, "/", col); col++; break;
                case '<':
                    if (col + 1 < length)
                    {
                        if (line[col + 1] == '=')
                        { token = new Token(TokenType.LessOrEqual, "<=", col); col += 2; break; }
                        else if (line[col + 1] == '>')
                        { token = new Token(TokenType.NotEqual, "<>", col); col += 2; break; }
                    }
                    token = new Token(TokenType.Less, "<", col); col++; break;
                case '>':
                    if (col + 1 < length && line[col + 1] == '=')
                    { token = new Token(TokenType.GreaterOrEqual, ">=", col); col += 2; break; }
                    token = new Token(TokenType.Greater, ">", col); col++; break;
                case '=':
                    if (col + 1 < length && line[col + 1] == '=')
                    { token = new Token(TokenType.Equal, "==", col); col += 2; }
                    else
                    { token = new Token(TokenType.Equal, "=", col); col++; }
                    break;
                case '|':
                    if (col + 1 < length && line[col + 1] == '|')
                    { token = new Token(TokenType.Or, "||", col); col += 2; }
                    else
                    { token = new Token(TokenType.Or, "|", col); col++; }
                    break;
                case '&':
                    if (col + 1 < length && line[col + 1] == '&')
                    { token = new Token(TokenType.And, "&&", col); col += 2; }
                    else
                    { token = new Token(TokenType.And, "&", col); col++; }
                    break;
            }

            if (token == null)
                throw new InvalidTokenException(line, col);
            tokens.Add(token);
        }

        return tokens;
    }
}
#nullable restore
