using System;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using Hitomiso.ONScripterMake.Lexer;
using Hitomiso.ONScripterMake.Parser;
using Hitomiso.ONScripterMake.Linter;
using Hitomiso.ONScripterMake.Processing;
using System.Threading;

namespace Hitomiso.ONScripterMake;

#nullable enable
public static class Program
{
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
        UnhandledException = 99,
    }

    private static ProjectConfiguration _config = new();

    private static SourceFileProcessor[] _processedFiles;
    private static string[] _correctIncludeOrder;
    private static ConcurrentBag<string> _filesToProcess = [];
    private static ConcurrentBag<SourceFileProcessor> _filesToLint = [];
    private static ConcurrentBag<SourceCodeMessage[]> _lintingMessages = [];
    private static OnsLinter _linter;

    public static void Main(string[] args)
    {
        try
        {
            Setup(args);

            bool isListValid = TryFormInputList(out List<string> filesToProcess);
            if (!isListValid && !_config.IgnoreErrors)
                Abort(ExitCodes.ListError);
            _correctIncludeOrder = [.. filesToProcess];
            foreach (string fileName in filesToProcess)
                _filesToProcess.Add(fileName);

            if (_config.Raw)
                ProcessFilesRaw(_config.OutputFile);
            else
                ProcessFilesSmart(_config.OutputFile);
            OutputHandler.PrintInfo($"Script saved in {_config.OutputFile}.");
        }
        catch (Exception ex)
        {
            OutputHandler.PrintError(ex.Message, "Unhandled exception");
            Abort(ExitCodes.UnhandledException);
        }
    }

    private static void ProcessFilesRaw(string outputPath)
    {
        using (StreamWriter outputWriter = new(outputPath))
        {
            foreach (string inputFile in _filesToProcess)
            {
                foreach (string line in File.ReadLines(inputFile))
                    outputWriter.WriteLine(line);
            }
        }
    }

    private static void ProcessFilesSmart(string outputPath)
    {
        int jobsCount = _config.JobsCount ?? Environment.ProcessorCount;
        if (jobsCount == 0)
            throw new ApplicationException("Somehow this machine has zero CPUs.");
        Task[] parallelJobs = new Task[jobsCount - 1];

        // Обрабатываем файлы, находим определения
        for (int i = 0; i < parallelJobs.Length; i++)
            parallelJobs[i] = Task.Run(FilesProcessingWork);
        FilesProcessingWork();
        foreach (Task t in parallelJobs)
            t.Wait();
        _processedFiles = [.. _filesToLint];

        // Выводим сообщения из каждого обработанного файла, регистрируем символы и снова выводим
        foreach (SourceFileProcessor procFile in _filesToLint)
        {
            foreach (SourceCodeMessage msg in procFile.ProcessingMessages)
            {
                if (!CheckIsMessageIgnored(msg))
                    OutputHandler.PrintMessage(msg);
            }
        }
        FinalScriptContext _scriptContext = new();
        foreach (SourceCodeMessage msg in _scriptContext.RegisterDefinitions(_filesToLint))
        {
            if (!CheckIsMessageIgnored(msg))
                OutputHandler.PrintMessage(msg);
        }
        _linter = new OnsLinter(_config.EngineCommands, _scriptContext);

        // Обязательно после регистрации в FinalScriptContext
        List<ProcessedLine>[] outputScriptLines = new List<ProcessedLine>[_correctIncludeOrder.Length];
        foreach (SourceFileProcessor file in _processedFiles)
        {
            int fileIndex = Array.IndexOf(_correctIncludeOrder, file.FileName);
            outputScriptLines[fileIndex] = file.OutputLines;
        }

        if (!_config.NoCommandCheck)
        {
            // Проверяем скрипт на ошибки
            for (int i = 0; i < parallelJobs.Length; i++)
                parallelJobs[i] = Task.Run(FilesLintingWork);
            FilesLintingWork();
            foreach (Task t in parallelJobs)
                t.Wait();

            // Выводим ошибки линтинга
            foreach (SourceCodeMessage[] messages in _lintingMessages)
            {
                foreach (SourceCodeMessage msg in messages)
                {
                    if (!CheckIsMessageIgnored(msg))
                        OutputHandler.PrintMessage(msg);
                }
            }
        }

        // Записываем в выходной файл
        if (OutputHandler.Errors > 0)
        {
            OutputHandler.PrintInfo($"Build FAILED with {OutputHandler.Errors} errors.");
            if (_config.IgnoreErrors)
                OutputHandler.PrintInfo($"Ignoring errors, writing output anyway...");
            else
                Abort(ExitCodes.ScriptError);
        }
        using (StreamWriter outputWriter = new(outputPath))
        {
            foreach (List<ProcessedLine> outputLines in outputScriptLines)
            {
                foreach (ProcessedLine line in outputLines)
                    outputWriter.WriteLine(line.OutputLine);
            }
        }
    }
    
    private static void FilesProcessingWork()
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        OnsParser parser = new();
        while (_filesToProcess.TryTake(out string? fileName))
        {
            SourceFileProcessor processor = new(fileName, _config, parser);
            _filesToLint.Add(processor);
        }
    }

    private static void FilesLintingWork()
    {
        Thread.CurrentThread.Priority = ThreadPriority.Highest;
        while (_filesToLint.TryTake(out SourceFileProcessor? processedFile))
        {
            _lintingMessages.Add(_linter.LintFile(processedFile));
        }
    }

    private static bool CheckIsMessageIgnored(SourceCodeMessage msg)
    {
        // Находим обработчик файла, где была ошибка
        SourceFileProcessor? fileProcessor = _processedFiles.FirstOrDefault(el => el.FileName == msg.FileName);
        if (fileProcessor == null)
            return false;

        // Находим нужный промежуток строк с игноренными ошибками
        (int lineIndex, MessageID[] errors)? disabledErrors = fileProcessor.DisabledErrorsHistory.FirstOrDefault(el => el.lineIndex < msg.Position.Line);
        if (disabledErrors == null || disabledErrors.Value.errors == null)
            return false;

        // Сравниваем заигноренные ошибки в промежутке с пришедшей ошибкой
        return disabledErrors.Value.errors.Contains(msg.MessageID);
    }

    private static void Setup(string[] args)
    {
        if (!_config.ParseArguments(args))
            Abort(ExitCodes.ArgumentsError);
        if (_config.PrintHelp)
        {
            Console.WriteLine(MessageTranslator.GetArgumentedString(MessageID.HELP, []));
            Abort(ExitCodes.Help);
        }

        try
        {
            if (_config.WorkingDirectory != null)
                Environment.CurrentDirectory = _config.WorkingDirectory;

            // Читаем и проверяем на ошибки конфиг
            if (_config.ConfigFile != null && !File.Exists(_config.ConfigFile))
            {
                OutputHandler.PrintError($"Config file '{Path.GetFullPath(_config.ConfigFile)}' not found.");
                Abort(ExitCodes.FileNotFound);
            }
            string configFileName = _config.ConfigFile ?? DEFAULT_CONFIG_FILE;
            if (File.Exists(configFileName))
            {
                uint errorsBeforeReadingConfig = OutputHandler.Errors;
                _config = new ProjectConfiguration(configFileName);
                if (OutputHandler.Errors != errorsBeforeReadingConfig)
                    Abort(ExitCodes.ConfigError);
                // Репарсим аргументы, чтобы они имели приоритет над конфигом
                if (!_config.ParseArguments(args))
                    Abort(ExitCodes.ArgumentsError);
            }
            OutputHandler.IsSilenced = _config.Silent;

            if (!CheckOutputFileOverwrite())
                Abort(ExitCodes.FileExists);
        }
        catch (IOException ex)
        {
            OutputHandler.PrintError(ex.Message, "IOException");
            Abort(ExitCodes.IOException);
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

    private static void Abort(ExitCodes exitCode)
    {
        Environment.Exit((int)exitCode);
    }
}
#nullable restore
