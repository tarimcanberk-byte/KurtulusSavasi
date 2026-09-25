using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Savaş ekranında bir alay (bölük kümesi): 8 x 4 figürlük bir nizam, bayrak ve bilgi yazısı.
// Her figür yaklaşık (asker / 32) kişiyi temsil eder; kayıp verdikçe figürler yere düşer.
public class Alay : MonoBehaviour
{
    public enum Durum { Hazir, Kacti, Bitti }

    public string ad;
    public Taraf taraf;
    public bool oyuncunun;
    public bool savunan;            // savunan taraf mevzisinde siper avantajı alır
    public int baslangic;
    public float adam;              // kalan asker (kesirli; kayıplar yavaş yavaş düşer)
    public float moral = 80f;
    public float cephane = 140f;    // atış sayısı
    public float verim = 1f;        // haritadaki moral ve ikmal durumundan gelen etkinlik
    public float siper = 1f;        // savunanın mevzi çarpanı (arazi + tahkimat)
    public Durum durum = Durum.Hazir;
    public bool duzenliCekildi;     // emirle çekilen alay kaçan kadar dağılmaz

    // Emirler
    public Vector3 hedefNokta;
    public Alay hedefAlay;
    public bool hucum;
    public bool yuruyor;
    public Vector3 mevzi;           // başladığı yer (savunan için siper)

    public const float YuruyusHizi = 2.2f, KosuHizi = 4.2f, AtisMenzili = 16f, TemasMesafesi = 2.6f;
    const int Sutun = 8, Sira = 4;
    const float AraX = 0.75f, AraZ = 0.85f;

    readonly List<Transform> figurler = new List<Transform>();
    readonly List<Vector3> daginiklik = new List<Vector3>();
    readonly List<bool> dustu = new List<bool>();
    Transform bayrak;
    TextMeshPro yazi;
    LineRenderer cerceve;
    LineRenderer emirCizgisi;      // emir oku: yürürken beyaz, saldırırken kırmızı
    bool secili;
    float adimSaati;

    public bool Savasabilir { get { return durum == Durum.Hazir && adam > 0.5f; } }
    public int GorunenAdam { get { return Mathf.CeilToInt(adam); } }
    public float Genislik { get { return Sutun * AraX; } }

    static readonly Dictionary<string, Material> malzemeler = new Dictionary<string, Material>();

    public static Alay Olustur(Transform ebeveyn, string ad, Taraf taraf, bool oyuncunun, int asker,
                               Vector3 yer, Vector3 bakis, float moral, float verim)
    {
        GameObject g = new GameObject(ad);
        g.transform.SetParent(ebeveyn, true);
        g.transform.position = MuharebeAlani.Uzerinde(yer);
        g.transform.rotation = Quaternion.LookRotation(bakis, Vector3.up);
        Alay a = g.AddComponent<Alay>();
        a.ad = ad; a.taraf = taraf; a.oyuncunun = oyuncunun;
        a.baslangic = asker; a.adam = asker;
        a.moral = Mathf.Clamp(moral, 20f, 100f);
        a.verim = verim;
        a.hedefNokta = g.transform.position;
        a.mevzi = g.transform.position;
        a.Kur();
        return a;
    }

    public static void Sifirla() { malzemeler.Clear(); }

    static Material Malzeme(Color c)
    {
        string anahtar = ColorUtility.ToHtmlStringRGB(c);
        if (malzemeler.TryGetValue(anahtar, out Material m) && m != null) return m;
        m = new Material(MuharebeAlani.LitShader());
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", 0.1f);
        m.enableInstancing = true;
        malzemeler[anahtar] = m;
        return m;
    }

    void Kur()
    {
        Color uniforma = TarafBilgi.UniformaRengi(taraf);
        Material govdeM = Malzeme(uniforma);
        Material tenM = Malzeme(new Color(0.78f, 0.60f, 0.46f));
        Material sapkaM = Malzeme(taraf == Taraf.Turk ? new Color(0.20f, 0.16f, 0.12f) : uniforma * 0.8f);
        Material tufekM = Malzeme(new Color(0.22f, 0.16f, 0.10f));

        for (int s = 0; s < Sira; s++)
            for (int c = 0; c < Sutun; c++)
            {
                GameObject f = new GameObject("Asker");
                f.transform.SetParent(transform.parent, true);   // figürler serbest dolaşır, alaya yavaşça yetişir
                Parca(PrimitiveType.Capsule, f.transform, new Vector3(0f, 0.42f, 0f), new Vector3(0.28f, 0.40f, 0.22f), govdeM);
                Parca(PrimitiveType.Sphere, f.transform, new Vector3(0f, 0.92f, 0f), new Vector3(0.17f, 0.17f, 0.17f), tenM);
                if (taraf == Taraf.Turk)   // kalpak
                    Parca(PrimitiveType.Cylinder, f.transform, new Vector3(0f, 1.04f, 0f), new Vector3(0.19f, 0.07f, 0.19f), sapkaM);
                else                       // kasket
                    Parca(PrimitiveType.Cylinder, f.transform, new Vector3(0f, 1.00f, 0.02f), new Vector3(0.20f, 0.03f, 0.22f), sapkaM);
                Parca(PrimitiveType.Cube, f.transform, new Vector3(0.12f, 0.62f, 0.05f), new Vector3(0.035f, 0.75f, 0.035f), tufekM)
                    .localRotation = Quaternion.Euler(12f, 0f, 0f);
                f.transform.position = SlotDunya(figurler.Count);
                f.transform.rotation = transform.rotation;
                figurler.Add(f.transform);
                daginiklik.Add(new Vector3(Random.Range(-0.12f, 0.12f), 0f, Random.Range(-0.12f, 0.12f)));
                dustu.Add(false);
            }

        // Bayrak (uzaktan birliği tanımak için)
        GameObject direk = new GameObject("Bayrak");
        direk.transform.SetParent(transform, false);
        direk.transform.localPosition = new Vector3(0f, 0f, -AraZ * Sira * 0.5f - 0.2f);
        Parca(PrimitiveType.Cylinder, direk.transform, new Vector3(0f, 1.4f, 0f), new Vector3(0.05f, 1.4f, 0.05f), tufekM);
        Transform bez = Parca(PrimitiveType.Cube, direk.transform, new Vector3(0.45f, 2.45f, 0f), new Vector3(0.85f, 0.55f, 0.03f),
                              Malzeme(TarafBilgi.OrduRengi(taraf)));
        if (taraf == Taraf.Turk)
            Parca(PrimitiveType.Cylinder, bez, new Vector3(-0.1f, 0f, -0.6f), new Vector3(0.35f, 0.02f, 0.5f), Malzeme(Color.white))
                .localRotation = Quaternion.Euler(90f, 0f, 0f);
        bayrak = direk.transform;

        // Bilgi yazısı
        GameObject y = new GameObject("Yazi");
        y.transform.SetParent(transform, false);
        yazi = y.AddComponent<TextMeshPro>();
        yazi.fontSize = 6f;
        yazi.alignment = TextAlignmentOptions.Center;
        yazi.rectTransform.sizeDelta = new Vector2(12f, 3f);
        yazi.outlineWidth = 0.25f;
        yazi.outlineColor = new Color32(0, 0, 0, 200);

        // Seçim / taraf çerçevesi
        cerceve = gameObject.AddComponent<LineRenderer>();
        cerceve.material = new Material(Shader.Find("Sprites/Default"));
        cerceve.loop = true;
        cerceve.positionCount = 4;
        cerceve.useWorldSpace = true;
        cerceve.widthMultiplier = 0.12f;

        BoxCollider kutu = gameObject.AddComponent<BoxCollider>();
        kutu.size = new Vector3(Genislik + 0.6f, 2.4f, Sira * AraZ + 0.8f);
        kutu.center = new Vector3(0f, 1.1f, 0f);
        // Emir oku
        GameObject ok = new GameObject("EmirOku");
        ok.transform.SetParent(transform.parent, false);
        emirCizgisi = ok.AddComponent<LineRenderer>();
        emirCizgisi.material = cerceve.material;
        emirCizgisi.useWorldSpace = true;
        emirCizgisi.numCapVertices = 2;
        emirCizgisi.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        emirCizgisi.enabled = false;

        SecimGoster(false);
    }

    static Transform Parca(PrimitiveType tur, Transform ebeveyn, Vector3 yer, Vector3 olcek, Material m)
    {
        GameObject g = GameObject.CreatePrimitive(tur);
        Object.Destroy(g.GetComponent<Collider>());
        g.transform.SetParent(ebeveyn, false);
        g.transform.localPosition = yer;
        g.transform.localScale = olcek;
        Renderer r = g.GetComponent<Renderer>();
        r.sharedMaterial = m;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        return g.transform;
    }

    // n. figürün nizamdaki yeri (dünya)
    Vector3 SlotDunya(int n)
    {
        int s = n / Sutun, c = n % Sutun;
        Vector3 yerel = new Vector3((c - (Sutun - 1) / 2f) * AraX, 0f, -(s - (Sira - 1) / 2f) * AraZ);
        if (n < daginiklik.Count) yerel += daginiklik[n];
        return MuharebeAlani.Uzerinde(transform.position + transform.rotation * yerel);
    }

    public void SecimGoster(bool s) { secili = s; }

    // ---------- Hareket (her kare) ----------

    public void Hareket(float dt)
    {
        if (durum == Durum.Bitti) return;
        Vector3 p = transform.position;
        Vector3 hedef = p;
        float hiz = YuruyusHizi;
        yuruyor = false;

        if (durum == Durum.Kacti)
        {
            // Kendi tarafının kenarına doğru kaç
            Vector3 kacis = (mevzi - MuharebeAlani.Merkez); kacis.y = 0f;
            kacis = kacis.sqrMagnitude < 1f ? -transform.forward : kacis.normalized;
            hedef = p + kacis * 10f;
            hiz = KosuHizi * 1.1f;
            if (!MuharebeAlani.Icinde(p, 1.5f)) { durum = Durum.Bitti; Gizle(); return; }
        }
        else if (hedefAlay != null && hedefAlay.Savasabilir)
        {
            float mesafe = Vector3.Distance(Duz(p), Duz(hedefAlay.transform.position));
            if (hucum) { if (mesafe > TemasMesafesi * 0.9f) hedef = hedefAlay.transform.position; hiz = KosuHizi; }
            else if (mesafe > AtisMenzili * 0.9f) hedef = hedefAlay.transform.position;
            else Bak(hedefAlay.transform.position - p, dt);
        }
        else
        {
            if (hedefAlay != null && !hedefAlay.Savasabilir) { hedefAlay = null; hucum = false; hedefNokta = p; }
            hedef = hedefNokta;
            if (hucum) hiz = KosuHizi;
        }

        Vector3 fark = Duz(hedef) - Duz(p);
        if (fark.magnitude > 0.3f)
        {
            // Yokuş yukarı yavaşlar
            Vector3 adim = fark.normalized * hiz * dt;
            if (adim.magnitude > fark.magnitude) adim = fark;
            float egim = MuharebeAlani.Yukseklik(p.x + adim.x, p.z + adim.z) - MuharebeAlani.Yukseklik(p.x, p.z);
            if (egim > 0f) adim *= Mathf.Clamp(1f - egim / Mathf.Max(0.001f, adim.magnitude) * 0.8f, 0.45f, 1f);
            p += adim;
            Bak(fark, dt);
            yuruyor = true;
        }
        transform.position = MuharebeAlani.Uzerinde(p);
    }

    static Vector3 Duz(Vector3 v) { v.y = 0f; return v; }

    void Bak(Vector3 yon, float dt)
    {
        yon.y = 0f;
        if (yon.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(yon), 90f * dt);
    }

    // Figürler nizamdaki yerlerine yetişir; kaçarken dağılır
    public void Gorsel(float dt)
    {
        if (durum == Durum.Bitti) return;
        int canli = Mathf.Clamp(Mathf.CeilToInt(figurler.Count * adam / Mathf.Max(1f, baslangic)), 0, figurler.Count);
        adimSaati += dt * (yuruyor ? (hucum || durum == Durum.Kacti ? 11f : 7f) : 0f);
        int sayac = 0;
        for (int n = 0; n < figurler.Count; n++)
        {
            Transform f = figurler[n];
            if (f == null || dustu[n]) continue;
            if (sayac >= canli) { Dusur(n); continue; }
            sayac++;
            Vector3 slot = SlotDunya(n);
            if (durum == Durum.Kacti) slot += new Vector3(Mathf.Sin(n * 1.7f) * 2.5f, 0f, Mathf.Cos(n * 2.3f) * 2.5f);
            Vector3 yeni = Vector3.MoveTowards(f.position, slot, (durum == Durum.Kacti ? KosuHizi * 1.4f : KosuHizi) * dt * 1.3f);
            yeni.y = MuharebeAlani.Yukseklik(yeni.x, yeni.z) + (yuruyor ? Mathf.Abs(Mathf.Sin(adimSaati + n)) * 0.06f : 0f);
            Vector3 hareket = yeni - f.position; hareket.y = 0f;
            f.position = yeni;
            Quaternion bakis = hareket.sqrMagnitude > 0.0004f && durum == Durum.Kacti ? Quaternion.LookRotation(hareket) : transform.rotation;
            f.rotation = Quaternion.Slerp(f.rotation, bakis, dt * 6f);
        }

        // Çerçeve
        Vector3 sag = transform.right * (Genislik * 0.5f + 0.3f), ileri = transform.forward * (Sira * AraZ * 0.5f + 0.4f);
        Vector3 c = transform.position;
        cerceve.SetPosition(0, MuharebeAlani.Uzerinde(c + sag + ileri, 0.08f));
        cerceve.SetPosition(1, MuharebeAlani.Uzerinde(c + sag - ileri, 0.08f));
        cerceve.SetPosition(2, MuharebeAlani.Uzerinde(c - sag - ileri, 0.08f));
        cerceve.SetPosition(3, MuharebeAlani.Uzerinde(c - sag + ileri, 0.08f));
        Color renk = secili ? new Color(1f, 0.85f, 0.3f, 0.95f)
                   : oyuncunun ? new Color(0.9f, 0.2f, 0.15f, 0.45f) : new Color(0.3f, 0.5f, 0.95f, 0.45f);
        cerceve.startColor = cerceve.endColor = renk;
        cerceve.widthMultiplier = secili ? 0.18f : 0.1f;

        EmirOkunuCiz();

        // Yazı: kameraya dönük
        Camera kam = Camera.main;
        yazi.transform.position = transform.position + Vector3.up * 3.6f;
        if (kam != null) yazi.transform.rotation = Quaternion.LookRotation(yazi.transform.position - kam.transform.position);
        string moralRengi = moral > 60f ? "#9f9" : moral > 30f ? "#fd6" : "#f77";
        string d = durum == Durum.Kacti ? " <color=#f77>KAÇIYOR</color>" : hucum ? " <color=#fc6>HÜCUM</color>" : "";
        yazi.text = "<b>" + ad + "</b>" + d + "\n" + GorunenAdam + "  <color=" + moralRengi + ">moral " + Mathf.RoundToInt(moral) + "</color>"
                  + (cephane <= 0f ? "  <color=#f99>cephane yok</color>" : "");
        yazi.color = secili ? new Color(1f, 0.92f, 0.5f) : Color.white;
        bayrak.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 2f + baslangic) * 8f, 0f);
    }

    // Seçili alayın emrini gösteren, araziyi izleyen ok
    void EmirOkunuCiz()
    {
        if (emirCizgisi == null) return;
        bool saldiri = hedefAlay != null && hedefAlay.Savasabilir;
        Vector3 bas = transform.position, son = saldiri ? hedefAlay.transform.position : hedefNokta;
        Vector3 fark = son - bas; fark.y = 0f;
        float uzunluk = fark.magnitude;
        if (!secili || !oyuncunun || durum != Durum.Hazir || uzunluk < 1.5f) { emirCizgisi.enabled = false; return; }

        // Ok, alayın önünden başlayıp hedefin biraz önünde biter
        Vector3 yon = fark / uzunluk;
        bas += yon * 1.8f;
        son -= yon * (saldiri ? 2.2f : 0.3f);
        uzunluk = Vector3.Distance(new Vector3(bas.x, 0f, bas.z), new Vector3(son.x, 0f, son.z));
        if (uzunluk < 0.5f) { emirCizgisi.enabled = false; return; }

        int n = Mathf.Clamp(Mathf.CeilToInt(uzunluk / 0.8f) + 1, 2, 120);
        emirCizgisi.positionCount = n;
        for (int i = 0; i < n; i++)
            emirCizgisi.SetPosition(i, MuharebeAlani.Uzerinde(Vector3.Lerp(bas, son, i / (float)(n - 1)), 0.25f));

        // Ucu ok başı: son 1,6 birimde genişleyip sivrilir
        float t = Mathf.Clamp01(1f - 1.6f / uzunluk);
        float govde = saldiri ? 0.35f : 0.25f;
        AnimationCurve egri = new AnimationCurve(
            new Keyframe(0f, govde), new Keyframe(t, govde), new Keyframe(Mathf.Min(0.999f, t + 0.001f), govde * 3.2f), new Keyframe(1f, 0f));
        emirCizgisi.widthCurve = egri;
        emirCizgisi.widthMultiplier = 1f;

        Color renk = saldiri ? (hucum ? new Color(1f, 0.15f, 0.1f, 0.95f) : new Color(0.9f, 0.12f, 0.1f, 0.8f))
                             : new Color(1f, 1f, 1f, 0.8f);
        emirCizgisi.startColor = new Color(renk.r, renk.g, renk.b, renk.a * 0.35f);
        emirCizgisi.endColor = renk;
        emirCizgisi.enabled = true;
    }

    // Bir figür vurulur: yere yatar ve öylece kalır
    void Dusur(int n)
    {
        dustu[n] = true;
        Transform f = figurler[n];
        Vector3 p = f.position;
        p.y = MuharebeAlani.Yukseklik(p.x, p.z) + 0.12f;
        f.position = p;
        f.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(Random.value < 0.5f ? -88f : 88f, 0f, 0f);
    }

    // Meydandan çıkan alay: ayakta kalan figürler kaybolur, düşenler yerde kalır
    void Gizle()
    {
        for (int n = 0; n < figurler.Count; n++)
            if (figurler[n] != null && !dustu[n]) figurler[n].gameObject.SetActive(false);
        gameObject.SetActive(false);
    }

    // Alayı derhal kaçış durumuna sok
    public void Bozgun()
    {
        if (durum != Durum.Hazir) return;
        durum = Durum.Kacti;
        hucum = false;
        hedefAlay = null;
    }

    public void Temizle()
    {
        foreach (Transform f in figurler) if (f != null) Destroy(f.gameObject);
        if (emirCizgisi != null) Destroy(emirCizgisi.gameObject);
        Destroy(gameObject);
    }
}
