using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TurboConverter.PdfConverterEngine
{
    public static class ImgToPdf
    {
        public static void Convert(List<string> imageFiles, string outputFolder, bool sifreleAktif, string sifre, CancellationToken token)
        {
            if (imageFiles.Count == 0) return;
            string outputPdfPath = FileHelper.GetUniquePath(Path.Combine(outputFolder, "Merged_Images.pdf"), false);

            try
            {
                Document.Create(container =>
                {
                    foreach (var imgPath in imageFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        if (FileHelper.IsImageFile(imgPath))
                        {
                            container.Page(page =>
                            {
                                page.Size(QuestPDF.Helpers.PageSizes.A4);
                                page.Margin(0);
                                page.PageColor(QuestPDF.Helpers.Colors.White);
                                page.Content().AlignCenter().AlignMiddle().Image(imgPath).FitArea();
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