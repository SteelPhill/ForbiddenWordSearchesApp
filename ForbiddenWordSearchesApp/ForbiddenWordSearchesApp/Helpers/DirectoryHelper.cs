using System.IO;

namespace ForbiddenWordSearchesApp.Helpers;

public static class DirectoryHelper
{
    public static string GetCorrectFilePath(string path)
    {
        var folderPath = Path.GetDirectoryName(path)!;

        for (var i = 1; ; i++)
        {
            var newName = $"{Path.GetFileNameWithoutExtension(path)}_{i}{Path.GetExtension(path)}";
            var newFilePath = Path.Combine(folderPath, newName);

            if (!File.Exists(newFilePath))
                return newFilePath;
        }
    }

    public static int CountFiles(string path)
    {
        var fileCount = 0;

        try
        {
            fileCount += Directory.EnumerateFiles(path, "*").Count();
        }
        catch (Exception)
        {
            fileCount--;
        }

        try
        {
            foreach (var subFolder in Directory.EnumerateDirectories(path))
                fileCount += CountFiles(subFolder);
        }
        catch (Exception)
        {
            // ignore
        }

        return fileCount;
    }
}