using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System.Text;
using ARLocation;
public class DatabaseScript : MonoBehaviour
{
    public static DatabaseScript Instance;  // Static Instance

    [Header("API Configuration")]
    public string baseUrl = "https://uhg-be.webtrx3.my.id/";
    public string loginEndpoint = "/api/v1/auth/login/";
    public string arSlugEndpoint = "/api/v1/augmented-reality/";
    public string credential = "yourUsername";
    public string password = "yourPassword";

    private string access_token;

    // Menyimpan data AR
    public static List<ARData> arDataList = new List<ARData>();

    void Awake()
    {
        // Memastikan hanya ada satu instance dari DatabaseScript
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
        DontDestroyOnLoad(gameObject);  // Agar tetap ada saat scene berganti
    }

    void Start()
    {
        StartCoroutine(LoginAndFetchData());
    }

    IEnumerator LoginAndFetchData()
    {
        // === 1. LOGIN ===
        string loginUrl = baseUrl + loginEndpoint;
        string loginJson = JsonUtility.ToJson(new LoginData(credential, password));
        byte[] bodyRaw = Encoding.UTF8.GetBytes(loginJson);

        UnityWebRequest loginRequest = new UnityWebRequest(loginUrl, "POST");
        loginRequest.uploadHandler = new UploadHandlerRaw(bodyRaw);
        loginRequest.downloadHandler = new DownloadHandlerBuffer();
        loginRequest.SetRequestHeader("Content-Type", "application/json");

        yield return loginRequest.SendWebRequest();

        if (loginRequest.result == UnityWebRequest.Result.Success)
        {
            TokenResponse tokenResponse = JsonUtility.FromJson<TokenResponse>(loginRequest.downloadHandler.text);
            access_token = tokenResponse.data.access_token;

            // === 2. FETCH AR LIST ===
            string arUrl = baseUrl + arSlugEndpoint;
            UnityWebRequest getRequest = UnityWebRequest.Get(arUrl);
            getRequest.SetRequestHeader("Authorization", "Bearer " + access_token);

            yield return getRequest.SendWebRequest();

            if (getRequest.result == UnityWebRequest.Result.Success)
            {
                ARResponse arResponse = JsonUtility.FromJson<ARResponse>(getRequest.downloadHandler.text);

                if (arResponse != null && arResponse.status && arResponse.data != null && arResponse.data.data != null)
                {
                    arDataList = arResponse.data.data;  // Menyimpan data AR yang didapat
                    Debug.Log("AR Data berhasil diambil");
                    WebMapLoader.instance.LoadFromDatabaseScript();
                }
                else
                {
                    Debug.LogError("Gagal parsing data atau status false.");
                }
            }
            else
            {
                Debug.LogError("Gagal fetch AR data: " + getRequest.error);
            }
        }
        else
        {
            Debug.LogError("Login failed: " + loginRequest.error);
        }
    }

    // ======== DATA CLASSES ========

    [System.Serializable]
    public class LoginData
    {
        public string credential;
        public string password;

        public LoginData(string u, string p)
        {
            credential = u;
            password = p;
        }
    }

    [System.Serializable]
    public class TokenResponse
    {
        public bool status;
        public string message;
        public TokenData data;
        public string errors;
    }

    [System.Serializable]
    public class TokenData
    {
        public string access_token;
        public bool is_verified;
    }

    [System.Serializable]
    public class ARResponse
    {
        public bool status;
        public string message;
        public ARPaginatedData data;
        public string errors;
    }

    [System.Serializable]
    public class ARPaginatedData
    {
        public List<ARData> data;
        public int current_page;
        public int first_item;
        public int last_item;
        public int per_page;
        public int last_page;
        public int total;
    }

    [System.Serializable]
    public class ARData
    {
        public int id;
        public string slug;
        public string featured_image;
        public string name;
        public string description;
        public string latitude;
        public string longitude;
        public string altitude;
        public ARCountry country;
        public ARCity city;
        public string address;
        public string url;
        public ARCategory categories;
    }

    [System.Serializable]
    public class ARCountry
    {
        public int id;
        public string name;
    }

    [System.Serializable]
    public class ARCity
    {
        public int id;
        public string name;
    }

    [System.Serializable]
    public class ARCategory
    {
        public int id;
        public string name;
    }
}
