using System.IO;
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
    private CancellationTokenSource _cancellationTokenSource = new();

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (!_forbiddenWordSearcher.IsSetSearch())
        {
            StartButton.IsEnabled = false;
            PauseButton.IsEnabled = true;
            _forbiddenWordSearcher.ResumeSearch();
            return;
        }

        try
        {
            CheckWhetherPossibleStart();
        }
        catch (Exception ex)
        {
            ResultTextBlock.Text = ex.Message;
            return;
        }

        _cancellationTokenSource = new CancellationTokenSource();

        var forbiddenWords = new List<string>();

        if (!string.IsNullOrEmpty(FirstTextBox.Text))
        {
            forbiddenWords.AddRange(
                FirstTextBox.Text.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries));

            FirstTextBox.IsReadOnly = true;
            FirstTextBox.Background = Brushes.DarkGray;
        }

        if (!string.IsNullOrEmpty(SecondTextBox.Text))
        {
            if (!File.Exists(SecondTextBox.Text))
            {
                ResultTextBlock.Text = "Ошибка: неверно указан путь к файлу с запрещёнными словами!";
                return;
            }

            var fileContent = await File.ReadAllTextAsync(SecondTextBox.Text, _cancellationTokenSource.Token);

            forbiddenWords.AddRange(
                fileContent.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries));

            SecondTextBox.IsReadOnly = true;
            SecondTextBox.Background = Brushes.DarkGray;
        }      

        ProgressBar.Value = 0;
        ProgressBar.Maximum = FileWorkHelper.CountFiles(ThirdTextBox.Text);

        ResultTextBlock.Text = "";

        StartButton.IsEnabled = false;
        PauseButton.IsEnabled = true;
        StopButton.IsEnabled = true;       

        ThirdTextBox.IsReadOnly = FourthTextBox.IsReadOnly = true;

        FirstTextBox.Background
            = SecondTextBox.Background
            = ThirdTextBox.Background
            = FourthTextBox.Background
            = Brushes.DarkGray;

        var progress = new Progress<int>(value => ProgressBar.Value += value);

        try
        {
            ResultTextBlock.Text = "Поиск...";

            await _forbiddenWordSearcher.SearchAsync(
                ThirdTextBox.Text,
                FourthTextBox.Text,
                forbiddenWords,
                progress,
                _cancellationTokenSource.Token);

            ResultTextBlock.Text = $"Поиск завершен!{Environment.NewLine}{Environment.NewLine}";

            ResultTextBlock.Text += $"Результаты поиска сохранены в директории:{Environment.NewLine}{Path
                .Combine(FourthTextBox.Text, $"SearchResult_{DateTime.Now:yyyyMMddHHmmss}")}";
        }
        catch (OperationCanceledException)
        {
            ResultTextBlock.Text = "Поиск отменён!";
        }
        catch (Exception)
        {
            ResultTextBlock.Text = "Неизвестная ошибка! Поиск отменён!";
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
        _forbiddenWordSearcher.PauseSearch();
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        StopSearched();
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

        FirstTextBox.Background
            = SecondTextBox.Background
            = ThirdTextBox.Background
            = FourthTextBox.Background
            = Brushes.White;

        Cancel();
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

    private void Cancel() => _cancellationTokenSource.Cancel();
}