using System;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.Text;
using Newtonsoft.Json;
using System.Collections.Generic;

public class MeshyAPI : MonoBehaviour
{
    private const string API_KEY = "msy_MFiRWHNHLLOUHMeVq4aIyGn6rtUji9gHfzKb";
    private const string BASE_URL = "https://api.meshy.ai/openapi/v2/text-to-3d";

    [SerializeField] private Transform spawnPoint;
    [SerializeField] GameObject loadingCircle;
    
    private GameObject currentModel;
    private Material previewMaterial;

    private void Awake()
    {
        // Create unlit white material
        previewMaterial = new Material(Shader.Find("Unlit/Color"));
        previewMaterial.color = Color.white;
    }

    private void OnDestroy()
    {
        // Clean up material
        if (previewMaterial != null)
        {
            Destroy(previewMaterial);
        }
    }

    [System.Serializable]
    private class PreviewRequest
    {
        public string mode = "preview";
        public string prompt;
        public string negative_prompt = "low quality, low resolution, low poly, ugly";
        public string art_style = "realistic";
        public bool should_remesh = true;
    }

    [System.Serializable]
    private class RefineRequest
    {
        public string mode = "refine";
        public string preview_task_id;
        public bool enable_pbr = true;
    }

    [System.Serializable]
    private class TaskResponse
    {
        public string result;
    }

    [System.Serializable]
    private class TaskStatus
    {
        public string id;
        public ModelUrls model_urls;
        public string status;
        public string task_error;
    }

    [System.Serializable]
    private class ModelUrls
    {
        public string glb;
        public string fbx;
        public string obj;
    }

    public void Generate3DModel(string prompt, Action<GameObject> onComplete = null)
    {
        loadingCircle.SetActive(true);
        StartCoroutine(GenerateModelCoroutine(prompt, onComplete));
    }

    private void ApplyPreviewMaterial(GameObject model)
    {
        if (model == null) return;

        // Apply material to all renderers
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

    private IEnumerator GenerateModelCoroutine(string prompt, Action<GameObject> onComplete = null)
    {
        // Step 1: Create preview task
        string previewTaskId = null;
        yield return StartPreviewTask(prompt, (taskId) => previewTaskId = taskId);
        
        if (string.IsNullOrEmpty(previewTaskId))
        {
            Debug.LogError("Failed to start preview task");
            onComplete?.Invoke(null);
            yield break;
        }

        // Step 2: Wait for preview to complete and show preview model
        bool previewComplete = false;
        while (!previewComplete)
        {
            string status = null;
            yield return CheckTaskStatus(previewTaskId, (s) => status = s);
            
            if (status == "SUCCEEDED")
            {
                previewComplete = true;
                
                // Download and show preview model
                string previewPath = null;
                yield return DownloadModel(previewTaskId, (path) => previewPath = path);

                if (!string.IsNullOrEmpty(previewPath))
                {
                    yield return LoadAndPlaceModel(previewPath, (previewObject) => {
                        if (previewObject != null && spawnPoint != null)
                        {
                            // Clean up previous model if it exists
                            if (currentModel != null)
                            {
                                Destroy(currentModel);
                            }
                            
                            currentModel = previewObject;
                            currentModel.transform.position = spawnPoint.position;
                            currentModel.transform.rotation = spawnPoint.rotation;
                            currentModel.name = "Preview_" + prompt;
                            
                            // Apply preview material
                            ApplyPreviewMaterial(currentModel);
                        }
                    });
                }
            }
            else if (status == "FAILED")
            {
                Debug.LogError("Preview task failed");
                onComplete?.Invoke(null);
                yield break;
            }
            yield return new WaitForSeconds(2f);
        }

        // Step 3: Start refine task
        string refineTaskId = null;
        yield return StartRefineTask(previewTaskId, (taskId) => refineTaskId = taskId);
        
        if (string.IsNullOrEmpty(refineTaskId))
        {
            Debug.LogError("Failed to start refine task");
            onComplete?.Invoke(currentModel);
            yield break;
        }

        // Step 4: Wait for refine to complete
        bool refineComplete = false;
        while (!refineComplete)
        {
            string status = null;
            yield return CheckTaskStatus(refineTaskId, (s) => status = s);
            
            if (status == "SUCCEEDED")
            {
                refineComplete = true;
                
                string refinedPath = null;
                yield return DownloadModel(refineTaskId, (path) => refinedPath = path);

                if (!string.IsNullOrEmpty(refinedPath))
                {
                    // Clean up preview model BEFORE loading the refined one
                    if (currentModel != null)
                    {
                        Destroy(currentModel);
                        currentModel = null;
                    }

                    yield return LoadAndPlaceModel(refinedPath, (refinedObject) => {
                        if (refinedObject != null && spawnPoint != null)
                        {
                            currentModel = refinedObject;
                            currentModel.transform.position = spawnPoint.position;
                            currentModel.transform.rotation = spawnPoint.rotation;
                            currentModel.name = "Refined_" + prompt;
                            onComplete?.Invoke(currentModel);
                        }
                    });
                }
                else
                {
                    onComplete?.Invoke(currentModel);
                }
            }
            else if (status == "FAILED")
            {
                Debug.LogError("Refine task failed");
                onComplete?.Invoke(currentModel);
                yield break;
            }
            yield return new WaitForSeconds(2f);
        }
    }

    private IEnumerator StartPreviewTask(string prompt, Action<string> onComplete)
    {
        var request = new PreviewRequest { prompt = prompt };
        string jsonData = JsonConvert.SerializeObject(request);

        using (UnityWebRequest www = new UnityWebRequest(BASE_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<TaskResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response.result);
            }
            else
            {
                Debug.LogError($"Preview task error: {www.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    private IEnumerator StartRefineTask(string previewTaskId, Action<string> onComplete)
    {
        var request = new RefineRequest { preview_task_id = previewTaskId };
        string jsonData = JsonConvert.SerializeObject(request);

        using (UnityWebRequest www = new UnityWebRequest(BASE_URL, "POST"))
        {
            byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
            www.uploadHandler = new UploadHandlerRaw(bodyRaw);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var response = JsonConvert.DeserializeObject<TaskResponse>(www.downloadHandler.text);
                onComplete?.Invoke(response.result);
            }
            else
            {
                Debug.LogError($"Refine task error: {www.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    private IEnumerator CheckTaskStatus(string taskId, Action<string> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{BASE_URL}/{taskId}"))
        {
            www.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var status = JsonConvert.DeserializeObject<TaskStatus>(www.downloadHandler.text);
                onComplete?.Invoke(status.status);
            }
            else
            {
                Debug.LogError($"Status check error: {www.error}");
                onComplete?.Invoke("FAILED");
            }
        }
    }

    private IEnumerator DownloadModel(string taskId, Action<string> onComplete)
    {
        using (UnityWebRequest www = UnityWebRequest.Get($"{BASE_URL}/{taskId}"))
        {
            www.SetRequestHeader("Authorization", $"Bearer {API_KEY}");
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success)
            {
                var taskStatus = JsonConvert.DeserializeObject<TaskStatus>(www.downloadHandler.text);
                string glbUrl = taskStatus.model_urls.glb;

                using (UnityWebRequest modelWww = UnityWebRequest.Get(glbUrl))
                {
                    yield return modelWww.SendWebRequest();

                    if (modelWww.result == UnityWebRequest.Result.Success)
                    {
                        // Create Models directory if it doesn't exist
                        string modelsDirectory = Path.Combine(Application.dataPath, "Models");
                        Directory.CreateDirectory(modelsDirectory);

                        // Save the model file
                        string path = Path.Combine(modelsDirectory, $"{taskId}.glb");
                        File.WriteAllBytes(path, modelWww.downloadHandler.data);
                        Debug.Log($"Model downloaded to: {path}");
                        onComplete?.Invoke(path);
                    }
                    else
                    {
                        Debug.LogError($"Model download error: {modelWww.error}");
                        onComplete?.Invoke(null);
                    }
                }
            }
            else
            {
                Debug.LogError($"Failed to get model URLs: {www.error}");
                onComplete?.Invoke(null);
            }
        }
    }

    private IEnumerator LoadAndPlaceModel(string path, Action<GameObject> onLoaded)
    {
        // Load the model asynchronously using GLTFUtility
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
        }
        catch (Exception e)
        {
            Debug.LogError($"Error starting GLTF model load: {e.Message}");
            onLoaded?.Invoke(null);
            yield break;
        }
    
        // Wait for the import to complete
        while (!completed)
        {
            yield return null;
        }

        if (loadedObject != null)
        {
            try
            {
                // Make sure the loaded object has a proper scale
                loadedObject.transform.localScale = Vector3.one;
            
                // Optionally, you might want to add a parent GameObject to help with organization
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
}