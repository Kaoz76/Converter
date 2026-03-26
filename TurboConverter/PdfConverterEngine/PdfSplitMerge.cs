using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using PdfSharp.Pdf;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfSplitMerge
    {
        public static void Split(string pdfPath, string outputFolder, bool sifreleAktif, string sifre, CancellationToken token)
        {
            try
            {
                using (PdfDocument inputDocument = PdfSecurity.GetPdfDocumentSafe(pdfPath))
                {
                    string name = Path.GetFileNameWithoutExtension(pdfPath);
                    for (int idx = 0; idx < inputDocument.PageCount; idx++)
                    {
                        token.ThrowIfCancellationRequested();
                        string outputPath = Path.Combine(outputFolder, $"{name}_Sayfa_{idx + 1}.pdf");

                        using (PdfDocument outputDocument = new PdfDocument())
                        {
                            outputDocument.Version = inputDocument.Version;
                            outputDocument.Info.Title = $"Sayfa {idx + 1}";
                            outputDocument.AddPage(inputDocument.Pages[idx]);
                            outputDocument.Save(outputPath);
                        }
                        PdfSecurity.CiktiyiSifrele(outputPath, sifreleAktif, sifre);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PDF Bölme Hatası: {ex.Message}"); }
        }

        public static void Merge(List<string> pdfFiles, string outputFolder, bool sifreleAktif, string sifre, CancellationToken token)
        {
            if (pdfFiles.Count < 2)
            {
                Application.Current.Dispatcher.Invoke(() => MessageBox.Show("Birleştirme işlemi için en az 2 adet PDF dosyası seçmelisiniz."));
                return;
            }

            string outputPath = FileHelper.GetUniquePath(Path.Combine(outputFolder, "Merged_Documents.pdf"), false);

            try
            {
                using (PdfDocument outputDocument = new PdfDocument())
                {
                    foreach (string file in pdfFiles)
                    {
                        if (Path.GetExtension(file).ToLower() != ".pdf") continue;
                        token.ThrowIfCancellationRequested();

                        using (PdfDocument inputDocument = PdfSecurity.GetPdfDocumentSafe(file))
                        {
                            for (int idx = 0; idx < inputDocument.PageCount; idx++)
                            {
                                token.ThrowIfCancellationRequested();
                                outputDocument.AddPage(inputDocument.Pages[idx]);
                            }
                        }
                    }
                    outputDocument.Save(outputPath);
                }
                PdfSecurity.CiktiyiSifrele(outputPath, sifreleAktif, sifre);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"PDF Birleştirme Hatası: {ex.Message}"); }
        }
    }
}