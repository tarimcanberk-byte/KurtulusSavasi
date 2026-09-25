using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Zafer ya da yenilgi olduğunda ekranın ortasında açılan panel
public static class OyunSonuPaneli
{
    public static void Goster(string baslik, string metin, bool devamEdilebilir)
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null) { Debug.Log(baslik + "\n" + metin); return; }

        // Karartılmış arka plan
        GameObject panel = new GameObject("OyunSonuPaneli", typeof(RectTransform), typeof(Image));
        panel.transform.SetParent(canvas.transform, false);
        RectTransform pr = panel.GetComponent<RectTransform>();
        pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one;
        pr.offsetMin = Vector2.zero; pr.offsetMax = Vector2.zero;
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.85f);

        // Yazı
        GameObject yaziGO = new GameObject("Metin", typeof(RectTransform));
        yaziGO.transform.SetParent(panel.transform, false);
        TextMeshProUGUI yazi = yaziGO.AddComponent<TextMeshProUGUI>();
        yazi.text = "<size=180%><b>" + baslik + "</b></size>\n\n" + metin;
        yazi.fontSize = 32;
        yazi.alignment = TextAlignmentOptions.Center;
        RectTransform yr = yaziGO.GetComponent<RectTransform>();
        yr.anchorMin = new Vector2(0.15f, 0.25f); yr.anchorMax = new Vector2(0.85f, 0.9f);
        yr.offsetMin = Vector2.zero; yr.offsetMax = Vector2.zero;

        if (devamEdilebilir)
            Dugme(panel.transform, "Oynamaya devam et", new Vector2(-180f, 120f), () => Object.Destroy(panel));
        Dugme(panel.transform, "Yeniden başla", new Vector2(devamEdilebilir ? 180f : 0f, 120f),
              () => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex));
    }

    static void Dugme(Transform ebeveyn, string yazi, Vector2 konum, UnityEngine.Events.UnityAction islem)
    {
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.transform.SetParent(ebeveyn, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0.5f, 0f);
        r.sizeDelta = new Vector2(320f, 80f);
        r.anchoredPosition = konum;
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi;
        t.fontSize = 28;
        d.GetComponent<Button>().onClick.AddListener(islem);
    }
}
