using System;
using System.IO;
using System.Threading;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfRotator
    {
        public static void Rotate(string inputPath, string outputFolder, int angle, CancellationToken token)
        {
            try
            {
                // PDF'i düzenleme modunda açıyoruz
                using (PdfDocument inputDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify))
                {
                    foreach (PdfPage page in inputDocument.Pages)
                    {
                        token.ThrowIfCancellationRequested();

                        // Sayfanın mevcut rotasyonuna seçilen açıyı ekliyoruz
                        // (WPF/PdfSharp'ta rotasyonlar 90'ın katları olmalıdır)
                        page.Rotate = (page.Rotate + angle) % 360;
                    }

                    string fileName = Path.GetFileNameWithoutExtension(inputPath) + "_Rotated.pdf";
                    string outputPath = Path.Combine(outputFolder, fileName);
                    inputDocument.Save(outputPath);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Rotate Hatası: {ex.Message}");
            }
        }
    }
}