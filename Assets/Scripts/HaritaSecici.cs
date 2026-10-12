using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Harita kontrolleri (Total War tarzı):
//   Sol tık  = seç (sancak ya da ordu)
//   Sağ tık  = seçili orduyla yürü / saldır
// Ayrıca bilgi panelini, "Birlik Topla" ve "Orduları Birleştir" düğmelerini yönetir.
public class HaritaSecici : MonoBehaviour
{
    public TMP_Text bilgiYazisi;
    public int birlikMaliyeti = 150;     // para
    public int birlikBuyuklugu = 1000;   // asker

    private Sancak seciliSancak;
    private Ordu seciliOrdu;
    private Button komutanDugmesi;
    private GameObject komutanPaneli;
    private Button toplaDugmesi;
    private Button birlestirDugmesi;
    private Button baskinDugmesi;
    private bool baskinModu = false;
    private Button sinirDugmesi, taarruzDugmesi, bekleDugmesi;
    private TMP_Text gunlukYazisi;
    private RectTransform gunlukPaneli;
    private const string Ipucu = "Sol tık: seç   |   Sağ tık: seçili orduyu gönder   |   Boşluk: duraklat   |   1-5: hız\n<size=80%>WASD: kaydır, tekerlek: yakınlaş</size>";

    private RectTransform bilgiPaneli;

    void Start()
    {
        if (bilgiYazisi != null) bilgiPaneli = ArayuzYardimci.ArkaPanelOlustur(bilgiYazisi.rectTransform);
        BilgiGoster(Ipucu);
        // Sancak seçiliyken
        toplaDugmesi = DugmeOlustur("BirlikToplaDugmesi", "Birlik Topla\n<size=70%>" + birlikMaliyeti + " para</size>", 125f, BirlikTopla);
        // Ordu seçiliyken (Hearts of Iron tarzı emirler)
        sinirDugmesi   = DugmeOlustur("SinirDugmesi",   "Sınırı Savun\n<size=60%>bölgedeki en tehlikeli sınıra gider</size>", 125f, () => EmirVer(Ordu.Emir.SinirSavunmasi));
        taarruzDugmesi = DugmeOlustur("TaarruzDugmesi", "Taarruz\n<size=60%>üstün olduğu yerlere saldırır</size>",       205f, () => EmirVer(Ordu.Emir.Taarruz));
        bekleDugmesi   = DugmeOlustur("BekleDugmesi",   "Bekle / Siper Kaz\n<size=60%>yürüyüşü durdurur</size>",          285f, () => EmirVer(Ordu.Emir.Bekle));
        baskinDugmesi  = DugmeOlustur("BaskinDugmesi",  "Baskın Yap\n<size=60%>düşmanı yıprat (haftada 1)</size>",        365f, BaskinModunuAc);
        birlestirDugmesi = DugmeOlustur("BirlestirDugmesi", "Orduları Birleştir",                                        445f, OrdulariBirlestir);
        komutanDugmesi = DugmeOlustur("KomutanDugmesi", "Komutan Ata\n<size=60%>orduya komutan seç</size>",            525f, KomutanPaneliAc);
        GunlukPaneliOlustur();
        DugmeleriGuncelle();
    }

    void Update()
    {
        if (MuharebeYoneticisi.Mesgul) return;   // savaş ekranı açıkken harita tıklanmaz
        Mouse fare = Mouse.current;
        if (fare == null) return;
        bool solTik = fare.leftButton.wasPressedThisFrame;
        bool sagTik = fare.rightButton.wasPressedThisFrame;
        if (!solTik && !sagTik) return;

        // Fare bir arayüz düğmesinin üzerindeyse haritaya tıklanmış sayma
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Ray isin = Camera.main.ScreenPointToRay(fare.position.ReadValue());
        bool carpti = Physics.Raycast(isin, out RaycastHit carpma);
        Ordu ordu = carpti ? carpma.collider.GetComponent<Ordu>() : null;
        Sancak sancak = carpti ? carpma.collider.GetComponent<Sancak>() : null;

        if (solTik)
        {
            if (ordu != null) OrduSec(ordu);
            else if (sancak != null) SancakSec(sancak);
            else { SecimleriTemizle(); BilgiGoster(Ipucu); }
        }
        else // sağ tık
        {
            if (seciliOrdu == null) return;
            if (ordu != null) sancak = ordu.bulunduguSancak;   // düşman ordusuna sağ tık = onun sancağına saldır
            if (sancak != null) Emret(sancak);
        }
    }

    // ---------- Seçim ----------

    void OrduSec(Ordu ordu)
    {
        SecimleriTemizle();
        ordu.Sec();
        if (ordu.OyuncununMu)
        {
            seciliOrdu = ordu;
            foreach (Sancak k in ordu.bulunduguSancak.komsular)
                {
                    if (DusmanVarMi(k, ordu.taraf) || k.sahip != ordu.taraf) k.SaldiriHedefiGoster();
                    else k.HedefOlarakGoster();
                }
        }
        BilgiGoster(ordu.BilgiMetni());
        DugmeleriGuncelle();
    }

    void SancakSec(Sancak sancak)
    {
        SecimleriTemizle();
        seciliSancak = sancak;
        sancak.Sec();
        BilgiGoster(sancak.BilgiMetni());
        DugmeleriGuncelle();
    }

    // Barış halindeki bir tarafa saldırmadan önce onay istemek için
    private Sancak onayBekleyenHedef;

    // Hedef sancakta barış halinde olduğumuz bir taraf varsa onu döndürür
    static Taraf? BaristakiTaraf(Sancak hedef)
    {
        foreach (Ordu o in hedef.Ordular())
            if (!o.OyuncununMu && !Diplomasi.SavastaMi(o.taraf)) return o.taraf;
        if (hedef.sahip != Taraf.Turk && !Diplomasi.SavastaMi(hedef.sahip)) return hedef.sahip;
        return null;
    }

    // Seçili orduya "şuraya git" emri ver (yol otomatik bulunur)
    void Emret(Sancak hedef)
    {
        if (!seciliOrdu.HareketEdebilirMi(hedef, out string neden))
        {
            BilgiGoster("<color=#ff8080>" + neden + "</color>\n" + seciliOrdu.BilgiMetni());
            return;
        }

        // Yol üzerinde barış halinde olduğumuz birinin toprağı var mı?
        List<Sancak> yol = baskinModu ? new List<Sancak> { hedef } : Sancak.YolBul(seciliOrdu.bulunduguSancak, hedef);
        if (yol == null) { BilgiGoster("<color=#ff8080>Oraya giden yol yok.</color>"); return; }
        Taraf? baristaki = null;
        foreach (Sancak s in yol) { baristaki = BaristakiTaraf(s); if (baristaki.HasValue) { hedef = s; break; } }

        string savasRaporu = "";
        if (baristaki.HasValue)
        {
            if (onayBekleyenHedef != hedef)
            {
                onayBekleyenHedef = hedef;
                BilgiGoster("<color=#ffb080><b>Dikkat:</b> " + hedef.sancakAdi + " " + TarafBilgi.Ad(baristaki.Value)
                    + " kontrolünde ve onunla savaşta değiliz.\nSaldırırsan " + TarafBilgi.Ad(baristaki.Value)
                    + " savaşa girer ve takviye çıkarır. Onaylamak için tekrar sağ tıkla.</color>");
                return;
            }
            foreach (string satir in Diplomasi.SavasIlanEt(baristaki.Value)) Gunluk.Ekle(satir, true);
            hedef = yol[yol.Count - 1];
        }
        onayBekleyenHedef = null;

        // Baskın modundaysak tam saldırı yerine baskın yapılır
        if (baskinModu)
        {
            if (!seciliOrdu.HareketHakkiVar)
            {
                BilgiGoster("<color=#ff8080>Bu birlik son baskından sonra hâlâ dinleniyor.</color>\n" + seciliOrdu.BilgiMetni());
                return;
            }
            bool dusmanVar = false;
            foreach (Ordu o in hedef.Ordular()) if (o.taraf != seciliOrdu.taraf) dusmanVar = true;
            if (!dusmanVar || !seciliOrdu.bulunduguSancak.KomsuMu(hedef))
            {
                BilgiGoster("<color=#ff8080>Baskın için düşman ordusu olan komşu bir sancak seç.</color>\n" + seciliOrdu.BilgiMetni());
                return;
            }
            Ordu baskinci = seciliOrdu;
            string rapor = Savas.Baskin(baskinci, hedef);
            Gunluk.Ekle(rapor.Replace("\n", " "));
            baskinModu = false;
            OrduSec(baskinci);
            return;
        }

        Ordu ordu = seciliOrdu;
        if (ordu.GitEmri(hedef))
            OrduSec(ordu);   // seçili kalsın, bilgi güncellensin
        else
            BilgiGoster("<color=#ff8080>Oraya giden yol bulunamadı.</color>");
    }

    void EmirVer(Ordu.Emir emir)
    {
        if (seciliOrdu == null) return;
        Ordu o = seciliOrdu;
        o.EmirVer(emir);
        OrduSec(o);
    }

    // Zaman ilerledikçe seçili şeyin bilgisini tazele
    public void BilgiyiYenile()
    {
        if (baskinModu || onayBekleyenHedef != null) return;
        if (seciliOrdu != null)
        {
            if (seciliOrdu.Yasiyor) BilgiGoster(seciliOrdu.BilgiMetni());
            else { SecimleriTemizle(); BilgiGoster(Ipucu); }
        }
        else if (seciliSancak != null) BilgiGoster(seciliSancak.BilgiMetni());
        DugmeleriGuncelle();
    }

    // ---------- Olay günlüğü (sol üst) ----------

    void GunlukPaneliOlustur()
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        GameObject g = new GameObject("GunlukYazisi", typeof(RectTransform));
        g.transform.SetParent(canvas.transform, false);
        gunlukYazisi = g.AddComponent<TextMeshProUGUI>();
        gunlukYazisi.fontSize = 22;
        gunlukYazisi.alignment = TextAlignmentOptions.TopLeft;
        gunlukYazisi.raycastTarget = false;
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(30f, -120f);
        r.sizeDelta = new Vector2(900f, 400f);
        gunlukPaneli = ArayuzYardimci.ArkaPanelOlustur(r);
        Gunluk.OlayEklendi += GunlukGuncelle;
        GunlukGuncelle(null, false);
    }

    void OnDestroy() { Gunluk.OlayEklendi -= GunlukGuncelle; }

    void GunlukGuncelle(string yeni, bool duraklat)
    {
        if (gunlukYazisi == null) return;
        string metin = "<b>Son olaylar</b>";
        for (int i = Gunluk.SonOlaylar.Count - 1; i >= 0; i--) metin += "\n• " + Gunluk.SonOlaylar[i];
        gunlukYazisi.text = metin;
        Vector2 boyut = gunlukYazisi.GetPreferredValues(metin, 900f, 0f);
        RectTransform r = gunlukYazisi.rectTransform;
        gunlukPaneli.anchoredPosition = r.anchoredPosition + new Vector2(-12f, 12f);
        gunlukPaneli.sizeDelta = new Vector2(Mathf.Min(boyut.x, 900f) + 24f, boyut.y + 24f);
    }

    public void SecimleriTemizle()
    {
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None)) s.SecimiKaldir();
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None)) o.SecimiKaldir();
        seciliOrdu = null;
        seciliSancak = null;
        onayBekleyenHedef = null;
        baskinModu = false;
        DugmeleriGuncelle();
    }

    static bool DusmanVarMi(Sancak s, Taraf biz)
    {
        foreach (Ordu o in s.Ordular()) if (o.taraf != biz) return true;
        return false;
    }

    // ---------- Düğmeler ----------

    Button DugmeOlustur(string ad, string yazi, float yukseklik, UnityEngine.Events.UnityAction islem)
    {
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return null;

        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.name = ad;
        d.transform.SetParent(canvas.transform, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(1f, 0f);
        r.pivot = new Vector2(1f, 0f);
        r.sizeDelta = new Vector2(300f, 72f);
        r.anchoredPosition = new Vector2(-30f, yukseklik);

        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi;
        t.fontSize = 26;

        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(islem);
        ArayuzYardimci.DugmeStili(b);
        return b;
    }

    void DugmeleriGuncelle()
    {
        if (toplaDugmesi != null)
            toplaDugmesi.gameObject.SetActive(seciliSancak != null && seciliSancak.sahip == Taraf.Turk);
        bool orduVar = seciliOrdu != null;
        if (baskinDugmesi != null)  baskinDugmesi.gameObject.SetActive(orduVar);
        if (sinirDugmesi != null)   sinirDugmesi.gameObject.SetActive(orduVar);
        if (taarruzDugmesi != null) taarruzDugmesi.gameObject.SetActive(orduVar);
        if (bekleDugmesi != null)   bekleDugmesi.gameObject.SetActive(orduVar);
        if (komutanDugmesi != null) komutanDugmesi.gameObject.SetActive(orduVar);
        if (!orduVar) KomutanPaneliKapat();
        if (birlestirDugmesi != null)
            birlestirDugmesi.gameObject.SetActive(seciliOrdu != null && BizimOrdular(seciliOrdu.bulunduguSancak).Count > 1);
    }

    static List<Ordu> BizimOrdular(Sancak s)
    {
        List<Ordu> liste = new List<Ordu>();
        foreach (Ordu o in s.Ordular()) if (o.OyuncununMu) liste.Add(o);
        return liste;
    }

    // ---------- Baskın ----------

    void BaskinModunuAc()
    {
        if (seciliOrdu == null) return;
        baskinModu = !baskinModu;
        BilgiGoster(baskinModu
            ? "<color=#ffb080><b>Baskın modu:</b> Düşman ordusu bulunan komşu bir sancağa sağ tıkla.\n"
              + "Baskın tam bir muharebe değildir: düşmana %4-8 kayıp verdirir, sen az kayıp verirsin, yer değiştirmezsin.\n"
              + "Kuva-yi Milliye birlikleri ve ikmali uzamış düşmanlara karşı daha etkilidir.</color>"
            : seciliOrdu.BilgiMetni());
    }

    // ---------- Birlik toplama ----------

    void BirlikTopla()
    {
        TurYoneticisi tur = FindAnyObjectByType<TurYoneticisi>();
        if (seciliSancak == null || tur == null) return;

        if (tur.para < birlikMaliyeti)
        {
            BilgiGoster("<color=#ff8080>Yeterli para yok (" + birlikMaliyeti + " gerekli).</color>\n" + seciliSancak.BilgiMetni());
            return;
        }
        tur.para -= birlikMaliyeti;

        List<Ordu> bizimkiler = BizimOrdular(seciliSancak);
        string mesaj;
        if (bizimkiler.Count > 0)
        {
            bizimkiler[0].askerSayisi += birlikBuyuklugu;
            mesaj = bizimkiler[0].orduAdi + " +" + birlikBuyuklugu + " asker aldı.";
        }
        else
        {
            Ordu yeni = Ordu.Olustur(seciliSancak.sancakAdi + " Kuva-yi Milliyesi", Taraf.Turk, birlikBuyuklugu, seciliSancak, 80);
            mesaj = yeni.orduAdi + " kuruldu.";
        }
        tur.EkraniGuncelle();
        BilgiGoster("<color=#9f9>" + mesaj + "</color>\n" + seciliSancak.BilgiMetni());
    }

    // ---------- Ordu birleştirme ----------

    // Aynı sancaktaki bütün ordularımızı seçili orduda toplar
    // ---------- Komutan atama ----------

    void KomutanPaneliAc()
    {
        KomutanPaneliKapat();
        if (seciliOrdu == null) return;
        Canvas canvas = FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        Ordu ordu = seciliOrdu;
        List<Komutan> bostakiler = Komutanlar.Bostakiler();

        komutanPaneli = new GameObject("KomutanPaneli", typeof(RectTransform), typeof(UnityEngine.UI.Image));
        komutanPaneli.transform.SetParent(canvas.transform, false);
        RectTransform pr = komutanPaneli.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);       // ekranın ortasında, bilgi panelini örtmesin
        pr.pivot = new Vector2(0.5f, 0.5f);
        float yukseklik = 100f + Mathf.Max(1, bostakiler.Count) * 82f + 70f;   // başlık + komutanlar + Kapat
        pr.sizeDelta = new Vector2(680f, yukseklik);
        pr.anchoredPosition = new Vector2(0f, 60f);
        komutanPaneli.GetComponent<UnityEngine.UI.Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.95f);

        GameObject bg = new GameObject("Baslik", typeof(RectTransform));
        bg.transform.SetParent(komutanPaneli.transform, false);
        TextMeshProUGUI baslik = bg.AddComponent<TextMeshProUGUI>();
        RectTransform br = baslik.rectTransform;
        br.anchorMin = br.anchorMax = new Vector2(0.5f, 1f); br.pivot = new Vector2(0.5f, 1f);
        br.anchoredPosition = new Vector2(0f, -12f); br.sizeDelta = new Vector2(600f, 80f);
        baslik.fontSize = 24; baslik.alignment = TextAlignmentOptions.Top;
        baslik.color = ArayuzYardimci.DugmeYazisi; baslik.raycastTarget = false;
        baslik.text = "<b>" + ordu.orduAdi + " için komutan</b>\n<size=80%>Şu an: "
                    + (ordu.komutan != null ? ordu.komutan.ad : "<color=#f99>komutan yok</color>") + "</size>";

        float y = -100f;
        if (bostakiler.Count == 0)
        {
            baslik.text += "\n\n<size=80%><color=#ccc>Boşta komutan yok. Yeni komutanlar tarihleri gelince Ankara'ya katılır.</color></size>";
        }
        foreach (Komutan k in bostakiler)
        {
            Komutan secilen = k;
            GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
            d.transform.SetParent(komutanPaneli.transform, false);
            RectTransform r = d.GetComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f); r.pivot = new Vector2(0.5f, 1f);
            r.sizeDelta = new Vector2(640f, 76f); r.anchoredPosition = new Vector2(0f, y);
            y -= 82f;
            TMP_Text t = d.GetComponentInChildren<TMP_Text>();
            t.fontSize = 20;
            t.text = "<b>" + k.ad + "</b>   <size=85%>" + k.Degerler + "</size>\n<size=75%>" + k.OzellikMetni + "</size>";
            Button b = d.GetComponent<Button>();
            b.onClick.AddListener(() =>
            {
                Komutanlar.Ata(secilen, ordu);
                KomutanPaneliKapat();
                BilgiGoster("<color=#9f9>" + secilen.ad + ", " + ordu.orduAdi + " komutanlığına atandı.</color>\n" + ordu.BilgiMetni());
            });
            ArayuzYardimci.DugmeStili(b);
        }

        GameObject kd = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        kd.transform.SetParent(komutanPaneli.transform, false);
        RectTransform kr = kd.GetComponent<RectTransform>();
        kr.anchorMin = kr.anchorMax = new Vector2(0.5f, 0f); kr.pivot = new Vector2(0.5f, 0f);
        kr.sizeDelta = new Vector2(200f, 50f); kr.anchoredPosition = new Vector2(0f, 12f);
        kd.GetComponentInChildren<TMP_Text>().text = "Kapat";
        kd.GetComponentInChildren<TMP_Text>().fontSize = 22;
        Button kb = kd.GetComponent<Button>();
        kb.onClick.AddListener(KomutanPaneliKapat);
        ArayuzYardimci.DugmeStili(kb);
    }

    void KomutanPaneliKapat()
    {
        if (komutanPaneli != null) Destroy(komutanPaneli);
        komutanPaneli = null;
    }

    void OrdulariBirlestir()
    {
        if (seciliOrdu == null) return;
        Ordu ana = seciliOrdu;
        int toplamAsker = ana.askerSayisi;
        float moralToplami = ana.moral * (float)ana.askerSayisi;
        bool hareketEdebilir = ana.HareketHakkiVar;
        int katilan = 0;

        foreach (Ordu o in BizimOrdular(ana.bulunduguSancak))
        {
            if (o == ana || o.HareketHalinde) continue;   // yürüyüştekiler katılmaz
            toplamAsker += o.askerSayisi;
            moralToplami += o.moral * (float)o.askerSayisi;
            if (!o.HareketHakkiVar) hareketEdebilir = false;   // biri yorgunsa birleşik ordu da bu tur yürüyemez
            if (ana.komutan == null && o.komutan != null) Komutanlar.Ata(o.komutan, ana);   // komutan birleşik orduya geçer
            o.YokOl();
            katilan++;
        }

        ana.askerSayisi = toplamAsker;
        ana.moral = Mathf.RoundToInt(moralToplami / toplamAsker);
        if (!hareketEdebilir) ana.HareketHakkiniKullan();
        ana.bulunduguSancak.OrdulariDiz();

        OrduSec(ana);
        BilgiGoster("<color=#9f9>" + katilan + " birlik " + ana.orduAdi + " içinde birleşti.</color>\n" + ana.BilgiMetni());
    }

    // Tur sonunda düşman hamlelerini sol altta listeler
    public void TurRaporuGoster(string tarih, List<string> rapor)
    {
        string metin = "<b>Tur raporu — " + tarih + "</b>";
        if (rapor.Count == 0) metin += "\nCephelerde sessizlik.";
        foreach (string satir in rapor) metin += "\n• " + satir;
        BilgiGoster(metin);
    }

    void BilgiGoster(string metin)
    {
        if (bilgiYazisi == null) { Debug.Log(metin); return; }
        bilgiYazisi.text = metin;

        // Arka paneli yazının gerçek boyutuna göre ayarla
        if (bilgiPaneli != null)
        {
            RectTransform yr = bilgiYazisi.rectTransform;
            Vector2 boyut = bilgiYazisi.GetPreferredValues(metin, yr.sizeDelta.x, 0f);
            float pay = 15f;
            bilgiPaneli.anchoredPosition = yr.anchoredPosition - new Vector2(pay, pay);
            bilgiPaneli.sizeDelta = new Vector2(Mathf.Min(boyut.x, yr.sizeDelta.x) + 2 * pay, boyut.y + 2 * pay);
        }
    }
}
