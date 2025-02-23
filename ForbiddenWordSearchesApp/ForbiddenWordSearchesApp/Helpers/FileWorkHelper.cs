using System.IO;

namespace ForbiddenWordSearchesApp.Helpers;

public static class FileWorkHelper
{
    public static string GetCorrectFilePath(string filePath)
    {
        string folderPath = Path.GetDirectoryName(filePath)!;

        for (var i = 1; ; i++)
        {
            string newFilePath = Path.Combine(
                folderPath,
                $"{Path.GetFileNameWithoutExtension(filePath)}_{i}{Path.GetExtension(filePath)}");

            if (!File.Exists(newFilePath))
                return newFilePath;
        }
    }

    public static int CountFiles(string path)
    {
        int fileCount = 0;

        try
        {
            fileCount += Directory.EnumerateFiles(path, "*").Count();
        }
        catch (Exception)
        {
            fileCount--;
            // ignore
        }

        try
        {
            foreach (string subDir in Directory.EnumerateDirectories(path))
                fileCount += CountFiles(subDir);
        }
        catch (Exception)
        {
            // ignore
        }

        return fileCount;
    }
}