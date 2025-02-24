using System.IO;

namespace ForbiddenWordSearchesApp;

public static class Constants
{
    public static readonly char[] Separators = " .,!?;:\n\r\t-\"'/\\|@#$%^&*()_+={}[]<>~`".ToCharArray();
    public static readonly string Substitute = new('*', 7);
    public static readonly string Separator = new ('-', 160);
    public static readonly string ResultFileName = "SearchReport.txt";
    public static readonly string LogFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log.txt");
    public static readonly string AppMutexName = "ForbiddenWordSearcherAppMutex";
    public static readonly int MillisecondsDelay = 500;
    public static readonly int TopNumberForbiddenWords  = 10;
    public static readonly int MinArgsNumber = 7;
}