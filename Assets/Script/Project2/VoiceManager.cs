using UnityEngine;
using Meta.WitAi;
using Meta.WitAi.Json;
using Meta.WitAi.Requests;
using Oculus.Voice;

public class WitPromptExtractor : MonoBehaviour
{
    
    [SerializeField] GameObject magicEffect;
    [SerializeField] private AppVoiceExperience voiceExperience;
    [SerializeField] private LeonardoAPI leonardoClient;
    
    private const string CREATE_ASSET_INTENT = "createAsset";
    private string _spellPrompt;
    private bool spellCharging = false;
    private bool spellActive = false;
    private float chargeTime = 0f;
    private const float CHARGE_REQUIRED = 3f;
    private bool requestCanceled = false;
    public bool loadingModel = false;
    
    private ParticleSystem particleSystem;
    private ParticleSystem.MainModule mainModule;
    
    // Colors for interpolation
    private Color darkPurple = new Color(0.3f, 0f, 0.3f, 1f);
    private Color brightPurple = new Color(1f, 0f, 1f, 1f);

    private void Start()
    {
        if (magicEffect != null)
        {
            particleSystem = magicEffect.GetComponent<ParticleSystem>();
            if (particleSystem != null)
            {
                mainModule = particleSystem.main;
            }
        }
        
        if (voiceExperience == null)
        {
            voiceExperience = GetComponent<AppVoiceExperience>();
        }
    }
    
    public void ButtonPressed()
    {
        Debug.Log("BUTTON PRESSED");
        if (!loadingModel)
        {
            spellCharging = true;
            requestCanceled = false;
        }
    }

    private void RestartSpell()
    {
        magicEffect.SetActive(false);
        DeactivateVoiceExperience();
        spellCharging = false;
        chargeTime = 0;
        spellActive = false;

    }
    
    public void ButtonReleased()
    {
        if (!loadingModel)
        {
            Debug.Log("BUTTON RELEASED");
            RestartSpell();

            if (chargeTime < CHARGE_REQUIRED)
            {
                requestCanceled = true;
            }
        }
    }

    public void OnComplete(VoiceServiceRequest request)
    {
        if (requestCanceled)
        {
            Debug.Log("Request was canceled - ignoring completion");
            return;
        }
        
        if (request?.ResponseData == null) 
        {
            Debug.LogWarning("No response data received");
            return;
        }

        WitResponseNode response = request.ResponseData;
        
        var intents = response["intents"].AsArray;
        if (intents.Count == 0) 
        {
            Debug.Log("No intents found in response");
            return;
        }

        var topIntent = intents[0];
        if (topIntent["name"].Value != CREATE_ASSET_INTENT || 
            topIntent["confidence"].AsFloat < 0.7f)
        {
            Debug.Log($"Intent mismatch or low confidence: {topIntent["name"].Value} ({topIntent["confidence"].AsFloat})");
            return;
        }

        string originalText = response["text"].Value;

        var createCommand = response["entities"]["create_command:create_command"].AsArray;
        if (createCommand == null || createCommand.Count == 0) 
        {
            Debug.Log("No create_command entity found");
            return;
        }

        Debug.Log($"Detected entities: {response["entities"].ToString()}");

        int endPosition = createCommand[0]["end"].AsInt;
        if (endPosition >= originalText.Length) return;

        string prompt = originalText.Substring(endPosition).Trim();

        if (!string.IsNullOrEmpty(prompt))
        {
            Debug.Log($"Generated Prompt: {prompt}");
            RestartSpell();
            GenerateImage(prompt);
        }
    }
    
    private void GenerateImage(string prompt)
    {
        loadingModel = true;
        leonardoClient.generateImage(prompt);
    }
    
    private void ActivateVoiceExperience()
    {
        if (voiceExperience != null)
        {
            try
            {
                voiceExperience.Activate();
                Debug.Log("Voice recognition activated");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error activating voice experience: {e.Message}");
            }
        }
        else
        {
            Debug.LogError("Voice Experience reference is missing!");
        }
    }

    private void DeactivateVoiceExperience()
    {
        if (voiceExperience != null)
        {
            try
            {
                voiceExperience.Deactivate();
                Debug.Log("Voice recognition deactivated");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error deactivating voice experience: {e.Message}");
            }
        }
    }
    
    private void Update()
    {
        if (spellCharging)
        {
            chargeTime += Time.deltaTime;
            
            float t = Mathf.Clamp01(chargeTime / CHARGE_REQUIRED);
            Color currentColor = Color.Lerp(darkPurple, brightPurple, t);
            
            if (particleSystem != null)
            {
                var main = particleSystem.main;
                main.startColor = currentColor;
            }

            if (chargeTime >= CHARGE_REQUIRED && !spellActive)
            {
                magicEffect.SetActive(true);
                spellActive = true;
                Debug.Log("Spell fully charged!");
                ActivateVoiceExperience();
            }
        }
    }
}