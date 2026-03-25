using System.Collections.Generic;
using System.IO;

namespace TurboConverter
{
    public static class FileHelper
    {
        // Klasörün içindeki tüm dosyaları bulur
        public static List<string> ResolveFiles(string[] paths)
        {
            var liste = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    string[] directoryFiles = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
                    liste.AddRange(directoryFiles);
                }
                else if (File.Exists(path))
                {
                    liste.Add(path);
                }
            }
            return liste;
        }

        // Dosyanın resim olup olmadığını kontrol eder
        public static bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = Path.GetExtension(path).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".bmp";
        }

        // Aynı isimde dosya varsa sonuna (1), (2) ekler
        public static string GetUniquePath(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                if (!Directory.Exists(path)) return path;
                int count = 1;
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string name = Path.GetFileName(path);
                string newPath;
                do
                {
                    newPath = Path.Combine(dir, $"{name} ({count})");
                    count++;
                } while (Directory.Exists(newPath));
                return newPath;
            }
            else
            {
                if (!File.Exists(path)) return path;
                int count = 1;
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);
                string newPath;
                do
                {
                    newPath = Path.Combine(dir, $"{name} ({count}){ext}");
                    count++;
                } while (File.Exists(newPath));
                return newPath;
            }
        }
    }
}