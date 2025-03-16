using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ForbiddenWordSearchesApp.Helpers;
using ForbiddenWordSearchesApp.Logic;

namespace ForbiddenWordSearchesApp;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly ForbiddenWordSearcher _forbiddenWordSearcher = new();
    private readonly StringBuilder _stringBuilder = new();   

    public MainWindow()
    {
        InitializeComponent();       
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (_forbiddenWordSearcher.IsSearchPaused())
        {
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;

            ResultTextBlock.Text = "Поиск...";

            _forbiddenWordSearcher.ResumeSearch();
            return;
        }

        try
        {
            CheckWhetherPossibleStart();
        }
        catch (Exception exception)
        {
            _stringBuilder.Clear();
            _stringBuilder.AppendLine();
            _stringBuilder.AppendLine(exception.Message);
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

            ResultTextBlock.Text = exception.Message;
            return;
        }

        var searchWords = new List<string>();

        if (!string.IsNullOrEmpty(FirstTextBox.Text))
            searchWords.AddRange(FirstTextBox.Text.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries));

        if (!string.IsNullOrEmpty(SecondTextBox.Text))
        {
            if (!File.Exists(SecondTextBox.Text))
            {                           
                ResultTextBlock.Text = "Ошибка: неверно указан путь к файлу с запрещёнными словами!";

                _stringBuilder.Clear();
                _stringBuilder.AppendLine();
                _stringBuilder.AppendLine(ResultTextBlock.Text);
                File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

                return;
            }

            var fileContent = await File.ReadAllTextAsync(SecondTextBox.Text);

            searchWords.AddRange(fileContent.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries));
        }

        ProgressBar.Value = 0;
        ProgressBar.Maximum = DirectoryHelper.CountFiles(ThirdTextBox.Text);

        ResultTextBlock.Text = "";

        StartButton.IsEnabled = false;
        PauseButton.IsEnabled = true;
        StopButton.IsEnabled = true;

        FirstTextBox.IsReadOnly 
            = SecondTextBox.IsReadOnly
            = ThirdTextBox.IsReadOnly
            = FourthTextBox.IsReadOnly
            = true;

        FirstTextBox.Background
            = SecondTextBox.Background
            = ThirdTextBox.Background
            = FourthTextBox.Background
            = Brushes.DarkGray;

        var progress = new Progress<int>(value => ProgressBar.Value += value);

        var resultFolder = Path.Combine(FourthTextBox.Text, $"SearchResult_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}");

        try
        {
            ResultTextBlock.Text = "Поиск...";           

            _stringBuilder.Clear();
            _stringBuilder.AppendLine();
            _stringBuilder.AppendLine($"Директория поиска: {ThirdTextBox.Text}");
            _stringBuilder.AppendLine($"Директория с результатами: {resultFolder}");
            _stringBuilder.Append("Запрещённые слова: ");
            searchWords.ForEach(s => _stringBuilder.Append($"{s} "));
            _stringBuilder.AppendLine();
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

            await _forbiddenWordSearcher.SearchAsync(
                ThirdTextBox.Text,
                resultFolder,
                searchWords,
                progress);

            ResultTextBlock.Text = $"Поиск завершен!{Environment.NewLine}{Environment.NewLine}";
            ResultTextBlock.Text += $"Результаты поиска сохранены в директории:{Environment.NewLine}{resultFolder}";
        }
        catch (OperationCanceledException)
        {
            ResultTextBlock.Text = "Поиск отменён!";

            _stringBuilder.Clear();
            _stringBuilder.AppendLine();
            _stringBuilder.AppendLine(ResultTextBlock.Text);
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

            Directory.Delete(resultFolder, recursive: true);
        }
        catch (Exception)
        {
            ResultTextBlock.Text = "Неизвестная ошибка! Поиск отменён!";

            _stringBuilder.Clear();
            _stringBuilder.AppendLine();
            _stringBuilder.AppendLine(ResultTextBlock.Text);
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

            Directory.Delete(resultFolder, recursive: true);
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

        ResultTextBlock.Text = "Пауза!";

        _forbiddenWordSearcher.PauseSearch();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        ProgressBar.Value = 0;
        StopSearched();
    }

    private void StopSearched()
    {
        StartButton.IsEnabled = true;
        PauseButton.IsEnabled = false;
        StopButton.IsEnabled = false;

        if (!string.IsNullOrEmpty(FirstTextBox.Text))
        {
            FirstTextBox.IsReadOnly = false;
            FirstTextBox.Background = Brushes.White;
        }

        if (!string.IsNullOrEmpty(SecondTextBox.Text))
        {
            SecondTextBox.IsReadOnly = false;
            SecondTextBox.Background = Brushes.White;
        }

        ThirdTextBox.IsReadOnly = FourthTextBox.IsReadOnly = false;
        ThirdTextBox.Background = FourthTextBox.Background = Brushes.White;

        _forbiddenWordSearcher.CancelSearch();
        _forbiddenWordSearcher.ResumeSearch();
    }

    private void CheckWhetherPossibleStart()
    {
        if (string.IsNullOrEmpty(FirstTextBox.Text) && string.IsNullOrEmpty(SecondTextBox.Text))
            throw new Exception("Ошибка: нет информации о запрещённых словах!");

        if (!Directory.Exists(ThirdTextBox.Text))
            throw new Exception("Ошибка: неверно указан путь к директории для поиска!");

        if (!Directory.Exists(FourthTextBox.Text))
            throw new Exception("Ошибка: неверно указан путь для создания директории с результатами!");
    }

    private void Keyboard_KeyUp(object sender, KeyEventArgs e)
    {
        if (string.IsNullOrEmpty(FirstTextBox.Text) && string.IsNullOrEmpty(SecondTextBox.Text))
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
}