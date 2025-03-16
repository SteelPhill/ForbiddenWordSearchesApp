using ForbiddenWordSearchesApp.Logic;
using System.IO;
using System.Text;
using System.Windows;

namespace ForbiddenWordSearchesApp;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    private static Mutex? _mutex;
    private readonly StringBuilder _stringBuilder = new();

    protected override async void OnStartup(StartupEventArgs e)
    {
        _mutex = new Mutex(true, Constants.AppMutexName, out var createdNew);       

        if (!createdNew)
        {
            //MessageBox.Show("Программа уже запущена!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            Current.Shutdown();
            return;
        }

        _stringBuilder.AppendLine();

        if (e.Args.Contains("--silent") && e.Args.Length >= Constants.MinArgsNumber)
        {
            _stringBuilder.AppendLine($"{DateTime.Now} Запуск в тихом режиме...");
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

            ShutdownMode = ShutdownMode.OnExplicitShutdown;

            await RunInSilentMode(e.Args);

            Current.Shutdown();
            return;
        }

        _stringBuilder.AppendLine($"{DateTime.Now} Запуск в оконном режиме...");
        File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());

        var mainWindow = new MainWindow(); 
        mainWindow.Show();

        base.OnStartup(e);
    }

    private async Task RunInSilentMode(string[] args)
    {
        var searchFolder = "";
        var resultFolder = "";
        var searchWords = new List<string>();
        var forbiddenWordSearcher = new ForbiddenWordSearcher();

        try
        {          
            if (args.Contains("--words") && args.Contains("--fileWithWords"))
                throw new Exception("Необходимо указать либо путь к файлу с запрещёнными словами, либо сами слова!");            

            for (var i = 0; i < args.Length; i++)
            {
                if (args[i] == "--searchFolder" && i + 1 < args.Length)
                {
                    searchFolder = args[i + 1];

                    if (!Directory.Exists(searchFolder))
                        throw new Exception($"Директория для поиска не существует: {searchFolder}");
                }

                if (args[i] == "--resultFolder" && i + 1 < args.Length)
                {
                    resultFolder = args[i + 1];

                    if (!Directory.Exists(resultFolder))
                        throw new Exception($"Директория для результатов не существует: {resultFolder}");
                }

                if (args[i] == "--fileWithWords" && i + 1 < args.Length)
                {
                    var fileWithWordsPath = args[i + 1];

                    if (!File.Exists(fileWithWordsPath))
                        throw new Exception($"Файл с запрещёнными словами не существует: {fileWithWordsPath}");

                    var fileContent = File.ReadAllText(fileWithWordsPath);

                    searchWords.AddRange(fileContent.Split(Constants.Separators, StringSplitOptions.RemoveEmptyEntries));
                }

                if (args[i] == "--words" && i + 1 < args.Length)
                    searchWords = args[i + 1].Split(',').ToList();
            }

            _stringBuilder.Clear();
            _stringBuilder.AppendLine();
            _stringBuilder.AppendLine($"Директория поиска: {searchFolder}");
            _stringBuilder.AppendLine($"Директория с результатами: {resultFolder}");
            _stringBuilder.Append("Запрещённые слова: ");
            searchWords.ForEach(s => _stringBuilder.Append($"{s} "));
            _stringBuilder.AppendLine();
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());          

            resultFolder = Path.Combine(resultFolder, $"SearchResult_{DateTime.Now:dd-MM-yyyy_HH-mm-ss}");

            await forbiddenWordSearcher.SearchAsync(
                searchFolder,
                resultFolder,
                searchWords,
                new Progress<int>());            
        }
        catch (Exception exception)
        {
            _stringBuilder.Clear();
            _stringBuilder.AppendLine();
            _stringBuilder.Append($"Ошибка: {exception.Message}");
            File.AppendAllText(Constants.LogFilePath, _stringBuilder.ToString());
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _mutex?.ReleaseMutex();
        _mutex?.Dispose();
        base.OnExit(e);
    }
}