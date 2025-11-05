using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[DisallowMultipleComponent]
public class TopoApiClient : MonoBehaviour
{
    [Tooltip("Endpoint base URL. The test dataset is fine for development.")]
    public string baseUrl = "https://api.opentopodata.org/v1/test-dataset";

    // Example: call this to fetch elevation for a single coordinate
    public void FetchElevationExample()
    {
        StartCoroutine(FetchElevationCoroutine(56f, 123f, elevation =>
        {
            Debug.Log($"Elevation at (56,123) = {elevation}");
        }));
    }

    // Coroutine that fetches elevation for a single location and returns it via callback
    public IEnumerator FetchElevationCoroutine(float lat, float lng, System.Action<float> onComplete)
    {
        // Ensure decimal point uses invariant culture so the URL uses '.' not ',' on some locales
        string latStr = lat.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string lngStr = lng.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string url = $"{baseUrl}?locations={latStr},{lngStr}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            req.timeout = 10; // seconds
            yield return req.SendWebRequest();

#if UNITY_2020_1_OR_NEWER
            if (req.result == UnityWebRequest.Result.ConnectionError || req.result == UnityWebRequest.Result.ProtocolError)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"Topo API request error: {req.error} (URL: {url})");
                onComplete?.Invoke(float.NaN);
                yield break;
            }

            string json = req.downloadHandler.text;

            if (string.IsNullOrEmpty(json))
            {
                Debug.LogError("Topo API returned empty response");
                onComplete?.Invoke(float.NaN);
                yield break;
            }

            // Deserialize response into structure matching OpenTopoData response, e.g.:
            // { "results": [ { "location": { "lat": 56, "lng": 123 }, "elevation": 123.45 } ], "status":"OK" }
            Root root = null;
            try
            {
                root = JsonUtility.FromJson<Root>(json);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Failed to parse Topo API JSON: {ex.Message}\nJSON:\n{json}");
            }

            if (root == null || root.results == null || root.results.Length == 0)
            {
                Debug.LogError($"Topo API: unexpected response or no results. JSON:\n{json}");
                onComplete?.Invoke(float.NaN);
                yield break;
            }

            float elevation = root.results[0].elevation;
            onComplete?.Invoke(elevation);
        }
    }

    // Small data classes for JsonUtility
    [System.Serializable]
    private class Root
    {
        public Result[] results;
        public string status;
    }

    [System.Serializable]
    private class Result
    {
        public Location location;
        public float elevation;
        public float? uncertainty; // sometimes present
    }

    [System.Serializable]
    private class Location
    {
        public float lat;
        public float lng;
    }
}
