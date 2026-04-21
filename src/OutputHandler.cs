using System;
using System.Text;

namespace Hitomiso.ONScripterMake;

public static class OutputHandler
{
    private const string PADDING_STRING = "    ";

    public static bool IsSilenced { get; set; }

    public static uint Errors { get; private set; }
    public static uint Warnings { get; private set; }
    public static uint Infos { get; private set; }

    private static readonly object _printLock = new();

    public static void PrintError(string message, string prefix = null)
    {
        Errors++;
        if (IsSilenced)
            return;

        lock (_printLock)
        {
            PrintMessage(prefix, "error: ", ConsoleColor.Red, message);
        }
    }

    public static void PrintInfo(string message, string prefix = null)
    {
        Infos++;
        if (IsSilenced)
            return;

        lock (_printLock)
        {
            PrintMessage(prefix, "info: ", ConsoleColor.Green, message);
        }
    }

    public static void PrintMessage(SourceCodeMessage message)
    {
		if (IsSilenced)
            return;
		
        string typeName;
        ConsoleColor typeColor;
        switch (message.Type)
        {
            case SourceCodeMessage.MessageType.Info:
                typeName = "info: ";
                typeColor = ConsoleColor.Green;
                Infos++;
                break;
            case SourceCodeMessage.MessageType.Warning:
                typeName = "warning: ";
                typeColor = ConsoleColor.Yellow;
                Warnings++;
                break;
            case SourceCodeMessage.MessageType.Error:
                typeName = "error: ";
                typeColor = ConsoleColor.Red;
                Errors++;
                break;
            default:
                throw new ArgumentException($"Unexpected message type '{message.Type}'.");
        }
        string messageText = MessageTranslator.GetArgumentedString(message.MessageID, message.MessageArgs);
        string trimmed = message.SourceLine.Replace("\t", " ").TrimStart();
        int underlineStart = message.Position.Column - (message.SourceLine.Length - trimmed.Length);
        trimmed = trimmed.TrimEnd();
        StringBuilder underline = new();
        if (underlineStart > 0)
            underline.Append(' ', underlineStart);
        underline.Append('^');
        if (message.Length > 0)
            underline.Append('~', message.Length - 1);

        lock (_printLock)
        {
            PrintMessage($"{message.FileName}:{message.Position.Line + 1}:{message.Position.Column + 1}", typeName, typeColor, messageText);
            Console.WriteLine(PADDING_STRING + trimmed);
            Console.ForegroundColor = typeColor;
            Console.WriteLine(PADDING_STRING + underline);
            Console.ResetColor();
        }
    }

    private static void PrintMessage(string prefix, string type, ConsoleColor typeColor, string message)
    {
		if (IsSilenced)
            return;
		
        Console.Write(PADDING_STRING);
        if (!string.IsNullOrWhiteSpace(prefix))
            Console.Write(prefix + " ");
        Console.ForegroundColor = typeColor;
        Console.Write(type);
        Console.ResetColor();
        Console.WriteLine(message);
    }
}
