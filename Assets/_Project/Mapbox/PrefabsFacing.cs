using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PrefabsFacing : MonoBehaviour
{

    void Update()
    {
        // Ambil posisi kamera
        Vector3 cameraPosition = Camera.main.transform.position;

        // Hitung arah ke kamera di bidang horizontal (abaikan Y)
        Vector3 direction = cameraPosition - transform.position;
        direction.y = 0; // Abaikan perbedaan ketinggian (sumbu Y)

        if (direction.sqrMagnitude > 0.001f)
        {
            // Buat rotasi untuk menghadap ke arah tersebut
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            transform.rotation = targetRotation;
        }
    }
}
