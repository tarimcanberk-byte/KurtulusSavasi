using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Savaş ekranı (Total War usulü), 1. aşama.
// Oyuncunun katıldığı her muharebede zaman durur ve sorulur: "Bizzat komuta et" ya da "Otomatik çöz".
// Bizzat komuta: ordular alaylara bölünür, gerçek arazide karşılaşır; sol tık seçer, sağ tık emir verir.
public class MuharebeYoneticisi : MonoBehaviour
{
    public static MuharebeYoneticisi Ornek;

    // Harita zamanı bu doğruyken durur
    public static bool Mesgul { get { return Ornek != null && (Ornek.kuyruk.Count > 0 || Ornek.karar || Ornek.aktif); } }

    struct Istek { public Ordu saldiran; public Sancak hedef; }
    readonly Queue<Istek> kuyruk = new Queue<Istek>();
    bool karar, aktif, devamEdecekti, elleSavasildi, acilacak;
    TurYoneticisi tur;

    // Karar penceresi
    GameObject kararPaneli;
    Istek simdiki;
    Ordu simdikiSavunan;

    // Savaş sahnesi
    Ordu saldiran, savunan;
    Sancak hedef;
    bool oyuncuSaldiriyor;
    GameObject kok;
    readonly List<Alay> alaylar = new List<Alay>();
    readonly List<Alay> secili = new List<Alay>();
    Canvas haritaCanvas, savasCanvas;
    Camera kam;
    Vector3 kamYer; Quaternion kamDonus; float kamUzak, sisYogunluk;
    KameraKontrol haritaKamerasi;
    MuharebeKamera savasKamerasi;
    TMP_Text ustYazi, seciliYazi, hizYazisi;
    RectTransform secimKutusu;
    ParticleSystem duman;
    bool bitti, savasDuraklatildi;
    int savasHizi = 1;
    float sure, carpismaSayaci, zekaSayaci;
    const float SureSiniri = 600f;      // 10 dakika: saldıran sonuç alamazsa savunan kazanır

    // Fare
    Vector2 surukleBaslangic;
    bool surukleniyor;
    float sonSagTik;
    Alay sonSagHedef;

    // ---------- Harita tarafı ----------

    // Bu çarpışmada oyuncu var mı ve gerçekten karşısında bir ordu var mı?
    public static bool OyuncuKatilimli(Ordu gelen, Sancak hedef)
    {
        bool dusmanOrdusu = false, turkVar = gelen.OyuncununMu;
        foreach (Ordu o in hedef.Ordular())
        {
            if (!o.Yasiyor) continue;
            if (o.taraf != gelen.taraf) dusmanOrdusu = true;
            if (o.OyuncununMu) turkVar = true;
        }
        return dusmanOrdusu && turkVar;
    }

    public static void Iste(Ordu saldiran, Sancak hedef)
    {
        if (Ornek == null)
        {
            GameObject g = new GameObject("MuharebeYoneticisi");
            Ornek = g.AddComponent<MuharebeYoneticisi>();
        }
        Ornek.IstekEkle(saldiran, hedef);
    }

    void IstekEkle(Ordu s, Sancak h)
    {
        if (tur == null) tur = FindAnyObjectByType<TurYoneticisi>();
        if (!karar && !aktif && kuyruk.Count == 0)
        {
            devamEdecekti = tur != null && !tur.Duraklatildi;
            elleSavasildi = false;
            if (tur != null) tur.Duraklat();
        }
        kuyruk.Enqueue(new Istek { saldiran = s, hedef = h });
        karar = true;
        acilacak = true;     // pencere bir sonraki karede açılır (gün döngüsü bitsin)
    }

    void SiradakiniAc()
    {
        acilacak = false;
        while (kuyruk.Count > 0)
        {
            Istek i = kuyruk.Dequeue();
            if (i.saldiran == null || !i.saldiran.Yasiyor) continue;
            Ordu sv = EnGucluDusman(i.saldiran, i.hedef);
            if (sv == null || !OyuncuKatilimli(i.saldiran, i.hedef))
            {
                // Artık ortada muharebe yok (ör. düşman çekildi): eski usulle ilerle
                string r = Savas.Ilerle(i.saldiran, i.hedef);
                i.saldiran.MuharebeBitti(i.hedef);
                if (r != null) Gunluk.Ekle(r.Replace("\n", " "));
                continue;
            }
            simdiki = i; simdikiSavunan = sv;
            KararPenceresiAc();
            return;
        }
        // Hepsi bitti
        karar = false;
        HaritayiYenile();
        if (devamEdecekti && !elleSavasildi && tur != null) tur.Devam();
        devamEdecekti = false;
    }

    static Ordu EnGucluDusman(Ordu saldiran, Sancak hedef)
    {
        Ordu sv = null;
        foreach (Ordu o in hedef.Ordular())
            if (o.Yasiyor && o.taraf != saldiran.taraf && (sv == null || o.Guc > sv.Guc)) sv = o;
        return sv;
    }

    void HaritayiYenile()
    {
        if (tur != null) tur.EkraniGuncelle();
        HaritaSecici secici = FindAnyObjectByType<HaritaSecici>();
        if (secici != null) secici.BilgiyiYenile();
    }

    void KararPenceresiAc()
    {
        haritaCanvas = FindAnyObjectByType<Canvas>();
        KameraKontrol kk = FindAnyObjectByType<KameraKontrol>();
        if (kk != null) kk.Odakla(simdiki.hedef.transform.position, 16f);
        if (haritaCanvas == null) { OtomatikCoz(); return; }

        Ordu s = simdiki.saldiran, d = simdikiSavunan;
        kararPaneli = TamEkranKarartma(haritaCanvas.transform);
        RectTransform kutu = Panel(kararPaneli.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 480f), ArayuzYardimci.PanelRengi);
        kutu.GetComponent<Image>().color = new Color(0.10f, 0.08f, 0.06f, 0.96f);

        string siper = d.tahkimat > 0 ? ", siper " + d.tahkimat + ". kademe" : "";
        string metin = "<size=130%><b>Muharebe: " + simdiki.hedef.sancakAdi + "</b></size>\n\n"
            + "<b>Saldıran:</b> " + s.orduAdi + " <size=80%>(" + TarafBilgi.Ad(s.taraf) + ")</size> — " + s.askerSayisi + " asker, moral " + s.moral + "\n"
            + "<b>Savunan:</b> " + d.orduAdi + " <size=80%>(" + TarafBilgi.Ad(d.taraf) + ")</size> — " + d.askerSayisi + " asker, moral " + d.moral + siper + "\n\n"
            + "<size=85%>Tahmini güç: saldırı " + Mathf.RoundToInt(s.Guc) + "  /  savunma " + Mathf.RoundToInt(d.SavunmaGucu)
            + "  <color=#bbb>(savunan arazi x" + d.bulunduguSancak.araziBonusu.ToString("0.0#") + ")</color></size>\n\n"
            + "<size=85%>Bizzat komuta edersen savaş ekranı açılır. Otomatik çözümde sonuç güç ve şansa bağlıdır.</size>";
        TMP_Text t = Yazi(kutu, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(840f, 330f), 28f, TextAlignmentOptions.Top);
        t.text = metin;

        Dugme(kutu, "Bizzat Komuta Et", new Vector2(0.5f, 0f), new Vector2(-200f, 70f), new Vector2(360f, 84f), BizzatKomuta, 30f);
        Dugme(kutu, "Otomatik Çöz", new Vector2(0.5f, 0f), new Vector2(200f, 70f), new Vector2(360f, 84f), OtomatikCoz, 30f);
    }

    void KararPenceresiKapat()
    {
        if (kararPaneli != null) Destroy(kararPaneli);
        kararPaneli = null;
    }

    void OtomatikCoz()
    {
        KararPenceresiKapat();
        string r = Savas.Ilerle(simdiki.saldiran, simdiki.hedef);
        simdiki.saldiran.MuharebeBitti(simdiki.hedef);
        if (r != null) Gunluk.Ekle(r.Replace("\n", " "));
        SiradakiniAc();
    }

    void BizzatKomuta()
    {
        KararPenceresiKapat();
        elleSavasildi = true;
        SavasiKur(simdiki.saldiran, simdikiSavunan, simdiki.hedef);
    }

    // ---------- Savaşın kurulması ----------

    void SavasiKur(Ordu s, Ordu d, Sancak h)
    {
        aktif = true; bitti = false; savasDuraklatildi = false; savasHizi = 1;
        saldiran = s; savunan = d; hedef = h;
        oyuncuSaldiriyor = s.OyuncununMu;
        sure = 0f; carpismaSayaci = 0f; zekaSayaci = 0f;
        alaylar.Clear(); secili.Clear();
        Alay.Sifirla();
        Time.timeScale = 1f;

        // Harita arayüzü ve kamerası kenara
        if (haritaCanvas != null) haritaCanvas.gameObject.SetActive(false);
        kam = Camera.main;
        kamYer = kam.transform.position; kamDonus = kam.transform.rotation; kamUzak = kam.farClipPlane;
        sisYogunluk = RenderSettings.fogDensity;
        RenderSettings.fogDensity = 0.0045f;
        kam.farClipPlane = 600f;
        haritaKamerasi = kam.GetComponent<KameraKontrol>();
        if (haritaKamerasi != null) haritaKamerasi.enabled = false;
        savasKamerasi = kam.gameObject.AddComponent<MuharebeKamera>();

        kok = new GameObject("Muharebe");
        MuharebeAlani.Kur(h, kok.transform);
        DumanKur();

        // Saldıran güneyden, savunan kuzeyden
        AlaylariKur(s, true, -24f);
        AlaylariKur(d, false, 12f);

        Vector3 m = MuharebeAlani.Merkez;
        if (oyuncuSaldiriyor) savasKamerasi.Ayarla(m + new Vector3(0f, 0f, -12f), 0f, 30f);
        else savasKamerasi.Ayarla(m + new Vector3(0f, 0f, 6f), 180f, 30f);

        SavasArayuzunuKur();
        Gunluk.Ekle("<b>" + h.sancakAdi + " meydan muharebesi başladı.</b> Komuta sende.");
    }

    void AlaylariKur(Ordu o, bool saldiranMi, float z)
    {
        int n = Mathf.Clamp(Mathf.RoundToInt(o.askerSayisi / 1500f), 1, 8);
        int her = Mathf.Max(1, o.askerSayisi / n);
        float verim = Mathf.Clamp(o.Guc / Mathf.Max(1f, o.askerSayisi), 0.3f, 1.5f);
        float siper = saldiranMi ? 1f : Mathf.Clamp(o.SavunmaGucu / Mathf.Max(1f, o.Guc), 1f, 2.2f);
        Vector3 bakis = saldiranMi ? Vector3.forward : Vector3.back;
        string onEk = o.OyuncununMu ? "" : TarafBilgi.Ad(o.taraf) + " ";
        for (int i = 0; i < n; i++)
        {
            float x = (i - (n - 1) / 2f) * 7.5f;
            Vector3 yer = MuharebeAlani.Merkez + new Vector3(saldiranMi ? x : -x, 0f, z);
            Alay a = Alay.Olustur(kok.transform, onEk + (i + 1) + ". Alay", o.taraf, o.OyuncununMu, her, yer, bakis, o.moral, verim);
            a.savunan = !saldiranMi;
            a.siper = siper;
            alaylar.Add(a);
        }
    }

    // Tüfek dumanı ve namlu ateşi için tek bir parçacık sistemi
    void DumanKur()
    {
        GameObject g = new GameObject("Duman");
        g.transform.SetParent(kok.transform, false);
        duman = g.AddComponent<ParticleSystem>();
        duman.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var ana = duman.main;
        ana.playOnAwake = false;
        ana.simulationSpace = ParticleSystemSimulationSpace.World;
        ana.maxParticles = 4000;
        ana.startLifetime = 3f;
        ana.startSpeed = 0.25f;
        ana.gravityModifier = -0.015f;
        var emisyon = duman.emission; emisyon.enabled = false;
        var sekil = duman.shape; sekil.enabled = false;
        var renk = duman.colorOverLifetime; renk.enabled = true;
        Gradient gr = new Gradient();
        gr.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                   new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0f, 1f) });
        renk.color = gr;
        var boy = duman.sizeOverLifetime; boy.enabled = true;
        boy.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.5f, 1f, 2.4f));

        Texture2D yumusak = new Texture2D(64, 64, TextureFormat.RGBA32, false);
        for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(31.5f, 31.5f)) / 32f;
                yumusak.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(1f - d) * Mathf.Clamp01(1f - d)));
            }
        yumusak.Apply();
        Material mm = new Material(Shader.Find("Sprites/Default"));
        mm.mainTexture = yumusak;
        g.GetComponent<ParticleSystemRenderer>().material = mm;
        duman.Play();
    }

    void AtesEt(Alay a)
    {
        if (duman == null) return;
        var ep = new ParticleSystem.EmitParams();
        for (int i = 0; i < 2; i++)
        {
            ep.position = a.transform.position + a.transform.forward * 1.8f + a.transform.right * Random.Range(-2.8f, 2.8f) + Vector3.up * 0.9f;
            ep.startColor = new Color(0.86f, 0.86f, 0.82f, 0.45f);
            ep.startSize = Random.Range(0.9f, 1.5f);
            ep.startLifetime = Random.Range(2.2f, 3.5f);
            ep.velocity = a.transform.forward * 0.4f + Random.insideUnitSphere * 0.15f;
            duman.Emit(ep, 1);
        }
        for (int i = 0; i < 3; i++)
        {
            ep.position = a.transform.position + a.transform.forward * 1.5f + a.transform.right * Random.Range(-2.8f, 2.8f) + Vector3.up * 0.75f;
            ep.startColor = new Color(1f, 0.85f, 0.4f, 1f);
            ep.startSize = 0.3f;
            ep.startLifetime = 0.12f;
            ep.velocity = Vector3.zero;
            duman.Emit(ep, 1);
        }
    }

    // ---------- Her kare ----------

    void Update()
    {
        if (acilacak && !aktif && kararPaneli == null) SiradakiniAc();
        if (!aktif) return;

        Girdi();
        if (bitti) return;

        float dt = Time.deltaTime;
        foreach (Alay a in alaylar) a.Hareket(dt);
        Ayristir();
        foreach (Alay a in alaylar) a.Gorsel(dt);

        if (dt > 0f)
        {
            sure += dt;
            carpismaSayaci += dt; zekaSayaci += dt;
            if (carpismaSayaci >= 0.5f) { carpismaSayaci -= 0.5f; Carpisma(); }
            if (zekaSayaci >= 1f) { zekaSayaci -= 1f; YapayZeka(); }
        }
        ArayuzuGuncelle();
        BitisKontrol();
    }

    // Alaylar iç içe girmesin
    void Ayristir()
    {
        for (int i = 0; i < alaylar.Count; i++)
            for (int j = i + 1; j < alaylar.Count; j++)
            {
                Alay a = alaylar[i], b = alaylar[j];
                if (a.durum == Alay.Durum.Bitti || b.durum == Alay.Durum.Bitti) continue;
                if (a.durum == Alay.Durum.Kacti || b.durum == Alay.Durum.Kacti) continue;
                float min = a.oyuncunun == b.oyuncunun ? 5f : Alay.TemasMesafesi * 0.85f;
                Vector3 f = b.transform.position - a.transform.position; f.y = 0f;
                float d = f.magnitude;
                if (d >= min) continue;
                Vector3 it = (d < 0.01f ? Vector3.right : f / d) * (min - d) * 0.5f;
                a.transform.position = MuharebeAlani.Uzerinde(a.transform.position - it);
                b.transform.position = MuharebeAlani.Uzerinde(b.transform.position + it);
            }
    }

    // ---------- Çarpışma (yarım saniyede bir) ----------

    void Carpisma()
    {
        Dictionary<Alay, float> kayip = new Dictionary<Alay, float>();
        HashSet<Alay> atesAltinda = new HashSet<Alay>();
        HashSet<Alay> gogusGoguse = new HashSet<Alay>();

        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            Alay yakin = EnYakinDusman(a, out float yakinMesafe);
            if (yakin == null) continue;
            float moralCarpani = 0.5f + a.moral / 200f;

            if (yakinMesafe <= Alay.TemasMesafesi + 0.5f)
            {
                // Göğüs göğüse (süngü)
                Alay h = (a.hedefAlay != null && a.hedefAlay.Savasabilir && Mesafe(a, a.hedefAlay) <= Alay.TemasMesafesi + 0.5f) ? a.hedefAlay : yakin;
                float z = a.adam * 0.010f * a.verim * moralCarpani * Kanat(a, h) * (a.hucum ? 1.3f : 1f) * Random.Range(0.75f, 1.25f) / SiperEtkisi(h);
                Ekle(kayip, h, z);
                gogusGoguse.Add(a); gogusGoguse.Add(h);
            }
            else if (a.cephane > 0f)
            {
                // Tüfek ateşi
                Alay h = (a.hedefAlay != null && a.hedefAlay.Savasabilir && Mesafe(a, a.hedefAlay) <= Alay.AtisMenzili) ? a.hedefAlay
                       : (yakinMesafe <= Alay.AtisMenzili ? yakin : null);
                if (h == null) continue;
                if (a.hucum) continue;   // hücumda koşan alay ateş etmez
                float mesafe = Mesafe(a, h);
                float isabet = 1.3f - 0.6f * mesafe / Alay.AtisMenzili;
                float tepe = a.transform.position.y > h.transform.position.y + 1f ? 1.25f : 1f;
                float z = a.adam * 0.0022f * a.verim * moralCarpani * isabet * tepe * (a.yuruyor ? 0.35f : 1f)
                          * Kanat(a, h) * Random.Range(0.75f, 1.25f) / SiperEtkisi(h);
                Ekle(kayip, h, z);
                atesAltinda.Add(h);
                a.cephane -= 1f;
                AtesEt(a);
            }
        }

        // Kaçanlar kovalanırken kayıp verir
        foreach (Alay k in alaylar)
        {
            if (k.durum != Alay.Durum.Kacti) continue;
            foreach (Alay a in alaylar)
                if (a.Savasabilir && a.oyuncunun != k.oyuncunun && Mesafe(a, k) < 5f) { Ekle(kayip, k, k.adam * 0.02f); break; }
        }

        // Kayıplar ve moral
        foreach (Alay a in alaylar)
        {
            if (a.durum == Alay.Durum.Bitti) continue;
            float z = kayip.ContainsKey(a) ? Mathf.Min(kayip[a], a.adam) : 0f;
            a.adam -= z;
            if (a.durum != Alay.Durum.Hazir) continue;
            if (z > 0f) a.moral -= z / Mathf.Max(1f, a.baslangic) * 100f * 1.4f;
            if (gogusGoguse.Contains(a)) a.moral -= 0.5f;
            if (z <= 0f && !gogusGoguse.Contains(a)) a.moral = Mathf.Min(100f, a.moral + 0.35f);
            if (a.moral < 15f || a.adam < a.baslangic * 0.2f) Bozul(a);
        }
    }

    void Bozul(Alay a)
    {
        a.Bozgun();
        if (a.oyuncunun) secili.Remove(a);
        a.SecimGoster(false);
        foreach (Alay b in alaylar)
        {
            if (b == a || !b.Savasabilir || Mesafe(a, b) > 12f) continue;
            if (b.oyuncunun == a.oyuncunun) b.moral -= 8f;       // yanındaki kaçınca panik
            else b.moral = Mathf.Min(100f, b.moral + 4f);
        }
    }

    static void Ekle(Dictionary<Alay, float> d, Alay a, float z) { d[a] = (d.ContainsKey(a) ? d[a] : 0f) + z; }

    static float Mesafe(Alay a, Alay b)
    {
        Vector3 f = a.transform.position - b.transform.position; f.y = 0f;
        return f.magnitude;
    }

    Alay EnYakinDusman(Alay a, out float mesafe)
    {
        Alay en = null; mesafe = float.MaxValue;
        foreach (Alay b in alaylar)
        {
            if (!b.Savasabilir || b.oyuncunun == a.oyuncunun) continue;
            float d = Mesafe(a, b);
            if (d < mesafe) { mesafe = d; en = b; }
        }
        return en;
    }

    // Yandan ya da arkadan vurmak çok daha etkilidir
    static float Kanat(Alay vuran, Alay hedef)
    {
        Vector3 yon = vuran.transform.position - hedef.transform.position; yon.y = 0f;
        if (yon.sqrMagnitude < 0.01f) return 1f;
        float d = Vector3.Dot(hedef.transform.forward, yon.normalized);
        if (d < -0.35f) return 2f;      // arkadan
        if (d < 0.45f) return 1.5f;     // yandan
        return 1f;
    }

    // Savunan, mevzisinden ayrılmadıkça siper ve arazi avantajını korur
    static float SiperEtkisi(Alay a)
    {
        if (!a.savunan) return 1f;
        Vector3 f = a.transform.position - a.mevzi; f.y = 0f;
        return f.magnitude < 10f ? a.siper : 1f;
    }

    // ---------- Düşman komutanı (saniyede bir) ----------

    void YapayZeka()
    {
        bool dusmanSaldiriyor = !oyuncuSaldiriyor;
        foreach (Alay a in alaylar)
        {
            if (a.oyuncunun || !a.Savasabilir) continue;
            Alay e = EnYakinDusman(a, out float d);
            if (e == null) continue;

            if (dusmanSaldiriyor)
            {
                if (a.hedefAlay == null || !a.hedefAlay.Savasabilir || Mesafe(a, a.hedefAlay) > d + 8f) { a.hedefAlay = e; a.hucum = false; }
                float h = Mesafe(a, a.hedefAlay);
                bool ustun = a.adam * a.moral > a.hedefAlay.adam * a.hedefAlay.moral * 1.3f;
                if (h < Alay.AtisMenzili + 2f && (ustun || a.hedefAlay.moral < 40f || a.cephane <= 0f || sure > 240f)) a.hucum = true;
            }
            else
            {
                // Savunan mevzisini tutar; menzile gireni vurur, zayıflayana hücum eder
                if (d <= Alay.AtisMenzili) { if (a.hedefAlay == null || !a.hedefAlay.Savasabilir) a.hedefAlay = e; }
                else if (!a.hucum) { a.hedefAlay = null; a.hedefNokta = a.mevzi; }
                if (d < 10f && (e.moral < 35f || a.cephane <= 0f)) { a.hedefAlay = e; a.hucum = true; }
                if (a.hucum && d > 18f) { a.hucum = false; a.hedefAlay = null; a.hedefNokta = a.mevzi; }   // fazla açılma, geri dön
            }
        }
    }

    // ---------- Oyuncunun girdisi ----------

    void Girdi()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (bitti || m == null) return;

        if (k != null)
        {
            if (k.spaceKey.wasPressedThisFrame) DuraklatDevam();
            if (k.hKey.wasPressedThisFrame) Hucum();
            if (k.xKey.wasPressedThisFrame) Dur();
            if (k.tabKey.wasPressedThisFrame) TumunuSec();
        }

        bool arayuzde = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
        Vector2 fp = m.position.ReadValue();

        // Sol tık: seç, sürükle: kutuyla seç
        if (m.leftButton.wasPressedThisFrame && !arayuzde) { surukleBaslangic = fp; surukleniyor = true; }
        if (surukleniyor)
        {
            Vector2 min = Vector2.Min(surukleBaslangic, fp), max = Vector2.Max(surukleBaslangic, fp);
            bool kutu = (max - min).magnitude > 12f;
            if (secimKutusu != null)
            {
                secimKutusu.gameObject.SetActive(kutu);
                float o = savasCanvas.scaleFactor;
                secimKutusu.anchoredPosition = min / o;
                secimKutusu.sizeDelta = (max - min) / o;
            }
            if (m.leftButton.wasReleasedThisFrame)
            {
                surukleniyor = false;
                if (secimKutusu != null) secimKutusu.gameObject.SetActive(false);
                bool ekle = k != null && (k.leftShiftKey.isPressed || k.rightShiftKey.isPressed);
                if (!ekle) SecimiTemizle();
                if (kutu)
                {
                    foreach (Alay a in alaylar)
                    {
                        if (!a.oyuncunun || !a.Savasabilir) continue;
                        Vector3 e = kam.WorldToScreenPoint(a.transform.position);
                        if (e.z > 0 && e.x >= min.x && e.x <= max.x && e.y >= min.y && e.y <= max.y) Sec(a);
                    }
                }
                else
                {
                    Alay a = FaredekiAlay(fp, out _);
                    if (a != null && a.oyuncunun && a.Savasabilir)
                    {
                        if (ekle && secili.Contains(a)) { secili.Remove(a); a.SecimGoster(false); }
                        else Sec(a);
                    }
                }
            }
        }

        // Sağ tık: yürü ya da saldır (iki kez: hücum)
        if (m.rightButton.wasPressedThisFrame && !arayuzde && secili.Count > 0)
        {
            Alay a = FaredekiAlay(fp, out Vector3 nokta);
            bool cift = Time.unscaledTime - sonSagTik < 0.4f;
            sonSagTik = Time.unscaledTime;
            if (a != null && !a.oyuncunun && a.Savasabilir)
            {
                bool hucum = cift && sonSagHedef == a;
                foreach (Alay s in secili) { s.hedefAlay = a; s.hucum = hucum; }
                sonSagHedef = a;
            }
            else if (nokta != Vector3.zero)
            {
                YuruyusEmri(nokta, cift);
                sonSagHedef = null;
            }
        }
    }

    Alay FaredekiAlay(Vector2 ekran, out Vector3 nokta)
    {
        nokta = Vector3.zero;
        Ray isin = kam.ScreenPointToRay(ekran);
        RaycastHit[] vuruslar = Physics.RaycastAll(isin, 800f);
        Alay en = null; float enYakin = float.MaxValue;
        foreach (RaycastHit v in vuruslar)
        {
            if (kok == null || !v.transform.IsChildOf(kok.transform)) continue;
            Alay a = v.collider.GetComponent<Alay>();
            if (a != null) { if (v.distance < enYakin) { enYakin = v.distance; en = a; } }
            else if (nokta == Vector3.zero || v.distance < Vector3.Distance(isin.origin, nokta)) nokta = v.point;
        }
        return en;
    }

    // Seçili alaylar tıklanan yere, yürüyüş yönüne dik bir hat halinde dizilir
    void YuruyusEmri(Vector3 nokta, bool kos)
    {
        Vector3 orta = Vector3.zero;
        foreach (Alay a in secili) orta += a.transform.position;
        orta /= secili.Count;
        Vector3 yon = nokta - orta; yon.y = 0f;
        if (yon.sqrMagnitude < 0.01f) yon = secili[0].transform.forward;
        yon.Normalize();
        Vector3 yan = Vector3.Cross(Vector3.up, yon);
        List<Alay> sirali = new List<Alay>(secili);
        sirali.Sort((p, q) => Vector3.Dot(p.transform.position, yan).CompareTo(Vector3.Dot(q.transform.position, yan)));
        for (int i = 0; i < sirali.Count; i++)
        {
            Vector3 hedefYer = nokta + yan * (i - (sirali.Count - 1) / 2f) * 7.5f;
            Vector3 yerel = hedefYer - MuharebeAlani.Merkez;
            float s = MuharebeAlani.Yari - 3f;
            hedefYer = MuharebeAlani.Merkez + new Vector3(Mathf.Clamp(yerel.x, -s, s), 0f, Mathf.Clamp(yerel.z, -s, s));
            Alay a = sirali[i];
            a.hedefAlay = null;
            a.hucum = kos;
            a.hedefNokta = MuharebeAlani.Uzerinde(hedefYer);
        }
    }

    void Sec(Alay a) { if (!secili.Contains(a)) secili.Add(a); a.SecimGoster(true); }

    void SecimiTemizle()
    {
        foreach (Alay a in secili) if (a != null) a.SecimGoster(false);
        secili.Clear();
    }

    void TumunuSec()
    {
        SecimiTemizle();
        foreach (Alay a in alaylar) if (a.oyuncunun && a.Savasabilir) Sec(a);
    }

    void Hucum()
    {
        foreach (Alay a in secili)
        {
            if (a.hedefAlay == null || !a.hedefAlay.Savasabilir) a.hedefAlay = EnYakinDusman(a, out _);
            a.hucum = a.hedefAlay != null;
        }
    }

    void Dur()
    {
        foreach (Alay a in secili) { a.hedefAlay = null; a.hucum = false; a.hedefNokta = a.transform.position; }
    }

    void DuraklatDevam()
    {
        savasDuraklatildi = !savasDuraklatildi;
        Time.timeScale = savasDuraklatildi ? 0f : savasHizi;
    }

    void HizDegistir()
    {
        savasHizi = savasHizi >= 3 ? 1 : savasHizi + 1;
        if (!savasDuraklatildi) Time.timeScale = savasHizi;
    }

    void GeriCekil()
    {
        if (bitti) return;
        foreach (Alay a in alaylar)
            if (a.oyuncunun && a.Savasabilir) { a.Bozgun(); a.duzenliCekildi = true; }
        SecimiTemizle();
        Bitir(!oyuncuSaldiriyor);    // çekilen taraf muharebeyi kaybeder
    }

    // Kalan kısmı güç ve şansla hesapla
    void OtomatikBitir()
    {
        if (bitti) return;
        float s = 0f, d = 0f;
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            float g = a.adam * (0.5f + a.moral / 200f) * a.verim;
            if (a.savunan) d += g * a.siper; else s += g;
        }
        bool kazandi = s * Random.Range(0.85f, 1.15f) > d;
        float oran = Mathf.Min(s, d) / Mathf.Max(1f, Mathf.Max(s, d));
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            bool kazanan = a.savunan != kazandi;
            a.adam *= kazanan ? 1f - 0.15f * oran : 0.7f;
            if (!kazanan) a.Bozgun();
        }
        Bitir(kazandi);
    }

    void BitisKontrol()
    {
        if (bitti) return;
        bool sVar = false, dVar = false;
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            if (a.savunan) dVar = true; else sVar = true;
        }
        if (!dVar) Bitir(true);
        else if (!sVar) Bitir(false);
        else if (sure >= SureSiniri) Bitir(false);    // saldıran vaktinde sonuç alamadı
    }

    // ---------- Sonuç ----------

    void Bitir(bool saldiranKazandi)
    {
        bitti = true;
        Time.timeScale = 0f;
        SecimiTemizle();

        float kalanS = 0f, kalanD = 0f;
        foreach (Alay a in alaylar)
        {
            float k = a.Savasabilir ? a.adam : a.adam * (a.duzenliCekildi ? 0.9f : 0.6f);
            if (a.savunan) kalanD += k; else kalanS += k;
        }
        int sKayip = Mathf.Clamp(saldiran.askerSayisi - Mathf.RoundToInt(kalanS), 0, saldiran.askerSayisi);
        int dKayip = Mathf.Clamp(savunan.askerSayisi - Mathf.RoundToInt(kalanD), 0, savunan.askerSayisi);
        int bizimKayip = oyuncuSaldiriyor ? sKayip : dKayip, onlarinKayip = oyuncuSaldiriyor ? dKayip : sKayip;

        string baslik = "<b>Meydan muharebesi: " + hedef.sancakAdi + "</b>  <size=80%>(bizzat komuta)</size>";
        string rapor = Savas.SonucUygula(saldiran, savunan, hedef, saldiranKazandi, sKayip, dKayip, baslik);
        saldiran.MuharebeBitti(hedef);
        Gunluk.Ekle(rapor.Replace("\n", " "));

        bool zafer = saldiranKazandi == oyuncuSaldiriyor;
        RectTransform p = Panel(savasCanvas.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1000f, 560f),
                                new Color(0.10f, 0.08f, 0.06f, 0.95f));
        TMP_Text t = Yazi(p, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f), new Vector2(940f, 400f), 28f, TextAlignmentOptions.Top);
        t.text = (zafer ? "<size=200%><color=#9f9><b>ZAFER</b></color></size>" : "<size=200%><color=#f88><b>YENİLGİ</b></color></size>")
               + "\n\nKayıplarımız: <b>" + bizimKayip + "</b>   ·   Düşman kayıpları: <b>" + onlarinKayip + "</b>\n\n"
               + "<size=85%>" + rapor + "</size>";
        Dugme(p, "Haritaya Dön", new Vector2(0.5f, 0f), new Vector2(0f, 70f), new Vector2(380f, 84f), Kapat, 32f);
    }

    void Kapat()
    {
        Time.timeScale = 1f;
        if (kok != null) Destroy(kok);
        if (savasCanvas != null) Destroy(savasCanvas.gameObject);
        if (savasKamerasi != null) Destroy(savasKamerasi);
        if (kam != null)
        {
            kam.transform.position = kamYer; kam.transform.rotation = kamDonus; kam.farClipPlane = kamUzak;
        }
        if (haritaKamerasi != null) haritaKamerasi.enabled = true;
        RenderSettings.fogDensity = sisYogunluk;
        if (haritaCanvas != null) haritaCanvas.gameObject.SetActive(true);
        alaylar.Clear(); secili.Clear();
        aktif = false;
        HaritayiYenile();
        SiradakiniAc();
    }

    // ---------- Savaş arayüzü ----------

    void SavasArayuzunuKur()
    {
        GameObject c = new GameObject("SavasArayuzu", typeof(RectTransform));
        savasCanvas = c.AddComponent<Canvas>();
        savasCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        savasCanvas.sortingOrder = 10;
        CanvasScaler olcek = c.AddComponent<CanvasScaler>();
        olcek.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        olcek.referenceResolution = new Vector2(1920f, 1080f);
        olcek.matchWidthOrHeight = 0.5f;
        c.AddComponent<GraphicRaycaster>();
        c.transform.SetParent(kok.transform, false);

        ArayuzYardimci.UstSeritOlustur(savasCanvas, 90f);
        ustYazi = Yazi(c.transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(1800f, 80f), 30f, TextAlignmentOptions.Center);

        float y = 50f;
        Dugme(c.transform, "Hücum!\n<size=60%>(H)</size>", new Vector2(0.5f, 0f), new Vector2(-575f, y), new Vector2(210f, 80f), Hucum);
        Dugme(c.transform, "Dur\n<size=60%>(X)</size>", new Vector2(0.5f, 0f), new Vector2(-345f, y), new Vector2(210f, 80f), Dur);
        Dugme(c.transform, "Tümünü Seç\n<size=60%>(Tab)</size>", new Vector2(0.5f, 0f), new Vector2(-115f, y), new Vector2(210f, 80f), TumunuSec);
        Button hiz = Dugme(c.transform, "Hız x1", new Vector2(0.5f, 0f), new Vector2(115f, y), new Vector2(210f, 80f), HizDegistir);
        hizYazisi = hiz.GetComponentInChildren<TMP_Text>();
        Dugme(c.transform, "Geri Çekil", new Vector2(0.5f, 0f), new Vector2(345f, y), new Vector2(210f, 80f), GeriCekil);
        Dugme(c.transform, "Otomatik Bitir", new Vector2(0.5f, 0f), new Vector2(575f, y), new Vector2(210f, 80f), OtomatikBitir);

        seciliYazi = Yazi(c.transform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(24f, 110f), new Vector2(560f, 200f), 24f, TextAlignmentOptions.BottomLeft);
        TMP_Text yardim = Yazi(c.transform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-24f, 110f), new Vector2(560f, 200f), 20f, TextAlignmentOptions.BottomRight);
        yardim.text = "<color=#ccc>Sol tık: alay seç  ·  sürükle: kutu ile seç  ·  Shift: ekle\n"
                    + "Sağ tık: yürü / düşmana saldır  ·  çift sağ tık: koş / hücum\n"
                    + "WASD: kaydır  ·  Q/E ya da orta tuş: döndür  ·  tekerlek: yakınlaş\n"
                    + "Boşluk: duraklat  ·  Yandan ve arkadan vurmak çok daha etkili</color>";

        GameObject kutu = new GameObject("SecimKutusu", typeof(RectTransform), typeof(Image));
        kutu.transform.SetParent(c.transform, false);
        secimKutusu = kutu.GetComponent<RectTransform>();
        secimKutusu.anchorMin = secimKutusu.anchorMax = Vector2.zero;
        secimKutusu.pivot = Vector2.zero;
        Image ki = kutu.GetComponent<Image>();
        ki.color = new Color(1f, 0.85f, 0.3f, 0.18f);
        ki.raycastTarget = false;
        kutu.SetActive(false);
    }

    void ArayuzuGuncelle()
    {
        int bizAdam = 0, bizAlay = 0, onlarAdam = 0, onlarAlay = 0;
        foreach (Alay a in alaylar)
        {
            if (!a.Savasabilir) continue;
            if (a.oyuncunun) { bizAdam += a.GorunenAdam; bizAlay++; } else { onlarAdam += a.GorunenAdam; onlarAlay++; }
        }
        int kalan = Mathf.Max(0, Mathf.CeilToInt(SureSiniri - sure));
        string durum = savasDuraklatildi ? "<color=#fc8>DURAKLATILDI</color>" : "Hız x" + savasHizi;
        ustYazi.text = "<b>" + hedef.sancakAdi + " Muharebesi</b>   |   <color=#f99>Biz: " + bizAdam + " (" + bizAlay + " alay)</color>"
                     + "   |   <color=#9bf>" + TarafBilgi.Ad(oyuncuSaldiriyor ? savunan.taraf : saldiran.taraf) + ": " + onlarAdam + " (" + onlarAlay + " alay)</color>"
                     + "   |   " + (oyuncuSaldiriyor ? "Saldırı süresi " : "Dayanılacak süre ") + (kalan / 60) + ":" + (kalan % 60).ToString("00")
                     + "   |   " + durum;
        if (hizYazisi != null) hizYazisi.text = "Hız x" + savasHizi;

        if (secili.Count == 0) { seciliYazi.text = "<color=#ccc>Alay seçilmedi.</color>"; return; }
        int adam = 0; float moral = 0f, cephane = 0f;
        foreach (Alay a in secili) { adam += a.GorunenAdam; moral += a.moral; cephane += a.cephane; }
        seciliYazi.text = "<b>" + (secili.Count == 1 ? secili[0].ad : secili.Count + " alay seçili") + "</b>\n"
                        + adam + " asker  ·  moral " + Mathf.RoundToInt(moral / secili.Count)
                        + "  ·  cephane %" + Mathf.RoundToInt(cephane / secili.Count / 140f * 100f);
    }

    // ---------- Arayüz yardımcıları ----------

    static GameObject TamEkranKarartma(Transform ebeveyn)
    {
        GameObject g = new GameObject("MuharebeKarari", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ebeveyn, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
        g.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.55f);
        return g;
    }

    static RectTransform Panel(Transform ebeveyn, Vector2 capa, Vector2 yer, Vector2 boyut, Color renk)
    {
        GameObject g = new GameObject("Panel", typeof(RectTransform), typeof(Image));
        g.transform.SetParent(ebeveyn, false);
        RectTransform r = g.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = capa; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        g.GetComponent<Image>().color = renk;
        return r;
    }

    static TMP_Text Yazi(Transform ebeveyn, Vector2 capa, Vector2 pivot, Vector2 yer, Vector2 boyut, float font, TextAlignmentOptions hiza)
    {
        GameObject g = new GameObject("Yazi", typeof(RectTransform));
        g.transform.SetParent(ebeveyn, false);
        TextMeshProUGUI t = g.AddComponent<TextMeshProUGUI>();
        RectTransform r = t.rectTransform;
        r.anchorMin = r.anchorMax = capa; r.pivot = pivot;
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        t.fontSize = font;
        t.alignment = hiza;
        t.color = ArayuzYardimci.DugmeYazisi;
        t.raycastTarget = false;
        return t;
    }

    static Button Dugme(Transform ebeveyn, string yazi, Vector2 capa, Vector2 yer, Vector2 boyut, UnityAction islem, float font = 26f)
    {
        GameObject d = TMP_DefaultControls.CreateButton(new TMP_DefaultControls.Resources());
        d.transform.SetParent(ebeveyn, false);
        RectTransform r = d.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = capa; r.pivot = new Vector2(0.5f, 0.5f);
        r.anchoredPosition = yer; r.sizeDelta = boyut;
        TMP_Text t = d.GetComponentInChildren<TMP_Text>();
        t.text = yazi; t.fontSize = font;
        Button b = d.GetComponent<Button>();
        b.onClick.AddListener(islem);
        ArayuzYardimci.DugmeStili(b);
        return b;
    }
}
