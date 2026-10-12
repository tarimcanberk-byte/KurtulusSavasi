using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Gazete sayfası: önemli haberler dönemin gazetesi ya da telgraf olarak ekrana gelir, oyun duraklar.
//   Eylül 1919'a kadar: Telgraf · Sivas'ta İrade-i Milliye (14 Eylül 1919) · Ankara'da Hâkimiyet-i Milliye (10 Ocak 1920)
//   6 Nisan 1920'den sonra haberler "Anadolu Ajansı bildiriyor" diye başlar.
public static class Gazete
{
    struct Sayfa { public string baslik, metin, etki; public DateTime tarih; }

    static readonly Queue<Sayfa> kuyruk = new Queue<Sayfa>();
    static GameObject panel;

    public static bool Acik { get { return panel != null; } }

    static readonly string[] aylar = { "Kânunusâni", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran", "Temmuz", "Ağustos", "Eylül", "Teşrinievvel", "Teşrinisâni", "Kânunuevvel" };

    public static void Sifirla() { kuyruk.Clear(); if (panel != null) UnityEngine.Object.Destroy(panel); panel = null; }

    public static void Ekle(string baslik, string metin, string etki, DateTime tarih)
    {
        kuyruk.Enqueue(new Sayfa { baslik = baslik, metin = metin, etki = etki, tarih = tarih });
        TurYoneticisi tur = UnityEngine.Object.FindAnyObjectByType<TurYoneticisi>();
        if (tur != null) tur.Duraklat();
        if (panel == null) Siradaki();
    }

    static void Siradaki()
    {
        if (panel != null) UnityEngine.Object.Destroy(panel);
        panel = null;
        if (kuyruk.Count == 0) return;
        if (MuharebeYoneticisi.Mesgul) return;   // savaş ekranındayken bekler; sonra gösterilir
        Canvas canvas = UnityEngine.Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        Sayfa s = kuyruk.Dequeue();

        // Arkadaki karartma
        panel = new GameObject("Gazete", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = pr.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);

        // Kâğıt
        Color murekkep = new Color(0.13f, 0.10f, 0.07f);
        RectTransform kagit = Kutu(panel.transform, Vector2.zero, new Vector2(980f, 720f), new Color(0.90f, 0.85f, 0.72f));
        kagit.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-1.2f, 1.2f));   // masaya atılmış gazete
        RectTransform cerceve = Kutu(kagit, Vector2.zero, new Vector2(940f, 680f), new Color(0f, 0f, 0f, 0f));
        Outline o = cerceve.gameObject.AddComponent<Outline>();
        o.effectColor = new Color(0.25f, 0.2f, 0.15f, 0.6f); o.effectDistance = new Vector2(2f, 2f);

        string ad, alt;
        if (s.tarih >= new DateTime(1920, 1, 10)) { ad = "HÂKİMİYET-İ MİLLİYE"; alt = "Ankara · Müdafaa-i Hukuk Cemiyeti'nin vasıta-i neşriyatıdır"; }
        else if (s.tarih >= new DateTime(1919, 9, 14)) { ad = "İRADE-İ MİLLİYE"; alt = "Sivas · Heyet-i Temsiliye'nin sesi"; }
        else { ad = "TELGRAF"; alt = "Telgrafhane · Acele ve mahremdir"; }

        TMP_Text bas = Yazi(kagit, new Vector2(0f, 300f), new Vector2(900f, 80f), 62f, murekkep, TextAlignmentOptions.Center);
        bas.text = "<b>" + ad + "</b>";
        bas.characterSpacing = 6f;
        Yazi(kagit, new Vector2(0f, 248f), new Vector2(900f, 30f), 20f, murekkep, TextAlignmentOptions.Center).text =
            "<i>" + alt + "</i>   —   " + s.tarih.Day + " " + aylar[s.tarih.Month - 1] + " " + s.tarih.Year;
        Kutu(kagit, new Vector2(0f, 228f), new Vector2(900f, 3f), murekkep);
        Kutu(kagit, new Vector2(0f, 222f), new Vector2(900f, 1.5f), murekkep);

        string onEk = s.tarih >= new DateTime(1920, 4, 6) ? "<size=60%>ANADOLU AJANSI BİLDİRİYOR</size>\n" : "";
        TMP_Text manset = Yazi(kagit, new Vector2(0f, 150f), new Vector2(880f, 120f), 44f, murekkep, TextAlignmentOptions.Center);
        manset.text = onEk + "<b>" + s.baslik.ToUpper(new System.Globalization.CultureInfo("tr-TR")) + "</b>";
        manset.enableAutoSizing = true; manset.fontSizeMin = 26f; manset.fontSizeMax = 44f;

        TMP_Text govde = Yazi(kagit, new Vector2(0f, -40f), new Vector2(860f, 240f), 26f, murekkep, TextAlignmentOptions.TopJustified);
        govde.text = s.metin;
        govde.enableAutoSizing = true; govde.fontSizeMin = 18f; govde.fontSizeMax = 26f;

        if (!string.IsNullOrEmpty(s.etki))
        {
            Kutu(kagit, new Vector2(0f, -182f), new Vector2(860f, 1.5f), murekkep);
            Yazi(kagit, new Vector2(0f, -215f), new Vector2(860f, 50f), 22f, new Color(0.45f, 0.08f, 0.05f), TextAlignmentOptions.Center).text =
                "<i>Etkisi: " + s.etki + "</i>";
        }

        // Düğme
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.transform.SetParent(kagit, false);
        RectTransform dr = d.GetComponent<RectTransform>();
        dr.anchoredPosition = new Vector2(0f, -300f); dr.sizeDelta = new Vector2(320f, 64f);
        TMP_Text dt = d.GetComponentInChildren<TMP_Text>();
        dt.text = kuyruk.Count > 0 ? "Sonraki haber (" + kuyruk.Count + ")" : "Gazeteyi kapat";
        dt.fontSize = 26f;
        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(Siradaki);
        ArayuzYardimci.DugmeStili(b);
    }

    // Savaş ekranından dönünce bekleyen haberleri göster
    public static void Denetle()
    {
        if (panel == null && kuyruk.Count > 0 && !MuharebeYoneticisi.Mesgul) Siradaki();
    }

    static RectTransform Kutu(Transform ebeveyn, Vector2 yer, Vector2 boyut, Color renk)
    {
        GameObject g = new GameObject("Kutu", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ebeveyn, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        Image im = g.GetComponent<Image>(); im.color = renk; im.raycastTarget = renk.a > 0.5f && boyut.y > 100f;
        return r;
    }

    static TMP_Text Yazi(Transform ebeveyn, Vector2 yer, Vector2 boyut, float font, Color renk, TextAlignmentOptions hiza)
    {
        GameObject g = new GameObject("Yazi", typeof(RectTransform));
        g.transform.SetParent(ebeveyn, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        t.rectTransform.anchoredPosition = yer; t.rectTransform.sizeDelta = boyut;
        t.fontSize = font; t.color = renk; t.alignment = hiza; t.raycastTarget = false;
        return t;
    }
}
