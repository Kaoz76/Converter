using System;
using System.IO;
using System.Text.Json;

namespace TurboConverter
{
    // 1. Ayarlarımızın Taslağı (Model)
    public class AppSettings
    {
        public int SeciliMod { get; set; } = 0;
        public int SeciliKalite { get; set; } = 2;
        public bool SifreleAktif { get; set; } = false;
        public bool KaranlikModAktif { get; set; } = false;
    }

    // 2. Ayarları Diske Yazan ve Okuyan Servisimiz
    public static class SettingsManager
    {
        private static readonly string _ayarlarDosyaYolu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TurboConverter", "settings.json");

        public static AppSettings Yukle()
        {
            try
            {
                if (File.Exists(_ayarlarDosyaYolu))
                {
                    string json = File.ReadAllText(_ayarlarDosyaYolu);
                    return JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
                }
            }
            catch { System.Diagnostics.Debug.WriteLine("Ayarlar yüklenemedi."); }

            return new AppSettings(); // Dosya yoksa veya bozuksa varsayılanı döndür
        }

        public static void Kaydet(AppSettings ayarlar)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_ayarlarDosyaYolu)!);
                string json = JsonSerializer.Serialize(ayarlar);
                File.WriteAllText(_ayarlarDosyaYolu, json);
            }
            catch { System.Diagnostics.Debug.WriteLine("Ayarlar kaydedilemedi."); }
        }
    }
}