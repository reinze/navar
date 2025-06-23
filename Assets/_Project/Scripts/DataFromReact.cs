using UnityEngine;
using TMPro;
using ARLocation.MapboxRoutes;
using ARLocation;
using System.Runtime.InteropServices; // Namespace ini sudah benar

public class NativeAPI
{
#if UNITY_IOS && !UNITY_EDITOR
    // Ganti D11Import menjadi DllImport
    [DllImport("__Internal")] 
    public static extern void sendMessageToMobileApp(string message);
#endif
}

public class DataFromReact : MonoBehaviour
{
    // Kelas ini merepresentasikan objek "data" yang ada di dalam JSON
    [System.Serializable]
    public class LocationData
    {
        public double latitude; // Gunakan double untuk presisi koordinat
        public double longitude;
    }

    // Kelas ini merepresentasikan struktur JSON utama
    [System.Serializable]
    public class PointOfInterestData
    {
        public string title;
        public LocationData data;
    }
    public TMP_Text debugReact;
    public MenuController menuController;
    //void Start()
    //{
    //    var dest = new Location
    //    {
    //        Latitude = -7.03838313226618,
    //        Longitude = 110.474305768093,
    //        Altitude = 0
    //    };
    //    menuController.GetFromReact(dest, "Testing");
    //}
    public void PanggilReact()
    {
        string t = "{\"title\":\"Monas\",\"data\":{\"latitude\":3.14530575477272,\"longitude\":101.684640125385}}";
        GetDataFromReact(t);
    }
    // Metode ini akan dipanggil dari React Native
    public void GetDataFromReact(string json)
    {
        print(json);
        //debugReact.text = validJson;
        // Lakukan parsing dari string JSON ke object C#
        PointOfInterestData poiData = JsonUtility.FromJson<PointOfInterestData>(json);

        // Sekarang data sudah menjadi variabel di dalam object 'poiData'
        // Kita bisa menggunakannya dengan mudah

        // 1. Variabel 'title'
        string a_title = poiData.title;

        // 2. Variabel 'latitude'
        double a_latitude = poiData.data.latitude;

        // 3. Variabel 'longitude'
        double a_longitude = poiData.data.longitude;

        // Cetak variabel-variabel tersebut ke console Unity untuk bukti
        Debug.Log("--- Hasil Konversi ---");
        Debug.Log("Judul: " + a_title);
        Debug.Log("Latitude: " + a_latitude);
        Debug.Log("Longitude: " + a_longitude);
        Debug.Log("----------------------");
         var dest = new Location
                {
                    Latitude = a_latitude,
                    Longitude = a_longitude,
                    Altitude = 0
                };
        menuController.GetFromReact(dest, a_title);
    }
    public void ButtonPressed()
    {
        if (Application.platform == RuntimePlatform.Android)
        {
            using (AndroidJavaClass jc = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
            {
                jc.CallStatic("sendMessageToMobileApp", "Here is the Message");
            }
        }
        else if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
#if UNITY_IOS && !UNITY_EDITOR
                        NativeAPI.sendMessageToMobileApp("Here is the Message");
#endif
        }
    }
}