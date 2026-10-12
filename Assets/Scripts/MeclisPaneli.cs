using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Meclis ekranı: yetki, gruplar, kanunlar; bir kanun seçilince söz verme, baskı ve oylama.
public static class MeclisPaneli
{
    static GameObject panel;
    static Meclis.Kanun secili;
    static TurYoneticisi tur;
    static string mesaj;

    public static bool Acik { get { return panel != null; } }

    // Sağ üstte "Meclis" düğmesi (Tekalif düğmesinin altında)
    public static void DugmeKur(Canvas canvas, TurYoneticisi t)
    {
        tur = t;
        Button b = Dugme(canvas.transform, "Meclis\n<size=60%>yetki ve kanunlar</size>", new Vector2(1f, 1f), new Vector2(1f, 1f),
                         new Vector2(-30f, -200f), new Vector2(340f, 80f), () => Ac(t), 26f);
        b.name = "MeclisDugmesi";
    }

    public static void Ac(TurYoneticisi t)
    {
        tur = t;
        if (tur != null) tur.Duraklat();
        Yenile();
    }

    public static void Kapat()
    {
        if (panel != null) Object.Destroy(panel);
        panel = null; secili = null; mesaj = null;
        Meclis.TeklifiTemizle();
    }

    static void Yenile()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) return;
        if (panel != null) Object.Destroy(panel);

        panel = new GameObject("MeclisPaneli", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);
        pr.sizeDelta = new Vector2(1500f, 940f);
        pr.anchoredPosition = new Vector2(0f, -45f);
        panel.GetComponent<Image>().color = new Color(0.09f, 0.07f, 0.05f, 0.97f);

        // Başlık ve yetki
        string baslik = Meclis.Acik ? "Türkiye Büyük Millet Meclisi" : "Heyet-i Temsiliye ve Millî Mücadele";
        Yazi(panel.transform, new Vector2(0f, 1f), new Vector2(30f, -20f), new Vector2(1100f, 50f), 40f, TextAlignmentOptions.TopLeft).text = "<b>" + baslik + "</b>";
        Yazi(panel.transform, new Vector2(0f, 1f), new Vector2(30f, -70f), new Vector2(1250f, 40f), 26f, TextAlignmentOptions.TopLeft).text =
            "Yetki: <b>" + Mathf.RoundToInt(Meclis.Yetki) + "</b>/100   —   Mustafa Kemal: <b>" + Meclis.Asama + "</b>"
            + (Meclis.Kriz ? "   <color=#f88>KRİZ!</color> <size=80%>(Başkomutanlık kolay geçer)</size>" : "");
        Cubuk(panel.transform, new Vector2(30f, -112f), 700f, Meclis.Yetki / 100f, new Color(0.85f, 0.65f, 0.25f));
        Dugme(panel.transform, "Kapat", new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-20f, -20f), new Vector2(160f, 56f), Kapat, 26f);

        // Gruplar
        Yazi(panel.transform, new Vector2(0f, 1f), new Vector2(30f, -140f), new Vector2(650f, 40f), 28f, TextAlignmentOptions.TopLeft).text = "<b>Meclis grupları</b>";
        float y = -185f;
        foreach (Meclis.Grup g in Meclis.Gruplar)
        {
            Yazi(panel.transform, new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(650f, 70f), 22f, TextAlignmentOptions.TopLeft).text =
                "<b>" + g.ad + "</b>   koltuk %" + Mathf.RoundToInt(g.koltuk) + "   güven " + Mathf.RoundToInt(g.guven)
                + "\n<size=80%><color=#ccc>" + g.onemsedigi + "</color></size>";
            Cubuk(panel.transform, new Vector2(30f, y - 62f), 600f, g.guven / 100f, GuvenRengi(g.guven));
            y -= 92f;
        }
        if (!Meclis.Acik)
            Yazi(panel.transform, new Vector2(0f, 1f), new Vector2(770f, -22f), new Vector2(520f, 80f), 20f, TextAlignmentOptions.TopLeft).text =
                "<color=#fc8>Meclis 23 Nisan 1920'de açılacak.</color>\n<size=85%>O güne kadar yetkin kongrelerle artar; yetki düşükken "
                + "komutanlar her atamayı kabul etmeyebilir.</size>";

        // Kanunlar
        Yazi(panel.transform, new Vector2(0f, 1f), new Vector2(720f, -140f), new Vector2(740f, 40f), 28f, TextAlignmentOptions.TopLeft).text = "<b>Kanunlar</b>";
        y = -185f;
        foreach (Meclis.Kanun k in Meclis.Kanunlar)
        {
            Meclis.Kanun kk = k;
            string engel = Meclis.TeklifEngeli(k);
            string durum = k.Aktif ? "<color=#9f9>" + engel + "</color>" : engel != null ? "<color=#bbb>" + engel + "</color>" : "<color=#fc8>Teklif edilebilir</color>";
            Button b = Dugme(panel.transform, "<b>" + k.ad + "</b>\n<size=70%>" + durum + "</size>", new Vector2(0f, 1f), new Vector2(0f, 1f),
                             new Vector2(720f, y), new Vector2(740f, 68f), () => { secili = kk; mesaj = null; Meclis.TeklifiTemizle(); Yenile(); }, 22f);
            if (secili == k) b.GetComponent<Image>().color = new Color(0.5f, 0.32f, 0.16f, 0.98f);
            y -= 74f;
        }

        if (secili != null) KanunAyrinti(secili);
        if (mesaj != null)
            Yazi(panel.transform, new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(1440f, 40f), 22f, TextAlignmentOptions.Bottom).text = mesaj;
    }

    // Seçili kanun: grupların tutumu, söz, baskı, oylama
    static void KanunAyrinti(Meclis.Kanun k)
    {
        RectTransform a = Panel(panel.transform, new Vector2(0f, 0f), new Vector2(20f, 60f), new Vector2(1460f, 300f), new Color(0.16f, 0.12f, 0.08f, 0.95f));
        Yazi(a, new Vector2(0f, 1f), new Vector2(16f, -10f), new Vector2(1420f, 70f), 22f, TextAlignmentOptions.TopLeft).text =
            "<b>" + k.ad + "</b>  —  " + k.aciklama + "\n<color=#9df>Etkisi: " + k.etki + "</color>";

        string engel = Meclis.TeklifEngeli(k);
        float x = 16f;
        for (int i = 0; i < Meclis.Gruplar.Count; i++)
        {
            int gi = i;
            Meclis.Grup g = Meclis.Gruplar[i];
            int oran = Mathf.RoundToInt(Meclis.GrupOrani(k, i) * 100f);
            string tutum = k.tutum[i] >= 2 ? "<color=#9f9>güçlü destek</color>" : k.tutum[i] == 1 ? "<color=#bf9>destekler</color>"
                         : k.tutum[i] == 0 ? "<color=#ddd>kararsız</color>" : k.tutum[i] == -1 ? "<color=#fb8>karşı</color>" : "<color=#f88>sert karşı</color>";
            Yazi(a, new Vector2(0f, 1f), new Vector2(x, -88f), new Vector2(350f, 60f), 20f, TextAlignmentOptions.TopLeft).text =
                "<b>" + g.ad + "</b>\n" + tutum + "  ·  evet %" + oran;
            if (engel == null)
            {
                bool verildi = Meclis.VerilenSozler.Contains(g);
                string bedel = g.sozPara > 0 ? g.sozPara + " para" : g.sozErzak > 0 ? g.sozErzak + " erzak" : g.sozAciklama;
                Button sb = Dugme(a, verildi ? "<color=#9f9>Söz verildi</color>" : "Söz ver: " + g.sozAdi + "\n<size=70%>" + bedel + "</size>",
                                  new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x, -152f), new Vector2(345f, 64f),
                                  () => { if (!verildi) { mesaj = Meclis.SozVer(gi, tur); Yenile(); } }, 18f);
                sb.interactable = !verildi;
            }
            x += 360f;
        }

        if (engel != null)
        {
            Yazi(a, new Vector2(0f, 0f), new Vector2(16f, 16f), new Vector2(1420f, 50f), 22f, TextAlignmentOptions.BottomLeft).text = "<color=#fc8>" + engel + "</color>";
            return;
        }
        int evet = Mathf.RoundToInt(Meclis.EvetOrani(k) * 100f);
        int gun = Meclis.TartismaSuresi();
        Yazi(a, new Vector2(0f, 0f), new Vector2(16f, 18f), new Vector2(700f, 56f), 24f, TextAlignmentOptions.BottomLeft).text =
            "Tahmini oy: <b>" + (evet > 50 ? "<color=#9f9>" : "<color=#f99>") + "%" + evet + " evet</color></b>"
            + "\n<size=80%>" + (gun > 0 ? "Güven düşük: oylama " + gun + " gün tartışıldıktan sonra yapılır." : "Güven yüksek: hemen oylanır.") + "</size>";
        Button bk = Dugme(a, Meclis.BaskiYapildi ? "<color=#fc8>Baskı yapıldı</color>" : "Baskı uygula\n<size=65%>oy +%20, bütün gruplarda güven −8 (yetki ≥ 50)</size>",
                          new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-430f, 16f), new Vector2(380f, 64f),
                          () => { mesaj = Meclis.Baski(); Yenile(); }, 20f);
        bk.interactable = !Meclis.BaskiYapildi;
        Button oy = Dugme(a, "<b>Oylamaya Sun</b>", new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-16f, 16f), new Vector2(380f, 64f), () =>
        {
            string sonuc = Meclis.OylamayaSun(k);
            Gunluk.Ekle(sonuc.Replace("\n", " "));
            mesaj = sonuc;
            if (tur != null) tur.EkraniGuncelle();
            Yenile();
        }, 26f);
        oy.GetComponent<Image>().color = new Color(0.45f, 0.18f, 0.12f, 0.97f);
    }

    static Color GuvenRengi(float g) { return g >= 60f ? new Color(0.4f, 0.75f, 0.35f) : g >= 40f ? new Color(0.85f, 0.7f, 0.3f) : new Color(0.8f, 0.3f, 0.25f); }

    // ---------- Arayüz parçaları ----------

    static void Cubuk(Transform ebeveyn, Vector2 yer, float genislik, float oran, Color renk)
    {
        RectTransform arka = Panel(ebeveyn, new Vector2(0f, 1f), yer, new Vector2(genislik, 12f), new Color(0f, 0f, 0f, 0.5f));
        arka.pivot = new Vector2(0f, 1f); arka.anchoredPosition = yer;
        RectTransform on = Panel(arka, new Vector2(0f, 0.5f), Vector2.zero, new Vector2(genislik * Mathf.Clamp01(oran), 12f), renk);
        on.pivot = new Vector2(0f, 0.5f); on.anchoredPosition = Vector2.zero;
    }

    static RectTransform Panel(Transform ebeveyn, Vector2 capa, Vector2 yer, Vector2 boyut, Color renk)
    {
        GameObject g = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ebeveyn, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = capa; r.pivot = capa;
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        Image im = g.GetComponent<Image>(); im.color = renk; im.raycastTarget = false;
        return r;
    }

    static TMP_Text Yazi(Transform ebeveyn, Vector2 capa, Vector2 yer, Vector2 boyut, float font, TextAlignmentOptions hiza)
    {
        GameObject g = new GameObject("Yazi", typeof(RectTransform));
        g.transform.SetParent(ebeveyn, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = capa; r.pivot = capa;
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        t.fontSize = font; t.alignment = hiza;
        t.color = ArayuzYardimci.DugmeYazisi;
        t.raycastTarget = false;
        return t;
    }

    static Button Dugme(Transform ebeveyn, string yazi, Vector2 capa, Vector2 pivot, Vector2 yer, Vector2 boyut, UnityAction islem, float font)
    {
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.transform.SetParent(ebeveyn, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = capa; r.pivot = pivot;
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi; t.fontSize = font;
        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(islem);
        ArayuzYardimci.DugmeStili(b);
        return b;
    }
}
