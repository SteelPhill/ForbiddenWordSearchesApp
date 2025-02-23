namespace ForbiddenWordSearchesApp;

public static class Constants
{
    public static readonly char[] Separators = " .,!?;:\n\r\t-\"'/\\|@#$%^&*()_+={}[]<>~`".ToCharArray();
    public static readonly string Substitute = "*******";
    public static readonly int MillisecondsDelay = 100;
}