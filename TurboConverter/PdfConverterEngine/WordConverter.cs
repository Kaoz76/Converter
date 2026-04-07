using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TurboConverter.PdfConverterEngine
{
    public static class WordConverter
    {
        // mod: 0 ise Word'den PDF'e, 1 ise PDF'ten Word'e
        public static void Convert(string inputPath, string outputFolder, int mod, CancellationToken token)
        {
            Type? wordType = Type.GetTypeFromProgID("Word.Application");
            if (wordType == null)
            {
                throw new Exception("Sisteminizde Microsoft Word bulunamadı!\nBu özellik arka planda Word'ün yüklü olmasını gerektirir.");
            }

            dynamic? wordApp = null;
            dynamic? document = null;

            try
            {
                token.ThrowIfCancellationRequested();

                // Word'ü arka planda görünmez olarak başlatıyoruz
                wordApp = Activator.CreateInstance(wordType);

                // HAYAT KURTARAN DETAY: Word, PDF açarken "Bunu Word'e çevireceğim, düzenlenebilir olacak, onaylıyor musun?" diye gizli bir uyarı penceresi çıkartır.
                // Arka planda çalıştığımız için kullanıcı bu uyarıya "Tamam" diyemez ve program kilitlenir. 
                // Bu yüzden Word'ün tüm uyarı mesajlarını (Alerts) tamamen susturuyoruz:
                wordApp.DisplayAlerts = 0;

                // Dosyayı sadece okunur modda aç (Visible: false)
                document = wordApp.Documents.Open(inputPath, ReadOnly: true, Visible: false);

                token.ThrowIfCancellationRequested();

                if (mod == 0) // Word ➔ PDF
                {
                    string fileName = Path.GetFileNameWithoutExtension(inputPath) + ".pdf";
                    string outputPath = Path.Combine(outputFolder, fileName);

                    // 17 = wdExportFormatPDF (PDF olarak dışa aktar)
                    document.ExportAsFixedFormat(outputPath, 17);
                }
                else if (mod == 1) // PDF ➔ Word
                {
                    string fileName = Path.GetFileNameWithoutExtension(inputPath) + ".docx";
                    string outputPath = Path.Combine(outputFolder, fileName);

                    // 16 = wdFormatXMLDocument (Standart DOCX formatında kaydet)
                    document.SaveAs2(outputPath, 16);
                }
            }
            finally
            {
                // Arka planda takılı kalmaması için Word'ü temizleyip kapatıyoruz
                if (document != null)
                {
                    try { document.Close(0); } catch { } // 0 = wdDoNotSaveChanges (Değişiklikleri kaydetme)
                    Marshal.ReleaseComObject(document);
                }
                if (wordApp != null)
                {
                    try { wordApp.Quit(0); } catch { }
                    Marshal.ReleaseComObject(wordApp);
                }
            }
        }
    }
}