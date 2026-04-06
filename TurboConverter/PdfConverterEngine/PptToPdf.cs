using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TurboConverter.PdfConverterEngine
{
    public static class PptToPdf
    {
        public static void Convert(string inputPath, string outputFolder, CancellationToken token)
        {
            // 1. KORUMA KALKANI: Bilgisayarda PowerPoint var mı diye kontrol et
            Type? pptType = Type.GetTypeFromProgID("PowerPoint.Application");
            if (pptType == null)
            {
                throw new Exception("Sisteminizde Microsoft PowerPoint bulunamadı!\nBu özellik, dosyayı kusursuz çizebilmek için arka planda PowerPoint'in yüklü olmasını gerektirir.");
            }

            dynamic? pptApp = null;
            dynamic? presentation = null;

            try
            {
                token.ThrowIfCancellationRequested();

                // 2. PowerPoint'i arka planda (görünmez şekilde ve sessizce) başlat
                pptApp = Activator.CreateInstance(pptType);

                // Parametreler: DosyaYolu, ReadOnly (-1), Untitled (0), WithWindow (0)
                // (Ekrana hiçbir pencere açılmadan dosyayı okuma modunda belleğe alır)
                presentation = pptApp.Presentations.Open(inputPath, ReadOnly: -1, Untitled: 0, WithWindow: 0);

                token.ThrowIfCancellationRequested();

                string fileName = Path.GetFileNameWithoutExtension(inputPath) + ".pdf";
                string outputPath = Path.Combine(outputFolder, fileName);

                // 3. Dosyayı PDF olarak kaydet (2 = PDF Format Kodu)
                presentation.ExportAsFixedFormat(outputPath, 2);
            }
            finally
            {
                // 4. TEMİZLİK: Arka planda asılı kalan görünmez PowerPoint'i (RAM'i) tamamen kapatıp temizle
                if (presentation != null)
                {
                    try { presentation.Close(); } catch { }
                    Marshal.ReleaseComObject(presentation);
                }
                if (pptApp != null)
                {
                    try { pptApp.Quit(); } catch { }
                    Marshal.ReleaseComObject(pptApp);
                }
            }
        }
    }
}