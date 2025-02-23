namespace ForbiddenWordSearchesApp;

public static class Constants
{
    public static readonly char[] Separators = " .,!?;:\n\r\t-\"'/\\|@#$%^&*()_+={}[]<>~`".ToCharArray();
    public static readonly string Substitute = new('*', 7);
    public static readonly string Separator = new ('-', 160);
    public static readonly string ResultFileName = "SearchReport.txt";
    public static readonly int MillisecondsDelay = 1000;
}