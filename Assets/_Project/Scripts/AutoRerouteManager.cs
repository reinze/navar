using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ARLocation;
using ARLocation.MapboxRoutes;

public class AutoRerouteManager : MonoBehaviour
{
    [Header("Mapbox & Route Settings")]
    [Tooltip("Referensi ke MapboxApi instance Anda")]
    public MapboxApi mapboxApi;

    [Tooltip("Waypoint tujuan akhir")]
    public RouteWaypoint destination;

    [Header("Auto-Reroute Parameters")]
    [Tooltip("Jarak (m) di luar jalur sebelum auto-reroute")]
    public float rerouteThreshold = 30f;

    [Tooltip("Interval (detik) untuk mengecek posisi user")]
    public float checkInterval = 1f;

    private RouteLoader routeLoader;
    private RouteResponse currentRoute;
    private List<Location> currentPolyline = new List<Location>();

    void Start()
    {
        print(mapboxApi);
        if (mapboxApi == null)
        {
            Debug.LogError("[AutoRerouteManager] MapboxApi belum di-set di Inspector!");
            enabled = false;
            return;
        }

        // Inisialisasi RouteLoader dengan verbose = true untuk debugging
        routeLoader = new RouteLoader(mapboxApi, verboseMode: true);
        // Jalankan loop auto-reroute
        StartCoroutine(RunRerouteLoop());
    }

    IEnumerator RunRerouteLoop()
    {
        while (true)
        {
            // 1. Ambil posisi user terbaru
            var userLoc = ARLocationProvider.Instance.CurrentLocation.ToLocation();
            var userWaypoint = new RouteWaypoint
            {
                Type = RouteWaypointType.Location,
                Location = userLoc
            };

            // 2. Load route dari user ke tujuan
            yield return StartCoroutine(
                routeLoader.LoadRoute(userWaypoint, destination, OnRouteLoaded)
            );

            // Jika polyline kosong (gagal load), tunggu dan ulangi
            if (currentPolyline.Count == 0)
            {
                Debug.LogWarning("[AutoRerouteManager] Rute gagal dimuat—mencoba lagi setelah delay.");
                yield return new WaitForSeconds(checkInterval);
                continue;
            }

            // 3. Pantau terus posisi user terhadap polyline
            while (true)
            {
                userLoc = ARLocationProvider.Instance.CurrentLocation.ToLocation();
                float dist = GetMinDistanceToPolyline(userLoc, currentPolyline);
                if (dist > rerouteThreshold)
                {
                    Debug.Log($"[AutoRerouteManager] Pengguna {dist:F1}m dari jalur—melakukan reroute.");
                    break;  // keluar loop, lalu ulangi load route baru
                }
                yield return new WaitForSeconds(checkInterval);
            }
        }
    }

    // Callback dipanggil saat routeLoader selesai
    private void OnRouteLoaded(string err, RouteResponse result)
    {
        if (err != null || result == null || result.routes.Count == 0)
        {
            Debug.LogError($"[AutoRerouteManager] LoadRoute error: {err}");
            currentPolyline.Clear();
            return;
        }

        currentRoute = result;
        // Ambil daftar titik jalur dari geometry.coordinates :contentReference[oaicite:0]{index=0}:contentReference[oaicite:1]{index=1}
        currentPolyline = currentRoute.routes[0].geometry.coordinates;
        Debug.Log($"[AutoRerouteManager] Rute dimuat: {currentPolyline.Count} titik.");
    }

    // Hitung jarak minimum (m) dari user ke setiap segmen polyline
    private float GetMinDistanceToPolyline(Location user, List<Location> polyline)
    {
        float minDeg = float.MaxValue;
        Vector2 p = new Vector2((float)user.Latitude, (float)user.Longitude);
        for (int i = 0; i < polyline.Count - 1; i++)
        {
            var a = polyline[i];
            var b = polyline[i + 1];
            float deg = DistancePointToSegment(
                p,
                new Vector2((float)a.Latitude, (float)a.Longitude),
                new Vector2((float)b.Latitude, (float)b.Longitude)
            );
            if (deg < minDeg) minDeg = deg;
        }
        // Konversi derajat (lat/lon) ke meter: ~111.139 m per derajat
        return minDeg * 111139f;
    }

    // Jarak 2D titik ke segmen (Euclidean dalam lat/lon)
    private float DistancePointToSegment(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ap = p - a, ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(ap, ab) / ab.sqrMagnitude);
        Vector2 closest = a + ab * t;
        return Vector2.Distance(p, closest);
    }
}
