using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Hitomiso.ONScripterMake.Processing;

namespace Hitomiso.ONScripterMake;

public static class Program
{
    private const string VERSION_STRING = "ONSMake 2.0";
    private const string DEFAULT_LIST_FILE = "ONSMakeList.txt";
    private const string DEFAULT_CONFIG_FILE = "ONSMakeConfig.json";
	
	private enum ExitCodes
	{
		Help = 0,
		ArgumentsError,
		ConfigError,
		ListError,
		ScriptError,
		FileExists,
		FileNotFound,
		IOException,
	}

    private static ProjectConfiguration _config = new();
	
    public static void Main(string[] args)
    {
        if (!ParseArguments(args))
			Environment.Exit((int)ExitCodes.ArgumentsError);
        if (_config.PrintHelp)
        {
            PrintHelp();
			Environment.Exit((int)ExitCodes.Help);
        }

        try
        {
            if (_config.WorkingDirectory != null)
                Environment.CurrentDirectory = _config.WorkingDirectory;
	
			// Читаем и проверяем на ошибки конфиг
            if (_config.ConfigFile != null && !File.Exists(_config.ConfigFile))
            {
                OutputHandler.PrintError($"Config file '{Path.GetFullPath(_config.ConfigFile)}' not found.");
				Environment.Exit((int)ExitCodes.FileNotFound);
            }
            string configFileName = _config.ConfigFile ?? DEFAULT_CONFIG_FILE;
            if (File.Exists(configFileName))
            {
				uint errorsBeforeReadingConfig = OutputHandler.Errors;
                _config = new ProjectConfiguration(configFileName);
				if (OutputHandler.Errors != errorsBeforeReadingConfig)
					Environment.Exit((int)ExitCodes.ConfigError);
                // Репарсим аргументы, чтобы они имели приоритет над конфигом
                if (!ParseArguments(args))
					Environment.Exit((int)ExitCodes.ArgumentsError);
            }

            if (!CheckOutputFileOverwrite())
				Environment.Exit((int)ExitCodes.FileExists);
			
            bool listIsCorrect = TryFormInputList(out List<string> filesToProcess);
            if (!listIsCorrect && !_config.IgnoreErrors)
				Environment.Exit((int)ExitCodes.ListError);

			// Препроцессинг и проверка на ошибки в скрипте
			OutputHandler.Errors = 0;
            var preproc = new ScriptProcessor(_config);
            List<string> outputContent = [];
            foreach (string file in filesToProcess)
            {
                string[] outLines;
                if (_config.Raw)
                    outLines = File.ReadAllLines(file);
                else
                    outLines = preproc.ProcessFile(file);
                outputContent.AddRange(outLines);
            }
			if (!_config.Raw)
				// preproc.CheckMemorizedCommandCalls();
				CommandCallChecker.

            // Вывод ошибки сборки, если есть
            if (OutputHandler.Errors > 0)
            {
                if (!_config.Silent)
                    Console.WriteLine($"\nBuild FAILED with {OutputHandler.Errors} errors.");
                if (_config.IgnoreErrors)
                {
                    if (!_config.Silent)
                        Console.WriteLine("Errors ignored, writing output...");
                }
                else
					Environment.Exit((int)ExitCodes.ScriptError);
            }

            StreamWriter sw = new(_config.OutputFile);
            foreach (string line in outputContent)
                sw.WriteLine(line);
            sw.Close();
        }
        catch (IOException ex)
        {
            OutputHandler.PrintError(ex.Message, "IOException");
			Environment.Exit((int)ExitCodes.IOException);
        }
    }

    public static bool ParseArguments(string[] args)
    {
        if (args == null)
            return true;
        uint errorsBeforeParsing = OutputHandler.Errors;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "-l":
                case "--list":
                    _config.ListFile = args.ElementAtOrDefault(++i);
                    if (_config.ListFile == null)
                        OutputHandler.PrintError("List option has no value. Specify list file's path.");
                    break;
                case "-i":
                case "--input":
                    string file = args.ElementAtOrDefault(++i);
                    if (file == null)
                        OutputHandler.PrintError("Input option has no value. Specify input file's path.");
                    else
                        _config.InputFiles.Add(file);
                    break;
                case "-c":
                case "--config":
                    _config.ConfigFile = args.ElementAtOrDefault(++i);
                    if (_config.ConfigFile == null)
                        OutputHandler.PrintError("Config option has no value. Specify config file's path.");
                    break;
                case "-o":
                case "--output":
                    _config.OutputFile = args.ElementAtOrDefault(++i);
                    if (_config.OutputFile == null)
                        OutputHandler.PrintError("Output option has no value. Specify output file's path.");
                    break;
                case "-w":
                case "--work-dir":
                    _config.WorkingDirectory = args.ElementAtOrDefault(++i);
                    if (_config.WorkingDirectory == null)
                        OutputHandler.PrintError("Working directory option has no value. Specify working directory's path.");
                    break;
                default:
                    _config.TryApplySimpleOption(args[i]);
                    break;
            }
        }
        if (_config.Silent && _config.OverwriteOutputFile == null)
            OutputHandler.PrintError("-y or -n must be specified in silent mode.");

        return OutputHandler.Errors == errorsBeforeParsing;
    }

    public static void PrintHelp()
    {
        string helpString =
            $"{VERSION_STRING}\n" +
            "Script \"build system\" for ONScripter-RU\n" +
            "Freeware. Licensed with GNU GPL v2\n" +
            "by Hitomiso, all lefts reserved.\n" +
            "\n" +
            "Usage:\n" +
            "\tONSMake [options...] -o <output file>\n" +
            "\n" +
            "Options:\n" +
            "-?\n" +
            "--help\n" +
            "\tPrint this message.\n" +
            "-o <output file>\n" +
            "--output <output file>\n" +
            "\tSpecify output file name. Must be set in CLI options or config file.\n" +
            "-c <config file>\n" +
            "--config <config file>\n" +
            "\tSpecify JSON config file.\n" +
            "\tDefault value: ONSMakeConfig.json\n" +
            "\tIf config file not found, then all options must be set in CLI.\n" +
            "-l <list file>\n" +
            "--list <list file>\n" +
            "\tSpecify file with list of included into output script files.\n" +
            "\tDefault value: ONSMakeList.txt\n" +
            "-w <directory>\n" +
            "--work-dir <directory>\n" +
            "\tSpecify working directory.\n" +
            "-s (-y|-n)\n" +
            "--silent (-y|-n)\n" +
            "\tDo not print info in console. When set, option -y or -n must be set as well.\n" +
            "-y\n" +
            "\tOverwrite output file.\n" +
            "-n\n" +
            "\tDo not overwrite output file.\n" +
            "-v" +
            "--verbose\n" +
            "\tPrint each processed line and its output.\n" +
            "-i <file path>\n" +
            "--input <file path>\n" +
            "\tSpecify files to include into output script, each should havs -i option.\n" +
            "\tCan be used when list file does not exists.\n" +
            "-r\n" +
            "--raw\n" +
            "\tConcatenate included files without processing.\n" +
            "--comment-directives\n" +
            "\tComment executed directives instead of deleting them.\n" +
            "--ignore-directives\n" +
            "\tDo not execute directives. They won't be deleted or commented.\n" +
            "--no-string-replaces\n" +
            "\tDo not replace strings with patterns from config file.\n" +
            "--no-script-check\n" +
            "\tDo not check output script for any errors.\n" +
            "--ignore-errors\n" +
            "\tWrite output file even when there are detected errors in script.\n" +
            "\n" +
            "Note: this program is created specifically for ONScripter-RU (by Umineko Project) and some cool features may not work for other forks of NScripter.\n" +
            "Software provided as-is, no guarantees, etc., etc...\n";
        Console.WriteLine(helpString);
    }
	
	private static bool CheckOutputFileOverwrite()
	{
		if (_config.OutputFile == null)
        {
            OutputHandler.PrintError("Output file is not specified.");
            return false;
        }
        if (!File.Exists(_config.OutputFile))
			return true;
        
        if (_config.OverwriteOutputFile == false)
        {
            if (_config.Silent)
                OutputHandler.PrintError($"File '{Path.GetFullPath(_config.OutputFile)}' already exists.");
            return false;
        }
        if (_config.OverwriteOutputFile == null)
        {
            if (_config.Silent || !AskYN(MessageID.ASK_OVERWRITE_FILE, [_config.OutputFile]))
                return false;
        }
		return true;
	}

    private static bool TryFormInputList(out List<string> list)
    {
        list = [];
        uint errorsBeforeForming = OutputHandler.Errors;

        string listFileName = _config.ListFile ?? DEFAULT_LIST_FILE;
        if (_config.ListFile != null && !File.Exists(_config.ListFile))
            OutputHandler.PrintError($"List file '{_config.ListFile}' not found.");
        if (!File.Exists(listFileName) && _config.InputFiles.Count == 0)
            OutputHandler.PrintError($"List file '{listFileName}' not found and no input options are specified.");

        if (File.Exists(listFileName))
        {
            foreach (string line in File.ReadAllLines(listFileName))
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed[0] == '#')
                    continue;

                if (File.Exists(trimmed))
                    list.Add(trimmed);
                else
                    OutputHandler.PrintError($"File '{Path.GetFullPath(trimmed)}' not found.");
            }
        }
        foreach (string file in _config.InputFiles)
        {
            if (File.Exists(file))
                list.Add(file);
            else
                OutputHandler.PrintError($"File '{Path.GetFullPath(file)}' not found.");
        }
        return OutputHandler.Errors == errorsBeforeForming;
    }

    private static bool AskYN(string message)
    {
        while (true)
        {
            Console.Write(message + " (y/n): ");
            var key = Console.ReadKey().Key;
            Console.WriteLine();
            if (key == ConsoleKey.Y)
                return true;
            if (key == ConsoleKey.N)
                return false;
        }
    }
	
	private static bool AskYN(MessageID id, string[] args)
	{
		return AskYN(MessageTranslator.GetArgumentedString(id, args));
	}
}
