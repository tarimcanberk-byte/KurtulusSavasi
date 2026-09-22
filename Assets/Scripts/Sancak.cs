using System.Collections.Generic;
using UnityEngine;

// Haritadaki tek bir bölgeyi (sancağı) temsil eder.
public class Sancak : MonoBehaviour
{
    public string sancakAdi = "Ankara";

    // Renkler
    public Color normalRenk = new Color(0.3f, 0.6f, 0.3f);   // yeşil
    public Color seciliRenk = new Color(0.9f, 0.8f, 0.2f);   // sarı
    public Color hedefRenk  = new Color(0.4f, 0.7f, 0.9f);   // açık mavi: ordunun gidebileceği yer

    // Bu sancağın her tur ürettiği kaynaklar
    public int paraUretimi = 10;
    public int erzakUretimi = 10;

    // Bu sancağa sınırı olan (doğrudan gidilebilen) sancaklar
    public List<Sancak> komsular = new List<Sancak>();

    private Renderer gorunum;

    // Awake, Start'tan da önce çalışır
    void Awake()
    {
        gorunum = GetComponent<Renderer>();
        gorunum.material.color = normalRenk;

        // Komşuluk iki yönlüdür: Ankara Eskişehir'e komşuysa Eskişehir de Ankara'ya komşudur
        foreach (Sancak k in komsular)
        {
            if (k != null && !k.komsular.Contains(this))
                k.komsular.Add(this);
        }
    }

    public bool KomsuMu(Sancak diger)
    {
        return komsular.Contains(diger);
    }

    public void Sec()              { gorunum.material.color = seciliRenk; }
    public void HedefOlarakGoster() { gorunum.material.color = hedefRenk; }
    public void SecimiKaldir()     { gorunum.material.color = normalRenk; }

    // Bilgi panelinde gösterilecek yazı
    public string BilgiMetni()
    {
        string komsuAdlari = "";
        foreach (Sancak k in komsular)
            komsuAdlari += (komsuAdlari == "" ? "" : ", ") + k.sancakAdi;

        return "<b>" + sancakAdi + " Sancağı</b>\n"
             + "Her tur: +" + paraUretimi + " para, +" + erzakUretimi + " erzak\n"
             + "Komşular: " + komsuAdlari;
    }
}
