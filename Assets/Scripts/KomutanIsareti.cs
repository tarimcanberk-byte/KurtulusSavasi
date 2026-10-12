using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Yeni görevine giden komutanın haritadaki işareti: atlı bir subay, adı ve kalan gün,
// arkasında ordusuna kadar uzanan altın renkli kesik çizgi.
public class KomutanIsareti : MonoBehaviour
{
    Komutan komutan;
    readonly List<Vector3> rota = new List<Vector3>();   // başlangıç + geçilecek sancaklar (son nokta ordunun yeri)
    int toplamGun;
    Vector3 hedefYer;
    TextMeshPro yazi;
    LineRenderer cizgi;
    Transform govde;
    float adim;

    static Material Renk(Color c)
    {
        Shader s = Shader.Find("Universal Render Pipeline/Lit");
        Material m = new Material(s != null ? s : Shader.Find("Standard"));
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.15f);
        return m;
    }

    static void Parca(PrimitiveType t, Transform ebeveyn, Vector3 yer, Vector3 olcek, Material m, Vector3 aci = default)
    {
        GameObject g = GameObject.CreatePrimitive(t);
        Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(ebeveyn, false);
        g.transform.localPosition = yer;
        g.transform.localScale = olcek;
        g.transform.localRotation = Quaternion.Euler(aci);
        g.GetComponent<Renderer>().sharedMaterial = m;
    }

    public static KomutanIsareti Olustur(Komutan k, Sancak bas, List<Sancak> yol, Ordu hedef, int gun)
    {
        GameObject kok = new GameObject("KomutanYolda " + k.kisaAd);
        KomutanIsareti i = kok.AddComponent<KomutanIsareti>();
        i.komutan = k;
        i.toplamGun = Mathf.Max(1, gun);
        i.rota.Add(bas.transform.position);
        if (yol != null) foreach (Sancak s in yol) i.rota.Add(s.transform.position);
        if (i.rota.Count < 2 || i.rota[i.rota.Count - 1] != hedef.transform.position) i.rota.Add(hedef.transform.position);
        i.Kur();
        i.Guncelle();
        kok.transform.position = i.hedefYer;
        return i;
    }

    void Kur()
    {
        // Atlı subay
        GameObject g = new GameObject("Gorsel");
        g.transform.SetParent(transform, false);
        govde = g.transform;
        govde.localScale = Vector3.one * 0.5f;
        Material at = Renk(new Color(0.18f, 0.12f, 0.08f));
        Material uniforma = Renk(TarafBilgi.UniformaRengi(Taraf.Turk));
        Material altin = Renk(new Color(0.85f, 0.66f, 0.22f));
        Parca(PrimitiveType.Cube, govde, new Vector3(0f, 0.78f, 0f), new Vector3(0.32f, 0.36f, 0.95f), at);
        Parca(PrimitiveType.Cube, govde, new Vector3(0f, 1.05f, 0.52f), new Vector3(0.16f, 0.42f, 0.22f), at, new Vector3(30f, 0f, 0f));
        Parca(PrimitiveType.Cube, govde, new Vector3(0f, 1.22f, 0.70f), new Vector3(0.14f, 0.16f, 0.32f), at);
        for (int n = 0; n < 4; n++)
            Parca(PrimitiveType.Cube, govde, new Vector3(n % 2 == 0 ? -0.11f : 0.11f, 0.32f, n < 2 ? 0.36f : -0.36f), new Vector3(0.07f, 0.62f, 0.07f), at);
        Parca(PrimitiveType.Capsule, govde, new Vector3(0f, 1.32f, -0.05f), new Vector3(0.26f, 0.32f, 0.2f), uniforma);
        Parca(PrimitiveType.Sphere, govde, new Vector3(0f, 1.72f, 0f), new Vector3(0.17f, 0.17f, 0.17f), Renk(new Color(0.78f, 0.6f, 0.46f)));
        Parca(PrimitiveType.Cylinder, govde, new Vector3(0f, 1.84f, 0f), new Vector3(0.19f, 0.07f, 0.19f), Renk(new Color(0.2f, 0.16f, 0.12f)));
        Parca(PrimitiveType.Cylinder, govde, new Vector3(0f, 1.79f, 0f), new Vector3(0.2f, 0.015f, 0.2f), altin);
        // Küçük sancak
        Parca(PrimitiveType.Cylinder, govde, new Vector3(-0.2f, 1.9f, -0.2f), new Vector3(0.03f, 0.8f, 0.03f), Renk(new Color(0.25f, 0.18f, 0.1f)));
        Parca(PrimitiveType.Cube, govde, new Vector3(-0.42f, 2.45f, -0.2f), new Vector3(0.45f, 0.3f, 0.02f), Renk(TarafBilgi.OrduRengi(Taraf.Turk)));
        // Zemin halkası: altın
        Parca(PrimitiveType.Cylinder, govde, new Vector3(0f, 0.02f, 0f), new Vector3(1.4f, 0.02f, 1.4f), altin);

        GameObject y = new GameObject("Yazi");
        y.transform.SetParent(transform, false);
        y.transform.localPosition = new Vector3(0f, 1.9f, 0f);
        yazi = y.AddComponent<TextMeshPro>();
        yazi.fontSize = 3.2f;
        yazi.alignment = TextAlignmentOptions.Center;
        yazi.rectTransform.sizeDelta = new Vector2(8f, 2f);
        yazi.color = new Color(1f, 0.88f, 0.5f);
        yazi.outlineWidth = 0.25f;
        yazi.outlineColor = new Color32(0, 0, 0, 220);

        GameObject c = new GameObject("Rota");
        c.transform.SetParent(transform, false);
        cizgi = c.AddComponent<LineRenderer>();
        cizgi.material = new Material(Shader.Find("Sprites/Default"));
        cizgi.useWorldSpace = true;
        cizgi.widthMultiplier = 0.06f;
        cizgi.startColor = cizgi.endColor = new Color(1f, 0.82f, 0.35f, 0.7f);
        cizgi.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Vector3 Zemin(Vector3 p, float ek = 0.03f) { return Arazi.Yuklu ? Arazi.Uzerinde(p, ek) : p + Vector3.up * ek; }

    // Her gün: ordunun yeni yeri ve kat edilen yol
    public void Guncelle()
    {
        if (komutan == null || komutan.yoldaHedef == null) return;
        rota[rota.Count - 1] = komutan.yoldaHedef.transform.position;   // ordu yürüyorsa hedef de kayar
        float oran = Mathf.Clamp01(1f - komutan.yolGun / (float)toplamGun);

        // Rota üzerinde orana denk gelen nokta
        float toplam = 0f;
        for (int n = 1; n < rota.Count; n++) toplam += Vector3.Distance(rota[n - 1], rota[n]);
        float kalan = toplam * oran;
        Vector3 yer = rota[0];
        int parca = 1;
        for (; parca < rota.Count; parca++)
        {
            float d = Vector3.Distance(rota[parca - 1], rota[parca]);
            if (kalan <= d) { yer = Vector3.Lerp(rota[parca - 1], rota[parca], d < 0.001f ? 1f : kalan / d); break; }
            kalan -= d;
            yer = rota[parca];
        }
        hedefYer = Zemin(yer);

        // Kalan yolu çiz
        List<Vector3> noktalar = new List<Vector3> { hedefYer };
        for (int n = Mathf.Min(parca, rota.Count - 1); n < rota.Count; n++)
        {
            Vector3 a = noktalar[noktalar.Count - 1], b = rota[n];
            int bolum = Mathf.Max(1, Mathf.CeilToInt(Vector3.Distance(a, b) / 0.15f));
            for (int i = 1; i <= bolum; i++) noktalar.Add(Zemin(Vector3.Lerp(a, b, i / (float)bolum), 0.05f));
        }
        cizgi.positionCount = noktalar.Count;
        cizgi.SetPositions(noktalar.ToArray());

        yazi.text = "<b>" + komutan.ad + "</b>\n<size=75%>" + komutan.yoldaHedef.orduAdi + "'na · " + komutan.yolGun + " gün</size>";
    }

    void Update()
    {
        // Günler arasında yumuşak ilerleme ve ileriye bakış
        Vector3 fark = hedefYer - transform.position;
        transform.position = Vector3.MoveTowards(transform.position, hedefYer, Mathf.Max(0.5f, fark.magnitude * 2f) * Time.deltaTime);
        fark.y = 0f;
        if (fark.sqrMagnitude > 0.0001f)
        {
            govde.rotation = Quaternion.Slerp(govde.rotation, Quaternion.LookRotation(fark), Time.deltaTime * 4f);
            adim += Time.deltaTime * 12f;
            govde.localPosition = new Vector3(0f, Mathf.Abs(Mathf.Sin(adim)) * 0.04f, 0f);
        }
        Camera kam = Camera.main;
        if (kam != null) yazi.transform.rotation = kam.transform.rotation;
    }
}
