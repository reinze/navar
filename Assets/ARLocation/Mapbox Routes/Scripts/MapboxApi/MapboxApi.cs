using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;

namespace ARLocation.MapboxRoutes
{
    using Vendor.SimpleJSON;

    // Kelas untuk parsing respons endpoint /suggest
    [System.Serializable]
    public class SearchBoxSuggestion
    {
        public string name;
        public string mapbox_id;
        public string feature_type;
        public string address;
        public string full_address;
        public string place_formatted;
        public Location coordinates; // Mencoba mem-parsing jika tersedia dari /suggest
        public string postcode;

        public static SearchBoxSuggestion Parse(JSONNode n)
        {
            var suggestion = new SearchBoxSuggestion
            {
                name = n["name"],
                mapbox_id = n["mapbox_id"],
                feature_type = n["feature_type"],
                address = n["address"],
                full_address = n["full_address"],
                place_formatted = n["place_formatted"],
                postcode = n["postcode"]
            };

            if (n["coordinates"] != null && n["coordinates"]["longitude"] != null && n["coordinates"]["latitude"] != null)
            {
                suggestion.coordinates = new Location(n["coordinates"]["latitude"].AsDouble, n["coordinates"]["longitude"].AsDouble);
            }
            else if (n["metadata"] != null && n["metadata"]["coordinates"] != null &&
                     n["metadata"]["coordinates"]["longitude"] != null && n["metadata"]["coordinates"]["latitude"] != null)
            {
                suggestion.coordinates = new Location(n["metadata"]["coordinates"]["latitude"].AsDouble, n["metadata"]["coordinates"]["longitude"].AsDouble);
            }
            else if (n["lat"] != null && n["lon"] != null)
            {
                suggestion.coordinates = new Location(n["lat"].AsDouble, n["lon"].AsDouble);
            }
            return suggestion;
        }
        public override string ToString()
        {
            return $"Suggestion{{ name = {name}, mapbox_id = {mapbox_id}, feature_type = {feature_type}, coordinates = {coordinates} }}";
        }
    }

    [System.Serializable]
    public class SearchBoxResponse
    {
        public List<SearchBoxSuggestion> suggestions = new List<SearchBoxSuggestion>();
        public string attribution;
        public string response_id;

        public static SearchBoxResponse Parse(string json) { return Parse(JSON.Parse(json)); }
        public static SearchBoxResponse Parse(JSONNode n)
        {
            var response = new SearchBoxResponse { attribution = n["attribution"], response_id = n["response_id"] };
            var suggestionsArray = n["suggestions"].AsArray;
            if (suggestionsArray != null)
            {
                foreach (JSONNode suggestionNode in suggestionsArray) { response.suggestions.Add(SearchBoxSuggestion.Parse(suggestionNode)); }
            }
            return response;
        }
    }

    // Kelas untuk parsing respons endpoint /retrieve
    [System.Serializable]
    public class RetrievedFeatureGeometry
    {
        public string type; // e.g., "Point"
        public List<double> coordinates; // [longitude, latitude]

        public static RetrievedFeatureGeometry Parse(JSONNode n)
        {
            var geometry = new RetrievedFeatureGeometry { type = n["type"] };
            var coordsArray = n["coordinates"].AsArray;
            if (coordsArray != null)
            {
                geometry.coordinates = new List<double>();
                foreach (JSONNode coordNode in coordsArray)
                {
                    geometry.coordinates.Add(coordNode.AsDouble);
                }
            }
            return geometry;
        }
    }

    [System.Serializable]
    public class RetrievedFeatureProperties
    {
        public string name;
        public string mapbox_id;
        public string feature_type;
        public string address;
        public string full_address;
        public string place_formatted;
        public string postcode;
        // Tambahkan properti lain yang mungkin dibutuhkan dari respons /retrieve
        public static RetrievedFeatureProperties Parse(JSONNode n)
        {
            return new RetrievedFeatureProperties
            {
                name = n["name"],
                mapbox_id = n["mapbox_id"],
                feature_type = n["feature_type"],
                address = n["address"],
                full_address = n["full_address"],
                place_formatted = n["place_formatted"],
                postcode = n["postcode"]
            };
        }
    }

    [System.Serializable]
    public class RetrievedFeature // Struktur respons utama dari /retrieve adalah GeoJSON Feature tunggal
    {
        public string type; // "Feature"
        public RetrievedFeatureProperties properties;
        public RetrievedFeatureGeometry geometry;
        public string attribution;

        public static RetrievedFeature Parse(string json) { return Parse(JSON.Parse(json)); }
        public static RetrievedFeature Parse(JSONNode n)
        {
            // Respons /retrieve biasanya adalah array dengan satu fitur utama
            // Jika API mengembalikan satu objek fitur langsung di root, sesuaikan ini.
            // Biasanya, strukturnya adalah {"type": "FeatureCollection", "features": [ { THE FEATURE } ]}
            // atau langsung objek {"type": "Feature", ...}
            // Berdasarkan dokumentasi SearchBox /retrieve, itu mengembalikan SATU Feature object.
            if (n["type"] != null && n["type"].Value == "Feature")
            {
                return new RetrievedFeature
                {
                    type = n["type"],
                    properties = RetrievedFeatureProperties.Parse(n["properties"]),
                    geometry = RetrievedFeatureGeometry.Parse(n["geometry"]),
                    attribution = n["attribution"] // Biasanya attribution ada di level atas SearchBoxResponse, bukan per fitur di retrieve. Periksa respons API.
                };
            }
            else if (n["features"] != null && n["features"].AsArray.Count > 0)
            { // Jika dibungkus dalam FeatureCollection
                JSONNode featureNode = n["features"].AsArray[0];
                return new RetrievedFeature
                {
                    type = featureNode["type"],
                    properties = RetrievedFeatureProperties.Parse(featureNode["properties"]),
                    geometry = RetrievedFeatureGeometry.Parse(featureNode["geometry"]),
                    attribution = n["attribution"] // Ambil attribution dari level atas jika ada
                };
            }
            Debug.LogError("[MapboxApi#RetrievedFeature.Parse] Respons /retrieve tidak memiliki struktur Feature yang diharapkan.");
            return null;
        }
    }


    [System.Serializable]
    public class MapboxApi
    {
        public string AccessToken;
        public MapboxApiLanguage Language;

        public RouteResponse QueryRouteResult { get; private set; }
        public GeocodingResponse QueryLocalResult { get; private set; } // Untuk endpoint geocoding lama
        public SearchBoxResponse QuerySuggestResult { get; private set; } // Untuk endpoint /suggest baru
        public RetrievedFeature QueryRetrieveResult { get; private set; } // Untuk endpoint /retrieve baru

        public string ErrorMessage { get; private set; }

        public MapboxApi(string token, MapboxApiLanguage lang = MapboxApiLanguage.English_US)
        {
            AccessToken = token;
            Language = lang;
        }

        public IEnumerator QueryLocal(string text, bool verbose = false)
        {
            var url = buildOldQueryLocalUrl(text);
            ErrorMessage = null;
            QueryLocalResult = null;
            if (verbose) Debug.Log($"[MapboxApi#QueryLocal-OLD]: {url}");

            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (Utils.Misc.WebRequestResultIsError(req))
                {
                    Debug.LogError("[MapboxApi#QueryLocal-OLD]: Error -> " + req.error);
                    ErrorMessage = req.error;
                }
                else
                {
                    if (req.responseCode != 200)
                    {
                        Debug.LogError("[MapboxApi#QueryLocal-OLD]: Error -> " + req.downloadHandler.text);
                        var node = JSON.Parse(req.downloadHandler.text);
                        ErrorMessage = node["message"]?.Value ?? "Unknown error";
                    }
                    else
                    {
                        if (verbose) Debug.Log("[MapboxApi#QueryLocal-OLD]: Success -> " + req.downloadHandler.text);
                        QueryLocalResult = GeocodingResponse.Parse(req.downloadHandler.text);
                    }
                }
            }
        }
        string buildOldQueryLocalUrl(string query)
        {
            var url = Uri.EscapeUriString($"https://api.mapbox.com/geocoding/v5/mapbox.places/{query}.json?access_token={AccessToken}");
            url += $"&language={Language.GetCode()}";
            return url;
        }

        public IEnumerator QuerySuggest(string query, string proximity, string types, int limit, string countryCode, bool autocomplete, string sessionToken, bool verbose = false)
        {
            var url = buildQuerySuggestUrl(query, proximity, types, limit, countryCode, autocomplete, sessionToken);
            ErrorMessage = null;
            QuerySuggestResult = null;
            if (verbose) Debug.Log($"[MapboxApi#QuerySuggest]: {url}");

            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (Utils.Misc.WebRequestResultIsError(req))
                {
                    Debug.LogError($"[MapboxApi#QuerySuggest]: Network Error -> {req.error} for URL: {url}");
                    ErrorMessage = req.error;
                }
                else
                {
                    if (verbose)
                    {
                        Debug.Log($"[MapboxApi#QuerySuggest]: Response Code: {req.responseCode}");
                        Debug.Log($"[MapboxApi#QuerySuggest]: Response Text: {req.downloadHandler.text}");
                    }
                    if (req.responseCode != 200)
                    {
                        Debug.LogError($"[MapboxApi#QuerySuggest]: API Error ({req.responseCode}) -> {req.downloadHandler.text}");
                        try
                        {
                            var node = JSON.Parse(req.downloadHandler.text);
                            ErrorMessage = node["message"]?.Value ?? $"API Error {req.responseCode}";
                        }
                        catch (Exception e) { ErrorMessage = $"API Error {req.responseCode}, failed to parse error message: {e.Message}"; }
                    }
                    else
                    {
                        try
                        {
                            QuerySuggestResult = SearchBoxResponse.Parse(req.downloadHandler.text);
                            if (verbose) Debug.Log($"[MapboxApi#QuerySuggest]: Success, Parsed {QuerySuggestResult.suggestions.Count} suggestions.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[MapboxApi#QuerySuggest]: Failed to parse successful response: {e.Message}\nResponse Text: {req.downloadHandler.text}");
                            ErrorMessage = "Failed to parse response: " + e.Message;
                        }
                    }
                }
            }
        }
        string buildQuerySuggestUrl(string query, string proximity, string types, int limit, string countryCode, bool autocomplete, string sessionToken)
        {
            string baseUrl = "https://api.mapbox.com/search/searchbox/v1/suggest";
            var sb = new System.Text.StringBuilder(baseUrl);
            sb.Append($"?q={Uri.EscapeDataString(query)}&access_token={AccessToken}&language={Language.GetCode()}&session_token={sessionToken}");
            if (!string.IsNullOrEmpty(proximity) && proximity != "0,0") sb.Append($"&proximity={proximity}");
            if (!string.IsNullOrEmpty(types)) sb.Append($"&types={Uri.EscapeDataString(types)}");
            if (limit > 0) sb.Append($"&limit={limit}");
            if (!string.IsNullOrEmpty(countryCode)) sb.Append($"&country={Uri.EscapeDataString(countryCode)}");
            return sb.ToString();
        }

        public IEnumerator RetrieveSuggestionDetails(string mapboxId, string sessionToken, bool verbose = false)
        {
            var url = $"https://api.mapbox.com/search/searchbox/v1/retrieve/{mapboxId}?access_token={AccessToken}&session_token={sessionToken}";
            ErrorMessage = null;
            QueryRetrieveResult = null;
            if (verbose) Debug.Log($"[MapboxApi#RetrieveSuggestionDetails]: {url}");

            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (Utils.Misc.WebRequestResultIsError(req))
                {
                    Debug.LogError($"[MapboxApi#RetrieveSuggestionDetails]: Network Error -> {req.error} for URL: {url}");
                    ErrorMessage = req.error;
                }
                else
                {
                    if (verbose)
                    {
                        Debug.Log($"[MapboxApi#RetrieveSuggestionDetails]: Response Code: {req.responseCode}");
                        Debug.Log($"[MapboxApi#RetrieveSuggestionDetails]: Response Text: {req.downloadHandler.text}");
                    }
                    if (req.responseCode != 200)
                    {
                        Debug.LogError($"[MapboxApi#RetrieveSuggestionDetails]: API Error ({req.responseCode}) -> {req.downloadHandler.text}");
                        try
                        {
                            var node = JSON.Parse(req.downloadHandler.text);
                            ErrorMessage = node["message"]?.Value ?? $"API Error {req.responseCode}";
                        }
                        catch (Exception e) { ErrorMessage = $"API Error {req.responseCode}, failed to parse error message: {e.Message}"; }
                    }
                    else
                    {
                        try
                        {
                            QueryRetrieveResult = RetrievedFeature.Parse(req.downloadHandler.text);
                            if (verbose) Debug.Log($"[MapboxApi#RetrieveSuggestionDetails]: Success, Parsed feature: {QueryRetrieveResult?.properties?.name}");
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"[MapboxApi#RetrieveSuggestionDetails]: Failed to parse successful response: {e.Message}\nResponse Text: {req.downloadHandler.text}");
                            ErrorMessage = "Failed to parse retrieve response: " + e.Message;
                        }
                    }
                }
            }
        }

        public IEnumerator QueryRoute(Location from, Location to, bool alternatives = false, bool verbose = false)
        {
            var url = buildQueryRouteUrl(from, to, alternatives);
            Debug.Log($"[MapboxApi#QueryRoute] Request URL: {url}");
            ErrorMessage = null;
            QueryRouteResult = null;

            using (var req = UnityWebRequest.Get(url))
            {
                yield return req.SendWebRequest();
                if (Utils.Misc.WebRequestResultIsError(req))
                {
                    Debug.LogError("[MapboxApi#QueryRoute]: Error -> " + req.error);
                    ErrorMessage = req.error;
                }
                else
                {
                    if (verbose)
                    {
                        Debug.Log("[MapboxApi#QueryRoute]: Success -> " + req.downloadHandler.text);
                        Debug.Log("[MapboxApi#QueryRoute]: Success -> " + req.responseCode);
                    }
                    try
                    {
                        QueryRouteResult = RouteResponse.Parse(req.downloadHandler.text);
                        if (QueryRouteResult.Code != "Ok")
                        {
                            ErrorMessage = QueryRouteResult.Code;
                            QueryRouteResult = null;
                        }
                        else
                        {
                            if (verbose) Debug.Log("[MapboxApi#QueryRoute]: Parsed result -> " + QueryRouteResult);
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[MapboxApi#QueryRoute]: Failed to parse route response: {e.Message}\nResponse Text: {req.downloadHandler.text}");
                        ErrorMessage = "Failed to parse route response: " + e.Message;
                    }
                }
            }
        }
        string buildQueryRouteUrl(Location from, Location to, bool alternatives)
        {
            string url = "https://api.mapbox.com/directions/v5/mapbox/walking/";
            string alt = alternatives ? "true" : "false";
            var fromLat = from.Latitude.ToString(CultureInfo.InvariantCulture);
            var fromLon = from.Longitude.ToString(CultureInfo.InvariantCulture);
            var toLat = to.Latitude.ToString(CultureInfo.InvariantCulture);
            var toLon = to.Longitude.ToString(CultureInfo.InvariantCulture);
            var langCode = Language.GetCode();
            url += $"{fromLon}%2C{fromLat}%3B{toLon}%2C{toLat}?alternatives={alt}&geometries=geojson&steps=true&access_token={AccessToken}&language={langCode}";
            return url;
        }
    }
}