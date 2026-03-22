using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using Docnet.Core;
using Docnet.Core.Models;
using SkiaSharp;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

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
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            AyarlariYukle();
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            AyarlariKaydet();
            base.OnClosing(e);
        }

        // ==========================================
        // YENİ: KARANLIK MOD YÖNETİMİ
        // ==========================================
        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(ToggleTheme.IsChecked == true);
        }

        private void SetTheme(bool isDark)
        {
            var bgBrush = (SolidColorBrush)this.Resources["BgBrush"];
            var panelBgBrush = (SolidColorBrush)this.Resources["PanelBgBrush"];
            var textMainBrush = (SolidColorBrush)this.Resources["TextMainBrush"];
            var textMutedBrush = (SolidColorBrush)this.Resources["TextMutedBrush"];
            var borderBrushColor = (SolidColorBrush)this.Resources["BorderBrushColor"];

            if (isDark)
            {
                bgBrush.Color = (Color)ColorConverter.ConvertFromString("#121212");
                panelBgBrush.Color = (Color)ColorConverter.ConvertFromString("#1E1E1E");
                textMainBrush.Color = (Color)ColorConverter.ConvertFromString("#E0E0E0");
                textMutedBrush.Color = (Color)ColorConverter.ConvertFromString("#AAAAAA");
                borderBrushColor.Color = (Color)ColorConverter.ConvertFromString("#333333");
                ToggleTheme.Content = "☀️ Aydınlık Mod";
            }
            else
            {
                bgBrush.Color = (Color)ColorConverter.ConvertFromString("#F8F9FA");
                panelBgBrush.Color = Colors.White;
                textMainBrush.Color = (Color)ColorConverter.ConvertFromString("#2C3E50");
                textMutedBrush.Color = (Color)ColorConverter.ConvertFromString("#7F8C8D");
                borderBrushColor.Color = (Color)ColorConverter.ConvertFromString("#E0E0E0");
                ToggleTheme.Content = "🌙 Karanlık Mod";
            }
        }

        private void ChkSifrele_Click(object sender, RoutedEventArgs e)
        {
            TxtSifre.IsEnabled = ChkSifrele.IsChecked == true;
        }

        private async void BtnSelectFiles_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog dialog = new OpenFileDialog { Multiselect = true };

            if (RbPdfToJpg.IsChecked == true || RbPdfToText.IsChecked == true || RbPdfMerge.IsChecked == true || RbPdfSplit.IsChecked == true)
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
                Process.Start(new ProcessStartInfo() { FileName = _sonCiktiKlasoru, UseShellExecute = true, Verb = "open" });
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
                BtnCancel.IsEnabled = false;
            }
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] dosyalar)
            {
                await ProcessFilesAsync(dosyalar);
            }
        }

        private async Task ProcessFilesAsync(string[] paths)
        {
            TxtDurumMetni.Text = "İşleniyor...";
            ProgressBarIlerleme.Value = 0;
            TxtIlerlemeYuzdesi.Text = "Hesaplanıyor...";
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

            string? firstFileDir = Path.GetDirectoryName(tumDosyalar[0]) ?? paths[0];

            string klasorAdi = "";
            if (RbPdfToJpg.IsChecked == true) klasorAdi = "Converted_Jpgs";
            else if (RbPdfToText.IsChecked == true) klasorAdi = "Extracted_Texts";
            else if (RbPdfSplit.IsChecked == true) klasorAdi = "Split_Pages";

            if (!string.IsNullOrEmpty(klasorAdi))
            {
                _sonCiktiKlasoru = GetUniquePath(Path.Combine(firstFileDir, klasorAdi), true);
                Directory.CreateDirectory(_sonCiktiKlasoru);
            }
            else
            {
                _sonCiktiKlasoru = firstFileDir; 
            }

            _cancellationTokenSource = new CancellationTokenSource();
            var token = _cancellationTokenSource.Token;

            Stopwatch sw = Stopwatch.StartNew();

            try
            {
                await Task.Run(() =>
                {
                    var pOptions = new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount, CancellationToken = token };

                    if (RbPdfToJpg.IsChecked == true || RbPdfToText.IsChecked == true || RbPdfSplit.IsChecked == true)
                    {
                        Parallel.ForEach(tumDosyalar, pOptions, (file) =>
                        {
                            token.ThrowIfCancellationRequested();
                            if (Path.GetExtension(file).ToLower() == ".pdf")
                            {
                                if (RbPdfToJpg.IsChecked == true) ConvertPdfToJpg(file, _sonCiktiKlasoru, kaliteCarpani, token);
                                else if (RbPdfToText.IsChecked == true) ConvertPdfToText(file, _sonCiktiKlasoru, token);
                                else if (RbPdfSplit.IsChecked == true) SplitPdf(file, _sonCiktiKlasoru, token);
                            }
                            UIGuncelle(Interlocked.Increment(ref islenenDosya), toplamDosya, sw);
                        });
                    }
                    else if (RbImgToPdf.IsChecked == true)
                    {
                        ConvertImagesToPdf(tumDosyalar, _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                    else if (RbTextToPdf.IsChecked == true)
                    {
                        ConvertTextToPdf(tumDosyalar, _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                    else if (RbPdfMerge.IsChecked == true)
                    {
                        MergePdfs(tumDosyalar, _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                }, token);

                sw.Stop();
                TxtDurumMetni.Text = "İşlem Başarıyla Tamamlandı!";
                BtnOpenFolder.Visibility = Visibility.Visible;
                SystemSounds.Asterisk.Play(); 
                MessageBox.Show("İşlem tamamlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                TxtDurumMetni.Text = "İşlem İptal Edildi veya Şifre Atlandı!";
                TxtIlerlemeYuzdesi.Text = "İptal Edildi";
                SystemSounds.Exclamation.Play(); 
                MessageBox.Show("İşlem iptal edildi veya gerekli bir şifre girilmedi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                sw.Stop();
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

        private string SifreIste(string dosyaAdi)
        {
            string girilenSifre = string.Empty;
            Dispatcher.Invoke(() =>
            {
                Window dialog = new Window
                {
                    Title = "Kilitli PDF Algılandı",
                    Width = 400, Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow,
                    Background = System.Windows.Media.Brushes.WhiteSmoke
                };

                StackPanel panel = new StackPanel { Margin = new Thickness(20) };
                panel.Children.Add(new TextBlock { Text = $"'{Path.GetFileName(dosyaAdi)}' şifre ile korunuyor.\nOkuyabilmemiz için parolayı girin:", Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });

                PasswordBox passBox = new PasswordBox { Padding = new Thickness(5) };
                panel.Children.Add(passBox);

                StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
                Button btnCancel = new Button { Content = "İptal Et", Width = 80, Padding = new Thickness(5), Margin = new Thickness(0, 0, 10, 0) };
                btnCancel.Click += (s, e) => { dialog.DialogResult = false; };
                
                Button btnOk = new Button { Content = "Kilidi Aç", Width = 80, Padding = new Thickness(5), IsDefault = true, Background = System.Windows.Media.Brushes.LightBlue };
                btnOk.Click += (s, e) => { girilenSifre = passBox.Password; dialog.DialogResult = true; };

                btnPanel.Children.Add(btnCancel);
                btnPanel.Children.Add(btnOk);
                panel.Children.Add(btnPanel);
                dialog.Content = panel;
                
                dialog.ShowDialog();
            });
            return girilenSifre;
        }

        private void CiktiyiSifrele(string pdfPath)
        {
            bool sifreleAktif = false;
            string sifre = string.Empty;

            Dispatcher.Invoke(() =>
            {
                sifreleAktif = ChkSifrele.IsChecked == true;
                sifre = TxtSifre.Text;
            });

            if (sifreleAktif && !string.IsNullOrWhiteSpace(sifre) && File.Exists(pdfPath))
            {
                try
                {
                    using (PdfDocument document = PdfReader.Open(pdfPath, PdfDocumentOpenMode.Modify))
                    {
                        document.SecuritySettings.UserPassword = sifre;
                        document.SecuritySettings.OwnerPassword = sifre;
                        document.Save(pdfPath);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Çıktı şifreleme hatası: {ex.Message}");
                }
            }
        }

        private IDocReader GetDocReaderSafe(string pdfPath, double scaleFactor)
        {
            try
            {
                return DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(scaleFactor));
            }
            catch (Exception)
            {
                string sifre = SifreIste(pdfPath);
                if (string.IsNullOrEmpty(sifre)) throw new OperationCanceledException();
                return DocLib.Instance.GetDocReader(pdfPath, sifre, new PageDimensions(scaleFactor));
            }
        }

        private PdfDocument GetPdfDocumentSafe(string pdfPath)
        {
            try
            {
                return PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import);
            }
            catch (PdfReaderException)
            {
                string sifre = SifreIste(pdfPath);
                if (string.IsNullOrEmpty(sifre)) throw new OperationCanceledException();
                return PdfReader.Open(pdfPath, sifre, PdfDocumentOpenMode.Import);
            }
        }

        private void ConvertPdfToJpg(string pdfPath, string outputFolder, double scaleFactor, CancellationToken token)
        {
            try
            {
                using var docReader = GetDocReaderSafe(pdfPath, scaleFactor);
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
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PDF Dönüştürme Hatası: {ex.Message}"); }
        }

        private void ConvertImagesToPdf(List<string> imageFiles, string outputFolder, CancellationToken token)
        {
            if (imageFiles.Count == 0) return;
            string basePdfPath = Path.Combine(outputFolder, "Merged_Images.pdf");
            string outputPdfPath = GetUniquePath(basePdfPath, false); 

            try
            {
                QuestPDF.Fluent.Document.Create(container =>
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
                
                CiktiyiSifrele(outputPdfPath);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"PDF Oluşturma Hatası: {ex.Message}"); }
        }

        private void ConvertPdfToText(string pdfPath, string outputFolder, CancellationToken token)
        {
            try
            {
                using var docReader = GetDocReaderSafe(pdfPath, 1.0);
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
                QuestPDF.Fluent.Document.Create(container =>
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

                CiktiyiSifrele(outputPdfPath);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"PDF Oluşturma Hatası: {ex.Message}"); }
        }

        private void SplitPdf(string pdfPath, string outputFolder, CancellationToken token)
        {
            try
            {
                using (PdfDocument inputDocument = GetPdfDocumentSafe(pdfPath))
                {
                    string name = Path.GetFileNameWithoutExtension(pdfPath);
                    for (int idx = 0; idx < inputDocument.PageCount; idx++)
                    {
                        token.ThrowIfCancellationRequested();
                        string outputPath = Path.Combine(outputFolder, $"{name}_Sayfa_{idx + 1}.pdf");

                        using (PdfDocument outputDocument = new PdfDocument())
                        {
                            outputDocument.Version = inputDocument.Version;
                            outputDocument.Info.Title = $"Sayfa {idx + 1}";
                            outputDocument.AddPage(inputDocument.Pages[idx]);
                            outputDocument.Save(outputPath);
                        }
                        CiktiyiSifrele(outputPath);
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"PDF Bölme Hatası: {ex.Message}"); }
        }

        private void MergePdfs(List<string> pdfFiles, string outputFolder, CancellationToken token)
        {
            if (pdfFiles.Count < 2) 
            { 
                Dispatcher.Invoke(() => MessageBox.Show("Birleştirme işlemi için en az 2 adet PDF dosyası seçmelisiniz."));
                return; 
            }

            string basePdfPath = Path.Combine(outputFolder, "Merged_Documents.pdf");
            string outputPath = GetUniquePath(basePdfPath, false);

            try
            {
                using (PdfDocument outputDocument = new PdfDocument())
                {
                    foreach (string file in pdfFiles)
                    {
                        if (Path.GetExtension(file).ToLower() != ".pdf") continue;

                        token.ThrowIfCancellationRequested();
                        using (PdfDocument inputDocument = GetPdfDocumentSafe(file))
                        {
                            int count = inputDocument.PageCount;
                            for (int idx = 0; idx < count; idx++)
                            {
                                token.ThrowIfCancellationRequested();
                                outputDocument.AddPage(inputDocument.Pages[idx]);
                            }
                        }
                    }
                    outputDocument.Save(outputPath);
                }
                CiktiyiSifrele(outputPath);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex) { MessageBox.Show($"PDF Birleştirme Hatası: {ex.Message}"); }
        }

        private void UIGuncelle(int current, int total, Stopwatch sw)
        {
            Dispatcher.Invoke(() =>
            {
                double yuzde = ((double)current / total) * 100;
                ProgressBarIlerleme.Value = yuzde;

                string kalanMetin = "";
                if (current > 0 && current < total)
                {
                    double gecenSaniye = sw.Elapsed.TotalSeconds;
                    double dosyaBasinaSure = gecenSaniye / current; 
                    double tahminiKalanSaniye = dosyaBasinaSure * (total - current);

                    if (tahminiKalanSaniye > 60)
                    {
                        TimeSpan ts = TimeSpan.FromSeconds(tahminiKalanSaniye);
                        kalanMetin = $" | Kalan Süre: {ts.Minutes} dk {ts.Seconds} sn";
                    }
                    else if (tahminiKalanSaniye > 0)
                    {
                        kalanMetin = $" | Kalan Süre: {Math.Ceiling(tahminiKalanSaniye)} sn";
                    }
                }
                else if (current == total)
                {
                    kalanMetin = " | Tamamlandı!";
                }

                TxtIlerlemeYuzdesi.Text = $"%{yuzde:F0} - ({current} / {total} Dosya){kalanMetin}";
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
                        ChkSifrele.IsChecked = ayarlar.SifreleAktif;
                        ChkSifrele_Click(null, null);

                        ToggleTheme.IsChecked = ayarlar.KaranlikModAktif;
                        SetTheme(ayarlar.KaranlikModAktif); // Temayı uygula

                        switch (ayarlar.SeciliMod)
                        {
                            case 0: RbPdfToJpg.IsChecked = true; break;
                            case 1: RbImgToPdf.IsChecked = true; break;
                            case 2: RbPdfToText.IsChecked = true; break;
                            case 3: RbTextToPdf.IsChecked = true; break;
                            case 4: RbPdfMerge.IsChecked = true; break;
                            case 5: RbPdfSplit.IsChecked = true; break;
                        }
                    }
                }
            }
            catch
            {
                System.Diagnostics.Debug.WriteLine("Ayarlar yüklenemedi.");
            }
        }

        private void AyarlariKaydet()
        {
            try
            {
                var ayarlar = new AppSettings
                {
                    SeciliKalite = CmbQuality.SelectedIndex,
                    SifreleAktif = ChkSifrele.IsChecked == true,
                    KaranlikModAktif = ToggleTheme.IsChecked == true, // Temayı kaydet
                    SeciliMod = RbPdfToJpg.IsChecked == true ? 0 :
                                RbImgToPdf.IsChecked == true ? 1 :
                                RbPdfToText.IsChecked == true ? 2 :
                                RbTextToPdf.IsChecked == true ? 3 :
                                RbPdfMerge.IsChecked == true ? 4 : 5
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
        public bool SifreleAktif { get; set; } = false;
        public bool KaranlikModAktif { get; set; } = false; // YENİ: Tema tercihi
    }
}