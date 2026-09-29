namespace ONSMake;

public enum ExitCode
{
    Help = 0,
    InvalidArgument,
    InvalidConfig,
    InvalidList,
    ScriptError,
    FileExists,
    FileNotFound,
    IOException,
    UnhandledException = int.MaxValue
}