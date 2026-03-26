using System;
using System.IO;
using System.Threading;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfToText
    {
        public static void Convert(string pdfPath, string outputFolder, CancellationToken token)
        {
            try
            {
                using var docReader = PdfSecurity.GetDocReaderSafe(pdfPath, 1.0);
                string outputFile = FileHelper.GetUniquePath(Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(pdfPath)}.txt"), false);
                using var writer = new StreamWriter(outputFile);

                for (int i = 0; i < docReader.GetPageCount(); i++)
                {
                    token.ThrowIfCancellationRequested();
                    using var pageReader = docReader.GetPageReader(i);
                    string text = pageReader.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        writer.WriteLine($"--- Sayfa {i + 1} ---");
                        writer.WriteLine(text);
                        writer.WriteLine();
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Metin Çıkarma Hatası: {ex.Message}"); }
        }
    }
}