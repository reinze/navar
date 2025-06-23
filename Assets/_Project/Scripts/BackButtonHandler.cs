using UnityEngine;

public class BackButtonHandler : MonoBehaviour
{
    // Update dipanggil setiap frame
    void Update()
    {
        // Input.GetKeyDown(KeyCode.Escape) akan mendeteksi tombol "kembali" di perangkat Android.
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            // Mencegah perilaku default apa pun (meskipun sudah tidak ada quit by default)
            // dan memastikan kita yang mengontrolnya.

            // Kita akan memanggil metode statis `sendMessageToMobileApp` yang ada di
            // kelas Java `ReactNativeUnityViewManager`. Ini adalah jembatan komunikasi
            // yang sudah disediakan oleh library.
#if UNITY_ANDROID
            using (AndroidJavaClass jc = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
            {
                // Kirim pesan "GO_BACK". Ini adalah string custom yang akan kita
                // kenali di sisi React Native untuk memicu navigasi.
                jc.CallStatic("sendMessageToMobileApp", "GO_BACK");
            }
#endif

            // Anda juga bisa menambahkan untuk iOS jika perlu
#if UNITY_IOS && !UNITY_EDITOR
            // NativeAPI.sendMessageToMobileApp("GO_BACK");
#endif
        }
    }
}