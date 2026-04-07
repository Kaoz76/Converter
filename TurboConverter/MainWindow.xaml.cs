using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using QuestPDF.Infrastructure;

// Motorlarımızın olduğu klasörü koda dahil ediyoruz
using TurboConverter.PdfConverterEngine;

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
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            AyarlariYukle();
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            AyarlariKaydet();
            base.OnClosing(e);
        }

        // ==========================================
        // ARAYÜZ (UI) VE TEMA ETKİLEŞİMLERİ
        // ==========================================
        private void ToggleTheme_Click(object sender, RoutedEventArgs e)
        {
            SetTheme(ToggleTheme.IsChecked == true);
        }

        private void SetTheme(bool isDark)
        {
            if (isDark)
            {
                this.Resources["BgBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#121212"));
                this.Resources["PanelBgBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#1E1E1E"));
                this.Resources["TextMainBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#E0E0E0"));
                this.Resources["TextMutedBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#AAAAAA"));
                this.Resources["BorderBrushColor"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#333333"));
                ToggleTheme.Content = "☀️ Aydınlık Mod";
            }
            else
            {
                this.Resources["BgBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#F8F9FA"));
                this.Resources["PanelBgBrush"] = new SolidColorBrush(System.Windows.Media.Colors.White);
                this.Resources["TextMainBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#2C3E50"));
                this.Resources["TextMutedBrush"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#7F8C8D"));
                this.Resources["BorderBrushColor"] = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#E0E0E0"));
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

            if (RbPdfToJpg.IsChecked == true || RbPdfToText.IsChecked == true || RbPdfMerge.IsChecked == true || 
                RbPdfSplit.IsChecked == true || RbPdfExtractImages.IsChecked == true || RbPdfRotate.IsChecked == true 
                || RbPdfCompress.IsChecked == true || RbPdfRepair.IsChecked == true || RbPdfToWord.IsChecked == true)
                dialog.Filter = "PDF Dosyaları (*.pdf)|*.pdf";
            else if (RbTextToPdf.IsChecked == true)
                dialog.Filter = "Metin Dosyaları (*.txt)|*.txt";
            else if (RbMdToPdf.IsChecked == true)
                dialog.Filter = "Markdown Dosyaları (*.md)|*.md";
            else if (RbPptToPdf.IsChecked == true)
                dialog.Filter = "PowerPoint Dosyaları (*.pptx;*.ppt)|*.pptx;*.ppt";
            else if (RbWordToPdf.IsChecked == true)
                dialog.Filter = "Word Dosyaları (*.docx;*.doc)|*.docx;*.doc";
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

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                DropZoneBackground.Fill = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#EAF2F8"));
                DropIcon.Text = "📥";
                TxtDropZoneMetni.Text = "Şimdi Dosyaları Bırakın!";
            }
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZoneBackground.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "PanelBgBrush");
            DropIcon.Text = "📂";
            TxtDropZoneMetni.Text = "İşlenecek Dosyaları Buraya Sürükleyin";
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZoneBackground.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "PanelBgBrush");
            DropIcon.Text = "📂";

            if (e.Data.GetDataPresent(DataFormats.FileDrop) && e.Data.GetData(DataFormats.FileDrop) is string[] dosyalar)
            {
                await ProcessFilesAsync(dosyalar);
            }
        }

        // ==========================================
        // ANA İŞLEM DÖNGÜSÜ (KUMANDA MERKEZİ)
        // ==========================================
        private async Task ProcessFilesAsync(string[] paths)
        {
            TxtDurumMetni.Text = "İşleniyor...";
            ProgressBarIlerleme.Value = 0;
            TxtIlerlemeYuzdesi.Text = "Hesaplanıyor...";
            BtnOpenFolder.Visibility = Visibility.Collapsed;
            BtnCancel.Visibility = Visibility.Visible;
            BtnCancel.IsEnabled = true;

            // 1. İŞLEM BAŞLARKEN EKRANI "İŞLENİYOR" MODUNA AL
            DropIcon.Text = "⚙️";
            TxtDropZoneMetni.Text = "Dosyalar İşleniyor...";
            DropZoneBackground.SetResourceReference(System.Windows.Shapes.Shape.FillProperty, "PanelBgBrush");

            var tumDosyalar = FileHelper.ResolveFiles(paths);
            if (tumDosyalar.Count == 0)
            {
                BtnCancel.Visibility = Visibility.Collapsed;
                DropIcon.Text = "📂";
                TxtDropZoneMetni.Text = "İşlenecek Dosyaları Buraya Sürükleyin";
                return;
            }

            int toplamDosya = tumDosyalar.Count;
            int islenenDosya = 0;

            int seciliMod = 0;
            if (RbPdfToJpg.IsChecked == true) seciliMod = 0;
            else if (RbImgToPdf.IsChecked == true) seciliMod = 1;
            else if (RbPdfToText.IsChecked == true) seciliMod = 2;
            else if (RbTextToPdf.IsChecked == true) seciliMod = 3;
            else if (RbPdfMerge.IsChecked == true) seciliMod = 4;
            else if (RbPdfSplit.IsChecked == true) seciliMod = 5;
            else if (RbMdToPdf.IsChecked == true) seciliMod = 6;
            else if (RbPdfExtractImages.IsChecked == true) seciliMod = 7;
            else if (RbImgToIco.IsChecked == true) seciliMod = 8;
            else if (RbPdfRotate.IsChecked == true) seciliMod = 9;
            else if (RbPdfCompress.IsChecked == true) seciliMod = 10;
            else if (RbPdfRepair.IsChecked == true) seciliMod = 11;
            else if (RbPptToPdf.IsChecked == true) seciliMod = 12;
            else if (RbWordToPdf.IsChecked == true) seciliMod = 13;
            else if (RbPdfToWord.IsChecked == true) seciliMod = 14;

            double kaliteCarpani = CmbQuality.SelectedIndex == 0 ? 1.0 :
                                   CmbQuality.SelectedIndex == 1 ? 2.0 :
                                   CmbQuality.SelectedIndex == 2 ? 4.0 : 6.0;

            bool sifreleAktif = ChkSifrele.IsChecked == true;
            string sifre = TxtSifre.Text;

            int donusAcisi = CmbRotateAngle.SelectedIndex == 0 ? 90 :
                             CmbRotateAngle.SelectedIndex == 1 ? 180 : 270;

            string? firstFileDir = Path.GetDirectoryName(tumDosyalar[0]) ?? paths[0];
            string klasorAdi = seciliMod == 0 ? "Converted_Jpgs" :
                               seciliMod == 2 ? "Extracted_Texts" :
                               seciliMod == 5 ? "Split_Pages" :
                               seciliMod == 6 ? "Markdown_PDFs" :
                               seciliMod == 7 ? "Extracted_Images" :
                               seciliMod == 8 ? "Converted_Icons" : 
                               seciliMod == 9 ? "Rotated_PDFs" : 
                               seciliMod == 10 ? "Compressed_PDFs" :
                               seciliMod == 11 ? "Repaired_PDFs" :
                               seciliMod == 12 ? "Converted_PPTs" :
                               seciliMod == 13 ? "Converted_from_Word" :
                               seciliMod == 14 ? "Converted_to_Word" : "";




            if (!string.IsNullOrEmpty(klasorAdi))
            {
                _sonCiktiKlasoru = FileHelper.GetUniquePath(Path.Combine(firstFileDir, klasorAdi), true);
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

                    
                    if (seciliMod == 0 || seciliMod == 2 || seciliMod == 5 || seciliMod == 7 || seciliMod == 9 || seciliMod == 10 || seciliMod == 11 || seciliMod == 12 || seciliMod == 13 || seciliMod == 14)
                    {
                        Parallel.ForEach(tumDosyalar, pOptions, (file) =>
                        {
                            token.ThrowIfCancellationRequested();
                            string ext = Path.GetExtension(file).ToLower();

                            
                            if (seciliMod == 0 && ext == ".pdf") PdfToJpg.Convert(file, _sonCiktiKlasoru, kaliteCarpani, token);
                            else if (seciliMod == 2 && ext == ".pdf") PdfToText.Convert(file, _sonCiktiKlasoru, token);
                            else if (seciliMod == 5 && ext == ".pdf") PdfSplitMerge.Split(file, _sonCiktiKlasoru, sifreleAktif, sifre, token);
                            else if (seciliMod == 7 && ext == ".pdf") PdfImageExtractor.Extract(file, _sonCiktiKlasoru, sifre, token);
                            else if (seciliMod == 9 && ext == ".pdf") PdfRotator.Rotate(file, _sonCiktiKlasoru, donusAcisi, token);
                            else if (seciliMod == 10 && ext == ".pdf") PdfCompressor.Compress(file, _sonCiktiKlasoru, token);
                            else if (seciliMod == 11 && ext == ".pdf") PdfRepairer.Repair(file, _sonCiktiKlasoru, token);

                            
                            else if (seciliMod == 12 && (ext == ".pptx" || ext == ".ppt")) PptToPdf.Convert(file, _sonCiktiKlasoru, token);
                            else if (seciliMod == 13 && (ext == ".docx" || ext == ".doc")) WordConverter.Convert(file, _sonCiktiKlasoru, 0, token);

                            else if (seciliMod == 14 && ext == ".pdf") WordConverter.Convert(file, _sonCiktiKlasoru, 1, token);

                            UIGuncelle(Interlocked.Increment(ref islenenDosya), toplamDosya, sw);
                        });
                    }
                    else if (seciliMod == 1)
                    {
                        ImgToPdf.Convert(tumDosyalar, _sonCiktiKlasoru, sifreleAktif, sifre, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                    else if (seciliMod == 3)
                    {
                        TextToPdf.Convert(tumDosyalar, _sonCiktiKlasoru, sifreleAktif, sifre, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                    else if (seciliMod == 4)
                    {
                        PdfSplitMerge.Merge(tumDosyalar, _sonCiktiKlasoru, sifreleAktif, sifre, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                    else if (seciliMod == 6)
                    {
                        MdToPdf.Convert(tumDosyalar, _sonCiktiKlasoru, sifreleAktif, sifre, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                    else if (seciliMod == 8)
                    {
                        ImgToIco.Convert(tumDosyalar.ToArray(), _sonCiktiKlasoru, token);
                        UIGuncelle(toplamDosya, toplamDosya, sw);
                    }
                }, token);

                sw.Stop();
                TxtDurumMetni.Text = "İşlem Başarıyla Tamamlandı!";

                // 2. İŞLEM BAŞARIYLA BİTİNCE EKRANI "HAZIR" MODUNA AL
                DropIcon.Text = "✅";
                TxtDropZoneMetni.Text = "Dosyalarınız Hazır!";
                DropZoneBackground.Fill = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#E8F8F5"));

                BtnOpenFolder.Visibility = Visibility.Visible;
                SystemSounds.Asterisk.Play();
                MessageBox.Show("İşlem tamamlandı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (OperationCanceledException)
            {
                sw.Stop();
                TxtDurumMetni.Text = "İşlem İptal Edildi veya Şifre Atlandı!";
                TxtIlerlemeYuzdesi.Text = "İptal Edildi";

                // 3. İPTAL DURUMUNDA EKRANI "UYARI" MODUNA AL
                DropIcon.Text = "⚠️";
                TxtDropZoneMetni.Text = "İşlem İptal Edildi";
                DropZoneBackground.Fill = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FEF9E7"));

                SystemSounds.Exclamation.Play();
                MessageBox.Show("İşlem iptal edildi veya gerekli bir şifre girilmedi.", "Uyarı", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                sw.Stop();
                TxtDurumMetni.Text = "Bir hata oluştu!";

                // 4. HATA DURUMUNDA EKRANI "HATA" MODUNA AL
                DropIcon.Text = "❌";
                TxtDropZoneMetni.Text = "Bir Hata Oluştu!";
                DropZoneBackground.Fill = new SolidColorBrush((System.Windows.Media.Color)ColorConverter.ConvertFromString("#FDEDEC"));

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

        // ==========================================
        // UI GÜNCELLEME VE AYARLAR
        // ==========================================
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

        private void AyarlariYukle()
        {
            var ayarlar = SettingsManager.Yukle();

            CmbQuality.SelectedIndex = ayarlar.SeciliKalite;
            ChkSifrele.IsChecked = ayarlar.SifreleAktif;
            ChkSifrele_Click(null, null);

            ToggleTheme.IsChecked = ayarlar.KaranlikModAktif;
            SetTheme(ayarlar.KaranlikModAktif);

            switch (ayarlar.SeciliMod)
            {
                case 0: RbPdfToJpg.IsChecked = true; break;
                case 1: RbImgToPdf.IsChecked = true; break;
                case 2: RbPdfToText.IsChecked = true; break;
                case 3: RbTextToPdf.IsChecked = true; break;
                case 4: RbPdfMerge.IsChecked = true; break;
                case 5: RbPdfSplit.IsChecked = true; break;
                case 6: RbMdToPdf.IsChecked = true; break;
                case 7: RbPdfExtractImages.IsChecked = true; break;
                case 8: RbImgToIco.IsChecked = true; break;
                case 9: RbPdfRotate.IsChecked = true; break;
                case 10: RbPdfCompress.IsChecked = true; break;
                case 11: RbPdfRepair.IsChecked = true; break;
                case 12: RbPptToPdf.IsChecked = true; break;
                case 13: RbWordToPdf.IsChecked = true; break;
                case 14: RbPdfToWord.IsChecked = true; break;
            }
        }

        private void AyarlariKaydet()
        {
            // Buradaki syntax hatası (eksik noktalı virgüller ve bozuk bağlama) tamamen çözüldü
            var ayarlar = new AppSettings
            {
                SeciliKalite = CmbQuality.SelectedIndex,
                SifreleAktif = ChkSifrele.IsChecked == true,
                KaranlikModAktif = ToggleTheme.IsChecked == true,
                SeciliMod = RbPdfToJpg.IsChecked == true ? 0 :
                            RbImgToPdf.IsChecked == true ? 1 :
                            RbPdfToText.IsChecked == true ? 2 :
                            RbTextToPdf.IsChecked == true ? 3 :
                            RbPdfMerge.IsChecked == true ? 4 :
                            RbPdfSplit.IsChecked == true ? 5 :
                            RbMdToPdf.IsChecked == true ? 6 :
                            RbPdfExtractImages.IsChecked == true ? 7 :
                            RbImgToIco.IsChecked == true ? 8 : 
                            RbPdfRotate.IsChecked == true ? 9 :
                            RbPdfCompress.IsChecked == true ? 10 :
                            RbPdfRepair.IsChecked == true ? 11 :
                            RbPptToPdf.IsChecked == true ? 12 :
                            RbWordToPdf.IsChecked == true ? 13 :
                            RbPdfToWord.IsChecked == true ? 14 : 0
            };

            SettingsManager.Kaydet(ayarlar);
        }
    }
}