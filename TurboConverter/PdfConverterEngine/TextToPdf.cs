using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TurboConverter.PdfConverterEngine
{
    public static class TextToPdf
    {
        public static void Convert(List<string> textFiles, string outputFolder, bool sifreleAktif, string sifre, CancellationToken token)
        {
            if (textFiles.Count == 0) return;
            string outputPdfPath = FileHelper.GetUniquePath(Path.Combine(outputFolder, "Merged_Texts.pdf"), false);

            try
            {
                Document.Create(container =>
                {
                    foreach (var txtPath in textFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        if (Path.GetExtension(txtPath).ToLower() == ".txt")
                        {
                            string content = File.ReadAllText(txtPath);
                            container.Page(page =>
                            {
                                page.Margin(40);
                                page.PageColor(QuestPDF.Helpers.Colors.White);
                                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Segoe UI").Fallback(f => f.FontFamily("Arial")).Fallback(f => f.FontFamily("Tahoma")));
                                page.Content().Text(content);
                            });
                        }
                    }
                }).GeneratePdf(outputPdfPath);

                PdfSecurity.CiktiyiSifrele(outputPdfPath, sifreleAktif, sifre);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"PDF Oluşturma Hatası: {ex.Message}"); }
        }
    }
}