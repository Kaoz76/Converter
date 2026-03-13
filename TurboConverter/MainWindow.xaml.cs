using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace TurboConverter
{
    public partial class MainWindow : Window
    {
        private string _sonCiktiKlasoru = string.Empty;
        private CancellationTokenSource? _cancellationTokenSource;

        public MainWindow()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
        }

        private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog
            {
                Multiselect = true
            };

            if (RbPdfToJpg.IsChecked == true)
                dialog.Filter = "PDF Dosyaları (*.pdf)|*.pdf";
            else
                dialog.Filter = "Resim Dosyaları|*.jpg;*.jpeg;*.png;*.webp;*.bmp";

            if (dialog.ShowDialog() == true)
            {
                await ProcessFilesAsync(dialog.FileNames);
            }
        }

        private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_sonCiktiKlasoru) && Directory.Exists(_sonCiktiKlasoru))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo()
                {
                    FileName = _sonCiktiKlasoru,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            else
            {
                MessageBox.Show("Klasör bulunamadı veya henüz oluşturulmadı.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                _cancellationTokenSource.Cancel();
                TxtDurumMetni.Text = "İptal ediliyor, lütfen bekleyin...";
                BtnCancel.IsEnabled = false; // Üst üste basılmasını engelle
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                if (e.Data.GetData(DataFormats.FileDrop) is string[] dosyalar)
                {
                    await ProcessFilesAsync(dosyalar);
                }
            }
        }

        private async Task ProcessFilesAsync(string[] paths)
        {
            // Arayüzü işlem başlangıcına göre hazırla
            TxtDurumMetni.Text = "İşleniyor...";
            ProgressBarIlerleme.Value = 0;
            BtnOpenFolder.Visibility = Visibility.Collapsed;
            
            // İptal butonunu görünür ve aktif yap
            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;

            var tumDosyalar = ResolveFiles(paths);
            if (tumDosyalar.Count == 0)
            {
                BtnCancel.Visibility = Visibility.Collapsed;
                return;
            }

            int toplamDosya = tumDosyalar.Count;
            int islenenDosya = 0;
            bool pdfModuMu = RbPdfToJpg.IsChecked == true;

            double kaliteCarpani = 2.0; // Varsayılan Orta kalite
            if (CmbQuality.SelectedIndex == 0) kaliteCarpani = 1.0;
            else if (CmbQuality.SelectedIndex == 1) kaliteCarpani = 2.0;
            else if (CmbQuality.SelectedIndex == 2) kaliteCarpani = 4.0;
            else if (CmbQuality.SelectedIndex == 3) kaliteCarpani = 6.0;

            string? firstFileDir = Path.GetDirectoryName(tumDosyalar[0]);
            if (firstFileDir == null) firstFileDir = paths[0]; 

            _sonCiktiKlasoru = pdfModuMu ? Path.Combine(firstFileDir, "Converted_Jpgs") : firstFileDir;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                await Task.Run(() =>
                {
                    if (pdfModuMu)
                    {
                        Directory.CreateDirectory(_sonCiktiKlasoru);

                        var parallelOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Environment.ProcessorCount,
                            CancellationToken = token
                        };

                        Parallel.ForEach(tumDosyalar, parallelOptions, (file) =>
                        {
                            token.ThrowIfCancellationRequested();

                            if (Path.GetExtension(file).ToLower() == ".pdf")
                            {
                                ConvertPdfToJpg(file, _sonCiktiKlasoru, kaliteCarpani, token);
                            }
                            UIGuncelle(Interlocked.Increment(ref islenenDosya), toplamDosya);
                        });
                    }
                    else
                    {
                        ConvertImagesToPdf(tumDosyalar, _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya);
                    }
                }, token);

                TxtDurumMetni.Text = "İşlem Başarıyla Tamamlandı!";
                BtnOpenFolder.Visibility = Visibility.Visible;
                MessageBox.Show("Dönüştürme işlemi tamamlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                TxtDurumMetni.Text = "İşlem Kullanıcı Tarafından İptal Edildi!";
                TxtIlerlemeYuzdesi.Text = "İptal Edildi";
                MessageBox.Show("İşlem iptal edildi. Oluşturulan bazı dosyalar klasörde kalmış olabilir.", "İptal", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                TxtDurumMetni.Text = "Bir hata oluştu!";
                MessageBox.Show($"Beklenmeyen bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                
                // İşlem bittiğinde iptal butonunu gizle
                BtnCancel.Visibility = Visibility.Collapsed;
            }
        }

        private void ConvertPdfToJpg(string pdfPath, string outputFolder, double scaleFactor, CancellationToken token)
        {
            try
            {
                using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(scaleFactor));
                int pageCount = docReader.GetPageCount();

                for (int i = 0; i < pageCount; i++)
                {
                    token.ThrowIfCancellationRequested();

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
            catch (OperationCanceledException)
            {
                throw; // İptal fırlatmasını üst bloğa ilet
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF Dönüştürme Hatası: {ex.Message}");
            }
        }

        private void ConvertImagesToPdf(List<string> imageFiles, string outputFolder, CancellationToken token)
        {
            if (imageFiles.Count == 0) return;
            string outputPdfPath = Path.Combine(outputFolder, "Merged_Images.pdf");

            try
            {
                Document.Create(container =>
                {
                    foreach (var imgPath in imageFiles)
                    {
                        token.ThrowIfCancellationRequested();

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
            catch (OperationCanceledException)
            {
                throw; // İptal fırlatmasını üst bloğa ilet
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF Oluşturma Hatası: {ex.Message}");
            }
        }

        private void UIGuncelle(int current, int total)
        {
            Dispatcher.Invoke(() =>
            {
                double yuzde = ((double)current / total) * 100;
                ProgressBarIlerleme.Value = yuzde;
                TxtIlerlemeYuzdesi.Text = $"%{yuzde:F0} - ({current} / {total} Dosya)";
            });
        }

        private List<string> ResolveFiles(string[] paths)
        {
            var liste = new List<string>();
            foreach (var path in paths)
            {
                if (Directory.Exists(path))
                {
                    string[] directoryFiles = Directory.GetFiles(path, "*.*", SearchOption.TopDirectoryOnly);
                    liste.AddRange(directoryFiles);
                }
                else if (File.Exists(path))
                {
                    liste.Add(path);
                }
            }
            return liste;
        }

        private bool IsImageFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            var ext = Path.GetExtension(path).ToLower();
            return ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".webp" || ext == ".bmp";
        }
    }
}