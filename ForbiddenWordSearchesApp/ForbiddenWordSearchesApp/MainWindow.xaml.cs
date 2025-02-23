using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ForbiddenWordSearchesApp.Helpers;
using Nito.AsyncEx;

namespace ForbiddenWordSearchesApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly AsyncManualResetEvent _asyncManualResetEvent = new(true);
    private CancellationTokenSource _cancellationTokenSource = new();

    private string _resultFolderPath = "";
    private string _resultFilePath = "";
    private int _fileCount = 0;

    public MainWindow()
    {
        InitializeComponent();
    }

    #region ButtonClicks

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!_asyncManualResetEvent.IsSet)
        {
            Resume();
            return;
        }

        try
        {
            CheckWhetherPossibleStart();
        }
        catch (Exception exception)
        {
            await Dispatcher.InvokeAsync(() => ResultTextBlock.Text = exception.Message);
            return;
        }

        var searchWords = new List<ForbiddenWord>();

        _cancellationTokenSource = new();
        _fileCount = 0;

        await Dispatcher.InvokeAsync(() =>
        {
            ResultTextBlock.Text = "";
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            StopButton.IsEnabled = true;
            ProgressBar.Value = 0;

            ThirdTextBox.IsReadOnly = FourthTextBox.IsReadOnly = true;
            FirstTextBox.Background = SecondTextBox.Background = ThirdTextBox.Background = FourthTextBox.Background = Brushes.DarkGray;
        });

        if (!string.IsNullOrEmpty(FirstTextBox.Text))
        {
            foreach (var word in FirstTextBox.Text.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries))
                searchWords.Add(new ForbiddenWord(word));

            FirstTextBox.IsReadOnly = true;
        }

        if (!string.IsNullOrEmpty(SecondTextBox.Text))
        {
            if (!File.Exists(SecondTextBox.Text))
            {
                await Dispatcher.InvokeAsync(() => ResultTextBlock.Text = "Ошибка: неверно указан путь к файлу с запрещенными словами!");
                return;
            }

            var fileContent = await File.ReadAllTextAsync(SecondTextBox.Text, _cancellationTokenSource.Token);
            foreach (var word in fileContent.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries))
                searchWords.Add(new ForbiddenWord(word));

            SecondTextBox.IsReadOnly = true;
        }

        _resultFolderPath = Directory.CreateDirectory(
            Path.Combine(FourthTextBox.Text,
            $"SearchResult_{DateTime.Now:yyyyMMddHHmmssmm}")).FullName;

        _resultFilePath = Path.Combine(_resultFolderPath, "SearchReport.txt");

        var searchDirectoryPath = ThirdTextBox.Text;
        var totalFiles = await Task.Run(() => FileWorkHelper.CountFiles(searchDirectoryPath), _cancellationTokenSource.Token);
        await Dispatcher.InvokeAsync(() => ProgressBar.Maximum = totalFiles);

        try
        {
            await SearchWordInFilesAsync(ThirdTextBox.Text, searchWords, _cancellationTokenSource.Token);

            await File.AppendAllTextAsync(_resultFilePath, $"Топ-10 самых популярных запрещенных слов:{Environment.NewLine}", _cancellationTokenSource.Token);
            await File.AppendAllLinesAsync(_resultFilePath, searchWords.OrderByDescending(w => w.TotalRepeatNumber).Take(10).Select(w => $"{w.Word} -> {w.TotalRepeatNumber}").ToArray(), _cancellationTokenSource.Token);

            await Dispatcher.InvokeAsync(() =>
            {
                ResultTextBlock.Text = $"Количество найденных запрещенных слов: {searchWords.Sum(w => w.TotalRepeatNumber)}{Environment.NewLine}";
                ResultTextBlock.Text += $"Количество проверенных файлов: {totalFiles}{Environment.NewLine}{Environment.NewLine}";
                ResultTextBlock.Text += $"Результаты поиска хранятся в директории:{Environment.NewLine}{_resultFolderPath}";
            });
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.InvokeAsync(() => ResultTextBlock.Text = "Поиск отменён.");
        }
        catch (Exception)
        {
            // ignore
        }
        finally
        {
            StopSearched();
        }
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = true;
        PauseButton.IsEnabled = false;
        Pause();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopSearched();
        Dispatcher.Invoke(() => ProgressBar.Value = 0);
        Directory.Delete(_resultFolderPath, true);
    }

    private void Keyboard_KeyUp(object sender, KeyEventArgs e)
    {
        if (string.IsNullOrEmpty(FirstTextBox.Text) &&
            string.IsNullOrEmpty(SecondTextBox.Text))
        {
            FirstTextBox.IsReadOnly = SecondTextBox.IsReadOnly = false;
            FirstTextBox.Background = SecondTextBox.Background = Brushes.White;
        }
        else if (!string.IsNullOrEmpty(FirstTextBox.Text))
        {
            SecondTextBox.IsReadOnly = true;
            SecondTextBox.Background = Brushes.DarkGray;
        }
        else if (!string.IsNullOrEmpty(SecondTextBox.Text))
        {
            FirstTextBox.IsReadOnly = true;
            FirstTextBox.Background = Brushes.DarkGray;
        }
    }

    #endregion

    #region HelperLogic

    private async Task SearchWordInFilesAsync(
        string folderPath,
        IReadOnlyCollection<ForbiddenWord> searchWords,
        CancellationToken token)
    {
        foreach (var subFolderPath in Directory.GetDirectories(folderPath))
        {
            try
            {
                await SearchWordInFilesAsync(subFolderPath, searchWords, token);
            }
            catch (Exception)
            {
                await File.AppendAllTextAsync(
                    _resultFilePath,
                    $"Ошибка: нет доступе к директории {subFolderPath}!{Environment.NewLine}{Environment.NewLine}",
                    token);
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

                    var pattern = @"\b" + Regex.Escape(word.Word) + @"\b";
                    var matches = Regex.Matches(fileContent, pattern, RegexOptions.IgnoreCase);

                    await Task.Delay(Constants.MillisecondsDelay, token);

                    word.RepeatNumberInFile = matches.Count;
                    word.TotalRepeatNumber += word.RepeatNumberInFile;

                    if (word.RepeatNumberInFile > 0)
                    {
                        isContains = true;
                        fileContent = Regex.Replace(fileContent, pattern, Constants.Substitute, RegexOptions.IgnoreCase);
                    }
                }

                await File.AppendAllTextAsync(_resultFilePath, $"Файл:   {Path.GetFullPath(filePath)}{Environment.NewLine}", token);
                await File.AppendAllTextAsync(_resultFilePath, $"Размер: {new FileInfo(filePath).Length} байт{Environment.NewLine}", token);
                await File.AppendAllTextAsync(_resultFilePath, $"Замен:  {searchWords.Sum(w => w.RepeatNumberInFile)}{Environment.NewLine}", token);
                await File.AppendAllTextAsync(_resultFilePath, $"Замененные слова:{Environment.NewLine}", token);
                await File.AppendAllLinesAsync(_resultFilePath, searchWords.Select(w => $"{w.Word} -> {w.RepeatNumberInFile} раз(а)").ToArray(), token);
                await File.AppendAllTextAsync(_resultFilePath, Environment.NewLine, token);

                if (isContains)
                {
                    var copyFilePath = Path.Combine(
                        _resultFolderPath,
                        $"{Path.GetFileNameWithoutExtension(filePath)}_Copy{Path.GetExtension(filePath)}");

                    if (File.Exists(copyFilePath))
                        copyFilePath = FileWorkHelper.GetCorrectFilePath(copyFilePath);

                    var replacedFilePath = Path.Combine(
                        _resultFolderPath,
                        $"{Path.GetFileNameWithoutExtension(filePath)}_Replaced{Path.GetExtension(filePath)}");

                    if (File.Exists(replacedFilePath))
                        replacedFilePath = FileWorkHelper.GetCorrectFilePath(replacedFilePath);

                    File.Copy(filePath, copyFilePath);

                    await File.WriteAllTextAsync(replacedFilePath, fileContent, token);
                }

                _fileCount++;
                await Dispatcher.InvokeAsync(() => ProgressBar.Value = _fileCount);
            }
            catch (Exception)
            {
                await File.AppendAllTextAsync(
                    _resultFilePath,
                    $"Ошибка: не удалось обработать файл {filePath}!{Environment.NewLine}{Environment.NewLine}",
                    token);
            }
        }
    }

    private void StopSearched()
    {
        StartButton.IsEnabled = true;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;

        if (!string.IsNullOrEmpty(FirstTextBox.Text))
            FirstTextBox.IsReadOnly = false;

        if (!string.IsNullOrEmpty(SecondTextBox.Text))
            SecondTextBox.IsReadOnly = false;

        ThirdTextBox.IsReadOnly = FourthTextBox.IsReadOnly = false;
        FirstTextBox.Background = SecondTextBox.Background = ThirdTextBox.Background = FourthTextBox.Background = Brushes.White;

        Cancel();
        Resume();
    }

    private void CheckWhetherPossibleStart()
    {
        if (string.IsNullOrEmpty(FirstTextBox.Text) && string.IsNullOrEmpty(SecondTextBox.Text))
            throw new Exception("Ошибка: нет информации о запрещённых словах!");

        if (!Path.Exists(ThirdTextBox.Text))
            throw new Exception("Ошибка: неверно указан путь к директории для поиска!");

        if (!Path.Exists(FourthTextBox.Text))
            throw new Exception("Ошибка: неверно указан путь для создания директории с результатами!");
    }

    private void Pause()
    {
        _asyncManualResetEvent.Reset();
    }

    private void Resume()
    {
        _asyncManualResetEvent.Set();
    }

    private void Cancel()
    {
        _cancellationTokenSource.Cancel();
    }

    #endregion
}