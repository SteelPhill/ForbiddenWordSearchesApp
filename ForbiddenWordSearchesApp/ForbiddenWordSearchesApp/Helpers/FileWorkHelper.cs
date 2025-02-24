using System.IO;

namespace ForbiddenWordSearchesApp.Helpers;

public static class FileWorkHelper
{
    public static string GetCorrectFilePath(string filePath)
    {
        var folderPath = Path.GetDirectoryName(filePath)!;

        for (var i = 1; ; i++)
        {
            var newName = $"{Path.GetFileNameWithoutExtension(filePath)}_{i}{Path.GetExtension(filePath)}";
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