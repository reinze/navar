using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;
using ARLocation;
using TMPro;
using UnityEngine.UI;
using System;
using Mapbox.Utils;
using System.Globalization;

namespace ARLocation.MapboxRoutes
{
    [System.Serializable]
    public class ListLoc
    {
        public GameObject go;
        public TMP_Text title;
        public TMP_Text desc;
    }

    public class MenuController : MonoBehaviour
    {
        public enum LineType
        {
            Route,
            NextTarget
        }

        [Header("Mapbox Search Box API Settings")]
        public string SearchBoxApiToken;
        public string SearchTypes = "poi,address";
        public int SearchLimit = 10;
        public string SearchCountry = "ID";
        public bool UseAutocomplete = true;
        private string sessionToken;


        public string MapboxToken = "pk.eyJ1IjoiZG1iZm0iLCJhIjoiY2tyYW9hdGMwNGt6dTJ2bzhieDg3NGJxNyJ9.qaQsMUbyu4iARFe0XB2SWg";
        public GameObject ARSession;
        public GameObject ARSessionOrigin;
        public GameObject RouteContainer;
        public Camera Camera;
        public Camera MapboxMapCamera;
        public MapboxRoute MapboxRoute;
        public AbstractRouteRenderer RoutePathRenderer;
        public AbstractRouteRenderer NextTargetPathRenderer;
        public Texture RenderTexture;
        public Mapbox.Unity.Map.AbstractMap Map;
        [Range(100, 800)]
        public int MapSize = 400;
        public Mapbox.Unity.MeshGeneration.Factories.DirectionsFactory DirectionsFactory;
        public int MinimapLayer;
        public Material MinimapLineMaterial;
        public float BaseLineWidth = 2;
        public float MinimapStepSize = 0.5f;

        public GameObject MapGede;
        public GameObject startButton;
        private AbstractRouteRenderer currentPathRenderer => s.LineType == LineType.Route ? RoutePathRenderer : NextTargetPathRenderer;

        public GameObject searchBtn;
        public GameObject searchUI;
        public GameObject searchResult;
        public GameObject routePreview;
        public GameObject startNav;
        public TMP_Text destinationText;
        public ListLoc[] listLocs;
        public TMP_InputField inputSearch;
        public TMP_Text durationTxt;
        public GameObject LoadingUI;

        // NEW: UI Elements for AR Reset
        [Header("AR Reset & Tracking Lost UI")]
        public Button arResetButton, arResetButton2; // Tombol untuk me-reset AR
        public GameObject arTrackingLostPanel; // Panel UI yang akan muncul saat tracking hilang
        public TMP_Text arTrackingLostMessage; // Teks pesan di panel tracking lost
        string destName;

        public LineType PathRendererType
        {
            get => s.LineType;
            set
            {
                if (value != s.LineType)
                {
                    if (currentPathRenderer) currentPathRenderer.enabled = false;
                    s.LineType = value;
                    if (currentPathRenderer) currentPathRenderer.enabled = true;

                    if (s.View == View.Route)
                    {
                        if (MapboxRoute) MapboxRoute.RoutePathRenderer = currentPathRenderer;
                    }
                }
            }
        }

        enum View
        {
            SearchMenu,
            Route,
        }

        [System.Serializable]
        private class State
        {
            public string QueryText = "";
            public List<MenuController.GeocodingFeature> Results = new List<MenuController.GeocodingFeature>();
            public View View = View.SearchMenu;
            public Location destinationLocation;
            public LineType LineType = LineType.NextTarget;
            public string ErrorMessage;
        }

        private State s = new State();

        private GUIStyle _textStyle;
        GUIStyle textStyle()
        {
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label);
                _textStyle.fontSize = 48;
                _textStyle.fontStyle = FontStyle.Bold;
            }
            return _textStyle;
        }

        private GUIStyle _textFieldStyle;
        GUIStyle textFieldStyle()
        {
            if (_textFieldStyle == null)
            {
                _textFieldStyle = new GUIStyle(GUI.skin.textField);
                _textFieldStyle.fontSize = 48;
            }
            return _textFieldStyle;
        }

        private GUIStyle _errorLabelStyle;
        GUIStyle errorLabelSytle()
        {
            if (_errorLabelStyle == null)
            {
                _errorLabelStyle = new GUIStyle(GUI.skin.label);
                _errorLabelStyle.fontSize = 24;
                _errorLabelStyle.fontStyle = FontStyle.Bold;
                _errorLabelStyle.normal.textColor = Color.red;
            }
            return _errorLabelStyle;
        }

        private GUIStyle _buttonStyle;
        GUIStyle buttonStyle()
        {
            if (_buttonStyle == null)
            {
                _buttonStyle = new GUIStyle(GUI.skin.button);
                _buttonStyle.fontSize = 48;
            }
            return _buttonStyle;
        }


        public event Action<RouteResponse> OnRouteReloaded;
        private Mapbox.Utils.Vector2d _lastMapCenterLatLon;
        private MapboxApi mapboxApiInstance;

        void Awake()
        {
            sessionToken = Guid.NewGuid().ToString(); // Inisialisasi token sesi
            string apiTokenToUse = string.IsNullOrEmpty(SearchBoxApiToken) ? MapboxToken : SearchBoxApiToken;
            if (string.IsNullOrEmpty(apiTokenToUse))
            {
                Debug.LogError("Mapbox Token is not set in MenuController! Please assign either MapboxToken or SearchBoxApiToken in the inspector.");
            }
            MapboxApiLanguage language = MapboxApiLanguage.English_US;
            if (MapboxRoute != null && MapboxRoute.Settings != null)
            {
                language = MapboxRoute.Settings.Language;
            }
            else
            {
                Debug.LogWarning("MapboxRoute or MapboxRoute.Settings not assigned in MenuController.Awake(). Defaulting language to English_US for MapboxApi.");
            }
            mapboxApiInstance = new MapboxApi(apiTokenToUse, language);
            Debug.Log("MapboxApi instance created in MenuController.Awake()");
        }

        void Start()
        {
            LoadingUI.SetActive(true);
            if (NextTargetPathRenderer) NextTargetPathRenderer.enabled = false;
            if (RoutePathRenderer) RoutePathRenderer.enabled = false;
            if (ARLocationProvider.Instance != null)
            {
                ARLocationProvider.Instance.OnEnabled.AddListener(onLocationEnabled);
            }
            else
            {
                Debug.LogError("ARLocationProvider.Instance is null in Start!");
            }

            if (Map != null)
            {
                Map.OnUpdated += OnMapRedrawn;
                _lastMapCenterLatLon = Map.CenterLatitudeLongitude;
            }
            else
            {
                Debug.LogError("Map (AbstractMap) is not assigned in MenuController!");
            }

            // NEW: Subscribe to AR Tracking events
            if (ARLocationManager.Instance != null)
            {
                ARLocationManager.Instance.OnTrackingLost.AddListener(OnARTrackingLostHandler);
                ARLocationManager.Instance.OnTrackingRestored.AddListener(OnARTrackingRestoredHandler);
            }
            else
            {
                Debug.LogError("ARLocationManager.Instance is null in Start! Cannot subscribe to tracking events.");
            }

            // Hide tracking lost panel initially
            if (arTrackingLostPanel != null)
            {
                arTrackingLostPanel.SetActive(false);
            }
        }

        private void OnMapRedrawn()
        {
            if (currentResponse != null)
            {
                buildMinimapRoute(currentResponse);
            }
        }

        private void onLocationEnabled(Location location)
        {
            if (Map != null)
            {
                Map.SetCenterLatitudeLongitude(new Mapbox.Utils.Vector2d(location.Latitude, location.Longitude));
                Map.UpdateMap();
            }

#if UNITY_EDITOR
            if (ARLocationProvider.Instance != null && ARLocationProvider.Instance.Provider is MockLocationProvider && ARLocationProvider.Instance.MockLocationData != null)
            {
                ((MockLocationProvider)ARLocationProvider.Instance.Provider).mockLocation = location;
            }
#endif
        }

        void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            // NEW: Add listener for AR Reset Button
            if (arResetButton != null)
            {
                arResetButton.onClick.AddListener(OnARResetButtonPressed);
            }
            if (arResetButton2 != null)
            {
                arResetButton2.onClick.AddListener(OnARResetButtonPressed);
            }
        }

        void OnDisable()
        {
            if (ARLocationProvider.Instance != null)
            {
                ARLocationProvider.Instance.OnEnabled.RemoveListener(loadRoute);
            }
            if (Map != null) Map.OnUpdated -= OnMapRedrawn;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            // NEW: Unsubscribe from AR Reset Button and AR Tracking events to prevent memory leaks
            if (arResetButton != null)
            {
                arResetButton.onClick.RemoveListener(OnARResetButtonPressed);
            }
            if (arResetButton2 != null)
            {
                arResetButton2.onClick.RemoveListener(OnARResetButtonPressed);
            }
            if (ARLocationManager.Instance != null)
            {
                ARLocationManager.Instance.OnTrackingLost.RemoveListener(OnARTrackingLostHandler);
                ARLocationManager.Instance.OnTrackingRestored.RemoveListener(OnARTrackingRestoredHandler);
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log($"Scene Loaded: {scene.name}");
        }

        public void StartGame()
        {
            if (startNav) startNav.SetActive(true);
            if (MapGede) MapGede.SetActive(false);
            LoadingUI.SetActive(false);
        }
        public void MainMenu()
        {
            if (searchUI) searchUI.SetActive(false);
            if (startButton) startButton.SetActive(true);
            if (routePreview) routePreview.SetActive(false);
            if (startNav) startNav.SetActive(false);
            if (MapGede) MapGede.SetActive(true);
        }
        public void SearchUI()
        {
            if (routePreview) routePreview.SetActive(false);
            if (startNav) startNav.SetActive(false);
            if (MapGede) MapGede.SetActive(true);
            if (searchUI) searchUI.SetActive(true);
            if (inputSearch) inputSearch.text = "";
            if (startButton) startButton.SetActive(false);
            if (listLocs != null)
            {
                foreach (var l in listLocs)
                {
                    if (l != null && l.go) l.go.SetActive(false);
                }
            }
        }

        public void Search()
        {
            if (inputSearch == null)
            {
                Debug.LogError("inputSearch (TMP_InputField) is not assigned in MenuController!");
                return;
            }
            s.QueryText = inputSearch.text;
            StartCoroutine(PerformMapboxSearch());
        }

        public void ListLocation()
        {
            if (listLocs == null) return;

            foreach (var locItem in listLocs)
            {
                if (locItem != null && locItem.go) locItem.go.SetActive(false);
            }

            if (s.Results == null) return;

            for (int i = 0; i < s.Results.Count && i < listLocs.Length; i++)
            {
                if (listLocs[i] == null) continue;

                if (listLocs[i].go) listLocs[i].go.SetActive(true);
                if (listLocs[i].title) listLocs[i].title.text = s.Results[i].place_name;
                if (listLocs[i].desc) listLocs[i].desc.text = s.Results[i].address_line1;

                int index = i;
                var button = listLocs[i].go.GetComponent<Button>();
                if (button)
                {
                    button.onClick.RemoveAllListeners();
                    button.onClick.AddListener(() => FindLocation(index));
                }
            }
        }

        public void FindLocation(int locIndex)
        {
            if (s.Results == null || locIndex < 0 || locIndex >= s.Results.Count)
            {
                Debug.LogError($"Invalid location index: {locIndex}");
                return;
            }

            var selectedFeature = s.Results[locIndex];

            bool suggestCoordsAreEffectivelyZero = selectedFeature.geometry_coordinates == null ||
                                           (Math.Abs(selectedFeature.geometry_coordinates.x) < 0.000001 &&
                                            Math.Abs(selectedFeature.geometry_coordinates.y) < 0.000001);

            if (suggestCoordsAreEffectivelyZero)
            {
                Debug.Log($"Coordinates for '{selectedFeature.place_name}' from /suggest are (0,0) or null. Mapbox ID: {selectedFeature.mapbox_id}. Attempting /retrieve.");
                if (string.IsNullOrEmpty(selectedFeature.mapbox_id))
                {
                    s.ErrorMessage = $"Tidak ada ID untuk mengambil detail lokasi '{selectedFeature.place_name}'.";
                    Debug.LogError(s.ErrorMessage);
                    // Di sini Anda mungkin ingin memperbarui UI untuk menampilkan pesan error ini kepada pengguna.
                    return;
                }
                // Memulai coroutine baru untuk mengambil detail dan kemudian memulai rute
                StartCoroutine(RetrieveAndStartRoute(selectedFeature.mapbox_id, selectedFeature.place_name));
            }
            else
            {
                Debug.Log($"Using coordinates directly from /suggest for {selectedFeature.place_name}: Lon={selectedFeature.geometry_coordinates.x}, Lat={selectedFeature.geometry_coordinates.y}");
                var dest = new Location
                {
                    Latitude = selectedFeature.geometry_coordinates.y,
                    Longitude = selectedFeature.geometry_coordinates.x,
                    Altitude = 0
                };
                if (destinationText) destinationText.text = selectedFeature.place_name;
                StartRoute(dest);
                print(dest.Latitude + " " + dest.Longitude);
            }
        }
        public void GetFromReact(Location dest, string destNamer)
        {
            StartRoute(dest);
            destName = destNamer;
            print("dessstt" + destName);
        }
        IEnumerator RetrieveAndStartRoute(string mapboxId, string placeName)
        {
            if (mapboxApiInstance == null)
            {
                Debug.LogError("MapboxApi instance is null in RetrieveAndStartRoute.");
                s.ErrorMessage = "Kesalahan sistem pencarian.";
                yield break;
            }
            if (string.IsNullOrEmpty(sessionToken)) // Pastikan sessionToken tersedia
            {
                sessionToken = Guid.NewGuid().ToString();
                Debug.LogWarning("Session token was null/empty in RetrieveAndStartRoute, generated a new one.");
            }

            Debug.Log($"[MenuController#RetrieveAndStartRoute] Retrieving details for mapboxId: {mapboxId}");
            yield return mapboxApiInstance.RetrieveSuggestionDetails(mapboxId, sessionToken, true);

            if (mapboxApiInstance.ErrorMessage != null)
            {
                s.ErrorMessage = $"Gagal mengambil detail untuk '{placeName}': {mapboxApiInstance.ErrorMessage}";
                Debug.LogError(s.ErrorMessage);
                // Di sini Anda mungkin ingin memperbarui UI untuk menampilkan pesan error ini kepada pengguna.
            }
            else if (mapboxApiInstance.QueryRetrieveResult != null &&
                     mapboxApiInstance.QueryRetrieveResult.geometry != null &&
                     mapboxApiInstance.QueryRetrieveResult.geometry.coordinates != null &&
                     mapboxApiInstance.QueryRetrieveResult.geometry.coordinates.Count >= 2)
            {
                var retrievedCoords = mapboxApiInstance.QueryRetrieveResult.geometry.coordinates;
                // GeoJSON order: longitude, latitude
                var dest = new Location
                {
                    Longitude = retrievedCoords[0],
                    Latitude = retrievedCoords[1],
                    Altitude = 0
                };

                string displayName = mapboxApiInstance.QueryRetrieveResult.properties?.place_formatted ?? mapboxApiInstance.QueryRetrieveResult.properties?.name ?? placeName;
                if (destinationText) destinationText.text = displayName;
                Debug.Log($"[MenuController#RetrieveAndStartRoute] Successfully retrieved coordinates for {displayName}: Lon={dest.Longitude}, Lat={dest.Latitude}. Starting route.");
                StartRoute(dest);
            }
            else
            {
                s.ErrorMessage = $"Detail lokasi untuk '{placeName}' tidak memiliki koordinat yang valid setelah retrieve.";
                Debug.LogError(s.ErrorMessage);
                // Di sini Anda mungkin ingin memperbarui UI untuk menampilkan pesan error ini kepada pengguna.
            }
        }


        private Texture2D _separatorTexture;
        // Properti separatorTexture tetap sama

        public void StartRoute(Location dest)
        {
            s.destinationLocation = dest;
            print(s.destinationLocation.Latitude+" " + s.destinationLocation.Longitude);
            if (ARLocationProvider.Instance != null && ARLocationProvider.Instance.IsEnabled)
            {
                loadRoute(ARLocationProvider.Instance.CurrentLocation.ToLocation());
            }
            else if (ARLocationProvider.Instance != null)
            {
                ARLocationProvider.Instance.OnEnabled.AddListener(loadRoute);
            }
            else
            {
                Debug.LogError("ARLocationProvider.Instance is null in StartRoute!");
            }
        }
        public void EndTrip()
        {
            startNav.SetActive(false);
            searchResult.SetActive(true);
            EndRoute();
        }
        public void EndRoute()
        {
            if (ARLocationProvider.Instance != null)
            {
                ARLocationProvider.Instance.OnEnabled.RemoveListener(loadRoute);
            }
            if (ARSession) ARSession.SetActive(false);
            if (ARSessionOrigin) ARSessionOrigin.SetActive(false);
            if (RouteContainer) RouteContainer.SetActive(false);
            if (Camera) Camera.gameObject.SetActive(true);
            if (MapboxMapCamera) MapboxMapCamera.gameObject.SetActive(false);
            s.View = View.SearchMenu;
            

            // NEW: Hide tracking lost panel when ending route
            if (arTrackingLostPanel != null)
            {
                arTrackingLostPanel.SetActive(false);
            }
        }

        private RouteResponse currentResponse;
        private void loadRoute(Location startLocation)
        {
            if (s.destinationLocation != null)
            {
                StartGame();

                Debug.Log($"[MenuController#loadRoute] Attempting to load route.");
                Debug.Log($"[MenuController#loadRoute] Start Location: Lat={startLocation.Latitude.ToString(CultureInfo.InvariantCulture)}, Lon={startLocation.Longitude.ToString(CultureInfo.InvariantCulture)}");
                Debug.Log($"[MenuController#loadRoute] Destination Location: Lat={s.destinationLocation.Latitude.ToString(CultureInfo.InvariantCulture)}, Lon={s.destinationLocation.Longitude.ToString(CultureInfo.InvariantCulture)}");


                if (mapboxApiInstance == null)
                {
                    string apiTokenToUse = string.IsNullOrEmpty(SearchBoxApiToken) ? MapboxToken : SearchBoxApiToken;
                    MapboxApiLanguage language = MapboxApiLanguage.English_US;
                    if (MapboxRoute != null && MapboxRoute.Settings != null) language = MapboxRoute.Settings.Language;
                    mapboxApiInstance = new MapboxApi(apiTokenToUse, language);
                }

                var loader = new RouteLoader(mapboxApiInstance);
                StartCoroutine(
                        loader.LoadRoute(
                            new RouteWaypoint { Type = RouteWaypointType.Location, Location = startLocation },
                            new RouteWaypoint { Type = RouteWaypointType.Location, Location = s.destinationLocation },
                            (err, res) =>
                            {
                                if (err != null)
                                {
                                    s.ErrorMessage = err;
                                    if (s.Results != null) s.Results.Clear();
                                    Debug.LogError($"MenuController: Error loading route: {err}");
                                    OnRouteReloaded?.Invoke(null);
                                    return;
                                }

                                if (ARSession) ARSession.SetActive(true);
                                if (ARSessionOrigin) ARSessionOrigin.SetActive(true);
                                if (RouteContainer) RouteContainer.SetActive(true);
                                if (Camera) Camera.gameObject.SetActive(false);
                                if (MapboxMapCamera) MapboxMapCamera.gameObject.SetActive(true);
                                s.View = View.Route;

                                if (currentPathRenderer) currentPathRenderer.enabled = true;
                                if (MapboxRoute)
                                {
                                    MapboxRoute.RoutePathRenderer = currentPathRenderer;
                                    MapboxRoute.BuildRoute(res);
                                }
                                currentResponse = res;
                                buildMinimapRoute(res);

                                OnRouteReloaded?.Invoke(res);
                            }));
            }
        }
        public void ReloadRouteWithNewStartLocation(Location newStartLocation)
        {
            Debug.Log($"MenuController: Reloading route from new start: {newStartLocation.Latitude}, {newStartLocation.Longitude}");
            loadRoute(newStartLocation);
        }


        private GameObject minimapRouteGo;
        private void buildMinimapRoute(RouteResponse res)
        {
            if (res == null || res.routes == null || res.routes.Count == 0 || Map == null) return;

            var geo = res.routes[0].geometry;
            var vertices = new List<Vector3>();
            var worldPositions = new List<Vector2>();

            if (geo.coordinates == null) return;

            foreach (var p in geo.coordinates)
            {
                var pos = Map.GeoToWorldPosition(new Mapbox.Utils.Vector2d(p.Latitude, p.Longitude), true);
                worldPositions.Add(new Vector2(pos.x, pos.z));
            }

            if (minimapRouteGo != null)
            {
                Destroy(minimapRouteGo);
            }

            minimapRouteGo = new GameObject("minimap_route_line");
            minimapRouteGo.layer = MinimapLayer;

            var mesh = minimapRouteGo.AddComponent<MeshFilter>().mesh;
            var lineWidth = BaseLineWidth * Mathf.Pow(2.0f, Map.Zoom - 18);
            LineBuilder.BuildLineMesh(worldPositions, mesh, lineWidth);

            var meshRenderer = minimapRouteGo.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = MinimapLineMaterial;

            if (durationTxt && res.routes.Count > 0)
            {
                durationTxt.text = (res.routes[0].duration / 60) + " Minutes (" + $"{res.routes[0].distance / 1000:0.00} km)";
            }
        }

        IEnumerator PerformMapboxSearch()
        {
            if (mapboxApiInstance == null)
            {
                Debug.LogError("MapboxApi instance is null in PerformMapboxSearch. Initializing now.");
                string apiTokenToUse = string.IsNullOrEmpty(SearchBoxApiToken) ? MapboxToken : SearchBoxApiToken;
                MapboxApiLanguage language = MapboxApiLanguage.English_US;
                if (MapboxRoute != null && MapboxRoute.Settings != null) language = MapboxRoute.Settings.Language;
                mapboxApiInstance = new MapboxApi(apiTokenToUse, language);
            }

            string proximityString = "";
            if (ARLocationProvider.Instance != null && ARLocationProvider.Instance.IsEnabled)
            {
                var currentLoc = ARLocationProvider.Instance.CurrentLocation;
                proximityString = $"{currentLoc.longitude.ToString(CultureInfo.InvariantCulture)},{currentLoc.latitude.ToString(CultureInfo.InvariantCulture)}";
            }

            if (string.IsNullOrEmpty(sessionToken))
            {
                sessionToken = Guid.NewGuid().ToString();
            }

            yield return mapboxApiInstance.QuerySuggest(s.QueryText, proximityString, SearchTypes, SearchLimit, SearchCountry, UseAutocomplete, sessionToken, true);

            if (mapboxApiInstance.ErrorMessage != null)
            {
                s.ErrorMessage = mapboxApiInstance.ErrorMessage;
                if (s.Results != null) s.Results.Clear();
                Debug.LogError($"Search Error: {s.ErrorMessage}");
            }
            else
            {
                s.ErrorMessage = null;
                if (s.Results != null) s.Results.Clear(); else s.Results = new List<GeocodingFeature>();

                if (mapboxApiInstance.QuerySuggestResult != null && mapboxApiInstance.QuerySuggestResult.suggestions != null)
                {
                    foreach (var suggestion in mapboxApiInstance.QuerySuggestResult.suggestions)
                    {
                        var feature = new GeocodingFeature
                        {
                            place_name = !string.IsNullOrEmpty(suggestion.place_formatted) ? suggestion.place_formatted : suggestion.name,
                            address_line1 = suggestion.address,
                            mapbox_id = suggestion.mapbox_id,
                            geometry_coordinates = (suggestion.coordinates != null) ?
                                new Mapbox.Utils.Vector2d(suggestion.coordinates.Longitude, suggestion.coordinates.Latitude) :
                                Mapbox.Utils.Vector2d.zero
                        };
                        if (s.Results != null) s.Results.Add(feature);
                    }
                }
                Debug.Log($"Search returned {(s.Results != null ? s.Results.Count : 0)} results.");
            }
            ListLocation();
        }

        [System.Serializable]
        public class GeocodingFeature
        {
            public string place_name;
            public string address_line1;
            public string mapbox_id;
            public Mapbox.Utils.Vector2d geometry_coordinates;
            public double distance;
        }


        Vector3 lastCameraPos;
        void Update()
        {
            if (s.View == View.Route)
            {
                if (Camera.main == null || ARLocationManager.Instance == null || ARLocationManager.Instance.gameObject == null) return;

                var cameraPos = Camera.main.transform.position;
                var arLocationRootAngle = ARLocationManager.Instance.gameObject.transform.localEulerAngles.y;
                var cameraAngle = Camera.main.transform.localEulerAngles.y;
                var mapAngle = cameraAngle - arLocationRootAngle;

                if (MapboxMapCamera) MapboxMapCamera.transform.eulerAngles = new Vector3(90, mapAngle, 0);

                if ((cameraPos - lastCameraPos).magnitude < MinimapStepSize)
                {
                    return;
                }

                lastCameraPos = cameraPos;

                var location = ARLocationManager.Instance.GetLocationForWorldPosition(Camera.main.transform.position);
                if (Map != null)
                {
                    Map.SetCenterLatitudeLongitude(new Mapbox.Utils.Vector2d(location.Latitude, location.Longitude));
                    Map.UpdateMap();
                }
            }
            else
            {
                if (MapboxMapCamera) MapboxMapCamera.transform.eulerAngles = new Vector3(90, 0, 0);
            }

#if UNITY_EDITOR
                if (ARLocationProvider.Instance != null && ARLocationProvider.Instance.Provider is MockLocationProvider && ARLocationProvider.Instance.MockLocationData != null && Map != null)
                {
                    if (Map.CenterLatitudeLongitude != _lastMapCenterLatLon)
                    {
                        _lastMapCenterLatLon = Map.CenterLatitudeLongitude;

                        ((MockLocationProvider)ARLocationProvider.Instance.Provider).mockLocation = new Location(
                            _lastMapCenterLatLon.x,
                            _lastMapCenterLatLon.y,
                            0
                        );
                        ARLocationProvider.Instance.ForceLocationUpdate();
                    }
                }
#endif
        }

        // NEW: Method to handle AR Reset button press
        public void OnARResetButtonPressed()
        {
            Debug.Log("AR Reset button pressed. Resetting AR Session...");
            if (ARLocationManager.Instance != null)
            {
                ARLocationManager.Instance.ResetARSession();
            }
            else
            {
                Debug.LogError("ARLocationManager.Instance is null! Cannot reset AR session.");
            }

            // Hide the tracking lost panel after reset
            if (arTrackingLostPanel != null)
            {
                arTrackingLostPanel.SetActive(false);
            }
            // Optionally, re-enable main camera if ARSession is off
            if (ARSession) ARSession.SetActive(true);
            if (ARSessionOrigin) ARSessionOrigin.SetActive(true);
        }

        // NEW: Handler for AR Tracking Lost event
        private void OnARTrackingLostHandler()
        {
            Debug.Log("AR Tracking Lost! Displaying message to user.");
            if (arTrackingLostPanel != null)
            {
                arTrackingLostPanel.SetActive(true);
            }
            if (arTrackingLostMessage != null)
            {
                arTrackingLostMessage.text = "Pelacakan AR hilang! Harap pindai lingkungan Anda atau tekan tombol Reset AR.";
            }
            // Opsional: Jika AR tracking hilang, hentikan sementara pembaruan posisi objek AR yang sangat bergantung pada AR tracking.
            // Anda bisa tambahkan logika untuk mem-pause komponen PlaceAtLocation atau MoveAlongPath di sini.
            // Contoh: Menghentikan ARSession dan Origin untuk "menghentikan" AR sampai reset manual.
            if (ARSession) ARSession.SetActive(false);
            if (ARSessionOrigin) ARSessionOrigin.SetActive(false);
        }

        // NEW: Handler for AR Tracking Restored event
        private void OnARTrackingRestoredHandler()
        {
            Debug.Log("AR Tracking Restored!");
            // Panel peringatan akan tetap terlihat sampai tombol reset ditekan jika RestartWhenARTrackingIsRestored = false
            // Jika Anda ingin panel hilang otomatis saat tracking pulih, ganti logika di OnARResetButtonPressed
            // atau atur RestartWhenARTrackingIsRestored menjadi true di ARLocationManager
        }
    }
}
