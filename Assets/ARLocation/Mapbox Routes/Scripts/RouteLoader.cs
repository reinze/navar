using UnityEngine;
using System;
using System.Collections;
using Mapbox.Unity.Map; // Meskipun AbstractMap _map tidak digunakan di sini, using directive bisa tetap ada jika direncanakan.

namespace ARLocation.MapboxRoutes
{
    public class RouteLoader
    {
        MapboxApi mapbox;
        bool verbose;

        // AbstractMap _map; // Variabel ini dideklarasikan tetapi tidak pernah digunakan. Bisa dihapus jika tidak ada rencana penggunaan.
        private string error;
        private RouteResponse result;

        public string Error => error;
        public RouteResponse Result => result;

        public RouteLoader(MapboxApi api, bool verboseMode = false)
        {
            mapbox = api;
            verbose = verboseMode;

            if (api == null)
            {
                Debug.LogError("[RouteLoader]: api is null.");
            }

            // mapbox akan null jika api null, jadi pengecekan mapbox di sini mungkin redundan jika api sudah dicek.
            // Namun, tidak masalah untuk tetap ada.
            if (mapbox == null)
            {
                Debug.LogError("[RouteLoader]: mapbox is null.");
            }
        }

        public IEnumerator LoadRoute(RouteWaypoint start, RouteWaypoint end, Action<string, RouteResponse> callback)
        {
            // Debug.Log("LoadRoute Action called"); // Log ini bisa membantu jika diperlukan
            yield return LoadRoute(start, end);
            callback?.Invoke(error, result);
        }

        public IEnumerator LoadRoute(RouteWaypoint start, RouteWaypoint end)
        {
            Debug.Assert(mapbox != null, "[RouteLoader] MapboxApi instance is null in LoadRoute coroutine.");

            if (verbose)
            {
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Loading route from {start} to {end}", verbose);
            }

            // Resolve start location
            var resolver = new RouteWaypointResolveLocation(mapbox, start);
            yield return resolver.Resolve();

            if (resolver.IsError)
            {
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Failed to resolve start waypoint: {resolver.ErrorMessage}", true);
                error = resolver.ErrorMessage;
                result = null;
                yield break;
            }
            Location startLocation = resolver.result;

            // Resolve end location
            resolver = new RouteWaypointResolveLocation(mapbox, end);
            yield return resolver.Resolve();

            if (resolver.IsError)
            {
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Failed to resolve end waypoint: {resolver.ErrorMessage}", true);
                error = resolver.ErrorMessage;
                result = null;
                yield break;
            }
            Location endLocation = resolver.result;

            if (verbose)
            {
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", "Querying route...", verbose);
            }

            // Query the route from startLocation to endLocation
            yield return mapbox.QueryRoute(startLocation, endLocation, false, verbose);

            // PERBAIKAN: Gunakan property publik ErrorMessage (E besar)
            if (mapbox.ErrorMessage != null)
            {
                // PERBAIKAN: Gunakan property publik ErrorMessage (E besar) di sini juga
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Route query failed: {mapbox.ErrorMessage}", true);

                // PERBAIKAN: Tetapkan error dari mapbox, bukan resolver sebelumnya
                error = mapbox.ErrorMessage;
                result = null;
                yield break;
            }

            // Pengecekan QueryRouteResult tetap sama
            if (mapbox.QueryRouteResult == null)
            {
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Route result is null after query!", verbose);
                error = "[MapboxApi]: Route result is null after query!"; // Tambahkan pesan error yang lebih spesifik
                result = null;
                yield break; // Penting untuk keluar jika hasil null
            }
            else if (mapbox.QueryRouteResult.routes == null || mapbox.QueryRouteResult.routes.Count == 0)
            {
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Route result is empty (no routes found)!", verbose);
                error = "[MapboxApi]: No routes found between the locations."; // Pesan error yang lebih spesifik
                result = null;
                yield break; // Penting untuk keluar jika tidak ada rute
            }


            if (verbose)
            {
                // QueryLocalResult mungkin tidak relevan di sini jika kita hanya melakukan QueryRoute
                // Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Done! {mapbox.QueryLocalResult}", verbose); 
                Utils.Logger.LogFromMethod("RouteLoader", "LoadRoute", $"Route query successful. Number of routes: {mapbox.QueryRouteResult.routes.Count}", verbose);
            }

            error = null;
            result = mapbox.QueryRouteResult;
        }
    }
}
