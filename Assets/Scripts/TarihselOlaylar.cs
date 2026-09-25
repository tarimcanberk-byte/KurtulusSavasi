using System;
using System.Collections.Generic;
using UnityEngine;

// Belirli tarihlerde (ve bazen belirli koşullarda) gerçekleşen tarihî olaylar.
// Yunan taarruz dalgaları, Sevr, İtalya'nın çekilmesi, Fransa ile Ankara Antlaşması...
public static class TarihselOlaylar
{
    static HashSet<string> olanlar = new HashSet<string>();

    public static void Sifirla() { olanlar = new HashSet<string>(); }

    // Olay bir kez olsun diye: daha önce olmadıysa true döner ve "oldu" diye işaretler
    static bool BirKez(string anahtar)
    {
        return olanlar.Add(anahtar);
    }

    static readonly string[] YunanCikarmaYerleri = { "İzmir", "Manisa", "Aydın", "Balıkesir", "Çanakkale" };

    public static List<string> Kontrol(DateTime tarih, TurYoneticisi tur)
    {
        List<string> r = new List<string>();

        // --- Yunan taarruz dalgaları (asker sayıları oyun dengesi içindir) ---
        if (tarih >= new DateTime(1919, 6, 1) && BirKez("yunan1"))
        {
            r.Add("<b>Yunan ordusu İzmir'den iç bölgelere yayılmak için takviye aldı.</b>");
            r.Add(Cikarma(Taraf.Yunan, 8000, "Yunan 1. Tümeni", YunanCikarmaYerleri));
        }
        if (tarih >= new DateTime(1920, 6, 15) && BirKez("yunan2"))
        {
            r.Add("<b>Yunan yaz taarruzu başladı!</b> İngiltere'nin onayıyla Yunan ordusu Batı Anadolu'da büyük bir harekâta girişiyor.");
            r.Add(Cikarma(Taraf.Yunan, 25000, "Yunan Taarruz Kolordusu", YunanCikarmaYerleri));
        }
        if (tarih >= new DateTime(1921, 1, 1) && BirKez("yunan3"))
        {
            r.Add("<b>Yunan ordusu Eskişehir yönünde yeni bir taarruza hazırlanıyor.</b>");
            r.Add(Cikarma(Taraf.Yunan, 20000, "Yunan 3. Kolordusu", YunanCikarmaYerleri));
        }
        if (tarih >= new DateTime(1921, 3, 15) && BirKez("yunan4"))
        {
            r.Add("<b>Yunan ordusu takviye aldı; ikinci büyük taarruz yaklaşıyor.</b>");
            r.Add(Cikarma(Taraf.Yunan, 25000, "Yunan 1. Kolordusu", YunanCikarmaYerleri));
        }
        if (tarih >= new DateTime(1921, 7, 1) && BirKez("yunan5"))
        {
            r.Add("<b>Büyük Yunan taarruzu!</b> Yunan ordusu bütün gücüyle Ankara'yı hedefliyor.");
            r.Add(Cikarma(Taraf.Yunan, 40000, "Yunan Küçük Asya Ordusu", YunanCikarmaYerleri));
        }

        // --- Siyasi olaylar ---
        if (tarih >= new DateTime(1920, 4, 23) && BirKez("tbmm"))
        {
            tur.para += 200;
            r.Add("<color=#9f9><b>Türkiye Büyük Millet Meclisi Ankara'da açıldı.</b> Vergiler toplanmaya başladı: +200 para.</color>");
        }
        if (tarih >= new DateTime(1920, 8, 10) && BirKez("sevr"))
        {
            int gonullu = 0;
            foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
                if (o.OyuncununMu && o.Yasiyor) { int ek = o.askerSayisi / 10; o.askerSayisi += ek; gonullu += ek; }
            r.Add("<b>İstanbul hükümeti Sevr Antlaşması'nı imzaladı.</b> Halkın öfkesi büyük: ordulara " + gonullu + " gönüllü katıldı.");
        }
        if (tarih >= new DateTime(1921, 3, 16) && BirKez("moskova"))
        {
            tur.para += 500;
            r.Add("<color=#9f9><b>Moskova Antlaşması imzalandı.</b> Sovyet Rusya'dan altın ve silah yardımı: +500 para.</color>");
        }

        // İtalya çekilmesi: İtalya ile savaşta değilsek
        if (tarih >= new DateTime(1921, 6, 1) && !Diplomasi.SavastaMi(Taraf.Italyan) && BirKez("italya"))
        {
            TarafiGeriCek(Taraf.Italyan);
            r.Add("<color=#9f9><b>İtalya Anadolu'dan çekiliyor.</b> Antalya ve Menteşe Ankara'ya bırakıldı.</color>");
        }

        // Fransa ile Ankara Antlaşması: ordumuz yeterince güçlüyse
        if (tarih >= new DateTime(1921, 10, 1) && Diplomasi.SavastaMi(Taraf.Fransiz)
            && ToplamAsker(Taraf.Turk) > ToplamAsker(Taraf.Fransiz) * 3 && BirKez("ankara_antlasmasi"))
        {
            TarafiGeriCek(Taraf.Fransiz);
            Diplomasi.BarisYap(Taraf.Fransiz);
            r.Add("<color=#9f9><b>Fransa ile Ankara Antlaşması imzalandı.</b> Güney cephesi kapandı; Fransız işgalindeki sancaklar geri verildi.</color>");
        }
        return r;
    }

    // Bir tarafın bütün ordularını haritadan kaldırır ve sancaklarını Ankara'ya verir
    static void TarafiGeriCek(Taraf t)
    {
        foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.taraf == t) o.YokOl();
        foreach (Sancak s in UnityEngine.Object.FindObjectsByType<Sancak>(FindObjectsSortMode.None))
            if (s.sahip == t) s.SahipDegistir(Taraf.Turk);
    }

    public static int ToplamAsker(Taraf t)
    {
        int n = 0;
        foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.taraf == t && o.Yasiyor) n += o.askerSayisi;
        return n;
    }

    // Deniz yoluyla asker çıkarma. Aday sancaklar arasından Türk gücünün en zayıf olduğu yeri seçer.
    // Orada Türk ordusu varsa savaş olur; çıkarma yenilirse birlik denize dökülür.
    public static string Cikarma(Taraf t, int asker, string ad, string[] adaylar)
    {
        Sancak hedef = null;
        float enAz = float.MaxValue;
        foreach (string a in adaylar)
        {
            Sancak s = Sancak.Bul(a);
            if (s == null) continue;
            float g = Savas.TarafGucu(s, Taraf.Turk);
            if (s.sahip == t && g == 0f) { hedef = s; break; }   // kendi güvenli limanı varsa oraya
            if (g < enAz) { enAz = g; hedef = s; }
        }
        if (hedef == null) return "";

        Ordu o = Ordu.Olustur(ad, t, asker, hedef);
        if (Savas.TarafGucu(hedef, Taraf.Turk) == 0f)
        {
            if (hedef.sahip != t) hedef.SahipDegistir(t);
            return "<color=#f99>" + ad + " (" + asker + " asker) çıkarma yaptı → " + hedef.sancakAdi + "</color>";
        }

        string sonuc = Savas.Ilerle(o, hedef);
        if (o.Yasiyor && Savas.TarafGucu(hedef, Taraf.Turk) > 0f)
        {
            o.YokOl();
            return sonuc.Replace("\n", " ") + " <color=#9f9>Çıkarma denize döküldü!</color>";
        }
        return sonuc.Replace("\n", " ");
    }
}
