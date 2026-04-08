using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace TurboConverter.PdfConverterEngine
{
    public static class ExcelToPdf
    {
        public static void Convert(string inputPath, string outputFolder, CancellationToken token)
        {
            // 1. KORUMA KALKANI: Excel yüklü mü kontrol et
            Type? excelType = Type.GetTypeFromProgID("Excel.Application");
            if (excelType == null)
            {
                throw new Exception("Sisteminizde Microsoft Excel bulunamadı!\nBu özellik, tabloları düzgün çizebilmek için arka planda Excel'in yüklü olmasını gerektirir.");
            }

            dynamic? excelApp = null;
            dynamic? workbooks = null;
            dynamic? workbook = null;

            try
            {
                token.ThrowIfCancellationRequested();

                // 2. Excel'i arka planda görünmez olarak başlat
                excelApp = Activator.CreateInstance(excelType);

                // Uyarıları sustur (Örn: "Bu dosya internetten indirildi, açmak güvenli mi?" uyarısında kilitlenmemesi için)
                excelApp.DisplayAlerts = false;
                excelApp.Visible = false;

                workbooks = excelApp.Workbooks;
                // Dosyayı sadece okunur modda açıyoruz
                workbook = workbooks.Open(inputPath, ReadOnly: true);

                token.ThrowIfCancellationRequested();

                string fileName = Path.GetFileNameWithoutExtension(inputPath) + ".pdf";
                string outputPath = Path.Combine(outputFolder, fileName);

                // 3. Dosyayı PDF olarak kaydet (0 = xlTypePDF format kodu)
                workbook.ExportAsFixedFormat(0, outputPath);
            }
            finally
            {
                // 4. TEMİZLİK: Excel'in görev yöneticisinde asılı kalmasını engelle
                if (workbook != null)
                {
                    try { workbook.Close(false); } catch { } // false = Değişiklikleri kaydetme
                    Marshal.ReleaseComObject(workbook);
                }
                if (workbooks != null)
                {
                    try { Marshal.ReleaseComObject(workbooks); } catch { }
                }
                if (excelApp != null)
                {
                    try { excelApp.Quit(); } catch { }
                    Marshal.ReleaseComObject(excelApp);
                }
            }
        }
    }
}