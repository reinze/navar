namespace Mapbox.Unity.MeshGeneration.Factories
{
	using UnityEngine;
	using Mapbox.Directions;
	using System.Collections.Generic;
	using System.Linq;
	using Mapbox.Unity.Map;
	using Data;
	using Modifiers;
	using Mapbox.Utils;
	using Mapbox.Unity.Utilities;
	using System.Collections;
	using System; // Tambahkan ini untuk menggunakan Action

	public class DirectionsFactory : MonoBehaviour
	{
		[SerializeField]
		AbstractMap _map;

		[SerializeField]
		MeshModifier[] MeshModifiers;
		[SerializeField]
		Material _material;

		[SerializeField]
		Transform[] _waypoints;
		private List<Vector3> _cachedWaypoints;

		[SerializeField]
		[Range(1, 10)]
		private float UpdateFrequency = 2;

		// ====================================================================================================
		// TAMBAHAN PENTING: Deklarasi event publik untuk memberi tahu ketika rute telah diperbarui
		// ====================================================================================================
		/// <summary>
		/// Event yang dipicu ketika rute baru berhasil digambar (setelah menerima respons dari Mapbox API).
		/// Menyediakan objek DirectionsResponse sebagai parameter.
		/// </summary>
		public event Action<DirectionsResponse> OnRouteUpdated;
		// ====================================================================================================


		private Directions _directions;
		private int _counter;

		GameObject _directionsGO;
		private bool _recalculateNext;

		protected virtual void Awake()
		{
			if (_map == null)
			{
				_map = FindObjectOfType<AbstractMap>();
			}
			_directions = MapboxAccess.Instance.Directions;
			_map.OnInitialized += Query;
			_map.OnUpdated += Query;
		}

		public void Start()
		{
			_cachedWaypoints = new List<Vector3>(_waypoints.Length);
			foreach (var item in _waypoints)
			{
				_cachedWaypoints.Add(item.position);
			}
			_recalculateNext = false;

			foreach (var modifier in MeshModifiers)
			{
				modifier.Initialize();
			}

			StartCoroutine(QueryTimer());
		}

		protected virtual void OnDestroy()
		{
			_map.OnInitialized -= Query;
			_map.OnUpdated -= Query;
		}

		public void Query() // Mengubah menjadi public agar bisa dipanggil dari luar
		{
			var count = _waypoints.Length;
			var wp = new Vector2d[count];
			for (int i = 0; i < count; i++)
			{
				wp[i] = _waypoints[i].GetGeoPosition(_map.CenterMercator, _map.WorldRelativeScale);
			}
			var _directionResource = new DirectionResource(wp, RoutingProfile.Driving);
			_directionResource.Steps = true;
			_directions.Query(_directionResource, HandleDirectionsResponse);
		}

		public IEnumerator QueryTimer()
		{
			while (true)
			{
				yield return new WaitForSeconds(UpdateFrequency);
				for (int i = 0; i < _waypoints.Length; i++)
				{
					if (_waypoints[i].position != _cachedWaypoints[i])
					{
						_recalculateNext = true;
						_cachedWaypoints[i] = _waypoints[i].position;
					}
				}

				if (_recalculateNext)
				{
					Query();
					_recalculateNext = false;
				}
			}
		}

		void HandleDirectionsResponse(DirectionsResponse response)
		{
			if (response == null || null == response.Routes || response.Routes.Count < 1)
			{
				// ====================================================================================================
				// PENTING: Panggil event OnRouteUpdated bahkan jika respons tidak valid/kosong,
				// tetapi dengan respons null atau kosong agar listener dapat menanganinya.
				// Ini penting untuk memastikan _isRerouting di RouteRerouter direset.
				// ====================================================================================================
				OnRouteUpdated?.Invoke(response); // Panggil event ini juga untuk kasus error/kosong
				return;
			}

			var meshData = new MeshData();
			var dat = new List<Vector3>();
			foreach (var point in response.Routes[0].Geometry)
			{
				dat.Add(Conversions.GeoToWorldPosition(point.x, point.y, _map.CenterMercator, _map.WorldRelativeScale).ToVector3xz());
			}

			var feat = new VectorFeatureUnity();
			feat.Points.Add(dat);

			foreach (MeshModifier mod in MeshModifiers.Where(x => x.Active))
			{
				mod.Run(feat, meshData, _map.WorldRelativeScale);
			}

			CreateGameObject(meshData);

			// ====================================================================================================
			// TAMBAHAN PENTING: Panggil event setelah rute berhasil digambar
			// ====================================================================================================
			OnRouteUpdated?.Invoke(response);
			// ====================================================================================================
		}

		GameObject CreateGameObject(MeshData data)
		{
			if (_directionsGO != null)
			{
				_directionsGO.Destroy();
			}
			_directionsGO = new GameObject("direction waypoint " + " entity");
			var mesh = _directionsGO.AddComponent<MeshFilter>().mesh;
			mesh.subMeshCount = data.Triangles.Count;

			mesh.SetVertices(data.Vertices);
			_counter = data.Triangles.Count;
			for (int i = 0; i < _counter; i++)
			{
				var triangle = data.Triangles[i];
				mesh.SetTriangles(triangle, i);
			}

			_counter = data.UV.Count;
			for (int i = 0; i < _counter; i++)
			{
				var uv = data.UV[i];
				mesh.SetUVs(i, uv);
			}

			mesh.RecalculateNormals();
			_directionsGO.AddComponent<MeshRenderer>().material = _material;
			return _directionsGO;
		}
	}
}
