using UnityEngine;

// Haritada hareket eden bir ordu birimi.
public class Ordu : MonoBehaviour
{
    public string orduAdi = "20. Kolordu";
    public Taraf taraf = Taraf.Turk;
    public Sancak bulunduguSancak;
    public int askerSayisi = 5000;
    public int moral = 100;            // 0 - 100 arası

    private bool hareketHakki = true;
    private bool yasiyor = true;
    private Renderer gorunum;

    public bool HareketHakkiVar { get { return hareketHakki; } }
    public bool OyuncununMu    { get { return taraf == Taraf.Turk; } }
    public bool Yasiyor        { get { return yasiyor; } }

    // Her 100 asker her tur 1 erzak yer, her 200 asker 1 para maaş alır
    public int ErzakTuketimi   { get { return Mathf.CeilToInt(askerSayisi / 100f); } }
    public int MaasGideri      { get { return Mathf.CeilToInt(askerSayisi / 200f); } }

    // Savaş gücü: asker sayısı ve moralin birleşimi
    public float Guc           { get { return askerSayisi * (0.5f + moral / 200f); } }

    void Awake()
    {
        gorunum = GetComponent<Renderer>();
    }

    // Yeni bir ordu oluşturur (harita kurulurken, birlik toplanırken, düşman takviyesinde)
    public static Ordu Olustur(string ad, Taraf taraf, int asker, Sancak sancak, int moral = 100)
    {
        GameObject kup = GameObject.CreatePrimitive(PrimitiveType.Cube);
        kup.name = ad;
        kup.transform.localScale = Vector3.one * 0.35f;
        Ordu o = kup.AddComponent<Ordu>();
        o.orduAdi = ad;
        o.taraf = taraf;
        o.askerSayisi = asker;
        o.moral = moral;
        o.bulunduguSancak = sancak;
        o.gorunum.material.color = TarafBilgi.OrduRengi(taraf);
        sancak.OrdulariDiz();
        return o;
    }

    void Start()
    {
        gorunum.material.color = TarafBilgi.OrduRengi(taraf);
        if (bulunduguSancak != null) bulunduguSancak.OrdulariDiz();
    }

    public bool HareketEdebilirMi(Sancak hedef, out string neden)
    {
        neden = "";
        if (!OyuncununMu)                    { neden = "Bu senin ordun değil."; return false; }
        if (!hareketHakki)                   { neden = orduAdi + " bu tur zaten hareket etti."; return false; }
        if (hedef == bulunduguSancak)        { neden = orduAdi + " zaten bu sancakta."; return false; }
        if (!bulunduguSancak.KomsuMu(hedef)) { neden = hedef.sancakAdi + " ile " + bulunduguSancak.sancakAdi + " komşu değil."; return false; }
        return true;
    }

    // Orduyu başka bir sancağa taşır ve iki sancaktaki dizilişi düzeltir
    public void Yerles(Sancak yeni)
    {
        Sancak eski = bulunduguSancak;
        bulunduguSancak = yeni;
        if (eski != null) eski.OrdulariDiz();
        yeni.OrdulariDiz();
    }

    public void HareketHakkiniKullan() { hareketHakki = false; }

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

    public void YeniTur(bool erzakYetti)
    {
        hareketHakki = true;
        MoralDegistir(erzakYetti ? 5 : -15);
    }

    public void Sec()          { gorunum.material.color = Color.Lerp(TarafBilgi.OrduRengi(taraf), Color.white, 0.45f); }
    public void SecimiKaldir() { gorunum.material.color = TarafBilgi.OrduRengi(taraf); }

    public string BilgiMetni()
    {
        string durum = !OyuncununMu ? "Düşman birliği."
                     : hareketHakki ? "Sağ tık: mavi sancağa yürü, kırmızı sancağa saldır."
                     : "Bu tur hareket etti.";

        // Kendi ordumuzsa komşudaki düşmanların gücünü göster (saldırmadan önce karşılaştır)
        if (OyuncununMu)
        {
            string dusmanlar = "";
            foreach (Sancak k in bulunduguSancak.komsular)
                foreach (Ordu o in k.Ordular())
                    if (o.taraf != taraf)
                        dusmanlar += "\n   • " + o.orduAdi + " (" + k.sancakAdi + ")  Güç: " + Mathf.RoundToInt(o.Guc)
                                   + "  <size=80%>savunmada ≈ " + Mathf.RoundToInt(o.Guc * 1.2f) + "</size>";
            if (dusmanlar != "") durum += "\n<color=#ffb080>Komşu düşmanlar:</color>" + dusmanlar;
        }
        return "<b>" + orduAdi + "</b>  (" + bulunduguSancak.sancakAdi + ")  —  " + TarafBilgi.Ad(taraf) + "\n"
             + "Asker: " + askerSayisi + "   Moral: " + moral + "   <b>Güç: " + Mathf.RoundToInt(Guc) + "</b>\nTur başına gider: " + ErzakTuketimi + " erzak, " + MaasGideri + " para\n"
             + durum;
    }
}
