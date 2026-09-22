using UnityEngine;

// Haritadaki tek bir bölgeyi (sancağı) temsil eder.
// Bu scripti haritadaki her sancak nesnesine ekleyeceğiz.
public class Sancak : MonoBehaviour
{
    // Inspector'da görünen ve değiştirilebilen alanlar
    public string sancakAdi = "Ankara";
    public Color normalRenk = new Color(0.3f, 0.6f, 0.3f);   // yeşil
    public Color seciliRenk = new Color(0.9f, 0.8f, 0.2f);   // sarı

    // Bu sancağın her tur ürettiği kaynaklar
    public int paraUretimi = 10;
    public int erzakUretimi = 10;

    private Renderer gorunum;

    // Oyun başladığında bir kez çalışır
    void Start()
    {
        gorunum = GetComponent<Renderer>();
        gorunum.material.color = normalRenk;
    }

    // Sancak seçildiğinde çağrılır
    public void Sec()
    {
        gorunum.material.color = seciliRenk;
        Debug.Log(sancakAdi + " seçildi  |  Her tur: +" + paraUretimi + " para, +" + erzakUretimi + " erzak");
    }

    // Başka bir sancak seçilince bu sancağın seçimi kaldırılır
    public void SecimiKaldir()
    {
        gorunum.material.color = normalRenk;
    }
}
