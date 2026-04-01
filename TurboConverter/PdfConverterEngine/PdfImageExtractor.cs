using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using UglyToad.PdfPig;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfImageExtractor
    {
        public static void Extract(string pdfPath, string outputFolder, string sifre, CancellationToken token)
        {
            try
            {
                // Şifre koruması varsa PdfPig'e bildiriyoruz
                var options = new ParsingOptions();
                if (!string.IsNullOrEmpty(sifre))
                {
                    options.Passwords = new List<string> { sifre };
                }

                // PDF'i açıyoruz
                using (var document = PdfDocument.Open(pdfPath, options))
                {
                    string baseName = Path.GetFileNameWithoutExtension(pdfPath);
                    int imageIndex = 1;

                    // Tüm sayfaları tek tek gez
                    foreach (var page in document.GetPages())
                    {
                        token.ThrowIfCancellationRequested();

                        // Sayfanın içindeki tüm gömülü resimleri bul
                        foreach (var image in page.GetImages())
                        {
                            token.ThrowIfCancellationRequested();

                            // 1. Önce kayıpsız PNG olarak çıkarmayı dene
                            if (image.TryGetPng(out byte[] pngBytes))
                            {
                                string outPath = Path.Combine(outputFolder, $"{baseName}_Resim_{imageIndex}.png");
                                File.WriteAllBytes(outPath, pngBytes);
                                imageIndex++;
                            }
                            // 2. Eğer PNG değilse (bu durumda genellikle orijinal ham JPEG'dir),
                            // hata veren TryGetBytes yerine her sürümde çalışan RawBytes'ı kullanıyoruz.
                            else if (image.RawBytes != null)
                            {
                                string outPath = Path.Combine(outputFolder, $"{baseName}_Resim_{imageIndex}.jpg");
                                File.WriteAllBytes(outPath, image.RawBytes.ToArray());
                                imageIndex++;
                            }
                        }
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Resim Ayıklama Hatası: {ex.Message}"); }
        }
    }
}