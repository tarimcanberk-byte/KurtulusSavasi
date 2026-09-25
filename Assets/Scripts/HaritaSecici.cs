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
    private Button toplaDugmesi;
    private Button birlestirDugmesi;
    private const string Ipucu = "Sol tık: seç   |   Sağ tık: seçili orduyla yürü / saldır   <size=80%>(WASD: kaydır, tekerlek: yakınlaş)</size>";

    void Start()
    {
        BilgiGoster(Ipucu);
        toplaDugmesi = DugmeOlustur("BirlikToplaDugmesi", "Birlik Topla\n<size=70%>" + birlikMaliyeti + " para</size>", 130f, BirlikTopla);
        birlestirDugmesi = DugmeOlustur("BirlestirDugmesi", "Orduları Birleştir", 230f, OrdulariBirlestir);
        DugmeleriGuncelle();
    }

    void Update()
    {
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
            if (ordu.HareketHakkiVar)
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

    // Seçili orduya "şuraya git" emri ver
    void Emret(Sancak hedef)
    {
        if (!seciliOrdu.HareketEdebilirMi(hedef, out string neden))
        {
            BilgiGoster("<color=#ff8080>" + neden + "</color>\n" + seciliOrdu.BilgiMetni());
            return;
        }
        Ordu ordu = seciliOrdu;
        string sonuc = Savas.Ilerle(ordu, hedef);
        Debug.Log(sonuc);
        SecimleriTemizle();
        BilgiGoster(sonuc + (ordu != null && ordu.Yasiyor ? "\n" + ordu.BilgiMetni() : ""));
    }

    public void SecimleriTemizle()
    {
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None)) s.SecimiKaldir();
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None)) o.SecimiKaldir();
        seciliOrdu = null;
        seciliSancak = null;
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
        r.sizeDelta = new Vector2(280f, 80f);
        r.anchoredPosition = new Vector2(-30f, yukseklik);

        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi;
        t.fontSize = 28;

        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(islem);
        return b;
    }

    void DugmeleriGuncelle()
    {
        if (toplaDugmesi != null)
            toplaDugmesi.gameObject.SetActive(seciliSancak != null && seciliSancak.sahip == Taraf.Turk);
        if (birlestirDugmesi != null)
            birlestirDugmesi.gameObject.SetActive(seciliOrdu != null && BizimOrdular(seciliOrdu.bulunduguSancak).Count > 1);
    }

    static List<Ordu> BizimOrdular(Sancak s)
    {
        List<Ordu> liste = new List<Ordu>();
        foreach (Ordu o in s.Ordular()) if (o.OyuncununMu) liste.Add(o);
        return liste;
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
            GameObject kup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kup.transform.localScale = Vector3.one * 0.35f;
            Ordu yeni = kup.AddComponent<Ordu>();
            yeni.orduAdi = seciliSancak.sancakAdi + " Kuva-yi Milliyesi";
            kup.name = yeni.orduAdi;
            yeni.taraf = Taraf.Turk;
            yeni.askerSayisi = birlikBuyuklugu;
            yeni.moral = 80;
            yeni.bulunduguSancak = seciliSancak;
            yeni.HareketHakkiniKullan();   // yeni kurulan birlik bu tur yürüyemez
            mesaj = yeni.orduAdi + " kuruldu.";
        }
        tur.EkraniGuncelle();
        BilgiGoster("<color=#9f9>" + mesaj + "</color>\n" + seciliSancak.BilgiMetni());
    }

    // ---------- Ordu birleştirme ----------

    // Aynı sancaktaki bütün ordularımızı seçili orduda toplar
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
            if (o == ana) continue;
            toplamAsker += o.askerSayisi;
            moralToplami += o.moral * (float)o.askerSayisi;
            if (!o.HareketHakkiVar) hareketEdebilir = false;   // biri yorgunsa birleşik ordu da bu tur yürüyemez
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

    void BilgiGoster(string metin)
    {
        if (bilgiYazisi != null) bilgiYazisi.text = metin;
        else Debug.Log(metin);
    }
}
