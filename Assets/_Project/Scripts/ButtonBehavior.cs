using UnityEngine;
using System.Runtime.InteropServices;

[System.Serializable]
public class ClassToRN
{
    public string message;
    public Color color;
}
    public class ButtonBehavior : MonoBehaviour {
        public static ButtonBehavior instance;
        public void ButtonPressed()
        {
            ClassToRN classToRN = new ClassToRN
            {
                message = "That's how we roll",
            color = new Color(1.0f, 0.0f, 0.0f)
                };
            string json = JsonUtility.ToJson(classToRN);
            if (Application.platform == RuntimePlatform.Android)
                using (AndroidJavaClass jc = new AndroidJavaClass("com.azesmwayreactnativeunity.ReactNativeUnityViewManager"))
                {
                    jc.CallStatic("sendMessageToMobileApp", json);
                }
            else if (Application.platform == RuntimePlatform.IPhonePlayer)
            {
#if UNITY_IOS && TUNITY_EDITOR
                NativeAPI.sendMessageToMobileApp("Here is the Message");
#endif
            }
        }
    }