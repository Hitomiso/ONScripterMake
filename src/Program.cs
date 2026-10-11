using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;
using ONSMake.Linter;
using ONSMake.Processing;

namespace ONSMake;

#nullable enable
public static class Program
{
    private const string DEFAULT_LIST_FILE = "ONSMakeList.txt";
    private const string DEFAULT_CONFIG_FILE = "ONSMakeConfig.json";

    private static ProjectConfiguration _config = new();

    public static void Main(string[] args)
    {
        try
        {
            Setup(args);

            bool isListValid = TryFormInputList(out List<string> scriptFiles);
            if (!isListValid && !_config.IgnoreErrors)
                Abort(ExitCode.InvalidList);
            string[] correctIncludeOrder = [.. scriptFiles];

            if (_config.Raw)
                ProcessFilesRaw(correctIncludeOrder, _config.OutputFile);
            else
                ProcessFilesSmart(correctIncludeOrder, _config.OutputFile);
            if (OutputHandler.Warnings > 0)
                OutputHandler.PrintInfo($"Script saved in {_config.OutputFile} with {OutputHandler.Warnings} warnings.");
            else
                OutputHandler.PrintInfo($"Script saved in {_config.OutputFile}.");
        }
        catch (Exception ex)
        {
            OutputHandler.PrintError(ex.Message, "Unhandled exception");
            Abort(ExitCode.UnhandledException);
        }
    }

    private static void ProcessFilesRaw(string[] scriptFiles, string outputPath)
    {
        using StreamWriter outputWriter = new(outputPath);
        foreach (string inputFile in scriptFiles)
        {
            foreach (string line in File.ReadLines(inputFile))
                outputWriter.WriteLine(line);
        }
    }
    
    private static ScriptUnit[] ProcessFilesParallel(string[] filesToProcess, ParallelOptions options)
    {
        // Обрабатываем файлы, находим определения
        ScriptUnit[] sortedUnits = new ScriptUnit[filesToProcess.Length];
        for (int i = 0; i < filesToProcess.Length; i++)
            sortedUnits[i] = new ScriptUnit(filesToProcess[i]);
        Parallel.ForEach(sortedUnits, options, unit =>
        {
            ScriptProcessor processor = new(_config);
            processor.ProcessScriptUnit(unit);
        });
        return sortedUnits;
    }
    
    private static void ProcessFilesSmart(string[] scriptFiles, string outputPath)
    {
        int jobsCount = _config.JobsCount ?? Environment.ProcessorCount;
        if (jobsCount <= 0)
            throw new ApplicationException($"Incorrect parallel jobs count: {jobsCount}.");
        ParallelOptions parallelOptions = new()
        {
            MaxDegreeOfParallelism = jobsCount
        };
        
        ScriptUnit[] unitsToLint = ProcessFilesParallel(scriptFiles, parallelOptions);
        
        FinalScriptContext scriptContext = new();
        scriptContext.RegisterDefinitions(unitsToLint);

        if (!_config.NoCommandCheck)
        {
            OnsLinter linter = new(_config.EngineCommands, scriptContext);
            Parallel.ForEach(unitsToLint, parallelOptions, unit => linter.LintUnit(unit));
        }

        // Выводим сообщения из каждого обработанного файла
        foreach (ScriptUnit procFile in unitsToLint)
        {
            foreach (ScriptMessage msg in procFile.Messages)
                OutputHandler.PrintMessage(msg);
        }

        // Записываем в выходной файл
        if (OutputHandler.Errors > 0)
        {
            if (OutputHandler.Warnings > 0)
                OutputHandler.PrintInfo($"Build FAILED with {OutputHandler.Errors} errors and {OutputHandler.Warnings} warnings.");
            else
                OutputHandler.PrintInfo($"Build FAILED with {OutputHandler.Errors} errors.");
            if (_config.IgnoreErrors)
                OutputHandler.PrintInfo($"Ignoring errors, writing output anyway...");
            else
                Abort(ExitCode.ScriptError);
        }
        using (StreamWriter outputWriter = new(outputPath))
        {
            foreach (var file in unitsToLint)
            {
                foreach (ProcessedLine line in file.OutputLines)
                    outputWriter.WriteLine(line.OutputLine);
            }
        }
    }

    private static void Setup(string[] args)
    {
        if (!_config.ParseArguments(args))
            Abort(ExitCode.InvalidArgument);
        if (_config.PrintHelp)
        {
            Console.WriteLine(MessageTranslator.GetArgumentedString(MessageID.HELP, []));
            Abort(ExitCode.Help);
        }

        try
        {
            if (_config.WorkingDirectory != null)
                Environment.CurrentDirectory = _config.WorkingDirectory;

            // Читаем и проверяем на ошибки конфиг
            if (_config.ConfigFile != null && !File.Exists(_config.ConfigFile))
            {
                OutputHandler.PrintError($"Config file '{Path.GetFullPath(_config.ConfigFile)}' not found.");
                Abort(ExitCode.FileNotFound);
            }
            string configFileName = _config.ConfigFile ?? DEFAULT_CONFIG_FILE;
            if (File.Exists(configFileName))
            {
                uint errorsBeforeReadingConfig = OutputHandler.Errors;
                _config = new ProjectConfiguration(configFileName);
                if (OutputHandler.Errors != errorsBeforeReadingConfig)
                    Abort(ExitCode.InvalidConfig);
                // Репарсим аргументы, чтобы они имели приоритет над конфигом
                if (!_config.ParseArguments(args))
                    Abort(ExitCode.InvalidArgument);
            }
            OutputHandler.IsSilenced = _config.Silent;

            if (!CheckOutputFileOverwrite())
                Abort(ExitCode.FileExists);
        }
        catch (IOException ex)
        {
            OutputHandler.PrintError(ex.Message, "IOException");
            Abort(ExitCode.IOException);
        }
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
            if (!_config.Silent)
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

        string? listFileName = _config.ListFile;
        if (listFileName == null && _config.InputFiles.Count == 0)
            listFileName = DEFAULT_LIST_FILE;

        if (listFileName != null && !File.Exists(listFileName))
            OutputHandler.PrintError($"List file '{_config.ListFile}' not found.");
        if (!File.Exists(listFileName) && _config.InputFiles.Count == 0)
            OutputHandler.PrintError($"List file '{listFileName}' not found and no input options are specified.");

        // Если указаны -i и нет --list, то список не используется
        if ((_config.InputFiles.Count == 0 || _config.ListFile != null) && File.Exists(listFileName))
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

    private static void Abort(ExitCode exitCode)
    {
        Environment.Exit((int)exitCode);
    }
}
#nullable restore
