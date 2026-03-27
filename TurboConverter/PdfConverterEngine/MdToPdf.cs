using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Windows;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TurboConverter.PdfConverterEngine
{
    public static class MdToPdf
    {
        public static void Convert(List<string> mdFiles, string outputFolder, bool sifreleAktif, string sifre, CancellationToken token)
        {
            if (mdFiles.Count == 0) return;
            string outputPdfPath = FileHelper.GetUniquePath(Path.Combine(outputFolder, "Merged_Markdown.pdf"), false);

            try
            {
                Document.Create(container =>
                {
                    foreach (var mdPath in mdFiles)
                    {
                        token.ThrowIfCancellationRequested();
                        if (Path.GetExtension(mdPath).ToLower() == ".md" || Path.GetExtension(mdPath).ToLower() == ".txt")
                        {
                            string[] lines = File.ReadAllLines(mdPath);

                            container.Page(page =>
                            {
                                page.Margin(50);
                                page.PageColor(QuestPDF.Helpers.Colors.White);
                                page.DefaultTextStyle(x => x.FontSize(12).FontFamily("Segoe UI").Fallback(f => f.FontFamily("Arial")));

                                // TEXT YERİNE COLUMN (SÜTUN) KULLANIYORUZ
                                page.Content().PaddingVertical(10).Column(column =>
                                {
                                    column.Spacing(5); // Satırlar arası varsayılan boşluk

                                    foreach (var line in lines)
                                    {
                                        if (string.IsNullOrWhiteSpace(line))
                                        {
                                            column.Item().Height(10); // Boş satır
                                            continue;
                                        }

                                        // Kutulara (Item) PaddingLeft ile boşluk verebiliyoruz!
                                        if (line.StartsWith("# "))
                                            column.Item().Text(line.Substring(2)).FontSize(24).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);

                                        else if (line.StartsWith("## "))
                                            column.Item().Text(line.Substring(3)).FontSize(20).Bold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken1);

                                        else if (line.StartsWith("### "))
                                            column.Item().Text(line.Substring(4)).FontSize(16).Bold();

                                        else if (line.StartsWith("- ") || line.StartsWith("* "))
                                            column.Item().PaddingLeft(15).Text("• " + line.Substring(2));

                                        else if (line.StartsWith("> "))
                                            column.Item().PaddingLeft(10).Text(line.Substring(2)).Italic().FontColor(QuestPDF.Helpers.Colors.Grey.Darken2);

                                        else
                                            column.Item().Text(line);
                                    }
                                });
                            });
                        }
                    }
                }).GeneratePdf(outputPdfPath);

                PdfSecurity.CiktiyiSifrele(outputPdfPath, sifreleAktif, sifre);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"Markdown Dönüştürme Hatası: {ex.Message}"); }
        }
    }
}