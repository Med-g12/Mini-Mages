using UnityEngine;

public class FireBossHeatWave : MonoBehaviour
{
    public float heatWaveInterval = 3f;
    public float heatWaveDuration = 1.2f;
    public float heatWaveRadius = 2.5f;
    public float heatWaveDamage = 20f;
    
    private float nextWaveTime;
    private bool isWaveActive;
    
    private GameObject shadeObject;
    private SpriteRenderer shadeRenderer;
    private Vector3 baseLocalScale;
    private Transform player;
    private PlayerResources playerResources;
    
    void Start()
    {
        nextWaveTime = Time.time + heatWaveInterval;
        
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerResources = playerObj.GetComponent<PlayerResources>();
        }
        
        CreateShadeObject();
    }
    
    void CreateShadeObject()
    {
        shadeObject = new GameObject("HeatWaveShade");
        shadeObject.transform.SetParent(transform, false);
        shadeObject.transform.localPosition = Vector3.zero;
        
        shadeRenderer = shadeObject.AddComponent<SpriteRenderer>();
        shadeRenderer.sprite = CreateCircleSprite(128);
        shadeRenderer.color = new Color(1f, 0.15f, 0f, 0.45f); // Reddish
        shadeRenderer.sortingOrder = 15; // Behind the boss but visible
        
        // Scale the 128x128 sprite (which is 1.28x1.28 units at 100 PPU) to match radius
        float targetWorldDiameter = heatWaveRadius * 2f;
        float baseSpriteSize = 1.28f;
        float localScaleRequired = targetWorldDiameter / baseSpriteSize;
        
        // Counteract the parent's scale so the world scale matches perfectly
        if (transform.localScale.x != 0)
        {
            localScaleRequired /= transform.localScale.x;
        }
        
        baseLocalScale = new Vector3(localScaleRequired, localScaleRequired, 1f);
        shadeObject.transform.localScale = baseLocalScale;
        
        shadeObject.SetActive(false);
    }
    
    Sprite CreateCircleSprite(int size)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        Color clear = new Color(0, 0, 0, 0);
        Color fill = Color.white;
        float center = size / 2f;
        float radius = size / 2f;
        
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                {
                    // Create a soft ring gradient (more opaque at edges, transparent center)
                    float normalizedDist = dist / radius;
                    float alpha = Mathf.Pow(normalizedDist, 2.5f); // Curve for softer inner fade
                    
                    // Add a tiny bit of noise/softness to the very outer edge
                    if (normalizedDist > 0.95f)
                    {
                        alpha *= (1f - normalizedDist) / 0.05f;
                    }
                    
                    Color pixelColor = new Color(fill.r, fill.g, fill.b, fill.a * alpha);
                    tex.SetPixel(x, y, pixelColor);
                }
                else
                {
                    tex.SetPixel(x, y, clear);
                }
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
    }
    
    void Update()
    {
        if (!isWaveActive && Time.time >= nextWaveTime)
        {
            StartCoroutine(PerformHeatWave());
        }
    }
    
    System.Collections.IEnumerator PerformHeatWave()
    {
        isWaveActive = true;
        shadeObject.SetActive(true);
        
        float startTime = Time.time;
        
        while (Time.time - startTime < heatWaveDuration)
        {
            float elapsed = Time.time - startTime;
            float normalizedTime = elapsed / heatWaveDuration;
            
            // Rapid pulsing visual effect
            float pulse = (Mathf.Sin(elapsed * 15f) + 1f) * 0.5f;
            
            // Smooth fade in and out
            float fade = 1f;
            if (normalizedTime < 0.15f) fade = normalizedTime / 0.15f;
            else if (normalizedTime > 0.85f) fade = (1f - normalizedTime) / 0.15f;

            shadeRenderer.color = new Color(1f, 0.2f, 0f, (0.35f + 0.45f * pulse) * fade);
            
            // Subtle "breathing" scale to make it feel hot and turbulent
            float scalePulse = 1f + (pulse * 0.04f);
            shadeObject.transform.localScale = baseLocalScale * scalePulse;
            
            // Check player distance
            if (player != null && playerResources != null)
            {
                float dist = Vector2.Distance(transform.position, player.position);
                if (dist <= heatWaveRadius)
                {
                    // Continuous damage per second while inside the heat wave
                    playerResources.TakeDamage(heatWaveDamage * Time.deltaTime);
                }
            }
            
            yield return null;
        }
        
        shadeObject.SetActive(false);
        isWaveActive = false;
        nextWaveTime = Time.time + heatWaveInterval;
    }
}
