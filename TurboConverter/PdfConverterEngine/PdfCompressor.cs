using System;
using System.IO;
using System.Threading;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfCompressor
    {
        public static void Compress(string inputPath, string outputFolder, CancellationToken token)
        {
            try
            {
                // PDF'i düzenleme (Modify) modunda açıyoruz ki içini temizleyebilelim
                using (PdfDocument document = PdfReader.Open(inputPath, PdfDocumentOpenMode.Modify))
                {
                    token.ThrowIfCancellationRequested();

                    // Kayıpsız ve Yapısal Sıkıştırma Ayarlarını Maksimuma Çekiyoruz
                    document.Options.CompressContentStreams = true;
                    document.Options.FlateEncodeMode = PdfFlateEncodeMode.BestCompression;
                    document.Options.UseFlateDecoderForJpegImages = PdfUseFlateDecoderForJpegImages.Automatic;
                    document.Options.NoCompression = false;

                    string fileName = Path.GetFileNameWithoutExtension(inputPath) + "_Compressed.pdf";
                    string outputPath = Path.Combine(outputFolder, fileName);

                    // Belgeyi kaydederken PdfSharp otomatik olarak ölü objeleri temizler
                    document.Save(outputPath);
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Sıkıştırma Hatası: {ex.Message}");
            }
        }
    }
}