using UnityEngine;
using UnityEngine.InputSystem;

// Savaş ekranı kamerası: WASD kaydır, Q/E döndür, tekerlek yakınlaş. Oyun duraklatılsa da çalışır.
public class MuharebeKamera : MonoBehaviour
{
    public float egim = 52f;
    public float yon = 0f;          // Y ekseninde dönüş (derece)
    public float yukseklik = 28f;
    public Vector3 odak;            // kameranın baktığı yer (zemin)

    public void Ayarla(Vector3 bakilan, float donus, float yuk)
    {
        odak = bakilan; yon = donus; yukseklik = yuk;
        Uygula();
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime;
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        if (k != null)
        {
            Vector3 ileri = Quaternion.Euler(0f, yon, 0f) * Vector3.forward;
            Vector3 sag = Quaternion.Euler(0f, yon, 0f) * Vector3.right;
            Vector3 d = Vector3.zero;
            if (k.wKey.isPressed || k.upArrowKey.isPressed) d += ileri;
            if (k.sKey.isPressed || k.downArrowKey.isPressed) d -= ileri;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) d += sag;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed) d -= sag;
            odak += d.normalized * (8f + yukseklik * 0.9f) * dt;
            if (k.qKey.isPressed) yon += 70f * dt;
            if (k.eKey.isPressed) yon -= 70f * dt;
        }
        if (m != null)
        {
            float teker = m.scroll.ReadValue().y;
            if (Mathf.Abs(teker) > 0.01f) yukseklik = Mathf.Clamp(yukseklik - Mathf.Sign(teker) * 3f, 7f, 70f);
            // Orta tuşla sürükleyerek döndür
            if (m.middleButton.isPressed) yon += m.delta.ReadValue().x * 0.25f;
        }
        Vector3 yerel = odak - MuharebeAlani.Merkez;
        float s = MuharebeAlani.Yari + 5f;
        odak = MuharebeAlani.Merkez + new Vector3(Mathf.Clamp(yerel.x, -s, s), 0f, Mathf.Clamp(yerel.z, -s, s));
        Uygula();
    }

    void Uygula()
    {
        float zemin = MuharebeAlani.Yukseklik(odak.x, odak.z);
        float egimAyarli = Mathf.Lerp(35f, egim + 10f, Mathf.InverseLerp(7f, 70f, yukseklik));   // yakında daha yatay
        Quaternion donus = Quaternion.Euler(egimAyarli, yon, 0f);
        float uzaklik = yukseklik / Mathf.Sin(egimAyarli * Mathf.Deg2Rad);
        Vector3 p = new Vector3(odak.x, zemin, odak.z) - donus * Vector3.forward * uzaklik;
        // Kamera tepelerin içine girmesin
        p.y = Mathf.Max(p.y, MuharebeAlani.Yukseklik(p.x, p.z) + 2f);
        transform.position = p;
        transform.rotation = donus;
    }
}
