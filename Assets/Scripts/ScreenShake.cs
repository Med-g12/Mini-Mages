using UnityEngine;

[DefaultExecutionOrder(10000)]
public class ScreenShake : MonoBehaviour
{
    private static ScreenShake instance;

    private float shakeTimeRemaining;
    private float shakeDuration;
    private float shakeStrength;
    private Vector3 lastOffset;

    private void Awake()
    {
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void LateUpdate()
    {
        transform.localPosition -= lastOffset;
        lastOffset = Vector3.zero;

        if (shakeTimeRemaining <= 0f)
        {
            return;
        }

        shakeTimeRemaining -= Time.deltaTime;
        float fade = shakeDuration > 0f ? shakeTimeRemaining / shakeDuration : 0f;
        lastOffset = new Vector3(
            Random.Range(-1f, 1f),
            Random.Range(-1f, 1f),
            0f
        ) * shakeStrength * Mathf.Clamp01(fade);

        transform.localPosition += lastOffset;
    }

    public static void Shake(float duration, float strength)
    {
        ScreenShake shaker = GetOrCreate();
        if (shaker == null)
        {
            return;
        }

        shaker.shakeDuration = Mathf.Max(duration, 0.01f);
        shaker.shakeTimeRemaining = Mathf.Max(shaker.shakeTimeRemaining, duration);
        shaker.shakeStrength = Mathf.Max(shaker.shakeStrength, strength);
    }

    private static ScreenShake GetOrCreate()
    {
        if (instance != null)
        {
            return instance;
        }

        Camera camera = Camera.main;
        if (camera == null)
        {
            return null;
        }

        instance = camera.GetComponent<ScreenShake>();
        if (instance == null)
        {
            instance = camera.gameObject.AddComponent<ScreenShake>();
        }

        return instance;
    }
}
