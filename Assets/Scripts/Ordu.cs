using UnityEngine;

// Haritada hareket eden bir ordu birimi.
public class Ordu : MonoBehaviour
{
    public string orduAdi = "20. Kolordu";
    public Sancak bulunduguSancak;     // Oyun başladığında ordunun durduğu sancak
    public int askerSayisi = 5000;
    public int moral = 100;            // 0 - 100 arası
    public int erzakTuketimi = 20;     // Her tur hazineden yediği erzak

    public Color normalRenk = new Color(0.7f, 0.1f, 0.1f);   // koyu kırmızı
    public Color seciliRenk = new Color(1f, 0.45f, 0.45f);   // açık kırmızı

    private bool hareketHakki = true;  // Bu tur hâlâ hareket edebilir mi?
    private Renderer gorunum;

    public bool HareketHakkiVar { get { return hareketHakki; } }

    void Awake()
    {
        gorunum = GetComponent<Renderer>();
        gorunum.material.color = normalRenk;
    }

    void Start()
    {
        if (bulunduguSancak != null)
            SancagaYerles(bulunduguSancak);
    }

    // Ordu hedef sancağa gidebilir mi? Gidemiyorsa nedenini "neden" içine yazar.
    public bool HareketEdebilirMi(Sancak hedef, out string neden)
    {
        neden = "";
        if (!hareketHakki)                    { neden = orduAdi + " bu tur zaten hareket etti."; return false; }
        if (hedef == bulunduguSancak)         { neden = orduAdi + " zaten bu sancakta."; return false; }
        if (!bulunduguSancak.KomsuMu(hedef))  { neden = hedef.sancakAdi + " ile " + bulunduguSancak.sancakAdi + " komşu değil."; return false; }
        return true;
    }

    public void HareketEt(Sancak hedef)
    {
        SancagaYerles(hedef);
        hareketHakki = false;
        Debug.Log(orduAdi + " yürüdü → " + hedef.sancakAdi);
    }

    // Orduyu sancağın tam üstüne koyar
    private void SancagaYerles(Sancak s)
    {
        bulunduguSancak = s;
        transform.position = s.transform.position + Vector3.up * 0.5f;
    }

    // Yeni tur başladığında Tur Yöneticisi bunu çağırır
    public void YeniTur(bool erzakYetti)
    {
        hareketHakki = true;
        if (erzakYetti)
            moral = Mathf.Min(100, moral + 5);    // karnı tok: moral toparlanır
        else
            moral = Mathf.Max(0, moral - 15);     // aç kaldı: moral düşer
    }

    public void Sec()          { gorunum.material.color = seciliRenk; }
    public void SecimiKaldir() { gorunum.material.color = normalRenk; }

    public string BilgiMetni()
    {
        return "<b>" + orduAdi + "</b>  (" + bulunduguSancak.sancakAdi + ")\n"
             + "Asker: " + askerSayisi + "   Moral: " + moral + "   Erzak gideri: " + erzakTuketimi + "/tur\n"
             + (hareketHakki ? "Gitmek istediğin mavi sancağa tıkla." : "Bu tur hareket etti.");
    }
}
