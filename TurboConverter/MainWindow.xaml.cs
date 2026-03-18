using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Media;
using System.Text.Json;
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
        private readonly string _ayarlarDosyaYolu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TurboConverter", "settings.json");

        
        public MainWindow()
        {
            InitializeComponent();
            QuestPDF.Settings.License = LicenseType.Community;
            
            // Uygulama açılırken ayarları yükle
            AyarlariYukle(); 
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            // Uygulama kapanırken son ayarları diske kaydet
            AyarlariKaydet();
            base.OnClosing(e);
        }

        private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Multiselect = true };

            if (RbPdfToJpg.IsChecked == true || RbPdfToText.IsChecked == true)
                dialog.Filter = "PDF Dosyaları (*.pdf)|*.pdf";
            else if (RbTextToPdf.IsChecked == true)
                dialog.Filter = "Metin Dosyaları (*.txt)|*.txt";
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
            TxtDurumMetni.Text = "İşleniyor...";
            ProgressBarIlerleme.Value = 0;
            BtnOpenFolder.Visibility = Visibility.Collapsed;
            
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

            double kaliteCarpani = 2.0; 
            if (CmbQuality.SelectedIndex == 0) kaliteCarpani = 1.0;
            else if (CmbQuality.SelectedIndex == 1) kaliteCarpani = 2.0;
            else if (CmbQuality.SelectedIndex == 2) kaliteCarpani = 4.0;
            else if (CmbQuality.SelectedIndex == 3) kaliteCarpani = 6.0;

            string? firstFileDir = Path.GetDirectoryName(tumDosyalar[0]);
            if (firstFileDir == null) firstFileDir = paths[0]; 

            string klasorAdi = RbPdfToJpg.IsChecked == true ? "Converted_Jpgs" : "Extracted_Texts";
            _sonCiktiKlasoru = (RbPdfToJpg.IsChecked == true || RbPdfToText.IsChecked == true) 
                ? GetUniquePath(Path.Combine(firstFileDir, klasorAdi), true) 
                : firstFileDir;

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            try
            {
                await Task.Run(() =>
                {
                    if (RbPdfToJpg.IsChecked == true || RbPdfToText.IsChecked == true)
                    {
                        Directory.CreateDirectory(_sonCiktiKlasoru);
                        var pOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token };

                        Parallel.ForEach(tumDosyalar, pOptions, (file) =>
                        {
                            token.ThrowIfCancellationRequested();

                            if (Path.GetExtension(file).ToLower() == ".pdf")
                            {
                                if (RbPdfToJpg.IsChecked == true) ConvertPdfToJpg(file, _sonCiktiKlasoru, kaliteCarpani, token);
                                else ConvertPdfToText(file, _sonCiktiKlasoru, token);
                            }
                            UIGuncelle(Interlocked.Increment(ref islenenDosya), toplamDosya);
                        });
                    }
                    else if (RbImgToPdf.IsChecked == true)
                    {
                        ConvertImagesToPdf(tumDosyalar, _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya);
                    }
                    else if (RbTextToPdf.IsChecked == true)
                    {
                        ConvertTextToPdf(tumDosyalar, _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya);
                    }
                }, token);

                TxtDurumMetni.Text = "İşlem Başarıyla Tamamlandı!";
                BtnOpenFolder.Visibility = Visibility.Visible;

                SystemSounds.Asterisk.Play(); 
                MessageBox.Show("Dönüştürme işlemi tamamlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                TxtDurumMetni.Text = "İşlem Kullanıcı Tarafından İptal Edildi!";
                TxtIlerlemeYuzdesi.Text = "İptal Edildi";
                
                SystemSounds.Exclamation.Play(); 
                MessageBox.Show("İşlem iptal edildi. Oluşturulan bazı dosyalar klasörde kalmış olabilir.", "İptal", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                TxtDurumMetni.Text = "Bir hata oluştu!";
                
                SystemSounds.Hand.Play(); 
                MessageBox.Show($"Beklenmeyen bir hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                if (_cancellationTokenSource != null)
                {
                    _cancellationTokenSource.Dispose();
                    _cancellationTokenSource = null;
                }
                
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
                throw; 
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF Dönüştürme Hatası: {ex.Message}");
            }
        }

        private void ConvertImagesToPdf(List<string> imageFiles, string outputFolder, CancellationToken token)
        {
            if (imageFiles.Count == 0) return;
            string basePdfPath = Path.Combine(outputFolder, "Merged_Images.pdf");
            string outputPdfPath = GetUniquePath(basePdfPath, false); 

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
                throw; 
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF Oluşturma Hatası: {ex.Message}");
            }
        }

        private void ConvertPdfToText(string pdfPath, string outputFolder, CancellationToken token)
        {
            try
            {
                using var docReader = DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(1.0));
                int pageCount = docReader.GetPageCount();

                string baseOutput = Path.Combine(outputFolder, $"{Path.GetFileNameWithoutExtension(pdfPath)}.txt");
                string outputFile = GetUniquePath(baseOutput, false);

                using var writer = new StreamWriter(outputFile);

                for (int i = 0; i < pageCount; i++)
                {
                    token.ThrowIfCancellationRequested();
                    using var pageReader = docReader.GetPageReader(i);
                    
                    string text = pageReader.GetText();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        writer.WriteLine($"--- Sayfa {i + 1} ---");
                        writer.WriteLine(text);
                        writer.WriteLine();
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"Metin Çıkarma Hatası: {ex.Message}"); }
        }

        private void ConvertTextToPdf(List<string> textFiles, string outputFolder, CancellationToken token)
        {
            if (textFiles.Count == 0) return;
            
            string basePdfPath = Path.Combine(outputFolder, "Merged_Texts.pdf");
            string outputPdfPath = GetUniquePath(basePdfPath, false);

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
                                page.DefaultTextStyle(x => x.FontSize(12));
                                page.Content().Text(content);
                            });
                        }
                    }
                }).GeneratePdf(outputPdfPath);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"PDF Oluşturma Hatası: {ex.Message}"); }
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

        private string GetUniquePath(string path, bool isDirectory)
        {
            if (isDirectory)
            {
                if (!Directory.Exists(path)) return path;
                
                int count = 1;
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string name = Path.GetFileName(path);
                
                string newPath;
                do
                {
                    newPath = Path.Combine(dir, $"{name} ({count})");
                    count++;
                } while (Directory.Exists(newPath));
                
                return newPath;
            }
            else
            {
                if (!File.Exists(path)) return path;
                
                int count = 1;
                string dir = Path.GetDirectoryName(path) ?? string.Empty;
                string name = Path.GetFileNameWithoutExtension(path);
                string ext = Path.GetExtension(path);
                
                string newPath;
                do
                {
                    newPath = Path.Combine(dir, $"{name} ({count}){ext}");
                    count++;
                } while (File.Exists(newPath));
                
                return newPath;
            }
        }

        private void AyarlariYukle()
        {
            try
            {
                if (File.Exists(_ayarlarDosyaYolu))
                {
                    string json = File.ReadAllText(_ayarlarDosyaYolu);
                    var ayarlar = JsonSerializer.Deserialize<AppSettings>(json);
                    
                    if (ayarlar != null)
                    {
                        
                        CmbQuality.SelectedIndex = ayarlar.SeciliKalite;

                        
                        switch (ayarlar.SeciliMod)
                        {
                            case 0: RbPdfToJpg.IsChecked = true; break;
                            case 1: RbImgToPdf.IsChecked = true; break;
                            case 2: RbPdfToText.IsChecked = true; break;
                            case 3: RbTextToPdf.IsChecked = true; break;
                        }
                    }
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Ayarlar yüklenemedi, varsayılanlar kullanılacak.");
            }
        }

        private void AyarlariKaydet()
        {
            try
            {
                var ayarlar = new AppSettings
                {
                    SeciliKalite = CmbQuality.SelectedIndex,
                    SeciliMod = RbPdfToJpg.IsChecked == true ? 0 :
                                RbImgToPdf.IsChecked == true ? 1 :
                                RbPdfToText.IsChecked == true ? 2 : 3
                };

               
                Directory.CreateDirectory(Path.GetDirectoryName(_ayarlarDosyaYolu)!);
                
                
                string json = JsonSerializer.Serialize(ayarlar);
                File.WriteAllText(_ayarlarDosyaYolu, json);
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Ayarlar kaydedilemedi.");
            }
        }
    } 

    
    public class AppSettings
    {
        public int SeciliMod { get; set; } = 0; 
        public int SeciliKalite { get; set; } = 2; 
    }
}