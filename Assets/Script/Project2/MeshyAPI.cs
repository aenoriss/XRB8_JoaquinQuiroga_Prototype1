using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

public class StabilityAPI : MonoBehaviour
{
    private const string API_KEY = "sk-7G7J6YCPmysA6dzvwXMYR3vuVI3kkYOQxPSdX230xsTVJDeE";
    private const string BASE_URL = "https://api.stability.ai/v2beta/3d/stable-fast-3d";

    [SerializeField] private Transform spawnPoint;
    [SerializeField] GameObject loadingCircle;
    [SerializeField] WitPromptExtractor WitPromptExtractor;
    [SerializeField] private float rotationSpeed = 30f;
    
    private GameObject currentModel;
    private Material previewMaterial;

    [System.Serializable]
    private class StabilityResponse
    {
        public string id;
        public string name;
        public List<string> errors;
    }

    private void Awake()
    {
        previewMaterial = new Material(Shader.Find("Unlit/Color"));
        previewMaterial.color = Color.white;
    }

    private void OnDestroy()
    {
        if (previewMaterial != null)
        {
            Destroy(previewMaterial);
        }
    }

    public void Generate3DModel(Texture texture, Action<GameObject> onComplete = null)
    {
        loadingCircle.SetActive(true);
        StartCoroutine(GenerateModelCoroutine(texture, onComplete));
    }

    private void ApplyPreviewMaterial(GameObject model)
    {
        if (model == null) return;

        foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
        {
            Material[] materials = new Material[renderer.materials.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                materials[i] = previewMaterial;
            }
            renderer.materials = materials;
        }
    }

    private IEnumerator GenerateModelCoroutine(Texture texture, Action<GameObject> onComplete = null)
{
    // Convert Texture to Texture2D
    Texture2D texture2D = null;
    
    // Check if it's already a Texture2D
    texture2D = texture as Texture2D;
    if (texture2D == null)
    {
        // Create a new Texture2D from the source texture
        texture2D = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
        RenderTexture tempRT = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        Graphics.Blit(texture, tempRT);
        
        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = tempRT;
        texture2D.ReadPixels(new Rect(0, 0, tempRT.width, tempRT.height), 0, 0);
        texture2D.Apply();
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(tempRT);
    }

    // Convert to PNG bytes
    byte[] imageData = texture2D.EncodeToPNG();
    
    // Clean up the temporary texture if we created one
    if (texture2D != texture)
    {
        Destroy(texture2D);
    }
    
    // Prepare form data
    WWWForm form = new WWWForm();
    form.AddBinaryData("image", imageData, "image.png", "image/png");

    using (UnityWebRequest www = UnityWebRequest.Post(BASE_URL, form))
    {
        www.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
        www.SetRequestHeader("Accept", "application/json");

        yield return www.SendWebRequest();

        if (www.result == UnityWebRequest.Result.Success)
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string modelsDirectory = Path.Combine(Application.dataPath, "Models");
            Directory.CreateDirectory(modelsDirectory);
            string modelPath = Path.Combine(modelsDirectory, $"model_{timestamp}.glb");
            File.WriteAllBytes(modelPath, www.downloadHandler.data);

            if (currentModel != null)
            {
                Destroy(currentModel);
            }

            yield return LoadAndPlaceModel(modelPath, (loadedObject) => {
                if (loadedObject != null && spawnPoint != null)
                {
                    currentModel = loadedObject;
                    currentModel.transform.position = spawnPoint.position;
                    currentModel.transform.rotation = spawnPoint.rotation;
                    currentModel.name = $"Model_{timestamp}";
                    onComplete?.Invoke(currentModel);
                }
                else
                {
                    Debug.LogError("Failed to load model");
                    onComplete?.Invoke(null);
                }
            });
        }
        else
        {
            string errorMessage = www.downloadHandler.text;
            try
            {
                var errorResponse = JsonConvert.DeserializeObject<StabilityResponse>(errorMessage);
                Debug.LogError($"Generation failed: {string.Join(", ", errorResponse.errors)}");
            }
            catch
            {
                Debug.LogError($"Generation failed: {www.error}");
            }
            onComplete?.Invoke(null);
        }

        loadingCircle.SetActive(false);
    }
}

    private IEnumerator LoadAndPlaceModel(string path, Action<GameObject> onLoaded)
    {
        GameObject loadedObject = null;
        bool completed = false;

        try
        {
            Siccity.GLTFUtility.Importer.LoadFromFileAsync(path, 
                new Siccity.GLTFUtility.ImportSettings(), 
                (importedObject, clips) => {
                    loadedObject = importedObject;
                    completed = true;
                });
            WitPromptExtractor.loadingModel = false;
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting GLTF model load: {e.Message}");
            onLoaded?.Invoke(null);
            yield break;
        }

        while (!completed)
        {
            yield return null;
        }

        if (loadedObject != null)
        {
            try
            {
                // Change the scale to 2 instead of 1
                loadedObject.transform.localScale = Vector3.one * 2f;
                GameObject container = new GameObject($"Model_{Path.GetFileNameWithoutExtension(path)}");
                loadedObject.transform.SetParent(container.transform, false);
                onLoaded?.Invoke(container);
            }
            catch (Exception e)
            {
                Debug.LogError($"Error setting up loaded model: {e.Message}");
                onLoaded?.Invoke(null);
            }
        }
        else
        {
            Debug.LogError("Failed to load GLTF model");
            onLoaded?.Invoke(null);
        }
    }
    
    private void Update()
    {
        // Check if we have a model and rotation is enabled
        if (currentModel != null)
        {
            // Rotate around the Y-axis
            currentModel.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }
    }
}