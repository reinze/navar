using UnityEngine;
using ARLocation; // Pastikan namespace ini tersedia dari aset Unity AR+GPS Location
using Mapbox.Directions; // Untuk RouteResponse dan kelas terkait (Mapbox.Directions.RouteResponse)
using Mapbox.Utils; // Untuk Vector2d
using System.Collections.Generic; // Untuk List
using System.Linq; // Untuk operasi LINQ seperti Min (jika digunakan)

namespace ARLocation.MapboxRoutes // Pastikan namespace ini sesuai jika RouteRerouter berada di namespace yang sama dengan MenuController
{
    /// <summary>
    /// Skrip ini menangani logika reroute rute navigasi AR berdasarkan posisi pengguna.
    /// Ini mendeteksi penyimpangan dari rute saat ini dan memicu permintaan rute baru
    /// ke Mapbox Directions API melalui MenuController.
    /// </summary>
    public class RouteRerouter : MonoBehaviour
    {
        [Header("Referensi Komponen")]
        [Tooltip("Referensi ke MenuController di scene Anda. Ini akan digunakan untuk memicu pemuatan ulang rute.")]
        public MenuController menuController;

        // startWaypointTransform dan endWaypointTransform tidak lagi di-assign di sini,
        // karena MenuController sekarang mengelolanya secara langsung (jika diperlukan).

        [Header("Pengaturan Reroute")]
        [Tooltip("Jarak (dalam meter) dari rute saat ini yang akan memicu reroute.")]
        public float rerouteThresholdMeters = 20f;

        [Tooltip("Jarak minimum (dalam meter) dari titik awal rute sebelum reroute dipertimbangkan. Mencegah reroute segera setelah memulai rute.")]
        public float minDistanceToReroute = 5f;

        [Tooltip("Waktu cooldown (dalam detik) setelah reroute untuk mencegah permintaan reroute berlebihan.")]
        public float rerouteCooldownSeconds = 10f;

        private List<Vector2d> _currentRouteGeometry; // Geometri rute saat ini (daftar koordinat lintang/bujur)
        private Vector2d _originalDestinationLatLon; // Koordinat lintang/bujur tujuan asli (tidak berubah)
        private bool _isRerouting = false; // Bendera untuk mencegah beberapa permintaan reroute secara bersamaan
        private float _lastRerouteTime = -Mathf.Infinity; // Waktu terakhir reroute terjadi, diinisialisasi agar reroute pertama bisa langsung terjadi

        /// <summary>
        /// Dipanggil saat skrip diaktifkan.
        /// Berlangganan event pembaruan lokasi dari ARLocationProvider dan event rute dimuat ulang dari MenuController.
        /// </summary>
        void OnEnable()
        {
            // ====================================================================================================
            // PERBAIKAN: Berlangganan ARLocationProvider.OnEnabled terlebih dahulu.
            // Ini memastikan ARLocationProvider sepenuhnya siap sebelum kita mencoba mendapatkan pembaruan lokasi.
            // ====================================================================================================
            if (ARLocationProvider.Instance != null)
            {
                ARLocationProvider.Instance.OnEnabled.AddListener(OnARLocationProviderReady);
                Debug.Log("RouteRerouter: Berlangganan ARLocationProvider.OnEnabled.");
            }
            else
            {
                Debug.LogError("RouteRerouter: ARLocationProvider.Instance tidak ditemukan. Pastikan aset AR+GPS Location sudah diimpor dan dikonfigurasi.");
            }
            // ====================================================================================================

            // Berlangganan event OnRouteReloaded dari MenuController untuk mendapatkan geometri rute yang diperbarui
            if (menuController != null)
            {
                menuController.OnRouteReloaded += OnRouteReloadedHandler;
                Debug.Log("RouteRerouter: Berlangganan event OnRouteReloaded dari MenuController.");
            }
            else
            {
                Debug.LogError("RouteRerouter: MenuController tidak ditetapkan. Pastikan Anda menetapkannya di Inspector Unity.");
            }
        }

        /// <summary>
        /// Dipanggil saat skrip dinonaktifkan atau dihancurkan.
        /// Berhenti berlangganan event untuk mencegah kebocoran memori dan masalah referensi.
        /// </summary>
        void OnDisable()
        {
            if (ARLocationProvider.Instance != null)
            {
                // Berhenti berlangganan dari OnEnabled
                ARLocationProvider.Instance.OnEnabled.RemoveListener(OnARLocationProviderReady);

                // Berhenti berlangganan dari OnLocationUpdatedEvent (jika sudah berlangganan)
                // ARLocationProvider.OnLocationUpdatedEvent berfungsi sebagai Add/Remove, jadi panggil dengan false untuk menghapus.
                ARLocationProvider.Instance.OnLocationUpdatedEvent(new LocationUpdatedDelegate(OnLocationUpdated), false);
                Debug.Log("RouteRerouter: Berhenti berlangganan dari ARLocationProvider events.");
            }

            if (menuController != null)
            {
                menuController.OnRouteReloaded -= OnRouteReloadedHandler;
            }
        }

        /// <summary>
        /// Callback yang dipanggil ketika ARLocationProvider telah berhasil diaktifkan.
        /// Ini adalah tempat yang aman untuk mulai berlangganan pembaruan lokasi.
        /// </summary>
        /// <param name="location">Lokasi awal yang diterima saat provider diaktifkan.</param>
        private void OnARLocationProviderReady(Location location)
        {
            // Sekarang ARLocationProvider sudah siap, aman untuk berlangganan pembaruan lokasi.
            // Parameter kedua 'false' memastikan kita tidak memicu pengecekan 'IsEnabled' segera.
            ARLocationProvider.Instance.OnLocationUpdatedEvent(new LocationUpdatedDelegate(OnLocationUpdated), false);
            Debug.Log("RouteRerouter: ARLocationProvider sudah aktif, sekarang berlangganan OnLocationUpdatedEvent.");
        }


        /// <summary>
        /// Callback yang dipanggil setiap kali data lokasi baru tersedia dari ARLocationProvider.
        /// Ini adalah tempat logika deteksi penyimpangan dan pemicu reroute terjadi.
        /// </summary>
        /// <param name="currentLocation">Objek LocationReading yang berisi data lokasi terbaru (dari GPS perangkat).</param>
        /// <param name="lastLocation">Objek LocationReading yang berisi data lokasi sebelumnya.</param>
        void OnLocationUpdated(LocationReading currentLocation, LocationReading lastLocation)
        {
            // Pastikan kita memiliki geometri rute dan tujuan asli sebelum mencoba reroute
            if (_currentRouteGeometry == null || _currentRouteGeometry.Count == 0 || _originalDestinationLatLon == Vector2d.zero)
            {
                // Debug.LogWarning("RouteRerouter: Geometri rute atau tujuan belum diatur. Tunggu rute pertama digambar.");
                return;
            }

            // Cek apakah sedang dalam proses reroute atau dalam masa cooldown
            if (_isRerouting || (Time.time - _lastRerouteTime < rerouteCooldownSeconds))
            {
                // Debug.Log("RouteRerouter: Sedang reroute atau dalam masa cooldown. Lewati deteksi penyimpangan.");
                return;
            }

            Vector2d currentUserLatLon;

            // Mengambil lokasi pengguna dari posisi kamera AR
            Location cameraWorldLocation = null;
            if (Camera.main != null && ARLocationManager.Instance != null)
            {
                cameraWorldLocation = ARLocationManager.Instance.GetLocationForWorldPosition(Camera.main.transform.position);
            }

            if (cameraWorldLocation == null)
            {
                Debug.LogWarning("RouteRerouter: Camera.main atau ARLocationManager.Instance tidak ditemukan, atau konversi posisi dunia gagal. Menggunakan lokasi GPS perangkat sebagai gantinya.");
                currentUserLatLon = new Vector2d(currentLocation.latitude, currentLocation.longitude); // Fallback ke lokasi GPS perangkat
            }
            else
            {
                currentUserLatLon = new Vector2d(cameraWorldLocation.Latitude, cameraWorldLocation.Longitude);
            }

            double distanceToRouteStart = Mapbox.Utils.Vector2d.Distance(currentUserLatLon, _currentRouteGeometry[0])*1000;
            if (distanceToRouteStart < minDistanceToReroute)
            {
                 //Debug.Log($"RouteRerouter: Terlalu dekat dengan titik awal rute ({distanceToRouteStart:F2}m). Tidak reroute.");
                return;
            }

            // Hitung jarak terpendek dari lokasi pengguna saat ini ke rute yang sedang diikuti
            double shortestDistanceToRoute = CalculateShortestDistanceToPolyline(currentUserLatLon, _currentRouteGeometry);

            Debug.Log($"RouteRerouter: Jarak ke rute: {shortestDistanceToRoute:F2}m (Ambang batas: {rerouteThresholdMeters}m)");

            // Jika jarak melebihi ambang batas, picu reroute
            if (shortestDistanceToRoute > rerouteThresholdMeters)
            {
                Debug.LogWarning($"RouteRerouter: Penyimpangan terdeteksi! Jarak dari rute: {shortestDistanceToRoute:F2}m. Memicu reroute...");
                TriggerReroute(currentUserLatLon);
            }
        }

        /// <summary>
        /// Callback yang dipanggil oleh MenuController ketika rute baru berhasil dimuat atau dimuat ulang.
        /// Kita menyimpan geometri rute yang baru dan memastikan tujuan asli tetap terjaga.
        /// </summary>
        /// <param name="response">Objek RouteResponse dari Mapbox API.</param>
        private void OnRouteReloadedHandler(RouteResponse response)
        {
            if (response != null && response.routes != null && response.routes.Count > 0)
            {
                _currentRouteGeometry = new List<Vector2d>();
                foreach (var loc in response.routes[0].geometry.coordinates)
                {
                    _currentRouteGeometry.Add(new Vector2d(loc.Latitude, loc.Longitude));
                }

                // Simpan tujuan asli (titik terakhir dari rute yang baru digambar)
                if (_currentRouteGeometry.Count > 0)
                {
                    _originalDestinationLatLon = _currentRouteGeometry[_currentRouteGeometry.Count - 1];
                    Debug.Log($"RouteRerouter: Rute baru dimuat ulang. Geometri disimpan. Tujuan asli: {_originalDestinationLatLon.ToString()}");
                }
                else
                {
                    Debug.LogError("RouteRerouter: Geometri rute kosong setelah dimuat ulang.");
                }

                _isRerouting = false; // Reroute selesai, siap untuk deteksi penyimpangan berikutnya
            }
            else
            {
                Debug.LogError("RouteRerouter: Respons rute kosong atau tidak valid setelah dimuat ulang.");
                _isRerouting = false; // Pastikan bendera direset bahkan jika ada error
            }
        }

        /// <summary>
        /// Memicu permintaan reroute ke MenuController.
        /// Ini dilakukan dengan memanggil metode di MenuController untuk memuat ulang rute.
        /// </summary>
        /// <param name="newStartLocationLatLon">Koordinat lintang/bujur lokasi awal yang baru (lokasi pengguna saat ini).</param>
        private void TriggerReroute(Vector2d newStartLocationLatLon)
        {
            _isRerouting = true; // Set bendera untuk mencegah reroute berulang
            _lastRerouteTime = Time.time; // Catat waktu reroute

            if (ARLocationManager.Instance != null && menuController != null)
            {
                Location newStartLocation = new Location(newStartLocationLatLon.x, newStartLocationLatLon.y, 0);

                // Secara eksplisit memanggil metode di MenuController untuk memuat ulang rute baru.
                menuController.ReloadRouteWithNewStartLocation(newStartLocation); // Memanggil metode baru di MenuController
                Debug.Log("RouteRerouter: Memanggil MenuController.ReloadRouteWithNewStartLocation() untuk reroute.");
            }
            else
            {
                Debug.LogError("RouteRerouter: Gagal memicu reroute. Pastikan ARLocationManager.Instance dan MenuController ditetapkan dengan benar.");
                _isRerouting = false; // Reset bendera jika gagal
            }
        }

        /// <summary>
        /// Menghitung jarak terpendek dari suatu titik (lokasi pengguna) ke polyline
        /// (representasi rute). Menggunakan algoritma jarak titik-ke-segmen garis.
        /// </summary>
        /// <param name="point">Titik (Vector2d) yang akan diukur jaraknya.</param>
        /// <param name="polyline">Daftar titik-titik (Vector2d) yang membentuk polyline.</param>
        /// <returns>Jarak terpendek dalam meter.</returns>
        private double CalculateShortestDistanceToPolyline(Vector2d point, List<Vector2d> polyline)
        {
            if (polyline == null || polyline.Count < 2)
            {
                return double.MaxValue; // Tidak ada polyline atau terlalu pendek untuk diukur
            }

            double minDistance = double.MaxValue;

            // Iterasi melalui setiap segmen garis dalam polyline
            for (int i = 0; i < polyline.Count - 1; i++)
            {
                Vector2d segmentStart = polyline[i];
                Vector2d segmentEnd = polyline[i + 1];

                double distance = GetDistanceToLineSegment(point, segmentStart, segmentEnd);
                if (distance < minDistance)
                {
                    minDistance = distance;
                }
            }

            return minDistance;
        }

        /// <summary>
        /// Menghitung jarak terpendek dari sebuah titik ke segmen garis.
        /// Diadaptasi dari algoritma umum untuk jarak titik-ke-segmen.
        /// </summary>
        /// <param name="point">Titik (Vector2d) yang akan diukur jaraknya.</param>
        /// <param name="segmentStart">Titik awal segmen garis (Vector2d).</param>
        /// <param name="segmentEnd">Titik akhir segmen garis (Vector2d).</param>
        /// <returns>Jarak terpendek dalam meter.</returns>
        private double GetDistanceToLineSegment(Vector2d point, Vector2d segmentStart, Vector2d segmentEnd)
        {
            // Konversi Vector2d ke Vector2 (untuk perhitungan dot product dan magnitude yang lebih mudah)
            Vector2 p = new Vector2((float)point.x, (float)point.y);
            Vector2 a = new Vector2((float)segmentStart.x, (float)segmentStart.y);
            Vector2 b = new Vector2((float)segmentEnd.x, (float)segmentEnd.y);

            Vector2 ab = b - a;
            Vector2 ap = p - a;

            // Hitung proyeksi ap pada ab, dinormalisasi oleh panjang ab kuadrat
            float proj = Vector2.Dot(ap, ab);
            float lengthSq = ab.sqrMagnitude; // Panjang kuadrat segmen ab

            float t = -1; // Parameter proyeksi

            if (lengthSq > 0) // Hindari pembagian dengan nol untuk segmen nol-panjang
            {
                t = proj / lengthSq;
            }

            Vector2 closestPoint;
            if (t < 0)
            {
                closestPoint = a; // Titik terdekat adalah titik awal segmen
            }
            else if (t > 1)
            {
                closestPoint = b; // Titik terdekat adalah titik akhir segmen
            }
            else
            {
                closestPoint = a + t * ab; // Titik terdekat berada di dalam segmen
            }

            // Ini adalah cara yang lebih akurat untuk menghitung jarak dalam meter:
            // Konversi titik-titik kembali ke Vector2d untuk menggunakan Mapbox.Utils.Vector2d.Distance
            Vector2d closestPointLatLon = new Vector2d(closestPoint.y, closestPoint.x); // Asumsi y=latitude, x=longitude
            return Mapbox.Utils.Vector2d.Distance(point, closestPointLatLon);
        }
    }
}
