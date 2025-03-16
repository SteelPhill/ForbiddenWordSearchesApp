namespace ForbiddenWordSearchesApp.Logic;

public interface IForbiddenWordSearcher
{
    void CancelSearch();
    bool IsSearchPaused();
    void PauseSearch();
    void ResumeSearch();
    Task SearchAsync(
        string searchFolder, 
        string resultFolder, 
        IEnumerable<string> searchWords, 
        IProgress<int> progress);
}