namespace ForbiddenWordSearchesApp.Domain.Entities;

public class ForbiddenWord
{
    public string Word { get; }
    public int RepeatNumberInFile { get; set; }
    public int TotalRepeatNumber { get; set; }

    public ForbiddenWord(string word)
    {
        Word = word;
        RepeatNumberInFile = 0;
        TotalRepeatNumber = 0;
    }
}