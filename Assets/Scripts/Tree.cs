using UnityEngine;

namespace YOLOGRAM
{
	public class Tree : MonoBehaviour
	{
		[SerializeField] private GameObject highDetailInstance;
		[SerializeField] private GameObject lowDetailInstance;
		private int currentLod = -1;

		public void Initialize(GameObject highPrefab, GameObject lowPrefab, int lodLevel, float uniformScale, float yRotationDegrees)
		{
			if (highPrefab != null && highDetailInstance == null)
			{
				highDetailInstance = Instantiate(highPrefab, transform);
			}

			if (lowPrefab != null && lowDetailInstance == null)
			{
				lowDetailInstance = Instantiate(lowPrefab, transform);
			}

			transform.localScale = Vector3.one * uniformScale;
			transform.localRotation = Quaternion.Euler(0f, yRotationDegrees, 0f);

			SetLOD(lodLevel);
		}

		public void SetLOD(int lodLevel)
		{
			if (lodLevel == currentLod) return;
			currentLod = lodLevel;

			bool useHigh = lodLevel == 0;
			if (highDetailInstance != null) highDetailInstance.SetActive(useHigh);
			if (lowDetailInstance != null) lowDetailInstance.SetActive(!useHigh);
		}
	}
}


