using System;
using System.Collections.Generic;
using UnityEngine;

// Yetki / Meşruiyet ve TBMM (Frostpunk tarzı kanun döngüsü), 1. aşama.
//   Yetki: Mustafa Kemal'in gücü; kongreler, Meclis'in açılışı, zaferler ve kanunlarla artar.
//   Meclis: 4 grup (koltuk + güven). Kanun teklif edilir, gruplara söz verilir ya da baskı yapılır, oylanır.
public static class Meclis
{
    // ---------- Gruplar ----------
    public class Grup
    {
        public string ad, onemsedigi;
        public float koltuk;       // yüzde
        public float guven;        // 0–100
        public string sozAdi, sozAciklama;
        public int sozPara, sozErzak;
    }

    public static readonly List<Grup> Gruplar = new List<Grup>();

    // ---------- Kanunlar ----------
    public class Kanun
    {
        public string ad, aciklama, etki;
        public DateTime acilis;
        public int[] tutum;        // her grubun bakışı: −2 (karşı) … +2 (destek)
        public bool gecti;
        public int bekleme;        // reddedilince yeniden teklif için gün
        public int sureGun;        // süreli kanunlar (Başkomutanlık)
        public int kalanGun;
        public int teklifSayisi;
        public bool Aktif { get { return gecti && (sureGun == 0 || kalanGun > 0); } }
    }

    public static readonly List<Kanun> Kanunlar = new List<Kanun>();

    public static float Yetki = 25f;
    public static bool Acik;                 // TBMM açıldı mı
    public static DateTime Tarih;
    public static int SonKayipGunu = -999;   // en son Türk sancağı kaybedildiğinden beri geçen gün sayacı için
    static int gunSayaci;

    // Oylamada bekleyen kanun (tartışma süresi)
    public static Kanun Tartisilan;
    public static int TartismaGun;
    static float tartisilanOran;

    // Söz ve baskı (bir teklif için)
    public static readonly HashSet<Grup> VerilenSozler = new HashSet<Grup>();
    public static bool BaskiYapildi;

    // Komutan ikna beklemeleri
    static readonly Dictionary<Komutan, int> iknaBekleme = new Dictionary<Komutan, int>();

    public static void Sifirla()
    {
        Yetki = 25f; Acik = false; gunSayaci = 0; SonKayipGunu = -999;
        Tartisilan = null; TartismaGun = 0;
        VerilenSozler.Clear(); BaskiYapildi = false; iknaBekleme.Clear();

        Gruplar.Clear();
        Gruplar.Add(new Grup { ad = "Müdafaa-i Hukukçular", onemsedigi = "Savaşın kazanılması, merkezî komuta", koltuk = 45, guven = 65,
                               sozAdi = "Cepheye ek kaynak", sozAciklama = "100 para hemen orduya", sozPara = 100 });
        Gruplar.Add(new Grup { ad = "Muhafazakârlar", onemsedigi = "Hilafet ve saltanata bağlılık, geleneksel düzen", koltuk = 20, guven = 50,
                               sozAdi = "Hilafete saygı beyanı", sozAciklama = "Halkçılar biraz rahatsız olur" });
        Gruplar.Add(new Grup { ad = "Muhalifler", onemsedigi = "Meclis üstünlüğü, yetkinin tek elde toplanmaması", koltuk = 20, guven = 40,
                               sozAdi = "Meclis denetimi güvencesi", sozAciklama = "Yetki −3" });
        Gruplar.Add(new Grup { ad = "Halkçılar", onemsedigi = "Sosyal reformlar, Sovyetlerle yakınlık", koltuk = 15, guven = 50,
                               sozAdi = "Köylüye tohum ve erzak", sozAciklama = "120 erzak köylüye", sozErzak = 120 });

        Kanunlar.Clear();
        Kanunlar.Add(new Kanun { ad = "Hıyanet-i Vataniye Kanunu", acilis = new DateTime(1920, 4, 29),
            aciklama = "Millî Mücadele'ye karşı çıkanlar vatan haini sayılır.",
            etki = "Firar −%30, Yetki +5", tutum = new[] { 2, 0, -1, 1 } });
        Kanunlar.Add(new Kanun { ad = "Mükellefiyet-i Askeriye Kanunu", acilis = new DateTime(1920, 6, 1),
            aciklama = "Askerlik yükümlülüğü düzenlenir, yeni birlikler daha kolay toplanır.",
            etki = "Birlik Topla maliyeti −%30", tutum = new[] { 2, -1, 0, -1 } });
        Kanunlar.Add(new Kanun { ad = "Firariler Kanunu ve İstiklal Mahkemeleri", acilis = new DateTime(1920, 9, 11),
            aciklama = "Firarla mücadele için olağanüstü mahkemeler kurulur.",
            etki = "Firar −%40, Yetki +5, Muhalifler güveni −5", tutum = new[] { 1, 0, -2, 0 } });
        Kanunlar.Add(new Kanun { ad = "Teşkilat-ı Esasiye Kanunu", acilis = new DateTime(1921, 1, 20),
            aciklama = "Egemenlik kayıtsız şartsız milletindir; yeni devletin ilk anayasası.",
            etki = "Yetki +10, gelir +%10", tutum = new[] { 1, -2, 1, 2 } });
        Kanunlar.Add(new Kanun { ad = "Başkomutanlık Kanunu", acilis = new DateTime(1920, 4, 23), sureGun = 90,
            aciklama = "Meclis'in yetkileri 3 aylığına Başkomutana devredilir. Kriz anında (yakın zamanda sancak kaybı ya da Ankara tehlikede) çok daha kolay geçer.",
            etki = "90 gün: komutanlar itirazsız atanır, Türk orduları +%10 güç, komutan yolculuğu −%30, Yetki +10",
            tutum = new[] { 2, 1, -2, 0 } });
    }

    public static string Asama
    {
        get
        {
            if (BaskomutanlikAktif) return "Başkomutan";
            if (Acik) return "TBMM Başkanı";
            if (Tarih >= new DateTime(1919, 9, 11)) return "Heyet-i Temsiliye Başkanı";
            if (Tarih >= new DateTime(1919, 7, 8)) return "Sivil lider (askerlikten istifa)";
            return "9. Ordu Müfettişi";
        }
    }

    public static void YetkiDegistir(float f) { Yetki = Mathf.Clamp(Yetki + f, 0f, 100f); }

    static void GuvenDegistir(int grup, float f) { Gruplar[grup].guven = Mathf.Clamp(Gruplar[grup].guven + f, 0f, 100f); }

    // ---------- Etkiler (diğer sistemler sorar) ----------

    public static bool Gecti(string ad) { foreach (Kanun k in Kanunlar) if (k.ad == ad && k.Aktif) return true; return false; }
    public static bool BaskomutanlikAktif { get { return Gecti("Başkomutanlık Kanunu"); } }

    public static float FirarCarpani
    {
        get
        {
            float c = 1f;
            if (Gecti("Hıyanet-i Vataniye Kanunu")) c *= 0.7f;
            if (Gecti("Firariler Kanunu ve İstiklal Mahkemeleri")) c *= 0.6f;
            return c;
        }
    }
    public static float BirlikMaliyetCarpani { get { return Gecti("Mükellefiyet-i Askeriye Kanunu") ? 0.7f : 1f; } }
    public static float GelirCarpani { get { return Gecti("Teşkilat-ı Esasiye Kanunu") ? 1.1f : 1f; } }
    public static float OrduCarpani { get { return BaskomutanlikAktif ? 1.1f : 1f; } }
    public static float YolCarpani { get { return BaskomutanlikAktif ? 0.7f : 1f; } }

    // Kriz: son 30 günde sancak kaybı ya da Ankara'nın komşusunda düşman
    public static bool Kriz
    {
        get
        {
            if (gunSayaci - SonKayipGunu <= 30) return true;
            Sancak ankara = Sancak.Bul("Ankara");
            if (ankara == null) return false;
            if (ankara.sahip != Taraf.Turk) return true;
            foreach (Sancak k in ankara.komsular) if (k.sahip == Taraf.Yunan) return true;
            return false;
        }
    }

    // ---------- Komutanların itaati ----------

    // Yetki düşükken komutanlar her atamayı kabul etmez (Başkomutanlıkta itiraz yok)
    public static bool AtamaKabul(Komutan k, out string neden)
    {
        neden = null;
        if (k.olumsuz || BaskomutanlikAktif) return true;
        if (iknaBekleme.TryGetValue(k, out int g) && g > 0)
        {
            neden = k.ad + " henüz ikna olmadı. " + g + " gün sonra yeniden dene.";
            return false;
        }
        float esik = (11 - k.sadakat) * 10f + (k.Var("Bağımsız Düşünce") ? 10f : 0f);
        if (Yetki >= esik) return true;
        float red = (esik - Yetki) / esik * 0.8f;
        if (UnityEngine.Random.value < red)
        {
            iknaBekleme[k] = 14;
            neden = k.ad + " bu atamayı kabul etmedi (yetkin " + Mathf.RoundToInt(Yetki) + ", ikna için ~" + Mathf.RoundToInt(esik) + " gerekli). 14 gün sonra yeniden dene.";
            return false;
        }
        return true;
    }

    // ---------- Oylama ----------

    public static float GrupOrani(Kanun k, int i)
    {
        Grup g = Gruplar[i];
        float oran = 0.5f + k.tutum[i] * 0.17f + (g.guven - 50f) * 0.008f;
        if (k.ad == "Başkomutanlık Kanunu" && Kriz) oran += 0.15f;
        if (k.ad == "Başkomutanlık Kanunu") oran -= (k.teklifSayisi > 0 && i == 2 ? 0.05f * k.teklifSayisi : 0f);   // her uzatmada muhalefet sertleşir
        if (VerilenSozler.Contains(g)) oran += 0.15f;
        if (BaskiYapildi) oran += 0.2f;
        return Mathf.Clamp01(oran);
    }

    public static float EvetOrani(Kanun k)
    {
        float e = 0f;
        for (int i = 0; i < Gruplar.Count; i++) e += Gruplar[i].koltuk / 100f * GrupOrani(k, i);
        return e;
    }

    public static int TartismaSuresi()
    {
        float g = 0f;
        foreach (Grup x in Gruplar) g += x.guven * x.koltuk / 100f;
        return Mathf.Clamp(Mathf.RoundToInt((60f - g) / 3f), 0, 12);
    }

    public static string TeklifEngeli(Kanun k)
    {
        if (!Acik) return "Meclis henüz açılmadı (23 Nisan 1920).";
        if (k.Aktif) return k.sureGun > 0 ? "Yürürlükte (" + k.kalanGun + " gün kaldı)." : "Kabul edildi.";
        if (Tarih < k.acilis) return "Henüz gündemde değil (" + k.acilis.Day + "." + k.acilis.Month + "." + k.acilis.Year + ").";
        if (k.bekleme > 0) return "Reddedildi; " + k.bekleme + " gün sonra yeniden teklif edilebilir.";
        if (Tartisilan != null) return "Meclis şu an " + Tartisilan.ad + " görüşüyor.";
        return null;
    }

    // Bir gruba söz ver: oyu artar, bedeli hemen ödenir
    public static string SozVer(int i, TurYoneticisi tur)
    {
        Grup g = Gruplar[i];
        if (VerilenSozler.Contains(g)) return null;
        if (tur.para < g.sozPara) return "Yeterli para yok (" + g.sozPara + ").";
        if (tur.erzak < g.sozErzak) return "Yeterli erzak yok (" + g.sozErzak + ").";
        tur.para -= g.sozPara; tur.erzak -= g.sozErzak;
        VerilenSozler.Add(g);
        GuvenDegistir(i, 6f);
        if (i == 1) GuvenDegistir(3, -5f);   // hilafet beyanı Halkçıları rahatsız eder
        if (i == 2) YetkiDegistir(-3f);       // denetim güvencesi yetkiyi kısar
        tur.EkraniGuncelle();
        return null;
    }

    // Baskı: oyları zorlar ama bütün grupların güvenini düşürür
    public static string Baski()
    {
        if (BaskiYapildi) return null;
        if (Yetki < 50f) return "Baskı için yetki en az 50 olmalı.";
        BaskiYapildi = true;
        for (int i = 0; i < Gruplar.Count; i++) GuvenDegistir(i, -8f);
        YetkiDegistir(-2f);
        return null;
    }

    public static void TeklifiTemizle() { VerilenSozler.Clear(); BaskiYapildi = false; }

    // Kanunu oylamaya sun: güven yüksekse hemen, düşükse birkaç gün tartışılır
    public static string OylamayaSun(Kanun k)
    {
        string engel = TeklifEngeli(k);
        if (engel != null) return engel;
        tartisilanOran = EvetOrani(k);
        TeklifiTemizle();
        k.teklifSayisi++;
        int gun = TartismaSuresi();
        if (gun <= 0) return Sonuclandir(k, tartisilanOran);
        Tartisilan = k; TartismaGun = gun;
        return "<b>" + k.ad + "</b> Meclis'te görüşülüyor; oylama " + gun + " gün sonra.";
    }

    static string Sonuclandir(Kanun k, float oran)
    {
        Tartisilan = null;
        int yuzde = Mathf.RoundToInt(oran * 100f);
        if (oran > 0.5f)
        {
            k.gecti = true;
            k.kalanGun = k.sureGun;
            for (int i = 0; i < Gruplar.Count; i++) GuvenDegistir(i, k.tutum[i] > 0 ? 3f : k.tutum[i] < 0 ? -3f : 0f);
            switch (k.ad)
            {
                case "Hıyanet-i Vataniye Kanunu": YetkiDegistir(5f); break;
                case "Firariler Kanunu ve İstiklal Mahkemeleri": YetkiDegistir(5f); GuvenDegistir(2, -5f); break;
                case "Teşkilat-ı Esasiye Kanunu": YetkiDegistir(10f); break;
                case "Başkomutanlık Kanunu": YetkiDegistir(10f); break;
            }
            return "<color=#9f9><b>TBMM " + k.ad + "'nu kabul etti</b> (%" + yuzde + " evet). " + k.etki + ".</color>";
        }
        k.bekleme = 30;
        YetkiDegistir(-2f);
        return "<color=#f99><b>TBMM " + k.ad + "'nu reddetti</b> (%" + yuzde + " evet). 30 gün sonra yeniden teklif edilebilir.</color>";
    }

    // ---------- Gün gün ----------

    public static List<string> GunIlerle(DateTime tarih)
    {
        List<string> h = new List<string>();
        Tarih = tarih;
        gunSayaci++;
        if (tarih == new DateTime(1919, 7, 8))
        { YetkiDegistir(-5f); h.Add("<b>Mustafa Kemal askerlikten istifa etti.</b> Artık resmî yetkisi yok; karizması ve millî destekle mücadele edecek. (Yetki −5)"); }
        if (tarih == new DateTime(1919, 8, 7))
        { YetkiDegistir(10f); h.Add("<b>Erzurum Kongresi sona erdi.</b> Heyet-i Temsiliye kuruldu, Mustafa Kemal başkan seçildi. (Yetki +10)"); }
        if (tarih == new DateTime(1919, 9, 11))
        { YetkiDegistir(15f); h.Add("<b>Sivas Kongresi sona erdi.</b> Bütün cemiyetler Anadolu ve Rumeli Müdafaa-i Hukuk Cemiyeti'nde birleşti. (Yetki +15)"); }
        if (tarih == new DateTime(1920, 4, 23))
        {
            Acik = true; YetkiDegistir(10f);
            h.Add("<b>Türkiye Büyük Millet Meclisi açıldı.</b> Mustafa Kemal Meclis Başkanı. Artık <b>Meclis</b> düğmesinden kanun teklif edebilirsin. (Yetki +10)");
        }

        foreach (Kanun k in Kanunlar)
        {
            if (k.bekleme > 0) k.bekleme--;
            if (k.gecti && k.sureGun > 0 && k.kalanGun > 0)
            {
                k.kalanGun--;
                if (k.kalanGun == 0)
                {
                    k.gecti = false;
                    h.Add("<color=#fc8><b>" + k.ad + "'nun süresi doldu.</b> Uzatmak için Meclis'e yeniden teklif et.</color>");
                }
            }
        }
        // Güven zamanla olağan seviyesine döner (haftada 1 puan)
        if (gunSayaci % 7 == 0)
            for (int i = 0; i < Gruplar.Count; i++)
            {
                float olagan = i == 0 ? 60f : i == 2 ? 40f : 50f;
                if (Gruplar[i].guven < olagan) GuvenDegistir(i, 1f);
                else if (Gruplar[i].guven > olagan) GuvenDegistir(i, -1f);
            }

        if (Tartisilan != null && --TartismaGun <= 0) h.Add(Sonuclandir(Tartisilan, tartisilanOran));

        List<Komutan> bitti = new List<Komutan>();
        foreach (Komutan k in new List<Komutan>(iknaBekleme.Keys)) if (--iknaBekleme[k] <= 0) bitti.Add(k);
        foreach (Komutan k in bitti) iknaBekleme.Remove(k);
        return h;
    }

    // Muharebe ve kayıpların siyasete etkisi
    public static void MuharebeSonucu(bool oyuncuKazandi)
    {
        if (oyuncuKazandi)
        {
            YetkiDegistir(1f);
            for (int i = 0; i < Gruplar.Count; i++) GuvenDegistir(i, i == 0 ? 3f : 2f);
        }
        else
        {
            YetkiDegistir(-1f);
            GuvenDegistir(0, -1f);
            // yenilgi muhalefeti güçlendirir: Müdafaa-i Hukuk'tan Muhaliflere koltuk kayar
            if (Gruplar[0].koltuk > 30f) { Gruplar[0].koltuk -= 1f; Gruplar[2].koltuk += 1f; }
        }
    }

    public static void SancakKaybi()
    {
        SonKayipGunu = gunSayaci;
        YetkiDegistir(-2f);
        GuvenDegistir(0, -2f);
    }
}
