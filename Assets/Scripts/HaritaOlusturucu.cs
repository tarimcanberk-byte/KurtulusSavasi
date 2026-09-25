using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Oyun başlarken HaritaVerisi'ndeki listelerden bütün sancakları, aralarındaki
// yolları, isim etiketlerini ve orduları otomatik olarak kurar.
public class HaritaOlusturucu : MonoBehaviour
{
    public float olcek = 4f;                 // 1 derece = kaç Unity birimi
    public float merkezEnlem = 39f;
    public float merkezBoylam = 35f;
    public float kareBoyutu = 0.9f;

    private readonly Dictionary<string, Sancak> sancaklar = new Dictionary<string, Sancak>();

    void Awake()
    {
        SancaklariKur();
        KomsulariKur();
        YollariCiz();
        OrdulariKur();
    }

    void Start()
    {
        KameraKontrol kamera = FindAnyObjectByType<KameraKontrol>();
        if (kamera != null && sancaklar.ContainsKey("Ankara"))
            kamera.Odakla(sancaklar["Ankara"].transform.position, 22f);
    }

    // Enlem/boylamı haritadaki bir noktaya çevirir
    Vector3 Konum(float enlem, float boylam)
    {
        float x = (boylam - merkezBoylam) * Mathf.Cos(merkezEnlem * Mathf.Deg2Rad) * olcek;
        float z = (enlem - merkezEnlem) * olcek;
        return new Vector3(x, 0f, z);
    }

    void SancaklariKur()
    {
        GameObject klasor = new GameObject("Sancaklar");
        foreach (var t in HaritaVerisi.Sancaklar)
        {
            GameObject kare = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kare.name = t.ad;
            kare.transform.SetParent(klasor.transform);
            kare.transform.position = Konum(t.enlem, t.boylam);
            kare.transform.localScale = new Vector3(kareBoyutu, 0.15f, kareBoyutu);

            Sancak s = kare.AddComponent<Sancak>();
            s.sancakAdi = t.ad;
            s.sahip = t.sahip;
            s.paraUretimi = t.para;
            s.erzakUretimi = t.erzak;
            s.RengiGuncelle();
            sancaklar[t.ad] = s;

            EtiketEkle(kare.transform, t.ad);
        }
    }

    void EtiketEkle(Transform kare, string yazi)
    {
        GameObject etiket = new GameObject("Etiket");
        TextMeshPro tmp = etiket.AddComponent<TextMeshPro>();
        etiket.transform.position = kare.position + new Vector3(0f, 0.1f, -0.65f);
        etiket.transform.rotation = Quaternion.Euler(90f, 0f, 0f);   // yere yatık, yukarı bakan
        etiket.transform.SetParent(kare, true);

        tmp.text = yazi;
        tmp.fontSize = 3f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.rectTransform.sizeDelta = new Vector2(4f, 0.6f);
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color32(0, 0, 0, 255);
    }

    static float MesafeKm(HaritaVerisi.SancakTanimi a, HaritaVerisi.SancakTanimi b)
    {
        float dy = (a.enlem - b.enlem) * 111f;
        float dx = (a.boylam - b.boylam) * 111f * Mathf.Cos((a.enlem + b.enlem) * 0.5f * Mathf.Deg2Rad);
        return Mathf.Sqrt(dx * dx + dy * dy);
    }

    static bool Yasakli(string a, string b)
    {
        var liste = HaritaVerisi.KomsuDegil;
        for (int i = 0; i < liste.GetLength(0); i++)
            if ((liste[i, 0] == a && liste[i, 1] == b) || (liste[i, 0] == b && liste[i, 1] == a)) return true;
        return false;
    }

    void Bagla(string a, string b)
    {
        if (!sancaklar.ContainsKey(a) || !sancaklar.ContainsKey(b)) { Debug.LogWarning("Bilinmeyen sancak: " + a + " / " + b); return; }
        Sancak sa = sancaklar[a], sb = sancaklar[b];
        if (!sa.komsular.Contains(sb)) sa.komsular.Add(sb);
        if (!sb.komsular.Contains(sa)) sb.komsular.Add(sa);
    }

    void KomsulariKur()
    {
        var liste = HaritaVerisi.Sancaklar;
        for (int i = 0; i < liste.Length; i++)
            for (int j = i + 1; j < liste.Length; j++)
                if (MesafeKm(liste[i], liste[j]) <= HaritaVerisi.KomsulukMesafesiKm && !Yasakli(liste[i].ad, liste[j].ad))
                    Bagla(liste[i].ad, liste[j].ad);

        var ek = HaritaVerisi.EkKomsuluklar;
        for (int i = 0; i < ek.GetLength(0); i++) Bagla(ek[i, 0], ek[i, 1]);
    }

    // Komşu sancaklar arasına ince yol çizgileri çizer
    void YollariCiz()
    {
        GameObject klasor = new GameObject("Yollar");
        Material malzeme = new Material(Shader.Find("Sprites/Default"));
        HashSet<string> cizilen = new HashSet<string>();

        foreach (Sancak s in sancaklar.Values)
        {
            foreach (Sancak k in s.komsular)
            {
                string anahtar = string.CompareOrdinal(s.sancakAdi, k.sancakAdi) < 0 ? s.sancakAdi + "|" + k.sancakAdi : k.sancakAdi + "|" + s.sancakAdi;
                if (!cizilen.Add(anahtar)) continue;

                GameObject yol = new GameObject("Yol " + anahtar);
                yol.transform.SetParent(klasor.transform);
                LineRenderer cizgi = yol.AddComponent<LineRenderer>();
                cizgi.material = malzeme;
                cizgi.startColor = cizgi.endColor = new Color(0.85f, 0.8f, 0.65f, 0.6f);
                cizgi.startWidth = cizgi.endWidth = 0.06f;
                cizgi.positionCount = 2;
                cizgi.SetPosition(0, s.transform.position + Vector3.down * 0.05f);
                cizgi.SetPosition(1, k.transform.position + Vector3.down * 0.05f);
            }
        }
    }

    void OrdulariKur()
    {
        GameObject klasor = new GameObject("Ordular");
        foreach (var t in HaritaVerisi.Ordular)
        {
            if (!sancaklar.ContainsKey(t.sancak)) { Debug.LogWarning("Ordu için sancak bulunamadı: " + t.sancak); continue; }

            GameObject kup = GameObject.CreatePrimitive(PrimitiveType.Cube);
            kup.name = t.ad;
            kup.transform.SetParent(klasor.transform);
            kup.transform.localScale = Vector3.one * 0.35f;

            Ordu o = kup.AddComponent<Ordu>();
            o.orduAdi = t.ad;
            o.taraf = t.taraf;
            o.askerSayisi = t.asker;
            o.bulunduguSancak = sancaklar[t.sancak];
        }
    }
}
