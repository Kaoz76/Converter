using System;
using System.IO;
using System.Threading;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfRepairer
    {
        public static void Repair(string inputPath, string outputFolder, CancellationToken token)
        {
            try
            {
                // Yepyeni ve hatasız, boş bir PDF dosyası oluşturuyoruz
                using (PdfDocument outputDocument = new PdfDocument())
                {
                    // Bozuk dosyayı "İçe Aktar (Import)" modunda, hataları görmezden gelmeye çalışarak açıyoruz
                    using (PdfDocument inputDocument = PdfReader.Open(inputPath, PdfDocumentOpenMode.Import))
                    {
                        // Bozuk dosyanın içindeki sayfaları tek tek kurtarıp yeni PDF'e kopyalıyoruz
                        foreach (PdfPage page in inputDocument.Pages)
                        {
                            token.ThrowIfCancellationRequested();
                            outputDocument.AddPage(page);
                        }
                    }

                    // Yeni ve sağlam dosyayı kaydediyoruz (Bu işlem bozuk XREF tablosunu sıfırdan ve doğru şekilde yazar)
                    string fileName = Path.GetFileNameWithoutExtension(inputPath) + "_Repaired.pdf";
                    string outputPath = Path.Combine(outputFolder, fileName);
                    outputDocument.Save(outputPath);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                // Eğer dosya fiziksel olarak tamamen yok olmuşsa (0 KB vb.) bu hatayı fırlatır
                System.Diagnostics.Debug.WriteLine($"Onarım Hatası: {ex.Message}");
            }
        }
    }
}