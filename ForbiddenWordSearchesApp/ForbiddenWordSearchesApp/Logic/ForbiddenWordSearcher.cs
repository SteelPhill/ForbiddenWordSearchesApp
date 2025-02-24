using ForbiddenWordSearchesApp.Domain.Entities;
using ForbiddenWordSearchesApp.Extensions;
using ForbiddenWordSearchesApp.Helpers;
using Nito.AsyncEx;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace ForbiddenWordSearchesApp.Logic;

public class ForbiddenWordSearcher
{
    private static readonly StringBuilder _logStringBuilder = new();
    private static readonly StringBuilder _resultStringBuilder = new();
    private readonly AsyncManualResetEvent _asyncManualResetEvent = new(true);

    public bool IsSetSearch() => _asyncManualResetEvent.IsSet;
    public void PauseSearch() => _asyncManualResetEvent.Reset();
    public void ResumeSearch() => _asyncManualResetEvent.Set();

    public async Task SearchAsync(
        string searchFolder,
        string resultFolder,
        IEnumerable<string> searchWords,
        IProgress<int> progress,
        CancellationToken token)
    {
        _resultStringBuilder.Clear();
        _logStringBuilder.Clear();

        _logStringBuilder.AppendLine($"{DateTime.Now} Начало поиска...");

        var forbiddenWords = searchWords.Select(s => new ForbiddenWord(s)).ToList();
        var resultFolderPath = Directory.CreateDirectory(resultFolder).FullName;
        var resultFilePath = Path.Combine(resultFolderPath, Constants.ResultFileName);
        var totalFiles = FileWorkHelper.CountFiles(searchFolder);

        progress.Report(0);

        await SearchWordInFilesAsync(
            searchFolder,
            forbiddenWords,
            resultFilePath,
            resultFolderPath,
            progress,
            token);

        _logStringBuilder.AppendLine($"{DateTime.Now} Поиск окончен...");

        AppendSearchResultToResultStringBuilder(totalFiles, forbiddenWords);

        _logStringBuilder.AppendLine($"Количество найденных запрещенных слов: {forbiddenWords.Sum(w => w.TotalRepeatNumber)}");
        _logStringBuilder.AppendLine($"Количество проверенных файлов: {totalFiles}");

        await File.AppendAllTextAsync(Constants.LogFilePath, _logStringBuilder.ToString(), token);
        await File.AppendAllTextAsync(resultFilePath, _resultStringBuilder.ToString(), token);
    }

    private async Task SearchWordInFilesAsync(
        string folderPath,
        IReadOnlyCollection<ForbiddenWord> searchWords,
        string resultFilePath,
        string resultFolderPath,
        IProgress<int> progress,
        CancellationToken token)
    {
        foreach (var subFolder in Directory.GetDirectories(folderPath))
        {
            try
            {
                await SearchWordInFilesAsync(
                    subFolder,
                    searchWords,
                    resultFilePath,
                    resultFolderPath,
                    progress,
                    token);
            }
            catch (Exception)
            {
                _resultStringBuilder.AppendLine($"Ошибка: нет доступа к директории {subFolder}!");
                _resultStringBuilder.AppendLine();

                _logStringBuilder.AppendLine($"Ошибка: нет доступа к директории {subFolder}!");
            }
        }

        foreach (var filePath in Directory.GetFiles(folderPath))
        {
            token.ThrowIfCancellationRequested();

            var isContains = false;

            try
            {
                var fileContent = await File.ReadAllTextAsync(filePath, token);

                foreach (var word in searchWords)
                {
                    await _asyncManualResetEvent.WaitAsync(token);

                    await Task.Delay(Constants.MillisecondsDelay, token);

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

                AppendInfoAboutFileToResultStringBuilder(filePath, searchWords);

                if (isContains)
                {
                    var copyFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_Copy{Path.GetExtension(filePath)}";
                    var copyFilePath = Path.Combine(resultFolderPath, copyFileName);

                    if (File.Exists(copyFilePath))
                        copyFilePath = FileWorkHelper.GetCorrectFilePath(copyFilePath);

                    var replacedFileName = $"{Path.GetFileNameWithoutExtension(filePath)}_Replaced{Path.GetExtension(filePath)}";
                    var replacedFilePath = Path.Combine(resultFolderPath, replacedFileName);

                    if (File.Exists(replacedFilePath))
                        replacedFilePath = FileWorkHelper.GetCorrectFilePath(replacedFilePath);

                    File.Copy(filePath, copyFilePath);
                    _logStringBuilder.AppendLine($"Создана копия файла: {copyFilePath}");

                    await File.WriteAllTextAsync(replacedFilePath, fileContent, token);
                    _logStringBuilder.AppendLine($"Создан файл с заменёнными словами: {replacedFilePath}");
                }

                progress.Report(1);
            }
            catch (Exception)
            {
                _resultStringBuilder.AppendLine($"Ошибка: не удалось обработать файл {filePath}!");
                _resultStringBuilder.AppendLine();

                _logStringBuilder.AppendLine($"Ошибка: не удалось обработать файл {filePath}!");
            }
        }
    }

    private static void AppendInfoAboutFileToResultStringBuilder(
        string filePathToReaded,
        IReadOnlyCollection<ForbiddenWord> searchWords)
    {
        _resultStringBuilder.AppendLine($"Файл:   {Path.GetFullPath(filePathToReaded)}");
        _resultStringBuilder.AppendLine($"Размер: {new FileInfo(filePathToReaded).Length} байт");
        _resultStringBuilder.AppendLine($"Замен:  {searchWords.Sum(w => w.RepeatNumberInFile)}");
        _resultStringBuilder.AppendLine("Заменённые слова:");        
        searchWords
            .Select(w => $"{w.Word} -> {w.RepeatNumberInFile} раз(а)")
            .ForEach(s => _resultStringBuilder.AppendLine(s));
        _resultStringBuilder.AppendLine();
    }

    private static void AppendSearchResultToResultStringBuilder(
        int filesCheckedNumber,
        IReadOnlyCollection<ForbiddenWord> searchWords)
    {
        _resultStringBuilder.AppendLine(Constants.Separator);
        _resultStringBuilder.AppendLine();
        _resultStringBuilder.AppendLine($"Количество найденных запрещенных слов: {searchWords.Sum(w => w.TotalRepeatNumber)}");
        _resultStringBuilder.AppendLine($"Количество проверенных файлов: {filesCheckedNumber}");
        _resultStringBuilder.AppendLine();
        _resultStringBuilder.AppendLine("Топ-10 самых популярных запрещённых слов:");
        searchWords
            .OrderByDescending(w => w.TotalRepeatNumber)
            .Take(Constants.TopNumberForbiddenWords)
            .Select(w => $"{w.Word} -> {w.TotalRepeatNumber} раз(а)")
            .ForEach(s => _resultStringBuilder.AppendLine(s));
    }
}