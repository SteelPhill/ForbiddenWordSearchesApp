using ForbiddenWordSearchesApp.Domain.Entities;
using ForbiddenWordSearchesApp.Helpers;
using Nito.AsyncEx;
using System.IO;
using System.Text.RegularExpressions;

namespace ForbiddenWordSearchesApp.Logic;

public class ForbiddenWordSearcher
{
    private readonly AsyncManualResetEvent _asyncManualResetEvent = new(true);

    public bool IsSetSearch() => _asyncManualResetEvent.IsSet;
    public void PauseSearch() => _asyncManualResetEvent.Reset();
    public void ResumeSearch() => _asyncManualResetEvent.Set();

    public async Task SearchAsync(
        string searchFolder,
        string resultFolder,
        IEnumerable<string> forbiddenWords,
        IProgress<int> progress,
        CancellationToken token)
    {
        var searchWords = forbiddenWords.Select(word => new ForbiddenWord(word)).ToList();

        var resultFolderPath = Directory.CreateDirectory(
            Path.Combine(resultFolder, $"SearchResult_{DateTime.Now:yyyyMMddHHmmss}")).FullName;

        var resultFilePath = Path.Combine(resultFolderPath, Constants.ResultFileName);

        var totalFiles = FileWorkHelper.CountFiles(searchFolder);

        progress.Report(0);

        await SearchWordInFilesAsync(
            searchFolder,
            searchWords,
            resultFilePath,
            resultFolderPath,
            progress,
            token);

        await WriteSearchResultToSearchReportFileAsync(resultFilePath, totalFiles, searchWords, token);
    }

    private async Task SearchWordInFilesAsync(
        string folderPath,
        IReadOnlyCollection<ForbiddenWord> searchWords,
        string resultFilePath,
        string resultFolderPath,
        IProgress<int> progress,
        CancellationToken token)
    {
        foreach (var subFolderPath in Directory.GetDirectories(folderPath))
        {
            try
            {
                await SearchWordInFilesAsync(
                    subFolderPath,
                    searchWords,
                    resultFilePath,
                    resultFolderPath,
                    progress,
                    token);
            }
            catch (Exception)
            {
                await File.AppendAllTextAsync(
                    resultFilePath,
                    $"Ошибка: нет доступа к директории {subFolderPath}!{Environment.NewLine}{Environment.NewLine}",
                    token);
            }
        }

        foreach (var filePath in Directory.GetFiles(folderPath))
        {
            token.ThrowIfCancellationRequested();

            await Task.Delay(Constants.MillisecondsDelay, token);

            var isContains = false;

            try
            {
                var fileContent = await File.ReadAllTextAsync(filePath, token);

                foreach (var word in searchWords)
                {
                    await _asyncManualResetEvent.WaitAsync(token);

                    var pattern = @"\b" + Regex.Escape(word.Word) + @"\b";
                    var matches = Regex.Matches(fileContent, pattern, RegexOptions.IgnoreCase);

                    word.RepeatNumberInFile = matches.Count;
                    word.TotalRepeatNumber += word.RepeatNumberInFile;

                    if (word.RepeatNumberInFile > 0)
                    {
                        isContains = true;

                        fileContent = Regex.Replace(
                            fileContent,
                            pattern,
                            Constants.Substitute,
                            RegexOptions.IgnoreCase);
                    }
                }

                await WriteInfoAboutFileToSearchReportFileAsync(resultFilePath, filePath, searchWords, token);

                if (isContains)
                {
                    var copyFilePath = Path.Combine(
                        resultFolderPath,
                        $"{Path.GetFileNameWithoutExtension(filePath)}_Copy{Path.GetExtension(filePath)}");

                    if (File.Exists(copyFilePath))
                        copyFilePath = FileWorkHelper.GetCorrectFilePath(copyFilePath);

                    var replacedFilePath = Path.Combine(
                        resultFolderPath,
                        $"{Path.GetFileNameWithoutExtension(filePath)}_Replaced{Path.GetExtension(filePath)}");

                    if (File.Exists(replacedFilePath))
                        replacedFilePath = FileWorkHelper.GetCorrectFilePath(replacedFilePath);

                    File.Copy(filePath, copyFilePath);
                    await File.WriteAllTextAsync(replacedFilePath, fileContent, token);
                }

                progress.Report(1);
            }
            catch (Exception)
            {
                await File.AppendAllTextAsync(
                    resultFilePath,
                    $"Ошибка: не удалось обработать файл {filePath}!{Environment.NewLine}{Environment.NewLine}",
                    token);
            }
        }
    }

    private async Task WriteInfoAboutFileToSearchReportFileAsync(
        string resultFilePath,
        string filePathToReaded,
        IReadOnlyCollection<ForbiddenWord> searchWords,
        CancellationToken token)
    {
        await File.AppendAllTextAsync(resultFilePath, $"Файл:   {Path.GetFullPath(filePathToReaded)}{Environment.NewLine}", token);
        await File.AppendAllTextAsync(resultFilePath, $"Размер: {new FileInfo(filePathToReaded).Length} байт{Environment.NewLine}", token);
        await File.AppendAllTextAsync(resultFilePath, $"Замен:  {searchWords.Sum(w => w.RepeatNumberInFile)}{Environment.NewLine}", token);
        await File.AppendAllTextAsync(resultFilePath, $"Заменённые слова:{Environment.NewLine}", token);
        await File.AppendAllLinesAsync(resultFilePath, searchWords.Select(w => $"{w.Word} -> {w.RepeatNumberInFile} раз(а)"), token);
        await File.AppendAllTextAsync(resultFilePath, Environment.NewLine, token);
    }

    private async Task WriteSearchResultToSearchReportFileAsync(
        string resultFilePath,
        int filesCheckedNumber,
        IReadOnlyCollection<ForbiddenWord> searchWords,
        CancellationToken token)
    {
        await File.AppendAllTextAsync(resultFilePath, $"{Constants.Separator}{Environment.NewLine}{Environment.NewLine}", token);
        await File.AppendAllTextAsync(resultFilePath, $"Количество найденных запрещенных слов: {searchWords.Sum(w => w.TotalRepeatNumber)}{Environment.NewLine}", token);
        await File.AppendAllTextAsync(resultFilePath, $"Количество проверенных файлов: {filesCheckedNumber}{Environment.NewLine}{Environment.NewLine}", token);
        await File.AppendAllTextAsync(resultFilePath, $"Топ-10 самых популярных запрещённых слов:{Environment.NewLine}", token);
        await File.AppendAllLinesAsync(resultFilePath, searchWords.OrderByDescending(w => w.TotalRepeatNumber).Take(10).Select(w => $"{w.Word} -> {w.TotalRepeatNumber}"), token);
    }
}