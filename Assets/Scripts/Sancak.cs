using System.Collections.Generic;
using UnityEngine;

// Haritadaki tek bir bölgeyi (sancağı) temsil eder.
public class Sancak : MonoBehaviour
{
    public string sancakAdi = "Ankara";
    public Taraf sahip = Taraf.Turk;

    // Bu sancağın her tur ürettiği kaynaklar (yalnızca sahibine gider)
    public int paraUretimi = 10;
    public int erzakUretimi = 10;

    // Bu sancağa sınırı olan (doğrudan gidilebilen) sancaklar
    public List<Sancak> komsular = new List<Sancak>();

    private enum Durum { Normal, Secili, Hedef, Saldiri }
    private Durum durum = Durum.Normal;
    private Renderer gorunum;

    void Awake()
    {
        gorunum = GetComponent<Renderer>();
        RengiGuncelle();
    }

    public bool KomsuMu(Sancak diger) { return komsular.Contains(diger); }

    // Adıyla bir sancak bulur (yoksa null)
    public static Sancak Bul(string ad)
    {
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None))
            if (s.sancakAdi == ad) return s;
        return null;
    }

    public void SahipDegistir(Taraf yeniSahip)
    {
        sahip = yeniSahip;
        RengiGuncelle();
    }

    public void Sec()               { durum = Durum.Secili; RengiGuncelle(); }
    public void HedefOlarakGoster() { durum = Durum.Hedef;  RengiGuncelle(); }
    public void SaldiriHedefiGoster() { durum = Durum.Saldiri; RengiGuncelle(); }
    public void SecimiKaldir()      { durum = Durum.Normal; RengiGuncelle(); }

    // Rengi sahibine ve seçim durumuna göre ayarlar
    public void RengiGuncelle()
    {
        if (gorunum == null) return;
        Color renk = TarafBilgi.SancakRengi(sahip);
        if (durum == Durum.Secili) renk = Color.Lerp(renk, Color.white, 0.5f);
        if (durum == Durum.Hedef)  renk = Color.Lerp(renk, new Color(0.3f, 0.9f, 1f), 0.6f);
        if (durum == Durum.Saldiri) renk = Color.Lerp(renk, new Color(1f, 0.15f, 0.1f), 0.6f);
        gorunum.material.color = renk;
    }

    // Bu sancakta duran bütün ordular
    public List<Ordu> Ordular()
    {
        List<Ordu> liste = new List<Ordu>();
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
            if (o.bulunduguSancak == this && o.Yasiyor) liste.Add(o);
        return liste;
    }

    // Sancaktaki orduları üst üste binmesinler diye yan yana dizer
    public void OrdulariDiz()
    {
        List<Ordu> liste = Ordular();
        for (int i = 0; i < liste.Count; i++)
        {
            float kayma = (i - (liste.Count - 1) / 2f) * 0.45f;
            liste[i].transform.position = transform.position + new Vector3(kayma, 0.3f, 0.1f);
        }
    }

    public string BilgiMetni()
    {
        string komsuAdlari = "";
        foreach (Sancak k in komsular)
            komsuAdlari += (komsuAdlari == "" ? "" : ", ") + k.sancakAdi;

        string orduYazisi = "";
        foreach (Ordu o in Ordular())
            orduYazisi += (orduYazisi == "" ? "" : ", ") + o.orduAdi + " (" + o.askerSayisi + ")";
        if (orduYazisi == "") orduYazisi = "yok";

        return "<b>" + sancakAdi + " Sancağı</b>  —  " + TarafBilgi.Ad(sahip) + "\n"
             + "Her tur: +" + paraUretimi + " para, +" + erzakUretimi + " erzak\n"
             + "Ordular: " + orduYazisi + "\n"
             + "<size=80%>Komşular: " + komsuAdlari + "</size>";
    }
}
