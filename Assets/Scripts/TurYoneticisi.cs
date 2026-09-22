using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Oyunun zamanını yönetir: her "Turu Bitir" tıklamasında tarih 2 hafta ilerler.
// Bu scripti sahnede tek bir nesneye ekleyeceğiz (adı "TurYoneticisi" olacak).
public class TurYoneticisi : MonoBehaviour
{
    // Inspector'dan bağlanacak arayüz parçaları
    public TMP_Text tarihYazisi;       // Ekranda tarihi gösteren yazı
    public Button turuBitirDugmesi;    // "Turu Bitir" düğmesi

    // Bir tur kaç gün sürer (tasarım dokümanındaki karar: 2 hafta)
    public int turBasinaGun = 14;

    // Oyunun o anki durumu
    private DateTime tarih = new DateTime(1919, 5, 19);   // 19 Mayıs 1919
    private int turSayisi = 1;

    // Ankara hükümetinin elindeki kaynaklar (başlangıç değerleri)
    public int para = 100;
    public int erzak = 100;

    // Türkçe ay adları (bilgisayarın dil ayarından bağımsız olsun diye elle yazdık)
    private static readonly string[] aylar =
    {
        "Ocak", "Şubat", "Mart", "Nisan", "Mayıs", "Haziran",
        "Temmuz", "Ağustos", "Eylül", "Ekim", "Kasım", "Aralık"
    };

    void Start()
    {
        // Düğmeye tıklanınca TuruBitir fonksiyonu çalışsın
        turuBitirDugmesi.onClick.AddListener(TuruBitir);
        EkraniGuncelle();
    }

    // Bir turu bitirir ve zamanı ilerletir
    public void TuruBitir()
    {
        tarih = tarih.AddDays(turBasinaGun);
        turSayisi++;
        KaynaklariTopla();
        OrdulariBesle();

        // Tur bitince haritadaki seçimleri temizle
        HaritaSecici secici = FindAnyObjectByType<HaritaSecici>();
        if (secici != null) secici.SecimleriTemizle();
        EkraniGuncelle();
        Debug.Log("Yeni tur başladı: " + turSayisi);

        // İleride burada: ordu hareketleri, isyan kontrolü, haberler...
    }

    // Haritadaki bütün sancakların ürettiği kaynakları hazineye ekler
    private void KaynaklariTopla()
    {
        // Sahnedeki bütün Sancak scriptlerini bul
        Sancak[] sancaklar = FindObjectsByType<Sancak>(FindObjectsSortMode.None);

        foreach (Sancak s in sancaklar)
        {
            para += s.paraUretimi;
            erzak += s.erzakUretimi;
        }
    }

    // Her ordu hazineden erzak yer. Erzak yetmezse ordu aç kalır ve morali düşer.
    private void OrdulariBesle()
    {
        foreach (Ordu o in FindObjectsByType<Ordu>(FindObjectsSortMode.None))
        {
            bool yetti = erzak >= o.erzakTuketimi;
            if (yetti)
                erzak -= o.erzakTuketimi;
            else
                Debug.LogWarning(o.orduAdi + " aç kaldı! Moral düşüyor.");

            o.YeniTur(yetti);
        }
    }

    // Ekrandaki tarih yazısını günceller
    private void EkraniGuncelle()
    {
        tarihYazisi.text = tarih.Day + " " + aylar[tarih.Month - 1] + " " + tarih.Year
                           + "   |   Tur " + turSayisi
                           + "   |   Para: " + para
                           + "   |   Erzak: " + erzak;
    }
}
