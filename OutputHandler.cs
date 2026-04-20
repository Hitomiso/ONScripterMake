using System;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake;

public static class OutputHandler
{
    private const string PADDING_STRING = "    ";

    public static bool IsSilenced;
    public static string DefaultPrefix;

    public static uint Errors;
    public static uint Warnings;
    public static uint Infos;

    public static void PrintError(string message, string prefix = null)
    {
        Errors++;
        if (IsSilenced)
            return;

        PrintMessage(prefix, "error: ", ConsoleColor.Red, message);
    }

    public static void PrintInfo(string message, string prefix = null)
    {
        Infos++;
        if (IsSilenced)
            return;

        PrintMessage(prefix, "info: ", ConsoleColor.Green, message);
    }

    public static void PrintPreprocessException(PreprocessException ex)
    {
        Errors++;
        if (IsSilenced)
            return;

        string trimmed = ex.Line.Content.Replace("\t", " ").Trim();
        int underlineStart = ex.ErrorStartColumn - (ex.Line.Content.Length - trimmed.Length);
	int lineRepeatCount = ex.Line.Column - ex.ErrorStartColumn;

        PrintMessage(ex.Line.ToFileReference(), "error: ", ConsoleColor.Red, ex.Message);
        Console.WriteLine(PADDING_STRING + trimmed);
        StringBuilder underline = new();
	if (underlineStart >= 0)
	        underline.Append(' ', underlineStart);
        underline.Append('^');
	if (lineRepeatCount >= 0)
	        underline.Append('~', lineRepeatCount);
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine(PADDING_STRING + underline);
        Console.ResetColor();
    }

    private static void PrintMessage(string prefix, string type, ConsoleColor typeColor, string message)
    {
        prefix = prefix ?? DefaultPrefix;
        Console.Write(PADDING_STRING);
        if (!string.IsNullOrWhiteSpace(prefix))
            Console.Write(prefix + " ");
        Console.ForegroundColor = typeColor;
        Console.Write(type);
        Console.ResetColor();
        Console.WriteLine(message);
    }
}
