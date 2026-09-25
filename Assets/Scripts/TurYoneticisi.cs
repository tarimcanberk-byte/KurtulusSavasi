using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Oyunun zamanını ve ekonomisini yönetir.
// Her "Turu Bitir" tıklamasında: tarihî olaylar → düşman hamleleri → gelir → giderler → oyun sonu kontrolü.
public class TurYoneticisi : MonoBehaviour
{
    // Inspector'dan bağlanan arayüz parçaları
    public TMP_Text tarihYazisi;
    public Button turuBitirDugmesi;

    public int turBasinaGun = 14;     // 1 tur = 2 hafta

    // Hazine
    public int para = 150;
    public int erzak = 300;

    // Tekalif-i Milliye (acil durum kararı)
    public int tekalifPara = 300;
    public int tekalifErzak = 800;
    public int tekalifSuresi = 6;          // kaç tur üretim düşük kalır
    public float tekalifUretimCarpani = 0.75f;

    private DateTime tarih = new DateTime(1919, 5, 19);
    private int turSayisi = 1;
    private int tekalifKalanTur = 0;
    private bool tekalifKullanildi = false;
    private Button tekalifDugmesi;

    // Oyun sonu takibi
    private bool oyunBitti = false;
    private int ankarasizTur = 0;
    private int ordusuzTur = 0;

    private static readonly string[] aylar =
    {
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
    };

    public string TarihYazisi { get { return tarih.Day + " " + aylar[tarih.Month - 1] + " " + tarih.Year; } }

    void Start()
    {
        turuBitirDugmesi.onClick.AddListener(TuruBitir);
        TekalifDugmesiOlustur();
        EkraniGuncelle();
    }

    public void TuruBitir()
    {
        tarih = tarih.AddDays(turBasinaGun);
        turSayisi++;

        HaritaSecici secici = FindAnyObjectByType<HaritaSecici>();
        if (secici != null) secici.SecimleriTemizle();

        List<string> rapor = new List<string>();
        rapor.AddRange(TarihselOlaylar.Kontrol(tarih, this));   // 1) Tarih akar
        rapor.AddRange(DusmanYapayZeka.TurOyna());              // 2) Düşman hamle yapar
        KaynaklariTopla();                                      // 3) Gelir
        OrdulariBesle(rapor);                                   // 4) Giderler
        if (tekalifKalanTur > 0) tekalifKalanTur--;

        rapor.RemoveAll(string.IsNullOrEmpty);
        EkraniGuncelle();
        if (secici != null) secici.TurRaporuGoster(TarihYazisi, rapor);
        OyunSonuKontrol();
    }

    // ---------- Ekonomi ----------

    float UretimCarpani { get { return tekalifKalanTur > 0 ? tekalifUretimCarpani : 1f; } }

    void KaynaklariTopla()
    {
        HesaplaGelir(out int p, out int e);
        para += p;
        erzak += e;
    }

    void HesaplaGelir(out int p, out int e)
    {
        p = 0; e = 0;
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None))
        {
            if (s.sahip != Taraf.Turk) continue;
            p += s.paraUretimi;
            e += s.erzakUretimi;
        }
        p = Mathf.RoundToInt(p * UretimCarpani);
        e = Mathf.RoundToInt(e * UretimCarpani);
    }

    void HesaplaGider(out int p, out int e)
    {
        p = 0; e = 0;
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.OyuncununMu && o.Yasiyor) { p += o.MaasGideri; e += o.ErzakTuketimi; }
    }

    // Her ordu erzak yer ve maaş alır. Erzak yoksa moral düşer; maaş yoksa moral düşer ve firar olur.
    void OrdulariBesle(List<string> rapor)
    {
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            if (!o.Yasiyor) continue;
            if (!o.OyuncununMu) { o.YeniTur(true); continue; }   // düşman ordularını kendi ülkeleri besliyor

            bool erzakYetti = erzak >= o.ErzakTuketimi;
            if (erzakYetti) erzak -= o.ErzakTuketimi;
            else rapor.Add("<color=#f99>" + o.orduAdi + " aç kaldı, moral düşüyor.</color>");

            if (para >= o.MaasGideri) para -= o.MaasGideri;
            else
            {
                int firar = Mathf.RoundToInt(o.askerSayisi * 0.05f);
                o.MoralDegistir(-10);
                rapor.Add("<color=#f99>" + o.orduAdi + " maaş alamadı: " + firar + " asker firar etti.</color>");
                o.KayipVer(firar);
                if (!o.Yasiyor) continue;
            }
            o.YeniTur(erzakYetti);
        }
    }

    // ---------- Tekalif-i Milliye ----------

    void TekalifDugmesiOlustur()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.name = "TekalifDugmesi";
        d.transform.SetParent(canvas.transform, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(1f, 1f);
        r.sizeDelta = new Vector2(340f, 80f);
        r.anchoredPosition = new Vector2(-30f, -20f);
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = "Tekalif-i Milliye\n<size=65%>acil durum kararı (1 kez)</size>";
        t.fontSize = 26;
        tekalifDugmesi = d.GetComponent<Button>();
        tekalifDugmesi.onClick.AddListener(TekalifIlanEt);
    }

    void TekalifIlanEt()
    {
        if (tekalifKullanildi) return;
        tekalifKullanildi = true;
        para += tekalifPara;
        erzak += tekalifErzak;
        tekalifKalanTur = tekalifSuresi;
        tekalifDugmesi.gameObject.SetActive(false);
        EkraniGuncelle();

        HaritaSecici secici = FindAnyObjectByType<HaritaSecici>();
        if (secici != null)
            secici.TurRaporuGoster(TarihYazisi, new List<string> {
                "<b>Tekalif-i Milliye emirleri yayımlandı.</b> Halktan erzak, giyecek ve hayvan toplandı: +"
                + tekalifPara + " para, +" + tekalifErzak + " erzak.",
                "<color=#f99>Halk yoruldu: " + tekalifSuresi + " tur boyunca sancak üretimi %"
                + Mathf.RoundToInt((1f - tekalifUretimCarpani) * 100) + " düşük.</color>" });
    }

    // ---------- Oyun sonu ----------

    void OyunSonuKontrol()
    {
        if (oyunBitti) return;

        // Zafer: İzmir bizde ve haritada Yunan askeri kalmadı
        Sancak izmir = Sancak.Bul("İzmir");
        if (izmir != null && izmir.sahip == Taraf.Turk && TarihselOlaylar.ToplamAsker(Taraf.Yunan) == 0)
        {
            oyunBitti = true;
            OyunSonuPaneli.Goster("ZAFER!", LozanRaporu(), true);
            return;
        }

        // Yenilgi 1: Ankara 4 tur boyunca düşman elinde
        Sancak ankara = Sancak.Bul("Ankara");
        ankarasizTur = (ankara != null && ankara.sahip != Taraf.Turk) ? ankarasizTur + 1 : 0;
        if (ankarasizTur >= 4)
        {
            oyunBitti = true;
            OyunSonuPaneli.Goster("YENİLGİ", "Ankara düştü ve geri alınamadı.\nMillî Mücadele'nin merkezi kaybedildi.", false);
            return;
        }

        // Yenilgi 2: ordumuz 3 tur boyunca 2000 askerin altında
        ordusuzTur = TarihselOlaylar.ToplamAsker(Taraf.Turk) < 2000 ? ordusuzTur + 1 : 0;
        if (ordusuzTur >= 3)
        {
            oyunBitti = true;
            OyunSonuPaneli.Goster("YENİLGİ", "Ordu dağıldı.\nDirenecek kuvvet kalmadı.", false);
        }
    }

    // Lozan masasında elde edilenler: oyuncunun o anki durumuna göre
    string LozanRaporu()
    {
        string r = "Yunan ordusu Anadolu'dan atıldı. Barış görüşmeleri başlıyor.\n"
                 + "<size=85%>Zafer tarihi: " + TarihYazisi + " (tarihte: 9 Eylül 1922)</size>\n\n"
                 + "<b>Lozan masasında durumun:</b>\n";

        r += Madde(TumuBizde("İstanbul", "Çanakkale", "İzmit"),
                   "Boğazlar ve İstanbul tamamen senin kontrolünde.",
                   "Boğazlar İtilaf kontrolünde; tarihteki gibi müzakere edilecek.");
        r += Madde(TumuBizde("Adana", "Antep", "Maraş", "Urfa"),
                   "Güney sınırı tamam.",
                   "Güneyde işgal altında sancaklar var.");
        r += Madde(TumuBizde("Kars"),
                   "Doğu sınırı Kars dahil güvende.",
                   "Kars henüz geri alınmadı.");
        r += Madde(TumuBizde("Antalya", "Menteşe"),
                   "Akdeniz kıyıları tamam.",
                   "Güneybatıda işgal sürüyor.");

        int eksik = 0;
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None)) if (s.sahip != Taraf.Turk) eksik++;
        string sonuc = eksik == 0 ? "Tarihten de büyük bir başarı: bütün Anadolu bağımsız."
                     : eksik <= 3 ? "Tarihe yakın bir başarı: Misak-ı Millî'nin büyük kısmı sağlandı."
                     : "Kısmi bir zafer: bazı bölgeler hâlâ işgal altında.";
        return r + "\n<b>" + sonuc + "</b>";
    }

    static string Madde(bool tamam, string iyi, string kotu)
    {
        return (tamam ? "<color=#9f9>+ " + iyi : "<color=#fc8>– " + kotu) + "</color>\n";
    }

    static bool TumuBizde(params string[] adlar)
    {
        foreach (string a in adlar)
        {
            Sancak s = Sancak.Bul(a);
            if (s != null && s.sahip != Taraf.Turk) return false;
        }
        return true;
    }

    // ---------- Ekran ----------

    public void EkraniGuncelle()
    {
        HesaplaGelir(out int gp, out int ge);
        HesaplaGider(out int cp, out int ce);
        tarihYazisi.text = TarihYazisi
            + "   |   Tur " + turSayisi
            + "   |   Para: " + para + " <size=70%>(" + Isaretli(gp - cp) + ")</size>"
            + "   |   Erzak: " + erzak + " <size=70%>(" + Isaretli(ge - ce) + ")</size>"
            + "   |   Sancak: " + TurkSancakSayisi()
            + (tekalifKalanTur > 0 ? "   <size=70%><color=#fc8>Tekalif: " + tekalifKalanTur + " tur</color></size>" : "");
    }

    static string Isaretli(int n) { return (n >= 0 ? "<color=#9f9>+" : "<color=#f99>") + n + "</color>"; }

    int TurkSancakSayisi()
    {
        int n = 0;
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None))
            if (s.sahip == Taraf.Turk) n++;
        return n;
    }
}
