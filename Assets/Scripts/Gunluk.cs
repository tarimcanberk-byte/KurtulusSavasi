using System;
using System.Collections.Generic;

// Oyun içi olay günlüğü. Savaşlar, fetihler ve tarihî olaylar buraya yazılır;
// ekrandaki günlük paneli bunu dinler. Önemli olaylar oyunu otomatik duraklatır.
public static class Gunluk
{
    public static event Action<string, bool> OlayEklendi;   // (metin, duraklatılsın mı)
    public static readonly List<string> SonOlaylar = new List<string>();

    public static void Sifirla() { SonOlaylar.Clear(); }

    public static void Ekle(string metin, bool duraklat = false)
    {
        if (string.IsNullOrEmpty(metin)) return;
        SonOlaylar.Add(metin);
        if (SonOlaylar.Count > 8) SonOlaylar.RemoveAt(0);
        OlayEklendi?.Invoke(metin, duraklat);
    }
}
