using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// Fareyle tıklanan sancağı ya da orduyu seçer, orduları hareket ettirir
// ve seçilen şeyin bilgisini ekranın sol altında gösterir.
public class HaritaSecici : MonoBehaviour
{
    public TMP_Text bilgiYazisi;

    private Sancak seciliSancak;
    private Ordu seciliOrdu;

    void Start()
    {
        BilgiGoster("Bir sancağa ya da orduya (kırmızı kare) tıkla.");
    }

    void Update()
    {
        if (Mouse.current == null || !Mouse.current.leftButton.wasPressedThisFrame)
            return;

        // Fare bir arayüz düğmesinin üzerindeyse haritaya tıklanmış sayma
        if (UnityEngine.EventSystems.EventSystem.current != null &&
            UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())
            return;

        Ray isin = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
        if (!Physics.Raycast(isin, out RaycastHit carpma))
        {
            SecimleriTemizle();
            BilgiGoster("Bir sancağa ya da orduya (kırmızı kare) tıkla.");
            return;
        }

        Ordu ordu = carpma.collider.GetComponent<Ordu>();
        Sancak sancak = carpma.collider.GetComponent<Sancak>();

        if (ordu != null)
            OrduyaTiklandi(ordu);
        else if (sancak != null)
            SancagaTiklandi(sancak);
    }

    void OrduyaTiklandi(Ordu ordu)
    {
        SecimleriTemizle();
        seciliOrdu = ordu;
        ordu.Sec();

        // Gidebileceği komşu sancakları maviye boya
        if (ordu.HareketHakkiVar)
            foreach (Sancak k in ordu.bulunduguSancak.komsular)
                k.HedefOlarakGoster();

        BilgiGoster(ordu.BilgiMetni());
    }

    void SancagaTiklandi(Sancak sancak)
    {
        // Bir ordu seçiliyse: sancak, hareket hedefidir
        if (seciliOrdu != null)
        {
            if (seciliOrdu.HareketEdebilirMi(sancak, out string neden))
            {
                Ordu ordu = seciliOrdu;
                ordu.HareketEt(sancak);
                SecimleriTemizle();
                BilgiGoster(ordu.orduAdi + " yürüdü → " + sancak.sancakAdi + "\n" + ordu.BilgiMetni());
            }
            else
            {
                BilgiGoster("<color=#ff8080>" + neden + "</color>\n" + seciliOrdu.BilgiMetni());
            }
            return;
        }

        // Ordu seçili değilse: sancağı seç ve bilgisini göster
        SecimleriTemizle();
        seciliSancak = sancak;
        sancak.Sec();
        BilgiGoster(sancak.BilgiMetni());
    }

    // Bütün seçimleri ve renk vurgularını kaldırır
    public void SecimleriTemizle()
    {
        foreach (Sancak s in FindObjectsByType<Sancak>(FindObjectsSortMode.None))
            s.SecimiKaldir();
        if (seciliOrdu != null) seciliOrdu.SecimiKaldir();
        seciliOrdu = null;
        seciliSancak = null;
    }

    void BilgiGoster(string metin)
    {
        if (bilgiYazisi != null) bilgiYazisi.text = metin;
        else Debug.Log(metin);
    }
}
