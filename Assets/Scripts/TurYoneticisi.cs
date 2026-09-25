using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Zaman Yöneticisi (Hearts of Iron tarzı): zaman gün gün akar, oyuncu duraklatıp hızlandırabilir.
//   Her gün   : ordular yürür ve çarpışır, otomatik emirler düşünür, tarihî olaylar kontrol edilir
//   Her hafta : gelir, giderler, ikmal yıpranması, düşman kararları
// (Sınıfın adı eski "tur" sisteminden kaldı; sahnedeki bağlantılar bozulmasın diye değiştirmedik.)
public class TurYoneticisi : MonoBehaviour
{
    public TMP_Text tarihYazisi;
    public Button turuBitirDugmesi;        // artık "Duraklat / Devam" düğmesi
    public int turBasinaGun = 14;          // (eski ayar, kullanılmıyor)

    // Hazine
    public int para = 150;
    public int erzak = 300;

    // Tekalif-i Milliye (acil durum kararı)
    public int tekalifPara = 300;
    public int tekalifErzak = 800;
    public int tekalifSuresiGun = 84;      // 12 hafta
    public float tekalifUretimCarpani = 0.75f;

    // Hız: bir oyun gününün gerçek saniye karşılığı
    private static readonly float[] gunSuresi = { 0f, 1.0f, 0.5f, 0.25f, 0.1f, 0.04f };
    private int hiz = 2;
    private bool duraklatildi = true;      // oyun duraklatılmış başlar
    private float birikim = 0f;

    private DateTime tarih = new DateTime(1919, 5, 19);
    private int gunSayisi = 0;
    private int tekalifKalanGun = 0;
    private bool tekalifKullanildi = false;
    private Button tekalifDugmesi;
    private TMP_Text duraklatYazisi;

    private bool oyunBitti = false;
    private int ankarasizGun = 0;
    private int ordusuzGun = 0;

    private static readonly string[] aylar =
    {
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
    };

    public string TarihYazisi { get { return tarih.Day + " " + aylar[tarih.Month - 1] + " " + tarih.Year; } }
    public bool Duraklatildi { get { return duraklatildi; } }

    void Awake()
    {
        Gunluk.Sifirla();
        Gunluk.OlayEklendi += OlayGeldi;
    }

    void OnDestroy()
    {
        Gunluk.OlayEklendi -= OlayGeldi;
    }

    void Start()
    {
        turuBitirDugmesi.onClick.AddListener(DuraklatDevam);
        ArayuzYardimci.DugmeStili(turuBitirDugmesi);
        duraklatYazisi = turuBitirDugmesi.GetComponentInChildren<TMP_Text>();
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas != null) ArayuzYardimci.UstSeritOlustur(canvas, 100f);
        HizDugmesi(">>", new Vector2(-320f, 30f), () => HizDegistir(+1));
        HizDugmesi("<<", new Vector2(-410f, 30f), () => HizDegistir(-1));
        TekalifDugmesiOlustur();
        EkraniGuncelle();
        Gunluk.Ekle("<b>19 Mayıs 1919.</b> Mustafa Kemal Samsun'a çıktı. Oyun duraklatıldı: başlatmak için <b>Boşluk</b> tuşuna ya da <b>Devam Et</b>'e bas.");
    }

    // ---------- Zaman kontrolü ----------

    void Update()
    {
        // Seçili kalan düğmeler Boşluk tuşunu yutmasın
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null)
            EventSystem.current.SetSelectedGameObject(null);

        // Savaş ekranı ya da muharebe kararı açıkken harita zamanı donar
        if (MuharebeYoneticisi.Mesgul) return;

        Keyboard k = Keyboard.current;
        if (k != null)
        {
            if (k.spaceKey.wasPressedThisFrame) DuraklatDevam();
            if (k.digit1Key.wasPressedThisFrame) HizAyarla(1);
            if (k.digit2Key.wasPressedThisFrame) HizAyarla(2);
            if (k.digit3Key.wasPressedThisFrame) HizAyarla(3);
            if (k.digit4Key.wasPressedThisFrame) HizAyarla(4);
            if (k.digit5Key.wasPressedThisFrame) HizAyarla(5);
            if (k.numpadPlusKey.wasPressedThisFrame || k.equalsKey.wasPressedThisFrame) HizDegistir(+1);
            if (k.numpadMinusKey.wasPressedThisFrame || k.minusKey.wasPressedThisFrame) HizDegistir(-1);
        }

        if (duraklatildi || oyunBitti) return;
        birikim += Time.deltaTime;
        // Çok hızlıda bir karede birden fazla gün geçebilir; bir olay duraklatırsa hemen dur
        while (birikim >= gunSuresi[hiz] && !duraklatildi && !oyunBitti)
        {
            birikim -= gunSuresi[hiz];
            GunIlerle();
        }
    }

    public void DuraklatDevam()
    {
        if (oyunBitti || MuharebeYoneticisi.Mesgul) return;
        duraklatildi = !duraklatildi;
        birikim = 0f;
        EkraniGuncelle();
    }

    public void Duraklat() { duraklatildi = true; EkraniGuncelle(); }
    public void Devam() { if (oyunBitti) return; duraklatildi = false; birikim = 0f; EkraniGuncelle(); }

    void HizAyarla(int yeni) { hiz = Mathf.Clamp(yeni, 1, 5); EkraniGuncelle(); }
    void HizDegistir(int fark) { HizAyarla(hiz + fark); }

    void HizDugmesi(string yazi, Vector2 konum, UnityEngine.Events.UnityAction islem)
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.name = "HizDugmesi " + yazi;
        d.transform.SetParent(canvas.transform, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(1f, 0f);
        r.sizeDelta = new Vector2(80f, 80f);
        r.anchoredPosition = konum;
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi;
        t.fontSize = 32;
        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(islem);
        ArayuzYardimci.DugmeStili(b);
    }

    // Günlüğe önemli bir olay düşerse oyunu duraklat
    void OlayGeldi(string metin, bool duraklat)
    {
        if (duraklat && !duraklatildi) Duraklat();
    }

    // ---------- Bir gün ----------

    void GunIlerle()
    {
        tarih = tarih.AddDays(1);
        gunSayisi++;

        // 1) Tarihî olaylar (her biri oyunu duraklatır)
        foreach (string olay in TarihselOlaylar.Kontrol(tarih, this))
            Gunluk.Ekle(olay, true);

        // 2) Ordular yürür ve çarpışır
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            if (!o.Yasiyor) continue;
            if (o.OyuncununMu) o.OtomatikEmirleriUygula();
            Taraf eskiSahipKontrol = Taraf.Turk;
            Sancak hedef = o.SonrakiDurak;
            bool hedefBizimdi = hedef != null && hedef.sahip == eskiSahipKontrol;
            string sonuc = o.GunIlerle();
            if (sonuc == null) continue;
            // Bir Türk sancağı düştüyse ya da Türk ordusu dağıldıysa duraklat
            bool kotu = hedefBizimdi && hedef.sahip != Taraf.Turk;
            Gunluk.Ekle(sonuc.Replace("\n", " "), kotu);
        }

        // 3) Haftalık işler
        if (gunSayisi % 7 == 0)
        {
            foreach (string satir in DusmanYapayZeka.TurOyna()) Gunluk.Ekle(satir);
            HaftalikEkonomi();
        }
        if (tekalifKalanGun > 0) tekalifKalanGun--;

        EkraniGuncelle();
        HaritaSecici secici = FindAnyObjectByType<HaritaSecici>();
        if (secici != null) secici.BilgiyiYenile();
        OyunSonuKontrol();
    }

    // ---------- Ekonomi (haftalık; değerler 2 haftalık tanımlı olduğu için yarısı) ----------

    float UretimCarpani { get { return tekalifKalanGun > 0 ? tekalifUretimCarpani : 1f; } }

    void HesaplaGelir(out int p, out int e)
    {
        p = 0; e = 0;
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None))
        {
            if (s.sahip != Taraf.Turk) continue;
            p += s.paraUretimi;
            e += s.erzakUretimi;
        }
        p = Mathf.RoundToInt(p * UretimCarpani * 0.5f);
        e = Mathf.RoundToInt(e * UretimCarpani * 0.5f);
    }

    void HesaplaGider(out int p, out int e)
    {
        p = 0; e = 0;
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.OyuncununMu && o.Yasiyor) { p += o.MaasGideri; e += o.ErzakTuketimi; }
        p = Mathf.CeilToInt(p * 0.5f);
        e = Mathf.CeilToInt(e * 0.5f);
    }

    void HaftalikEkonomi()
    {
        HesaplaGelir(out int gp, out int ge);
        para += gp;
        erzak += ge;

        // İkmal yıpranması
        Dictionary<Taraf, int> yipranma = new Dictionary<Taraf, int>();
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            if (!o.Yasiyor) continue;
            int kayip = o.IkmalYipranmasi(0.5f);
            if (kayip > 0) yipranma[o.taraf] = (yipranma.ContainsKey(o.taraf) ? yipranma[o.taraf] : 0) + kayip;
        }
        foreach (var y in yipranma)
            Gunluk.Ekle((y.Key == Taraf.Turk ? "<color=#f99>" : "<color=#9f9>") + TarafBilgi.Ad(y.Key)
                        + " ordularında ikmal sıkıntısı: " + y.Value + " asker kaybı.</color>");

        // Erzak ve maaş
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            if (!o.Yasiyor) continue;
            if (!o.OyuncununMu) { o.YeniTur(true); continue; }

            int yemek = Mathf.CeilToInt(o.ErzakTuketimi * 0.5f);
            int maas = Mathf.CeilToInt(o.MaasGideri * 0.5f);

            bool erzakYetti = erzak >= yemek;
            if (erzakYetti) erzak -= yemek;
            else Gunluk.Ekle("<color=#f99>" + o.orduAdi + " aç kaldı, moral düşüyor.</color>");

            if (para >= maas) para -= maas;
            else
            {
                int firar = Mathf.RoundToInt(o.askerSayisi * 0.03f);
                o.MoralDegistir(-6);
                Gunluk.Ekle("<color=#f99>" + o.orduAdi + " maaş alamadı: " + firar + " asker firar etti.</color>");
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
        r.anchoredPosition = new Vector2(-30f, -110f);
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = "Tekalif-i Milliye\n<size=65%>acil durum kararı (1 kez)</size>";
        t.fontSize = 26;
        tekalifDugmesi = d.GetComponent<Button>();
        tekalifDugmesi.onClick.AddListener(TekalifIlanEt);
        ArayuzYardimci.DugmeStili(tekalifDugmesi);
    }

    void TekalifIlanEt()
    {
        if (tekalifKullanildi) return;
        tekalifKullanildi = true;
        para += tekalifPara;
        erzak += tekalifErzak;
        tekalifKalanGun = tekalifSuresiGun;
        tekalifDugmesi.gameObject.SetActive(false);
        EkraniGuncelle();
        Gunluk.Ekle("<b>Tekalif-i Milliye emirleri yayımlandı.</b> +" + tekalifPara + " para, +" + tekalifErzak
                    + " erzak. <color=#f99>Halk yoruldu: " + (tekalifSuresiGun / 7) + " hafta boyunca üretim %"
                    + Mathf.RoundToInt((1f - tekalifUretimCarpani) * 100) + " düşük.</color>");
    }

    // ---------- Oyun sonu ----------

    void OyunSonuKontrol()
    {
        if (oyunBitti) return;

        Sancak izmir = Sancak.Bul("İzmir");
        if (izmir != null && izmir.sahip == Taraf.Turk && TarihselOlaylar.ToplamAsker(Taraf.Yunan) == 0)
        {
            oyunBitti = true;
            OyunSonuPaneli.Goster("ZAFER!", LozanRaporu(), true);
            return;
        }

        Sancak ankara = Sancak.Bul("Ankara");
        ankarasizGun = (ankara != null && ankara.sahip != Taraf.Turk) ? ankarasizGun + 1 : 0;
        if (ankarasizGun == 1) Gunluk.Ekle("<color=#f66><b>Ankara düştü!</b> 8 hafta içinde geri alınmazsa Millî Mücadele çöker.</color>", true);
        if (ankarasizGun >= 56)
        {
            oyunBitti = true;
            OyunSonuPaneli.Goster("YENİLGİ", "Ankara düştü ve geri alınamadı.\nMillî Mücadele'nin merkezi kaybedildi.", false);
            return;
        }

        ordusuzGun = TarihselOlaylar.ToplamAsker(Taraf.Turk) < 2000 ? ordusuzGun + 1 : 0;
        if (ordusuzGun >= 42)
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
        r += Madde(TumuBizde("Adana", "Hatay", "Antep", "Maraş", "Urfa"),
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
            + "   |   " + (duraklatildi ? "<color=#fc8>DURAKLATILDI</color>" : "Hız " + hiz)
            + "   |   Para: " + para + " <size=70%>(" + Isaretli(gp - cp) + "/hafta)</size>"
            + "   |   Erzak: " + erzak + " <size=70%>(" + Isaretli(ge - ce) + "/hafta)</size>"
            + "   |   Sancak: " + TurkSancakSayisi()
            + (tekalifKalanGun > 0 ? "   <size=70%><color=#fc8>Tekalif: " + tekalifKalanGun + " gün</color></size>" : "");
        if (duraklatYazisi != null)
            duraklatYazisi.text = duraklatildi ? "Devam Et\n<size=60%>(Boşluk)</size>" : "Duraklat\n<size=60%>(Boşluk)</size>";
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
