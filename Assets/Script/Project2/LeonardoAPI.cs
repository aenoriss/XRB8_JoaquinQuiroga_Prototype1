using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json; 

public class LeonardoAPI : MonoBehaviour
{
   private const string API_KEY = "d326c183-8415-4922-b6ce-908b89ace75f";
   private const string BASE_URL = "https://cloud.leonardo.ai/api/rest/v1/generations";
   private List<GeneratedImage> currentImages = new List<GeneratedImage>();
   private string generationID;
   
   [SerializeField] private GameObject Menu;
   [SerializeField] private GameObject[] Panels;
   [SerializeField] private Transform spawnPoint;
   [SerializeField] GameObject loadingCircle;
   
   [Serializable]
   public class imageGenReq
   {
      public bool alchemy = true;
      public int height = 768;
      public string modelId = "b24e16ff-06e3-43eb-8d33-4416c2d75876";
      public int num_images = 3;
      public string presetStyle = "ILLUSTRATION";
      public string prompt;
      public int width = 768;
      public bool promptMagic = true;
      public bool @public = false;
   }
   
   private class TaskResponse
   {
      public SdGenerationJob sdGenerationJob;
   }

   private class SdGenerationJob
   {
      public string generationId;
      public int? apiCreditCost;
   }
   
   [Serializable]
   public class GenerationResult
   {
      [JsonProperty("generations_by_pk")]
      public GenerationDetails generations_by_pk { get; set; }
   }

   [Serializable] 
   public class GenerationDetails
   {
      public string createdAt { get; set; }
      public string status { get; set; }
      public List<GeneratedImage> generated_images { get; set; }
   }

   [Serializable]
   public class GeneratedImage 
   {
      public List<ImageVariation> generated_image_variation_generics { get; set; }
      public string id { get; set; }
      public string status { get; set; }
      public string url { get; set; }
   }

   [Serializable]
   public class ImageVariation
   {
      public string id { get; set; }
      public string status { get; set; }
      public string transformType { get; set; }
      public string url { get; set; }
   }

   private void Awake()
   {
      // generateImage("create orb of light");
      Menu.SetActive(false);
   }

   public void generateImage(string prompt)
   {
      Debug.Log($"image generation begun with prompt: {prompt}");
      loadingCircle.SetActive(true);
      StartCoroutine(GenerateImageCoroutine(prompt));
   }

   private IEnumerator GenerateImageCoroutine(string prompt)
   {
      var request = new imageGenReq { prompt = prompt+",white background, concept art for 3D model style" };
      string jsonData = JsonConvert.SerializeObject(request, new JsonSerializerSettings { 
         TypeNameHandling = TypeNameHandling.None 
      });

      using (UnityWebRequest www = new UnityWebRequest(BASE_URL, "POST"))
      {
         byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
         
         www.uploadHandler = new UploadHandlerRaw(bodyRaw);
         www.downloadHandler = new DownloadHandlerBuffer();
         
         www.SetRequestHeader("accept", "application/json");
         www.SetRequestHeader("Content-Type", "application/json; charset=UTF-8");
         www.SetRequestHeader("authorization", $"Bearer {API_KEY}");
         
         yield return www.SendWebRequest();
         
         Debug.Log($"Response Code: {www.responseCode}");
         Debug.Log($"Full Response: {www.downloadHandler.text}");

         if (www.result == UnityWebRequest.Result.Success)
         {
            var response = JsonConvert.DeserializeObject<TaskResponse>(www.downloadHandler.text);
            Debug.Log("RESPONSE"+ response.sdGenerationJob.generationId);

            generationID = response.sdGenerationJob.generationId;
            
            StartCoroutine(getImagesCoroutine());
         } else
         {
            Debug.LogError($"Error: {www.error}\nResponse: {www.downloadHandler.text}");
         }
         
      }
   }

   private IEnumerator getImagesCoroutine()
   {
      bool isComplete = false;
      bool backgroundRemovedReq = false;
      int readyImgCounter = 0;
      float startTime = Time.time;
      float timeout = 300f;
      
      while (!isComplete)
      {
         if (Time.time - startTime > timeout)
         {
            Debug.LogError("Operation timed out");
            yield break;
         }
         
         using (UnityWebRequest www = new UnityWebRequest(BASE_URL + "/" + generationID, "GET"))
         {
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("accept", "application/json");
            www.SetRequestHeader("authorization", $"Bearer {API_KEY}");

            yield return www.SendWebRequest();

            Debug.Log($"GEN IMG Response Code: {www.responseCode}");
            Debug.Log($"Full GEN IMG Response: {www.downloadHandler.text}");

            if (www.result == UnityWebRequest.Result.Success)
            {
               var result = JsonConvert.DeserializeObject<GenerationResult>(www.downloadHandler.text);
               
               //Solicit background removal if not already done
               if (result.generations_by_pk.generated_images.Count > 0 && result.generations_by_pk.status == "COMPLETE")
               {
                  
                  if (!backgroundRemovedReq)
                  {
                     foreach (var image in result.generations_by_pk.generated_images)
                     {
                        Debug.Log($"Image URL: {image.id}");
                        StartCoroutine(removeBackground(image.id));
                     }
                     backgroundRemovedReq = true;
                  }

                  if (backgroundRemovedReq && readyImgCounter < result.generations_by_pk.generated_images.Count)
                  {
                     foreach (var image in result.generations_by_pk.generated_images)
                     {
                        if (image.generated_image_variation_generics != null && 
                            image.generated_image_variation_generics.Count > 0 &&
                            image.generated_image_variation_generics[0].status == "COMPLETE")
                        {
                           readyImgCounter += 1;
                        }
                     }

                     if (readyImgCounter < result.generations_by_pk.generated_images.Count)
                     {
                        readyImgCounter = 0;
                        yield return new WaitForSeconds(1f);
                     }
                  }
                  else
                  {
                     isComplete = true;
                     //when everything is ready store image in global variable
                     currentImages = result.generations_by_pk.generated_images;
                     
                     //Render Images
                     renderImages();
                  }
               }
               else
               {
                  Debug.Log("Generation in progress... waiting 1 second");
                  yield return new WaitForSeconds(1f);
               }
            }
            else
            {
               Debug.LogError($"Error: {www.error}\nResponse: {www.downloadHandler.text}");
               break;
            }
         }
      }
   }

   private IEnumerator removeBackground(string imageID)
   {
      string nobgUrl = "https://cloud.leonardo.ai/api/rest/v1/variations/nobg";
      Debug.Log("REMOVING BACKGROUND...."+imageID);
      
    
      // Create request body
      var requestBody = new
      {
         id = imageID,
         isVariation = false,
      };
    
      string jsonData = JsonConvert.SerializeObject(requestBody, new JsonSerializerSettings { 
         TypeNameHandling = TypeNameHandling.None 
      });

      using (UnityWebRequest www = new UnityWebRequest(nobgUrl, "POST"))
      {
         byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonData);
        
         www.uploadHandler = new UploadHandlerRaw(bodyRaw);
         www.downloadHandler = new DownloadHandlerBuffer();
        
         www.SetRequestHeader("accept", "application/json");
         www.SetRequestHeader("Content-Type", "application/json");
         www.SetRequestHeader("authorization", $"Bearer {API_KEY}");

         yield return www.SendWebRequest();

         Debug.Log($"Remove BG Response Code: {www.responseCode}");
         Debug.Log($"Remove BG Response: {www.downloadHandler.text}");

         if (www.result != UnityWebRequest.Result.Success)
         {
            Debug.LogError($"Background removal error: {www.error}\nResponse: {www.downloadHandler.text}");
         }
      }
   }
   
   private void renderImages()
   {
      Debug.Log("PROCESS FINISHED"+currentImages.Count);
      loadingCircle.SetActive(false);
    
      // Start downloading images for each panel
      for (int i = 0; i < currentImages.Count && i < Panels.Length; i++)
      {
         string imageUrl = currentImages[i].url;
         if (currentImages[i].generated_image_variation_generics != null && 
             currentImages[i].generated_image_variation_generics.Count > 0)
         {
            // Use the background-removed version if available
            imageUrl = currentImages[i].generated_image_variation_generics[0].url;
         }
        
         StartCoroutine(DownloadAndApplyTexture(imageUrl, i));
      }
   }

   private IEnumerator DownloadAndApplyTexture(string url, int panelIndex)
   {
      using (UnityWebRequest www = UnityWebRequestTexture.GetTexture(url))
      {
         yield return www.SendWebRequest();

         if (www.result == UnityWebRequest.Result.Success)
         {
            Texture2D texture = DownloadHandlerTexture.GetContent(www);
            
            MeshRenderer meshRenderer = Panels[panelIndex].GetComponent<MeshRenderer>();
            if (meshRenderer != null)
            {
               Material material = new Material(Shader.Find("Unlit/Transparent"));
               material.mainTexture = texture;

               // Flip the texture by setting negative scale and adjusting offset
               material.mainTextureScale = new Vector2(1, -1);
               material.mainTextureOffset = new Vector2(0, 1);

               // Set transparency settings
               material.SetFloat("_Mode", 2);
               material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
               material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
               material.SetInt("_ZWrite", 0);
               material.DisableKeyword("_ALPHATEST_ON");
               material.EnableKeyword("_ALPHABLEND_ON");
               material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
               material.renderQueue = 3000;

               meshRenderer.material = material;
               Panels[panelIndex].gameObject.GetComponent<OptionPanel>().imageTexture = texture;
               Menu.SetActive(true);
            }
            else
            {
               Debug.LogError($"No MeshRenderer found on panel {panelIndex}");
            }
         }
         else
         {
            Debug.LogError($"Failed to download texture: {www.error}");
         }
      }
   }
}
