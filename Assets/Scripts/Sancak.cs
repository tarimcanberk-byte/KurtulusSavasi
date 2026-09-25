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

    public bool saydam = false;          // arazinin üstüne serilen yarı saydam katman mı
    public bool kiyiMi = false;          // denize kıyısı var mı (yabancı ordular için ikmal kaynağı)
    public float araziBonusu = 1f;       // savunmaya çarpan

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

    // İki sancak arasındaki en kısa yol (adım sayısı olarak). Başlangıç dahil değil, hedef dahil.
    public static List<Sancak> YolBul(Sancak baslangic, Sancak hedef)
    {
        if (baslangic == null || hedef == null) return null;
        Dictionary<Sancak, Sancak> nereden = new Dictionary<Sancak, Sancak> { { baslangic, null } };
        Queue<Sancak> kuyruk = new Queue<Sancak>();
        kuyruk.Enqueue(baslangic);
        while (kuyruk.Count > 0)
        {
            Sancak s = kuyruk.Dequeue();
            if (s == hedef) break;
            foreach (Sancak k in s.komsular)
                if (!nereden.ContainsKey(k)) { nereden[k] = s; kuyruk.Enqueue(k); }
        }
        if (!nereden.ContainsKey(hedef)) return null;
        List<Sancak> yol = new List<Sancak>();
        for (Sancak s = hedef; s != baslangic; s = nereden[s]) yol.Add(s);
        yol.Reverse();
        return yol;
    }

    // Bir sancağın çevresindeki (verilen adım sayısı kadar) belli bir tarafa ait sancaklar, kendisi dahil
    public static List<Sancak> Cevresi(Sancak merkez, int adim, Taraf sahibi)
    {
        List<Sancak> sonuc = new List<Sancak>();
        Dictionary<Sancak, int> uzaklik = new Dictionary<Sancak, int> { { merkez, 0 } };
        Queue<Sancak> kuyruk = new Queue<Sancak>();
        kuyruk.Enqueue(merkez);
        while (kuyruk.Count > 0)
        {
            Sancak s = kuyruk.Dequeue();
            if (s.sahip == sahibi) sonuc.Add(s);
            if (uzaklik[s] >= adim) continue;
            foreach (Sancak k in s.komsular)
                if (!uzaklik.ContainsKey(k) && k.sahip == sahibi) { uzaklik[k] = uzaklik[s] + 1; kuyruk.Enqueue(k); }
        }
        return sonuc;
    }

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
        if (durum == Durum.Secili) renk = Color.Lerp(renk, new Color(1f, 0.95f, 0.6f), 0.55f);
        if (durum == Durum.Hedef)  renk = Color.Lerp(renk, new Color(0.3f, 0.9f, 1f), 0.6f);
        if (durum == Durum.Saldiri) renk = Color.Lerp(renk, new Color(1f, 0.15f, 0.1f), 0.6f);
        if (saydam) renk.a = durum == Durum.Normal ? 0.24f : 0.5f;
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
        List<Ordu> liste = new List<Ordu>();
        foreach (Ordu o in Ordular()) if (!o.HareketHalinde) liste.Add(o);   // yürüyenler kendi yerini bilir
        for (int i = 0; i < liste.Count; i++)
        {
            float kayma = (i - (liste.Count - 1) / 2f) * 0.55f * Ordu.Boyut;
            Vector3 yer = transform.position + new Vector3(kayma, 0f, 0.35f);
            liste[i].transform.position = (Arazi.Yuklu ? Arazi.Uzerinde(yer) : yer) + new Vector3(0f, 0.02f, 0f);
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
             + "Her tur: +" + paraUretimi + " para, +" + erzakUretimi + " erzak"
             + (araziBonusu > 1f ? "   Arazi: savunma +%" + Mathf.RoundToInt((araziBonusu - 1f) * 100) : "")
             + (kiyiMi ? "   (kıyı)" : "") + "\n"
             + "Ordular: " + orduYazisi + "\n"
             + "<size=80%>Komşular: " + komsuAdlari + "</size>";
    }
}
