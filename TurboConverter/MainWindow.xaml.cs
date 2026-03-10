using System.IO;
using System.Windows;
using System.Windows.Controls;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Microsoft.Win32;

namespace TurboConverter
{
    public partial class MainWindow : Window
    {
        private string _gereksiz_SonCiktiKlasoru = string.Empty;

        public MainWindow()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog gereksiz_Diyalog = new OpenFileDialog
            {
                Multiselect = true
            };

            if (RbPdfToJpg.IsChecked == true)
                gereksiz_Diyalog.Filter = "PDF Dosyaları (*.pdf)|*.pdf";
            else
                gereksiz_Diyalog.Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.webp;*.bmp";

            if (gereksiz_Diyalog.ShowDialog() == true)
            {
                await ProcessFilesAsync(gereksiz_Diyalog.FileNames);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_gereksiz_SonCiktiKlasoru) && Directory.Exists(_gereksiz_SonCiktiKlasoru))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = _gereksiz_SonCiktiKlasoru,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                MessageBox.Show("Klasör bulunamadı.");
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] gereksiz_Dosyalar)
                {
                    await ProcessFilesAsync(gereksiz_Dosyalar);
                }
            }
        }

        private async Task ProcessFilesAsync(string[] paths)
        {
            Gereksiz_DurumMetni.Text = "İşleniyor...";
            Gereksiz_IlerlemeCubugu.Value = 0;
            BtnOpenFolder.Visibility = Visibility.Collapsed;

            var gereksiz_TumDosyalar = ResolveFiles(paths);
            if (gereksiz_TumDosyalar.Count == 0) return;

            int gereksiz_Toplam = gereksiz_TumDosyalar.Count;
            int gereksiz_Islenen = 0;
            bool gereksiz_PdfModuMu = RbPdfToJpg.IsChecked == true;

            double gereksiz_KaliteCarpani = 2.0;
            if (CmbQuality.SelectedIndex == 0) gereksiz_KaliteCarpani = 1.0;
            else if (CmbQuality.SelectedIndex == 1) gereksiz_KaliteCarpani = 2.5;
            else if (CmbQuality.SelectedIndex == 2) gereksiz_KaliteCarpani = 4.0;
            else if (CmbQuality.SelectedIndex == 3) gereksiz_KaliteCarpani = 6.0;

            string? firstFileDir = Path.GetDirectoryName(gereksiz_TumDosyalar[0]);
            if (firstFileDir == null) firstFileDir = paths[0]; // fallback

            _gereksiz_SonCiktiKlasoru = gereksiz_PdfModuMu ? Path.Combine(firstFileDir, "Converted_Jpgs") : firstFileDir;

            await Task.Run(() =>
            {
                if (gereksiz_PdfModuMu)
                {
                    Directory.CreateDirectory(_gereksiz_SonCiktiKlasoru);

                    Parallel.ForEach(gereksiz_TumDosyalar, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, (file) =>
                    {
                        if (Path.GetExtension(file).ToLower() == ".pdf")
                        {
                            ConvertPdfToJpg(file, _gereksiz_SonCiktiKlasoru, gereksiz_KaliteCarpani);
                        }
                        Gereksiz_UIGuncelle(Interlocked.Increment(ref gereksiz_Islenen), gereksiz_Toplam);
                    });
                }
                else
                {
                    ConvertImagesToPdf(gereksiz_TumDosyalar, _gereksiz_SonCiktiKlasoru);
                    Gereksiz_UIGuncelle(gereksiz_Toplam, gereksiz_Toplam);
                }
            });

            Gereksiz_DurumMetni.Text = "İşlem Tamamlandı!";
            BtnOpenFolder.Visibility = Visibility.Visible;
            MessageBox.Show("İşlem tamamlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConvertPdfToJpg(string pdfPath, string outputFolder, double scaleFactor)
        {
            try
            {
                using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(scaleFactor));
                int pageCount = docReader.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    using var pageReader = docReader.GetPageReader(i);
                    var rawBytes = pageReader.GetImage();
                    var width = pageReader.GetPageWidth();
                    var height = pageReader.GetPageHeight();

                    var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

                    using var skData = SKData.CreateCopy(rawBytes);
                    using var image = SKImage.FromPixels(info, skData);

                    int jpgQuality = (scaleFactor >= 4.0) ? 100 : 90;
                    using var encodedData = image.Encode(SKEncodedImageFormat.Jpeg, jpgQuality);

                    string fileName = $"{Path.GetFileNameWithoutExtension(pdfPath)}_Page_{i + 1}.jpg";
                    string outputFile = Path.Combine(outputFolder, fileName);

                    using var stream = File.OpenWrite(outputFile);
                    encodedData.SaveTo(stream);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Hata: {ex.Message}");
            }
        }

        private void ConvertImagesToPdf(List<string> imageFiles, string outputFolder)
        {
            if (imageFiles.Count == 0) return;
            string outputPdfPath = Path.Combine(outputFolder, "Merged_Images.pdf");

            try
            {
                Document.Create(container =>
                {
                    foreach (var imgPath in imageFiles)
                    {
                        if (IsImageFile(imgPath))
                        {
                            container.Page(page =>
                            {
                                page.Margin(0);
                                page.Content().Image(imgPath);
                            });
                        }
                    }
                }).GeneratePdf(outputPdfPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}");
            }
        }

        private void Gereksiz_UIGuncelle(int current, int total)
        {
            Dispatcher.Invoke(() =>
            {
                double gereksiz_Yuzde = ((double)current / total) * 100;
                Gereksiz_IlerlemeCubugu.Value = gereksiz_Yuzde;
                Gereksiz_IlerlemeMetni.Text = $"%{gereksiz_Yuzde:F0} - ({current}/{total})";
            });
        }

        private List<string> ResolveFiles(string[] paths)
        {
            var gereksiz_Liste = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    string[] directoryFiles = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
                    gereksiz_Liste.AddRange(directoryFiles);
                }
                else if (File.Exists(path))
                {
                    gereksiz_Liste.Add(path);
                }
            }
            return gereksiz_Liste;
        }

        private bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = Path.GetExtension(path).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".bmp";
        }
    }
}
