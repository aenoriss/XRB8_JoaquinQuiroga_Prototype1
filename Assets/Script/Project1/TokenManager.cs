using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;

public enum TintColor 
{
    Red,
    Green
}

public class TokenManager : MonoBehaviour
{
    [SerializeField] private GameObject Face;
    [SerializeField] private GameObject Cross;
    [SerializeField] private GameObject Body;
    
    [SerializeField] private string TokenTicker;
    [SerializeField] private string TokenPrice;
    [SerializeField] private float TokenPriceChange;
    [SerializeField] private Texture TokenLogo;
    
    [SerializeField] private Material[] BodyMaterials;
    
    [SerializeField] private TMP_Text TokenTickerText;
    [SerializeField] private TMP_Text TokenPriceText;
    [SerializeField] private TMP_Text TokenPriceChangeText;
        
    
    [SerializeField] private GameObject TokenUI;
    [SerializeField] private float cooldownTime = 1.0f;
    
    [SerializeField] private HandGrabInteractable _handInteractable;
    [SerializeField] private HandGrabInteractable _handTapInteractable;

    [SerializeField] private string _tokenAddress;
    
    [SerializeField] private GameObject _tokenRing;
    
    [SerializeField] private ParticleSystem volumeParticles;
    
    private bool _isgrababble = false;
    private bool tokenStatus = false;
    private bool canActivate = true;
    private bool currentlyObserved = false;
    private List<Material> allMaterials = new List<Material>();
    private APIManager apiManager;
    
    private float lerpDuration = 0.5f;
    private bool isLerping = false;
    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Quaternion startRotation;
    private Quaternion targetRotation;
    private float lerpStartTime;
    
    //-1 if not in carrousel
    private Transform carrouselPos;
    private bool currentlyGrabbed = false;
    private bool particlesRendered = false;
    
    private Material ringMaterial;
    private float _tokenVolume;
    private float _tokenTXBalBuys;
    private float _tokenTXBalSells;
    public int _carrouselId;
    
    void Awake()
    {
        apiManager = FindObjectOfType<APIManager>();
        ringMaterial = _tokenRing.GetComponent<Renderer>().material;
        Body.GetComponent<MeshRenderer>().material = BodyMaterials[0];
    }

    private void Start()
    {
        _handInteractable.enabled = _isgrababble;
        _handTapInteractable.enabled = _isgrababble;
        volumeParticles.gameObject.SetActive(false);
        particlesRendered = false;
        
    }
    
    public void UpdateTransactionBalance(float buys, float sells)
    {
        if (_tokenRing == null || ringMaterial == null) return;
    
        float buyRatio = (float)buys / (buys + sells);
        ringMaterial.SetFloat("_BuyRatio", buyRatio);
    }
    
    public void SetupToken(string ticker, string address, string price, float priceChange, Texture logo, float balTXBuy, float balTXSells, float volume)
    {

        TokenPrice = price;
        TokenPriceChange = priceChange;
        TokenTicker = ticker;
        TokenTickerText.text = ticker;
        TokenPriceText.text = TokenPrice;
        TokenPriceChangeText.text = TokenPriceChange.ToString();
        _tokenAddress = address;
        _tokenVolume = volume;
        
        Debug.Log(":)"+balTXBuy + " " + balTXSells );
        UpdatePriceChangeText(TokenPriceChange);
        
        _tokenTXBalBuys = balTXBuy;
        _tokenTXBalSells = balTXSells;
        _tokenAddress = address;
        
        if (_tokenRing != null)
        {
            UpdateTransactionBalance(balTXBuy, balTXSells);
        }
        
        Debug.Log($"SETUP TOKEN - Address being set: {address}");
        Debug.Log($"SETUP TOKEN - Stored address right after setting: {_tokenAddress}");        
        MeshRenderer meshRenderer = Face.GetComponent<MeshRenderer>();

        if (meshRenderer != null && logo != null)
        {
            Material materialInstance = new Material(meshRenderer.material);
            materialInstance.mainTexture = logo;
            
            Face.gameObject.GetComponent<MeshRenderer>().material = materialInstance;
            Cross.gameObject.GetComponent<MeshRenderer>().material = materialInstance;

            transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }
        
        Debug.Log("New material mainTexture is null: " + (meshRenderer.material.mainTexture == null));
    }

    public void updateCarrouselPosition(Transform newPosition, int id)
    {
        Debug.Log($"ENTERING updateCarrouselPosition - Current stored address: {_tokenAddress}");
        
        if (carrouselPos != null)
        {
            startPosition = transform.position;
            targetPosition = newPosition.position;
            startRotation = transform.rotation;
            targetRotation = newPosition.rotation;
            lerpStartTime = Time.time;
            isLerping = true;
            
            Debug.Log("THIS IS THE CARROUSEL ID:"+ id);
            
        
            StartCoroutine(LerpToPosition());
        }
        carrouselPos = newPosition;
        particlesRendered = false;
        _carrouselId = id;
        
        if (id == 2)
        {
            Debug.Log($"BEFORE setWatchedToken - Address about to be sent: {_tokenAddress}");
            TokenUI.gameObject.SetActive(true); 
            apiManager.setWatchedToken(_tokenAddress);
            apiManager.GetFinancialData();
            _tokenRing.SetActive(true);
            apiManager.showWatchedTokenUpdate = false;
            UpdateVolumeParticles(_tokenVolume, TokenPriceChange);
            volumeParticles.gameObject.SetActive(true);
            
            Debug.Log($"AFTER setWatchedToken - Address that was sent: {_tokenAddress}");

            currentlyObserved = true;
            StartCoroutine(ReenableInteractables());
        }
        else
        {
            currentlyObserved = false;
            _handInteractable.enabled = false;
            _handTapInteractable.enabled = false;
            TokenUI.gameObject.SetActive(false); 
            _tokenRing.SetActive(false);
            volumeParticles.gameObject.SetActive(false);
            Body.GetComponent<MeshRenderer>().material = BodyMaterials[0];
            
        }
        
        SetObjectTransparency(id);
    }
    
    private void UpdatePriceChangeText(float priceChange)
    {
        
        Debug.Log("priceChangepriceChange"+priceChange);
        TokenPriceChangeText.text = priceChange.ToString() + "%";

        if (priceChange < 0)
        {
            TokenPriceChangeText.color = Color.red;
        }
        else
        {
            TokenPriceChangeText.color = Color.green;
        }

        if (_carrouselId == 2)
        {
            if (priceChange < -15)
            {
                if (priceChange >= -40)
                {
                    Body.GetComponent<MeshRenderer>().material = BodyMaterials[1];
                } else {
                    Body.GetComponent<MeshRenderer>().material = BodyMaterials[2];
                }
            }
            else
            {
                if (priceChange > 15)
                {
                    if (priceChange <= 60)
                    {
                        Body.GetComponent<MeshRenderer>().material = BodyMaterials[3];
                    } else {
                        Body.GetComponent<MeshRenderer>().material = BodyMaterials[4];
                    }
                }
            }
            
        } else
        {
            Body.GetComponent<MeshRenderer>().material = BodyMaterials[0];
        }
    }

    private IEnumerator ReenableInteractables()
    {
        yield return null;
        
        _handInteractable.enabled = false;
        _handTapInteractable.enabled = false;
    
        yield return null;
    
        _handInteractable.enabled = true;
        _handTapInteractable.enabled = true;
    }
    
private void UpdateVolumeParticles(float volume, float priceChange)
{
    if (volumeParticles == null) return;

    var main = volumeParticles.main;
    var emission = volumeParticles.emission;
    var shape = volumeParticles.shape;
    var velocityOverLifetime = volumeParticles.velocityOverLifetime;
    var renderer = volumeParticles.GetComponent<ParticleSystemRenderer>();

    // Log scale transformation for volume
    float normalizedVolume = volume > 0 ? 
        Mathf.Log10(Mathf.Clamp(volume, 1f, 5000000f)) / Mathf.Log10(5000000f) : 
        0f;

    // Base values for particles with more reduced scaling
    float emissionRate = Mathf.Lerp(1f, 30f, normalizedVolume); // Further reduced emission rate scaling
    emissionRate = Mathf.Max(emissionRate, 2f); // Minimum emission rate is now 2 for low-volume tokens

    float speed = Mathf.Lerp(1.5f, 4f, normalizedVolume); // Reduced particle speed scaling
    float size = Mathf.Lerp(0.3f, 0.6f, normalizedVolume); // Further increased size range for particles

    // Configure shape for orbital emission
    shape.shapeType = ParticleSystemShapeType.Circle;
    shape.radius = 1.0f;
    shape.radiusThickness = 0.01f;
    shape.rotation = new Vector3(90f, 0f, 0f); // Set shape rotation to face upwards
    
    // Align with token
    volumeParticles.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

    // Configure orbital motion
    velocityOverLifetime.enabled = true;
    velocityOverLifetime.orbitalZ = new ParticleSystem.MinMaxCurve(Mathf.Lerp(1f, 3f, normalizedVolume)); // Slower for smaller volumes

    // Main particle settings
    main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2f);
    main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.3f, speed); // Slower speeds for small volumes
    main.simulationSpace = ParticleSystemSimulationSpace.World;
    main.maxParticles = Mathf.RoundToInt(Mathf.Lerp(20f, 300f, normalizedVolume)); // Reduced range for max particles

    // Color based on price direction and volume intensity
    float transparency = Mathf.Lerp(0.8f, 1f, normalizedVolume); // Increased minimum transparency to 0.8
    
    Color baseColor;
    if (priceChange < 0) {
        // For negative price: lerp from opaque to vivid red based on volume
        baseColor = Color.Lerp(
            new Color(0.5f, 0f, 0f, transparency), // opaque/darker red
            new Color(1f, 0f, 0f, transparency),   // vivid red
            normalizedVolume
        );
    } else {
        // For positive price: lerp from opaque to vivid green based on volume
        baseColor = Color.Lerp(
            new Color(0f, 0.5f, 0f, transparency), // opaque/darker green
            new Color(0f, 1f, 0f, transparency),   // vivid green
            normalizedVolume
        );
    }
    
    main.startColor = baseColor;

    // Configure renderer for proper VR visibility
    renderer.renderMode = ParticleSystemRenderMode.Billboard;
    renderer.sortMode = ParticleSystemSortMode.Distance;
    renderer.allowRoll = true;
    renderer.minParticleSize = 0.0001f;
    renderer.maxParticleSize = 1.5f; // Increased from 1.0f to 1.5f

    // Set material properties for proper depth handling
    if (renderer.material != null)
    {
        renderer.material.SetInt("_ZWrite", 1);
        renderer.material.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.LessEqual);
        renderer.material.renderQueue = 2000;
    }

    // Configure trails
    var trails = volumeParticles.trails;
    trails.enabled = true;
    trails.mode = ParticleSystemTrailMode.PerParticle;  // Changed to PerParticle mode
    trails.ratio = 1.0f;
    trails.lifetime = 0.2f;
    trails.widthOverTrail = new ParticleSystem.MinMaxCurve(1.0f, new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(1f, 0f)
    ));
    trails.inheritParticleColor = true;
    trails.dieWithParticles = true;
    trails.textureMode = ParticleSystemTrailTextureMode.Stretch;
    trails.minVertexDistance = 0.01f;
    trails.worldSpace = true;

    // Particle size and emission
    main.startSize = new ParticleSystem.MinMaxCurve(size, size * 2.5f); // Increased multiplier for size
    emission.rateOverTime = emissionRate;

    if (!volumeParticles.isPlaying)
    {
        volumeParticles.Play();
    }
}
   

    public void IsTokenGrabbed(bool isGrabbed)
    {
        currentlyGrabbed = isGrabbed;

        if (isGrabbed)
        {
            TokenUI.gameObject.SetActive(false); 
        }
        else
        {
            if (carrouselPos != null)
            {
                Debug.Log($"TOKEN WAS RELEASED - Current Position: {transform.position}, Target Position: {carrouselPos}");
                StartCoroutine(ReleaseWithDelay());
            }
        }
    }
    
    private IEnumerator ReleaseWithDelay()
    {
        yield return new WaitForSeconds(0.3f);
    
        if (carrouselPos != null)
        {
            transform.position = carrouselPos.position;
            transform.rotation = carrouselPos.rotation;
        }
    }
    
    
    public void SwitchTokenMenu(bool isHovering)
    {
        Debug.Log("TOKEN SELECTED"+tokenStatus);
        
        if (canActivate && !currentlyGrabbed)
        {
            tokenStatus = !tokenStatus;
            TokenUI.gameObject.SetActive(tokenStatus);
        }

        if (isHovering)
        {
            canActivate = false;
        }
        else
        {
            canActivate = true;
        }
    }
    
    private void SetObjectTransparency(int id)
    {
        float alpha = 1f;

        if (id == 2)
        {
            alpha = 1f;
        }
        else if (id == 0 || id == 4)
        {
            alpha = 0.2f;
        }
        else if (id == 1 || id == 3)
        {
            alpha = 0.5f;
        }
        
        allMaterials.Clear();
        
        foreach (MeshRenderer renderer in GetComponentsInChildren<MeshRenderer>())
        {
            allMaterials.Add(renderer.material);
        }

        foreach (Material material in allMaterials)
        {
            if (material.shader.name == "Custom/TransactionBalanceRing")
                continue;
    
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = 3000;

            Color color = material.color;
            color.a = alpha;
            material.color = color;
        }
    }
    
    private IEnumerator LerpToPosition()
    {
        while (isLerping)
        {
            float timeSinceStart = Time.time - lerpStartTime;
            float percentageComplete = timeSinceStart / lerpDuration;
            
            float smoothPercentage = percentageComplete * percentageComplete * (3f - 2f * percentageComplete);
        
            transform.position = Vector3.Lerp(startPosition, targetPosition, smoothPercentage);
            transform.rotation = Quaternion.Lerp(startRotation, targetRotation, smoothPercentage);
        
            if (percentageComplete >= 1.0f)
            {
                isLerping = false;
                transform.position = targetPosition;
                transform.rotation = targetRotation;
            }
        
            yield return null;
        }
    }
    
    private void Update()
    {
        if (currentlyObserved && apiManager.showWatchedTokenUpdate)
        {
            TokenPrice = apiManager.currentlyWatchedTokenPrice;
            TokenPriceChange = apiManager.currentlyWatchedTokenPriceChange;
            _tokenTXBalBuys = apiManager.currentlyWatchedTokenTXBalBuys;
            _tokenTXBalSells = apiManager.currentlyWatchedTokenTXBalSells;

            if (!particlesRendered)
            {
                _tokenVolume = apiManager.currentlyWatchedTokenVolume;
                UpdateVolumeParticles(_tokenVolume, TokenPriceChange);
                volumeParticles.gameObject.SetActive(true);
                particlesRendered = true;

            }
  
            TokenPriceText.text = $"${TokenPrice}";
            UpdatePriceChangeText(TokenPriceChange);
            UpdateTransactionBalance(_tokenTXBalBuys, _tokenTXBalSells);
        }
    }
}
