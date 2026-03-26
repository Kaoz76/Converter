using System;
using System.IO;
using System.Threading;
using SkiaSharp;
using Docnet.Core.Models;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfToJpg
    {
        public static void Convert(string pdfPath, string outputFolder, double scaleFactor, CancellationToken token)
        {
            try
            {
                using var docReader = PdfSecurity.GetDocReaderSafe(pdfPath, scaleFactor);
                int pageCount = docReader.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    token.ThrowIfCancellationRequested();
                    using var pageReader = docReader.GetPageReader(i);
                    var rawBytes = pageReader.GetImage();

                    var info = new SKImageInfo(pageReader.GetPageWidth(), pageReader.GetPageHeight(), SKColorType.Bgra8888, SKAlphaType.Premul);
                    using var skData = SKData.CreateCopy(rawBytes);
                    using var pdfImage = SKImage.FromPixels(info, skData);

                    using var surface = SKSurface.Create(info);
                    using var canvas = surface.Canvas;
                    canvas.Clear(SKColors.White);
                    canvas.DrawImage(pdfImage, 0, 0);

                    using var finalImage = surface.Snapshot();
                    int jpgQuality = (scaleFactor >= 4.0) ? 100 : 90;
                    using var encodedData = finalImage.Encode(SKEncodedImageFormat.Jpeg, jpgQuality);

                    string outputFile = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(pdfPath)}_Page_{i + 1}.jpg");
                    using var stream = File.OpenWrite(outputFile);
                    encodedData.SaveTo(stream);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PDF Dönüştürme Hatası: {ex.Message}"); }
        }
    }
}