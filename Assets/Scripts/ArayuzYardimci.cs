using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Arayüzün ortak görünümü: koyu, yarı saydam paneller ve eski kâğıt tonlarında düğmeler.
public static class ArayuzYardimci
{
    public static readonly Color PanelRengi   = new Color(0.08f, 0.07f, 0.06f, 0.80f);
    public static readonly Color DugmeRengi   = new Color(0.30f, 0.22f, 0.14f, 0.95f);
    public static readonly Color DugmeYazisi  = new Color(0.97f, 0.92f, 0.80f);

    public static void DugmeStili(Button b)
    {
        if (b == null) return;
        Image img = b.GetComponent<Image>();
        if (img != null) { img.sprite = null; img.color = DugmeRengi; }

        ColorBlock renkler = b.colors;
        renkler.normalColor      = Color.white;
        renkler.highlightedColor = new Color(1f, 0.85f, 0.6f);
        renkler.pressedColor     = new Color(0.7f, 0.6f, 0.45f);
        renkler.selectedColor    = Color.white;
        b.colors = renkler;

        TMP_Text t = b.GetComponentInChildren<TMP_Text>();
        if (t != null) t.color = DugmeYazisi;
    }

    // Ekranın üstüne, tarih ve kaynak yazısının arkasına koyu bir şerit
    public static void UstSeritOlustur(Canvas canvas, float yukseklik)
    {
        GameObject s = new GameObject("UstSerit", typeof(RectTransform), typeof(Image));
        s.transform.SetParent(canvas.transform, false);
        s.transform.SetAsFirstSibling();   // her şeyin arkasında kalsın
        RectTransform r = s.GetComponent<RectTransform>();
        r.anchorMin = new Vector2(0f, 1f); r.anchorMax = new Vector2(1f, 1f);
        r.pivot = new Vector2(0.5f, 1f);
        r.anchoredPosition = Vector2.zero;
        r.sizeDelta = new Vector2(0f, yukseklik);
        Image img = s.GetComponent<Image>();
        img.color = PanelRengi;
        img.raycastTarget = false;
    }

    // Bir yazının hemen arkasına, boyutu yazıya göre ayarlanabilen bir panel
    public static RectTransform ArkaPanelOlustur(RectTransform yazi)
    {
        GameObject p = new GameObject("ArkaPanel", typeof(RectTransform), typeof(Image));
        p.transform.SetParent(yazi.parent, false);
        p.transform.SetSiblingIndex(yazi.GetSiblingIndex());   // yazının arkasında
        RectTransform r = p.GetComponent<RectTransform>();
        r.anchorMin = yazi.anchorMin; r.anchorMax = yazi.anchorMax;
        r.pivot = yazi.pivot;
        Image img = p.GetComponent<Image>();
        img.color = PanelRengi;
        img.raycastTarget = false;
        return r;
    }
}
