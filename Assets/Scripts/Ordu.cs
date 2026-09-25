using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Haritada hareket eden bir ordu birimi.
public class Ordu : MonoBehaviour
{
    public string orduAdi = "20. Kolordu";
    public Taraf taraf = Taraf.Turk;
    public Sancak bulunduguSancak;
    public int askerSayisi = 5000;
    public int moral = 100;            // 0 - 100 arası

    // ---------- Emirler ve hareket (gerçek zamanlı) ----------
    public enum Emir { Bekle, Git, SinirSavunmasi, Taarruz }
    public Emir emir = Emir.Bekle;
    public Sancak savunmaMerkezi;          // Sınır savunması emrinin merkezi
    public float hizKmGun = 15f;           // yürüyüş hızı (km/gün)

    private readonly List<Sancak> yol = new List<Sancak>();   // sıradaki duraklar
    private float ilerleme = 0f;           // şu anki yol parçasında 0..1
    private int sabitGun = 0;              // kaç gündür yerinde duruyor (siper için)
    private int baskinBeklemesi = 0;       // baskından sonra dinlenme (gün)
    private int kararSayaci = 0;           // otomatik emirlerin ne sıklıkla düşüneceği
    private LineRenderer rotaCizgisi;

    public bool HareketHalinde { get { return yol.Count > 0; } }
    public Sancak SonrakiDurak { get { return yol.Count > 0 ? yol[0] : null; } }
    public Sancak VarisNoktasi { get { return yol.Count > 0 ? yol[yol.Count - 1] : null; } }

    // Savaş ekranında (ya da oyuncunun kararında) bekleyen muharebe
    private bool muharebeBekliyor = false;
    public bool MuharebeBekliyor { get { return muharebeBekliyor; } }

    private bool hareketHakki = true;
    private bool yasiyor = true;

    // Siper / tahkimat: yerinden kıpırdamayan ordu her tur biraz daha kazılır (en fazla 3 kademe)
    public int tahkimat = 0;
    public const int MaxTahkimat = 3;
    public float TahkimatCarpani { get { return 1f + 0.15f * tahkimat; } }

    public int IkmalMesafesi { get { return Ikmal.Mesafe(this); } }

    // Görsel parçalar: taş (zemin), direk, bayrak ve asker sayısı yazısı
    private Transform gorsel;
    private Renderer tas, bayrak;
    private TextMeshPro sayiYazisi;
    private GameObject[] figurler;
    public const float Boyut = 1.8f;   // ordu işaretlerinin genel büyüklüğü
    private int sonGosterilenAsker = -1;
    private bool sonGosterilenHak = true;

    // Baskın yapabilir mi (baskınlar arasında dinlenmek gerekir)
    public bool HareketHakkiVar { get { return baskinBeklemesi <= 0; } }
    public bool OyuncununMu    { get { return taraf == Taraf.Turk; } }
    public bool Yasiyor        { get { return yasiyor; } }

    // Her 100 asker her tur 1 erzak yer, her 200 asker 1 para maaş alır
    public int ErzakTuketimi   { get { return Mathf.CeilToInt(askerSayisi / 100f); } }
    public int MaasGideri      { get { return Mathf.CeilToInt(askerSayisi / 200f); } }

    // Savaş gücü: asker sayısı ve moralin birleşimi
    // İkmalden uzak ordu zayıflar
    public float Guc           { get { return askerSayisi * (0.5f + moral / 200f) * Ikmal.Carpan(IkmalMesafesi); } }

    // Bulunduğu sancağı savunurken gücü: arazi ve siper avantajıyla
    public float SavunmaGucu   { get { return Guc * 1.2f * TahkimatCarpani * (bulunduguSancak != null ? bulunduguSancak.araziBonusu : 1f); } }

    // Yeni bir ordu oluşturur (harita kurulurken, birlik toplanırken, düşman takviyesinde)
    public static Ordu Olustur(string ad, Taraf taraf, int asker, Sancak sancak, int moral = 100)
    {
        GameObject kok = new GameObject(ad);
        GameObject klasor = GameObject.Find("Ordular");
        if (klasor != null) kok.transform.SetParent(klasor.transform, true);

        // Tıklanabilmesi için görünmez bir kutu
        BoxCollider kutu = kok.AddComponent<BoxCollider>();
        kutu.center = new Vector3(0f, 0.5f * Boyut, 0f);
        kutu.size = new Vector3(0.55f, 1f, 0.5f) * Boyut;

        Ordu o = kok.AddComponent<Ordu>();
        o.orduAdi = ad;
        o.taraf = taraf;
        o.askerSayisi = asker;
        o.moral = moral;
        o.bulunduguSancak = sancak;
        o.GorselKur();
        sancak.OrdulariDiz();
        return o;
    }

    // Ordu işaretini oluşturur: yuvarlak taş + direk + taraf renginde bayrak + asker sayısı
    void GorselKur()
    {
        gorsel = new GameObject("Gorsel").transform;
        gorsel.SetParent(transform, false);

        tas = Parca(PrimitiveType.Cylinder, new Vector3(0f, 0.04f, 0f), new Vector3(0.42f, 0.04f, 0.42f), Color.white);

        // Tabanın üstünde küçük asker figürleri (ordunun büyüklüğüne göre 1-5 tanesi görünür)
        Color uniforma = TarafBilgi.UniformaRengi(taraf);
        Color basRengi = taraf == Taraf.Turk ? new Color(0.22f, 0.17f, 0.12f) : Color.Lerp(uniforma, Color.black, 0.2f);  // Türk: kalpak
        Vector3[] dizilis = { new Vector3(0.02f, 0f, -0.08f), new Vector3(0.11f, 0f, -0.02f), new Vector3(-0.07f, 0f, -0.02f), new Vector3(0.06f, 0f, 0.07f), new Vector3(-0.03f, 0f, 0.08f) };
        figurler = new GameObject[dizilis.Length];
        for (int i = 0; i < dizilis.Length; i++)
        {
            GameObject f = new GameObject("Asker");
            f.transform.SetParent(gorsel, false);
            f.transform.localPosition = dizilis[i] + new Vector3(0f, 0.08f, 0f);
            Transform eskiGorsel = gorsel;
            gorsel = f.transform;                                   // parçaları figürün içine koy
            Parca(PrimitiveType.Capsule,  new Vector3(0f, 0.07f, 0f),   new Vector3(0.055f, 0.07f, 0.045f), uniforma);   // gövde
            Parca(PrimitiveType.Sphere,   new Vector3(0f, 0.16f, 0f),   new Vector3(0.045f, 0.045f, 0.045f), new Color(0.80f, 0.64f, 0.50f)); // yüz
            Parca(PrimitiveType.Cylinder, new Vector3(0f, 0.185f, 0f),  new Vector3(0.045f, 0.018f, 0.045f), basRengi);  // kalpak / şapka
            Renderer tufek = Parca(PrimitiveType.Cube, new Vector3(0.035f, 0.11f, -0.01f), new Vector3(0.01f, 0.16f, 0.01f), new Color(0.25f, 0.18f, 0.1f));
            tufek.transform.localRotation = Quaternion.Euler(0f, 0f, -12f);
            gorsel = eskiGorsel;
            figurler[i] = f;
        }
        Parca(PrimitiveType.Cylinder, new Vector3(-0.13f, 0.45f, 0f), new Vector3(0.03f, 0.42f, 0.03f), new Color(0.3f, 0.22f, 0.14f));
        bayrak = Parca(PrimitiveType.Cube, new Vector3(0.03f, 0.72f, 0f), new Vector3(0.32f, 0.2f, 0.02f), Color.white);

        // Türk bayrağına küçük beyaz ay-yıldız izlenimi veren bir daire
        if (taraf == Taraf.Turk)
        {
            Renderer ay = Parca(PrimitiveType.Cylinder, new Vector3(-0.02f, 0.72f, -0.012f), new Vector3(0.09f, 0.004f, 0.09f), Color.white);
            ay.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        GameObject yazi = new GameObject("AskerSayisi");
        sayiYazisi = yazi.AddComponent<TextMeshPro>();
        yazi.transform.SetParent(gorsel, false);
        yazi.transform.localPosition = new Vector3(0f, 1.02f, 0f);
        yazi.transform.localRotation = Quaternion.Euler(60f, 0f, 0f);   // kameraya dönük
        sayiYazisi.fontSize = 2.6f;
        sayiYazisi.fontStyle = FontStyles.Bold;
        sayiYazisi.alignment = TextAlignmentOptions.Center;
        sayiYazisi.rectTransform.sizeDelta = new Vector2(2f, 0.5f);
        sayiYazisi.outlineWidth = 0.3f;
        sayiYazisi.outlineColor = new Color32(0, 0, 0, 255);

        RengiUygula(false);
        GorunumuGuncelle();
    }

    Renderer Parca(PrimitiveType tur, Vector3 konum, Vector3 boyut, Color renk)
    {
        GameObject p = GameObject.CreatePrimitive(tur);
        Destroy(p.GetComponent<Collider>());
        p.transform.SetParent(gorsel, false);
        p.transform.localPosition = konum;
        p.transform.localScale = boyut;
        Renderer r = p.GetComponent<Renderer>();
        r.material.color = renk;
        return r;
    }

    void RengiUygula(bool secili)
    {
        Color renk = TarafBilgi.OrduRengi(taraf);
        if (secili) renk = Color.Lerp(renk, Color.white, 0.45f);
        if (bayrak != null) bayrak.material.color = renk;
        if (tas != null) tas.material.color = secili ? new Color(1f, 0.9f, 0.4f) : Color.Lerp(renk, Color.black, 0.3f);
    }

    // Asker sayısı değişince yazıyı ve işaretin büyüklüğünü günceller
    void GorunumuGuncelle()
    {
        if (sayiYazisi == null) return;
        sonGosterilenAsker = askerSayisi;
        sonGosterilenHak = HareketHalinde;
        sayiYazisi.text = askerSayisi >= 1000 ? (askerSayisi / 1000f).ToString("0.#") + "k" : askerSayisi.ToString();
        // Yürüyüşteki kendi ordumuzun yazısı sarımsı
        sayiYazisi.color = (OyuncununMu && HareketHalinde) ? new Color(1f, 0.9f, 0.55f) : Color.white;
        // Görünen asker figürü sayısı ordunun büyüklüğünü gösterir (her ~5000 asker bir figür)
        if (figurler != null)
            for (int i = 0; i < figurler.Length; i++)
                figurler[i].SetActive(i < Mathf.Clamp(1 + askerSayisi / 5000, 1, figurler.Length));
        float olcek = Boyut * Mathf.Clamp(0.8f + askerSayisi / 40000f, 0.8f, 1.6f);
        gorsel.localScale = Vector3.one * olcek;
    }

    void LateUpdate()
    {
        if (askerSayisi != sonGosterilenAsker || HareketHalinde != sonGosterilenHak) GorunumuGuncelle();
        RotayiCiz();
    }

    // Kendi ordularımızın gideceği yolu haritada çizer
    void RotayiCiz()
    {
        if (!OyuncununMu) return;
        if (!HareketHalinde)
        {
            if (rotaCizgisi != null) rotaCizgisi.enabled = false;
            return;
        }
        if (rotaCizgisi == null)
        {
            GameObject g = new GameObject("Rota");
            g.transform.SetParent(transform, false);
            rotaCizgisi = g.AddComponent<LineRenderer>();
            rotaCizgisi.material = new Material(Shader.Find("Sprites/Default"));
            rotaCizgisi.startColor = new Color(1f, 0.95f, 0.6f, 0.9f);
            rotaCizgisi.endColor = new Color(1f, 0.6f, 0.2f, 0.9f);
            rotaCizgisi.startWidth = rotaCizgisi.endWidth = 0.08f;
        }
        rotaCizgisi.enabled = true;
        rotaCizgisi.positionCount = yol.Count + 1;
        rotaCizgisi.SetPosition(0, transform.position + Vector3.up * 0.1f);
        for (int i = 0; i < yol.Count; i++)
            rotaCizgisi.SetPosition(i + 1, yol[i].transform.position + Vector3.up * 0.1f);
    }

    // ---------- Emir verme ----------

    // Oyuncunun "şuraya git" emri. Yol bulunamazsa false döner.
    public bool GitEmri(Sancak hedef)
    {
        if (!YurumeBaslat(hedef)) return false;
        emir = HareketHalinde ? Emir.Git : Emir.Bekle;
        return true;
    }

    public void EmirVer(Emir yeniEmir)
    {
        emir = yeniEmir;
        kararSayaci = 0;
        if (yeniEmir == Emir.SinirSavunmasi) savunmaMerkezi = bulunduguSancak;
        if (yeniEmir == Emir.Bekle) YurumeBaslat(bulunduguSancak);   // yürüyüşü iptal et
    }

    // Hedefe giden yolu hesaplar ve yürüyüşe başlar (emri değiştirmez)
    public bool YurumeBaslat(Sancak hedef)
    {
        if (hedef == null || muharebeBekliyor) return false;
        if (hedef == bulunduguSancak)
        {
            yol.Clear();
            ilerleme = 0f;
            bulunduguSancak.OrdulariDiz();
            return true;
        }
        // Yol ortasındaysak önce o parçayı bitiririz
        bool yolOrtasinda = HareketHalinde && ilerleme > 0f;
        Sancak baslangic = yolOrtasinda ? yol[0] : bulunduguSancak;
        List<Sancak> yeniYol = Sancak.YolBul(baslangic, hedef);
        if (yeniYol == null) return false;
        if (yolOrtasinda) yeniYol.Insert(0, yol[0]);
        yol.Clear();
        yol.AddRange(yeniYol);
        if (!yolOrtasinda) ilerleme = 0f;
        sabitGun = 0;
        tahkimat = 0;
        return true;
    }

    // ---------- Gün gün ilerleme ----------

    // Zaman Yöneticisi her gün çağırır. Bir olay olduysa (savaş, fetih) raporunu döndürür.
    public string GunIlerle()
    {
        if (muharebeBekliyor) return null;     // muharebe sonuçlanana kadar bekler
        if (baskinBeklemesi > 0) baskinBeklemesi--;

        if (!HareketHalinde)
        {
            sabitGun++;
            tahkimat = Mathf.Min(MaxTahkimat, sabitGun / 7);   // her hafta bir kademe siper
            return null;
        }

        Sancak sonraki = yol[0];
        float km = Vector3.Distance(bulunduguSancak.transform.position, sonraki.transform.position) / 4f * 111f;
        ilerleme += hizKmGun / Mathf.Max(20f, km);

        if (ilerleme < 1f)
        {
            Vector3 yer = Vector3.Lerp(bulunduguSancak.transform.position, sonraki.transform.position, ilerleme)
                          + new Vector3(0f, 0f, 0.35f);
            transform.position = (Arazi.Yuklu ? Arazi.Uzerinde(yer) : yer) + new Vector3(0f, 0.02f, 0f);
            return null;
        }

        // Varış
        ilerleme = 0f;
        yol.RemoveAt(0);
        string sonuc = null;
        bool dusmanVar = sonraki.sahip != taraf;
        foreach (Ordu o in sonraki.Ordular()) if (o.taraf != taraf) dusmanVar = true;

        // Oyuncunun katıldığı gerçek bir çarpışma: karar (otomatik / bizzat komuta) oyuncuya sorulur
        if (dusmanVar && MuharebeYoneticisi.OyuncuKatilimli(this, sonraki))
        {
            muharebeBekliyor = true;
            MuharebeYoneticisi.Iste(this, sonraki);
            return null;
        }

        if (dusmanVar) sonuc = Savas.Ilerle(this, sonraki);
        else Yerles(sonraki);

        if (!yasiyor || bulunduguSancak != sonraki)
        {
            yol.Clear();                       // yenildi: yürüyüş durur
            if (yasiyor) bulunduguSancak.OrdulariDiz();
        }
        if (!HareketHalinde && emir == Emir.Git) emir = Emir.Bekle;
        return sonuc;
    }

    // Muharebe (otomatik ya da savaş ekranında) sonuçlandıktan sonra çağrılır
    public void MuharebeBitti(Sancak hedef)
    {
        muharebeBekliyor = false;
        if (!yasiyor) return;
        if (bulunduguSancak != hedef)
        {
            yol.Clear();                       // yenildi: yürüyüş durur
            bulunduguSancak.OrdulariDiz();
        }
        if (!HareketHalinde && emir == Emir.Git) emir = Emir.Bekle;
        RotayiCiz();
    }

    // ---------- Otomatik emirler (Hearts of Iron tarzı) ----------

    // Zaman Yöneticisi her gün çağırır; sınır savunması ve taarruz emri alan ordular kendileri karar verir
    public void OtomatikEmirleriUygula()
    {
        if (!yasiyor || HareketHalinde) return;
        if (emir != Emir.SinirSavunmasi && emir != Emir.Taarruz) return;
        if (--kararSayaci > 0) return;
        kararSayaci = 3;   // üç günde bir düşün

        if (emir == Emir.SinirSavunmasi)
        {
            Sancak hedef = EnTehlikeliSinir(out float hedefIhtiyac, out float buradakiIhtiyac);
            if (hedef != null && hedef != bulunduguSancak && hedefIhtiyac > buradakiIhtiyac * 1.2f + 500f)
                YurumeBaslat(hedef);
        }
        else if (emir == Emir.Taarruz)
        {
            if (IkmalMesafesi > Ikmal.GuvenliMesafe) return;   // ikmali uzatmadan ilerle
            Sancak enIyi = null;
            float enIyiOran = 1.3f;                             // ancak açık üstünlük varsa saldır
            foreach (Sancak k in bulunduguSancak.komsular)
            {
                bool dusmanToprak = k.sahip != taraf && Diplomasi.SavastaMi(k.sahip);
                float savunma = 0f;
                bool barisVar = false;
                foreach (Ordu o in k.Ordular())
                {
                    if (o.taraf == taraf) continue;
                    if (!Diplomasi.SavastaMi(o.taraf)) barisVar = true;
                    savunma += o.SavunmaGucu;
                }
                if (barisVar) continue;                          // savaşta olmadığımız kimseye dokunma
                if (!dusmanToprak && savunma == 0f) continue;
                float oran = savunma <= 0f ? 99f : Guc / savunma;
                if (oran > enIyiOran) { enIyiOran = oran; enIyi = k; }
            }
            if (enIyi != null) YurumeBaslat(enIyi);
        }
    }

    // Savunma merkezine 2 adım mesafedeki kendi sancaklarımızdan, düşman tehdidinin savunmayı en çok aştığı yer
    Sancak EnTehlikeliSinir(out float enIyiIhtiyac, out float buradakiIhtiyac)
    {
        enIyiIhtiyac = 0f;
        buradakiIhtiyac = 0f;
        Sancak merkez = savunmaMerkezi != null ? savunmaMerkezi : bulunduguSancak;
        Sancak enIyi = null;

        foreach (Sancak s in Sancak.Cevresi(merkez, 2, taraf))
        {
            float tehdit = 0f;
            foreach (Sancak k in s.komsular)
                foreach (Ordu o in k.Ordular())
                    if (o.taraf != taraf && Diplomasi.SavastaMi(o.taraf)) tehdit += o.Guc;
            if (tehdit <= 0f) continue;

            float savunma = 0f;
            foreach (Ordu o in s.Ordular())
                if (o.taraf == taraf && o != this) savunma += o.SavunmaGucu;

            float ihtiyac = tehdit - savunma;
            if (s == bulunduguSancak) buradakiIhtiyac = ihtiyac;
            if (ihtiyac > enIyiIhtiyac) { enIyiIhtiyac = ihtiyac; enIyi = s; }
        }
        return enIyi;
    }

    public bool HareketEdebilirMi(Sancak hedef, out string neden)
    {
        neden = "";
        if (!OyuncununMu) { neden = "Bu senin ordun değil."; return false; }
        return true;
    }

    // Orduyu başka bir sancağa taşır ve iki sancaktaki dizilişi düzeltir
    public void Yerles(Sancak yeni)
    {
        Sancak eski = bulunduguSancak;
        bulunduguSancak = yeni;
        if (eski != yeni) { tahkimat = 0; sabitGun = 0; }   // yeni yerde siper yeniden kazılmalı
        if (eski != null) eski.OrdulariDiz();
        yeni.OrdulariDiz();
    }

    // Baskından sonra bir hafta dinlenme
    public void HareketHakkiniKullan() { baskinBeklemesi = 7; }

    public void KayipVer(int kayip)
    {
        askerSayisi = Mathf.Max(0, askerSayisi - kayip);
        if (askerSayisi < 200) YokOl();
    }

    public void MoralDegistir(int miktar) { moral = Mathf.Clamp(moral + miktar, 0, 100); }

    public void YokOl()
    {
        yasiyor = false;
        Sancak s = bulunduguSancak;
        gameObject.SetActive(false);
        Destroy(gameObject);
        if (s != null) s.OrdulariDiz();
    }

    // Haftalık: erzak durumuna göre moral
    public void YeniTur(bool erzakYetti)
    {
        MoralDegistir(erzakYetti ? 3 : -8);
    }

    // İkmalden kopuk ordu her tur asker ve moral kaybeder. Kaybedilen asker sayısını döndürür.
    public int IkmalYipranmasi(float carpan = 1f)
    {
        int mesafe = IkmalMesafesi;
        float oran = Ikmal.YipranmaOrani(mesafe) * carpan;
        if (oran <= 0f) return 0;
        int kayip = Mathf.RoundToInt(askerSayisi * oran);
        MoralDegistir(mesafe >= Ikmal.Kusatilmis ? -8 : -3);
        KayipVer(kayip);
        return kayip;
    }

    public void Sec()          { RengiUygula(true); }
    public void SecimiKaldir() { RengiUygula(false); }

    public string BilgiMetni()
    {
        string durum;
        if (!OyuncununMu) durum = HareketHalinde ? "Düşman birliği — yürüyüşte → " + VarisNoktasi.sancakAdi : "Düşman birliği.";
        else
        {
            durum = "Emir: <b>" + EmirAdi() + "</b>";
            if (HareketHalinde)
            {
                int kalanGun = TahminiVarisGunu();
                durum += "   Yürüyüş: → " + VarisNoktasi.sancakAdi + " (~" + kalanGun + " gün)";
            }
            durum += "\n<size=80%>Sağ tık: herhangi bir sancağa yürü (yol otomatik bulunur)</size>";
        }

        // Kendi ordumuzsa komşudaki düşmanların gücünü göster (saldırmadan önce karşılaştır)
        if (OyuncununMu)
        {
            string dusmanlar = "";
            foreach (Sancak k in bulunduguSancak.komsular)
                foreach (Ordu o in k.Ordular())
                    if (o.taraf != taraf)
                        dusmanlar += "\n   • " + o.orduAdi + " (" + k.sancakAdi + ")  savunma gücü ≈ " + Mathf.RoundToInt(o.SavunmaGucu)
                                   + "  <size=80%>ikmal: " + Ikmal.Aciklama(o.IkmalMesafesi) + "</size>";
            if (dusmanlar != "") durum += "\n<color=#ffb080>Komşu düşmanlar:</color>" + dusmanlar;
        }
        return "<b>" + orduAdi + "</b>  (" + bulunduguSancak.sancakAdi + ")  —  " + TarafBilgi.Ad(taraf) + "\n"
             + "Asker: " + askerSayisi + "   Moral: " + moral + "   <b>Güç: " + Mathf.RoundToInt(Guc) + "</b>"
             + "   Savunmada: " + Mathf.RoundToInt(SavunmaGucu) + "\n"
             + "Siper: " + tahkimat + "/" + MaxTahkimat + "   İkmal: " + Ikmal.Aciklama(IkmalMesafesi)
             + "   Gider (2 haftalık): " + ErzakTuketimi + " erzak, " + MaasGideri + " para\n"
             + durum;
    }

    string EmirAdi()
    {
        switch (emir)
        {
            case Emir.Git:            return "Yürüyüş";
            case Emir.SinirSavunmasi: return "Sınır savunması (" + (savunmaMerkezi != null ? savunmaMerkezi.sancakAdi : "?") + " bölgesi)";
            case Emir.Taarruz:        return "Taarruz (üstün olduğu yerlere saldırır)";
            default:                  return "Bekle / siper kaz";
        }
    }

    int TahminiVarisGunu()
    {
        float gun = 0f;
        Sancak onceki = bulunduguSancak;
        for (int i = 0; i < yol.Count; i++)
        {
            float km = Vector3.Distance(onceki.transform.position, yol[i].transform.position) / 4f * 111f;
            float parca = Mathf.Max(20f, km) / hizKmGun;
            gun += (i == 0) ? parca * (1f - ilerleme) : parca;
            onceki = yol[i];
        }
        return Mathf.CeilToInt(gun);
    }
}
