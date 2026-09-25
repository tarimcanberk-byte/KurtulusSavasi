using UnityEngine;
using UnityEngine.InputSystem;

// Harita kamerası: WASD / ok tuşlarıyla kaydır, fare tekerleğiyle yakınlaş.
public class KameraKontrol : MonoBehaviour
{
    public float egim = 60f;              // Kameranın yere bakış açısı
    public float kaydirmaHizi = 12f;
    public float zoomAdimi = 2.5f;
    public float minYukseklik = 6f;
    public float maxYukseklik = 45f;

    void Awake()
    {
        transform.rotation = Quaternion.Euler(egim, 0f, 0f);
    }

    void Update()
    {
        Keyboard k = Keyboard.current;
        Mouse m = Mouse.current;
        Vector3 p = transform.position;
        float tanE = Mathf.Tan(egim * Mathf.Deg2Rad);

        // Kaydırma (yükseldikçe daha hızlı)
        if (k != null)
        {
            Vector3 yon = Vector3.zero;
            if (k.wKey.isPressed || k.upArrowKey.isPressed)    yon.z += 1;
            if (k.sKey.isPressed || k.downArrowKey.isPressed)  yon.z -= 1;
            if (k.dKey.isPressed || k.rightArrowKey.isPressed) yon.x += 1;
            if (k.aKey.isPressed || k.leftArrowKey.isPressed)  yon.x -= 1;
            p += yon.normalized * kaydirmaHizi * (p.y / 20f) * Time.deltaTime;
        }

        // Yakınlaşma: bakılan nokta sabit kalsın diye z de ayarlanır
        if (m != null)
        {
            float teker = m.scroll.ReadValue().y;
            if (Mathf.Abs(teker) > 0.01f)
            {
                float odakZ = p.z + p.y / tanE;
                float yeniY = Mathf.Clamp(p.y - Mathf.Sign(teker) * zoomAdimi, minYukseklik, maxYukseklik);
                p.y = yeniY;
                p.z = odakZ - yeniY / tanE;
            }
        }
        transform.position = p;
    }

    // Kamerayı bir noktaya baktırır
    public void Odakla(Vector3 nokta, float yukseklik)
    {
        float tanE = Mathf.Tan(egim * Mathf.Deg2Rad);
        transform.position = new Vector3(nokta.x, yukseklik, nokta.z - yukseklik / tanE);
    }
}
