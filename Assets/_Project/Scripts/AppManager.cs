using UnityEngine;

public class AppManager : MonoBehaviour
{
    void Start()
    {
        // Perintahkan Unity agar TIDAK meminimalkan aplikasi
        // saat tombol kembali (Escape) ditekan.
        Input.backButtonLeavesApp = false;
    }
}