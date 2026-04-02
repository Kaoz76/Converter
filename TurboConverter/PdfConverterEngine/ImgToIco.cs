using System;
using System.IO;
using System.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace TurboConverter.PdfConverterEngine
{
    public static class ImgToIco
    {
        public static void Convert(string[] inputFiles, string outputFolder, CancellationToken token)
        {
            foreach (var file in inputFiles)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    string ext = Path.GetExtension(file).ToLower();
                    if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".webp" && ext != ".bmp")
                        continue;

                    // 1. Resmi WPF ile belleğe yükle
                    BitmapImage bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri(file);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Arka plan işlemine (Thread) uygun hale getir

                    // 2. İkon standardı olan 256x256 boyutuna zorla ölçekle
                    var resized = new TransformedBitmap(bitmap, new ScaleTransform(256.0 / bitmap.PixelWidth, 256.0 / bitmap.PixelHeight));

                    // 3. PNG formatına çevir (Modern ICO dosyaları içinde PNG barındırır)
                    PngBitmapEncoder encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(resized));

                    using (MemoryStream pngStream = new MemoryStream())
                    {
                        encoder.Save(pngStream);
                        byte[] pngBytes = pngStream.ToArray();

                        string baseName = Path.GetFileNameWithoutExtension(file);
                        string outPath = Path.Combine(outputFolder, $"{baseName}.ico");

                        // 4. Saf C# ile ICO dosyasının matematiksel başlığını (Header) yaz ve kaydet
                        using (FileStream fs = new FileStream(outPath, FileMode.Create))
                        using (BinaryWriter bw = new BinaryWriter(fs))
                        {
                            // ICO Başlığı (6 Bayt)
                            bw.Write((short)0); // Rezerve
                            bw.Write((short)1); // Tip: 1 = ICO
                            bw.Write((short)1); // İçindeki resim sayısı: 1

                            // Dizin Girişi (16 Bayt)
                            bw.Write((byte)0);  // Genişlik: 0 (256 piksel anlamına gelir)
                            bw.Write((byte)0);  // Yükseklik: 0 (256 piksel anlamına gelir)
                            bw.Write((byte)0);  // Renk Paleti
                            bw.Write((byte)0);  // Rezerve
                            bw.Write((short)1); // Renk Düzlemi
                            bw.Write((short)32); // Bit Derinliği (32-bit saydam destekli)
                            bw.Write((int)pngBytes.Length); // Resmin boyutu
                            bw.Write((int)22);  // Başlıkların toplam boyutu (Offset)

                            // Gerçek resim verisini yaz
                            bw.Write(pngBytes);
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"ICO Çevirme Hatası ({file}): {ex.Message}");
                }
            }
        }
    }
}