using System;
using System.Collections.Generic;
using UnityEngine;

// Bir komutan: Millî Mücadele'nin gerçek komutanları, tasarım dökümanındaki değerlerle (1–10).
public class Komutan
{
    public string ad;            // tam adı ve unvanı (ör. "Ali Fuat Paşa")
    public string kisaAd;        // savaş ekranı için (ör. "Ali Fuat")
    public int komuta, taktik, idare, karizma, sadakat, siyaset;
    public List<string> ozellikler = new List<string>();
    public DateTime katilmaTarihi;   // Ankara'ya katıldığı tarih (öncesinde atanamaz)
    public string katilmaHaberi;
    public bool olumsuz;         // Mustafa Kemal ölemez
    public bool canli = true;
    public bool katildi;
    public int yaraliGun;
    public int zafer, yenilgi;
    public Ordu ordu;            // komuta ettiği ordu (yoksa null)

    public bool Musait { get { return canli && katildi && yaraliGun <= 0; } }
    public bool Var(string ozellik) { return ozellikler.Contains(ozellik); }

    public string Degerler { get { return "Komuta " + komuta + " · Taktik " + taktik + " · İdare " + idare + " · Karizma " + karizma; } }
    public string OzellikMetni { get { return string.Join(", ", ozellikler); } }
}

public static class Komutanlar
{
    public static readonly List<Komutan> Liste = new List<Komutan>();

    static Komutan Yeni(string ad, string kisa, int ko, int ta, int id, int ka, int sa, int si, string ozellik,
                        DateTime katilma, string haber = null, bool olumsuz = false)
    {
        Komutan k = new Komutan
        {
            ad = ad, kisaAd = kisa, komuta = ko, taktik = ta, idare = id, karizma = ka, sadakat = sa, siyaset = si,
            katilmaTarihi = katilma, katilmaHaberi = haber, olumsuz = olumsuz
        };
        k.ozellikler.AddRange(ozellik.Split(new[] { ", " }, StringSplitOptions.RemoveEmptyEntries));
        Liste.Add(k);
        return k;
    }

    // Oyun başında çağrılır (Play'e her basışta temiz başlangıç)
    public static void Sifirla()
    {
        Liste.Clear();
        DateTime bas = new DateTime(1919, 5, 19);
        Yeni("Mustafa Kemal Paşa", "Mustafa Kemal", 10, 9, 8, 10, 10, 10, "Karizmatik Lider, Başkomutan, Stratejist", bas, null, true);
        Yeni("Kâzım Karabekir Paşa", "Kâzım Karabekir", 8, 8, 9, 7, 7, 7, "Doğu Cephesi Komutanı, Ordu Kurucusu, Bağımsız Düşünce", bas);
        Yeni("Ali Fuat Paşa", "Ali Fuat", 7, 7, 7, 8, 8, 7, "Diplomat, Milislerle Uyum", bas);
        Yeni("Refet Bey", "Refet", 7, 7, 7, 7, 8, 7, "Çok Yönlü", bas);
        Yeni("Kâzım Bey (Özalp)", "Kâzım Özalp", 7, 7, 7, 7, 9, 6, "Milis Örgütçüsü", bas);
        Yeni("Ali Bey (Çetinkaya)", "Ali Çetinkaya", 6, 6, 5, 8, 9, 5, "İlk Direniş", bas);
        Yeni("İsmet Bey", "İsmet", 8, 8, 8, 6, 9, 8, "Savunma Ustası, Diplomat", new DateTime(1920, 4, 9),
             "<b>İsmet Bey İstanbul'dan kaçıp Ankara'ya ulaştı.</b> Komuta edebilir.");
        Yeni("Fevzi Paşa", "Fevzi", 8, 7, 10, 6, 10, 5, "Kurmay, Disiplinli", new DateTime(1920, 4, 27),
             "<b>Fevzi Paşa Ankara'ya katıldı.</b> Komuta edebilir.");
        Yeni("Fahrettin Paşa", "Fahrettin", 8, 9, 6, 8, 9, 5, "Süvari Komutanı, Atılgan", new DateTime(1920, 6, 1),
             "<b>Fahrettin Paşa (Altay) Millî Mücadele'ye katıldı.</b> Süvari komutanı olarak atanabilir.");
        Yeni("Yakup Şevki Paşa", "Yakup Şevki", 7, 7, 7, 6, 9, 4, "Tecrübeli", new DateTime(1920, 8, 1),
             "<b>Yakup Şevki Paşa Ankara'ya katıldı.</b> Komuta edebilir.");
        Yeni("Nurettin Paşa", "Nurettin", 8, 7, 7, 5, 7, 4, "Sert Komutan", new DateTime(1920, 10, 1),
             "<b>Nurettin Paşa Millî Mücadele'ye katıldı.</b> Komuta edebilir.");
    }

    public static Komutan Bul(string kisaAd)
    {
        foreach (Komutan k in Liste) if (k.kisaAd == kisaAd) return k;
        return null;
    }

    // Harita kurulduktan sonra: başlangıç orduları tarihteki komutanlarıyla
    public static void Baslat()
    {
        foreach (Komutan k in Liste) k.katildi = k.katilmaTarihi <= new DateTime(1919, 5, 19);
        Ata(Bul("Ali Fuat"), OrduBul("20. Kolordu"));
        Ata(Bul("Kâzım Karabekir"), OrduBul("15. Kolordu"));
        Ata(Bul("Refet"), OrduBul("3. Kolordu"));
        Ata(Bul("Kâzım Özalp"), OrduBul("61. Tümen"));
        Ata(Bul("Ali Çetinkaya"), OrduBul("57. Tümen"));
    }

    static Ordu OrduBul(string ad)
    {
        foreach (Ordu o in UnityEngine.Object.FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.orduAdi == ad && o.Yasiyor) return o;
        return null;
    }

    // Komutanı orduya atar (önceki görevinden ve ordunun eski komutanından ayırır)
    public static void Ata(Komutan k, Ordu o)
    {
        if (k == null || o == null) return;
        if (k.ordu != null) k.ordu.komutan = null;
        if (o.komutan != null) o.komutan.ordu = null;
        k.ordu = o;
        o.komutan = k;
    }

    public static void Birak(Ordu o)
    {
        if (o == null || o.komutan == null) return;
        o.komutan.ordu = null;
        o.komutan = null;
    }

    public static List<Komutan> Bostakiler()
    {
        List<Komutan> l = new List<Komutan>();
        foreach (Komutan k in Liste) if (k.Musait && k.ordu == null) l.Add(k);
        return l;
    }

    // Her gün: yeni katılanlar, iyileşen yaralılar
    public static List<string> GunIlerle(DateTime tarih)
    {
        List<string> haberler = new List<string>();
        foreach (Komutan k in Liste)
        {
            if (!k.canli) continue;
            if (!k.katildi && tarih >= k.katilmaTarihi)
            {
                k.katildi = true;
                if (k.katilmaHaberi != null) haberler.Add(k.katilmaHaberi);
            }
            if (k.yaraliGun > 0)
            {
                k.yaraliGun--;
                if (k.yaraliGun == 0) haberler.Add(k.ad + " iyileşti ve yeniden göreve hazır.");
            }
        }
        return haberler;
    }

    // ---------- Etkiler ----------

    // Ordunun genel gücüne komuta yeteneği: 5 nötr, 10'da +%15
    public static float GucCarpani(Ordu o)
    {
        if (o == null || o.komutan == null) return 1f;
        Komutan k = o.komutan;
        float c = 1f + (k.komuta - 5) * 0.03f;
        if (k.Var("Muzaffer")) c *= 1.05f;
        return c;
    }

    // Belirli bir çarpışmada (saldırı ya da savunma) komutan özelliklerinin etkisi
    public static float SavasCarpani(Ordu biz, Ordu karsi, bool saldiri)
    {
        if (biz == null || biz.komutan == null) return 1f;
        Komutan k = biz.komutan;
        float c = 1f;
        if (saldiri && k.Var("Atılgan")) c *= 1.10f;
        if (saldiri && k.Var("Stratejist")) c *= 1.05f;
        if (!saldiri && k.Var("Savunma Ustası")) c *= 1.12f;
        if (k.Var("Temkinli")) c *= saldiri ? 0.95f : 1.05f;
        if (karsi != null && karsi.taraf == Taraf.Ermeni && k.Var("Doğu Cephesi Komutanı")) c *= 1.10f;
        return c;
    }

    // Baskında milis ve gerilla tecrübesi
    public static float BaskinCarpani(Ordu o)
    {
        if (o == null || o.komutan == null) return 1f;
        Komutan k = o.komutan;
        return (k.Var("Milislerle Uyum") || k.Var("Milis Örgütçüsü") || k.Var("İlk Direniş")) ? 1.3f : 1f;
    }

    // İkmal sıkıntısında idare yeteneği: kayıplar azalır
    public static float YipranmaCarpani(Ordu o)
    {
        if (o == null || o.komutan == null) return 1f;
        Komutan k = o.komutan;
        float c = 1f - (k.idare - 5) * 0.06f;
        if (k.Var("Kurmay") || k.Var("Disiplinli")) c *= 0.85f;
        return Mathf.Clamp(c, 0.4f, 1.2f);
    }

    // Savaş ekranında karargâhın etki alanı ve moral desteği
    public static float AuraYaricapi(Komutan k) { return k == null ? 12f : 8f + k.karizma * 0.8f; }
    public static float MoralDayanimi(Komutan k)
    {
        if (k == null) return 0.8f;
        float d = 1f - k.karizma * 0.035f;
        if (k.Var("Sert Komutan")) d -= 0.05f;
        return Mathf.Clamp(d, 0.55f, 0.85f);
    }

    // ---------- Muharebe sonrası: tecrübe, yaralanma, ölüm ----------

    // kazanan/kaybeden: muharebeden ÖNCEKİ komutanlar (ordu dağılırsa komutan zaten boşa düşmüştür)
    public static List<string> MuharebeSonucu(Komutan kazanan, Komutan kaybeden, bool kaybedenDagildi,
                                              bool kazananKarargahDustu, bool kaybedenKarargahDustu)
    {
        List<string> h = new List<string>();
        if (kazanan != null)
        {
            kazanan.zafer++;
            if (kazanan.zafer >= 3 && !kazanan.Var("Muzaffer"))
            {
                kazanan.ozellikler.Add("Muzaffer");
                h.Add("<color=#9f9>" + kazanan.ad + " üst üste kazandığı muharebelerle <b>Muzaffer</b> unvanını aldı (+%5 güç).</color>");
            }
            KaderZarı(kazanan, kazananKarargahDustu ? 0.10f : 0.005f, kazananKarargahDustu ? 0.30f : 0.02f, h);
        }
        if (kaybeden != null)
        {
            kaybeden.yenilgi++;
            if (kaybeden.yenilgi >= 3 && !kaybeden.Var("Temkinli"))
            {
                kaybeden.ozellikler.Add("Temkinli");
                h.Add("<color=#fc8>" + kaybeden.ad + " ağır yenilgilerden sonra <b>Temkinli</b> oldu (savunmada +%5, saldırıda −%5).</color>");
            }
            float olum = kaybedenKarargahDustu ? 0.20f : kaybedenDagildi ? 0.15f : 0.03f;
            float yara = kaybedenKarargahDustu ? 0.40f : kaybedenDagildi ? 0.35f : 0.10f;
            KaderZarı(kaybeden, olum, yara, h);
        }
        return h;
    }

    static void KaderZarı(Komutan k, float olum, float yara, List<string> h)
    {
        float z = UnityEngine.Random.value;
        if (z < olum)
        {
            if (k.olumsuz)
            {
                // Mustafa Kemal ölmez: hafif yara alır ama cepheden ayrılmaz
                h.Add("<color=#fc8><b>" + k.ad + " hafif yaralandı</b>, ancak cepheden ayrılmadı.</color>");
                return;
            }
            k.canli = false;
            Ordu o = k.ordu;
            if (o != null) Birak(o);
            h.Add("<color=#f66><b>" + k.ad + " şehit düştü!</b>" + (o != null ? " " + o.orduAdi + " komutansız kaldı." : "") + "</color>");
        }
        else if (z < olum + yara)
        {
            if (k.olumsuz) { h.Add("<color=#fc8>" + k.ad + " hafif yaralandı, görevine devam ediyor.</color>"); return; }
            k.yaraliGun = UnityEngine.Random.Range(30, 61);
            Ordu o = k.ordu;
            if (o != null) Birak(o);
            h.Add("<color=#fc8><b>" + k.ad + " yaralandı</b> (" + k.yaraliGun + " gün tedavi)."
                  + (o != null ? " " + o.orduAdi + " komutansız kaldı; yeni bir komutan ata." : "") + "</color>");
        }
    }
}
