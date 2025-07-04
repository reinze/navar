using System.Collections.Generic;
using System.Globalization;
using System.Xml;
using UnityEngine;

namespace ARLocation
{
    public class WebMapLoader : MonoBehaviour
    {
        public static WebMapLoader instance;
        public class DataEntry
        {
            public int id;
            public double lat;
            public double lng;
            public double altitude;
            public string altitudeMode;
            public string name;
            public string meshId;
            public float movementSmoothing;
            public int maxNumberOfLocationUpdates;
            public bool useMovingAverage;
            public bool hideObjectUtilItIsPlaced;

            public AltitudeMode getAltitudeMode()
            {
                if (altitudeMode == "GroundRelative")
                {
                    return AltitudeMode.GroundRelative;
                }
                else if (altitudeMode == "DeviceRelative")
                {
                    return AltitudeMode.DeviceRelative;
                }
                else if (altitudeMode == "Absolute")
                {
                    return AltitudeMode.Absolute;
                }
                else
                {
                    return AltitudeMode.Ignore;
                }
            }
        }

        /// <summary>
        ///   The PrefabDatabase ScriptableObject, containing a dictionary of Prefabs with a string ID.
        /// </summary>
        public PrefabDatabase PrefabDatabase;

        /// <summary>
        ///   The XML data file download from the Web Map Editor (htttps://editor.unity-ar-gps-location.com)
        /// </summary>
        public TextAsset XmlDataFile;

        /// <summary>
        ///   If true, enable DebugMode on the PlaceAtLocation generated instances.
        /// </summary>
        public bool DebugMode;

        /// <summary>
        /// Returns a list of the PlaceAtLocation instances created by this compoonent.
        /// >/summary>
        public List<PlaceAtLocation> Instances
        {
            get => _placeAtComponents;
        }

        private List<DataEntry> _dataEntries = new List<DataEntry>();
        private List<PlaceAtLocation> _placeAtComponents = new List<PlaceAtLocation>();

        // Start is called before the first frame update
        void Start()
        {
            instance = this;
            //LoadXmlFile();
            //BuildGameObjects();
        }

        /// <summary>
        ///
        /// Calls SetActive(value) for each of the gameObjects created by this component.
        ///
        /// </summary>
        public void SetActiveGameObjects(bool value)
        {
            foreach (var i in _placeAtComponents)
            {
                i.gameObject.SetActive(value);
            }
        }

        /// <summary>
        ///
        /// Hides all the meshes contained on each of the gameObjects created
        /// by this component, but does not disable the gameObjects.
        ///
        /// </summary>
        public void HideMeshes()
        {
            foreach (var i in _placeAtComponents)
            {
                Utils.Misc.HideGameObject(i.gameObject);
            }
        }

        /// <summary>
        ///
        /// Makes all the gameObjects visible after calling HideMeshes.
        ///
        /// </summary>
        public void ShowMeshes()
        {
            foreach (var i in _placeAtComponents)
            {
                Utils.Misc.ShowGameObject(i.gameObject);
            }
        }

        void BuildGameObjects()
        {
            foreach (var entry in _dataEntries)
            {
                var Prefab = PrefabDatabase.GetEntryById(entry.meshId);

                if (!Prefab)
                {
                    Debug.LogWarning($"[ARLocation#WebMapLoader]: Prefab {entry.meshId} not found.");
                    continue;
                }
                var PlacementOptions = new PlaceAtLocation.PlaceAtOptions()
                {
                    MovementSmoothing = entry.movementSmoothing,
                    MaxNumberOfLocationUpdates = entry.maxNumberOfLocationUpdates,
                    UseMovingAverage = entry.useMovingAverage,
                    HideObjectUntilItIsPlaced = false
                };

                var location = new Location()
                {
                    //-24.496197, -47.86848
                    Latitude = entry.lat,
                    Longitude = entry.lng,
                    Altitude = 2,
                    AltitudeMode = entry.getAltitudeMode(),
                    Label = entry.name
                };

                //print(location.Latitude + " " + location.Longitude);
                var instance = PlaceAtLocation.CreatePlacedInstance(Prefab,
                                                                    location,
                                                                    PlacementOptions,
                                                                    DebugMode);
                
                _placeAtComponents.Add(instance.GetComponent<PlaceAtLocation>());
            }
        }

       public void LoadFromDatabaseScript()
        {
            _dataEntries.Clear();

            foreach (var ar in DatabaseScript.arDataList)
            {
                double lat = double.Parse(ar.latitude, CultureInfo.InvariantCulture);
                double lng = double.Parse(ar.longitude, CultureInfo.InvariantCulture);
                //print(lat + " " + lng);
                double altitude = double.TryParse(ar.altitude, NumberStyles.Float, CultureInfo.InvariantCulture, out double alt) ? alt : 0;

                DataEntry entry = new DataEntry()
                {
                    id = ar.id,
                    lat = lat,
                    lng = lng,
                    altitude = altitude,
                    altitudeMode = "GroundRelative", // Default or you can decide based on category/city/etc.
                    name = ar.name,
                    meshId = "Cube", // Pastikan meshId sesuai dengan ID prefab kamu
                    movementSmoothing = 0f,
                    maxNumberOfLocationUpdates = 3,
                    useMovingAverage = false,
                    hideObjectUtilItIsPlaced = false
                };

                _dataEntries.Add(entry);
            }
            BuildGameObjects();
        }

        // Update is called once per frame
        void LoadXmlFile()
        {
            var xmlString = XmlDataFile.text;

            Debug.Log(xmlString);

            XmlDocument xmlDoc = new XmlDocument();

            try
            {
                xmlDoc.LoadXml(xmlString);
            }
            catch (XmlException e)
            {
                Debug.LogError("[ARLocation#WebMapLoader]: Failed to parse XML file: " + e.Message);
            }

            var root = xmlDoc.FirstChild;
            var nodes = root.ChildNodes;
            foreach (XmlNode node in nodes)
            {
                //Debug.Log(node.InnerXml);
                //Debug.Log(node["id"].InnerText);

                int id = int.Parse(node["id"].InnerText);
                double lat = double.Parse(node["lat"].InnerText, CultureInfo.InvariantCulture);
                double lng = double.Parse(node["lng"].InnerText, CultureInfo.InvariantCulture);
                double altitude = double.Parse(node["altitude"].InnerText, CultureInfo.InvariantCulture);
                string altitudeMode = node["altitudeMode"].InnerText;
                string name = node["name"].InnerText;
                string meshId = node["meshId"].InnerText;
                float movementSmoothing = float.Parse(node["movementSmoothing"].InnerText, CultureInfo.InvariantCulture);
                int maxNumberOfLocationUpdates = int.Parse(node["maxNumberOfLocationUpdates"].InnerText);
                bool useMovingAverage = bool.Parse(node["useMovingAverage"].InnerText);
                bool hideObjectUtilItIsPlaced = bool.Parse(node["hideObjectUtilItIsPlaced"].InnerText);

                DataEntry entry = new DataEntry()
                {
                    id = id,
                    lat = lat,
                    lng = lng,
                    altitudeMode = altitudeMode,
                    altitude = altitude,
                    name = name,
                    meshId = meshId,
                    movementSmoothing = movementSmoothing,
                    maxNumberOfLocationUpdates = maxNumberOfLocationUpdates,
                    useMovingAverage = useMovingAverage,
                    hideObjectUtilItIsPlaced = hideObjectUtilItIsPlaced
                };

                _dataEntries.Add(entry);

                //Debug.Log($"{id}, {lat}, {lng}, {altitude}, {altitudeMode}, {name}, {meshId}, {movementSmoothing}, {maxNumberOfLocationUpdates}, {useMovingAverage}, {hideObjectUtilItIsPlaced}");
            }
        }
    }
}