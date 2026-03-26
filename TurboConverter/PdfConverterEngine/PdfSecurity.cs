using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Docnet.Core;
using Docnet.Core.Models;
using Docnet.Core.Readers;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace TurboConverter.PdfConverterEngine
{
    public static class PdfSecurity
    {
        public static string SifreIste(string dosyaAdi)
        {
            string girilenSifre = string.Empty;
            Application.Current.Dispatcher.Invoke(() =>
            {
                Window dialog = new Window
                {
                    Title = "Kilitli PDF Algılandı",
                    Width = 400,
                    Height = 180,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    ResizeMode = ResizeMode.NoResize,
                    WindowStyle = WindowStyle.ToolWindow,
                    Background = System.Windows.Media.Brushes.WhiteSmoke
                };

                StackPanel panel = new StackPanel { Margin = new Thickness(20) };
                panel.Children.Add(new TextBlock { Text = $"'{Path.GetFileName(dosyaAdi)}' şifre ile korunuyor.\nOkuyabilmemiz için parolayı girin:", Margin = new Thickness(0, 0, 0, 10), TextWrapping = TextWrapping.Wrap });
                PasswordBox passBox = new PasswordBox { Padding = new Thickness(5) };
                panel.Children.Add(passBox);

                StackPanel btnPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = System.Windows.HorizontalAlignment.Right, Margin = new Thickness(0, 15, 0, 0) };
                Button btnCancel = new Button { Content = "İptal Et", Width = 80, Padding = new Thickness(5), Margin = new Thickness(0, 0, 10, 0) };
                btnCancel.Click += (s, e) => { dialog.DialogResult = false; };

                Button btnOk = new Button { Content = "Kilidi Aç", Width = 80, Padding = new Thickness(5), IsDefault = true, Background = System.Windows.Media.Brushes.LightBlue };
                btnOk.Click += (s, e) => { girilenSifre = passBox.Password; dialog.DialogResult = true; };

                btnPanel.Children.Add(btnCancel); btnPanel.Children.Add(btnOk); panel.Children.Add(btnPanel);
                dialog.Content = panel; dialog.ShowDialog();
            });
            return girilenSifre;
        }

        public static void CiktiyiSifrele(string pdfPath, bool sifreleAktif, string sifre)
        {
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
                catch { System.Diagnostics.Debug.WriteLine("Şifreleme hatası."); }
            }
        }

        public static IDocReader GetDocReaderSafe(string pdfPath, double scaleFactor)
        {
            try { return DocLib.Instance.GetDocReader(pdfPath, new PageDimensions(scaleFactor)); }
            catch (Exception)
            {
                string sifre = SifreIste(pdfPath);
                if (string.IsNullOrEmpty(sifre)) throw new OperationCanceledException();
                return DocLib.Instance.GetDocReader(pdfPath, sifre, new PageDimensions(scaleFactor));
            }
        }

        public static PdfDocument GetPdfDocumentSafe(string pdfPath)
        {
            try { return PdfReader.Open(pdfPath, PdfDocumentOpenMode.Import); }
            catch (PdfReaderException)
            {
                string sifre = SifreIste(pdfPath);
                if (string.IsNullOrEmpty(sifre)) throw new OperationCanceledException();
                return PdfReader.Open(pdfPath, sifre, PdfDocumentOpenMode.Import);
            }
        }
    }
}