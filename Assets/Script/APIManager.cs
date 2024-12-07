using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class TokenData
{
    public string url;
    public string chainId;
    public string tokenAddress;
    public string icon;
    public string description;
}

[Serializable]
public class TokenDataResponse
{
    public TokenData[] data;
}

public class APIManager : MonoBehaviour
{
    [SerializeField] private GameObject tokenPrefab;
    [SerializeField] private Transform tokensParent;
    [SerializeField] private float spacingBetweenTokens = 1.0f;
    
    void Start()
    {
        StartCoroutine(GetDexScreenerData());
    }
    
    private IEnumerator GetDexScreenerData()
    {
        string url = "https://api.dexscreener.com/token-profiles/latest/v1";
        
        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                TokenData[] tokens = JsonUtility.FromJson<TokenDataResponse>("{\"data\":" + webRequest.downloadHandler.text + "}").data;
                
                for (int i = 0; i < tokens.Length; i++)
                {
                    Vector3 position = tokensParent.position + new Vector3(i * spacingBetweenTokens, 0, 0);
                    StartCoroutine(InstantiateToken(tokens[i], position));
                }
            }
            else
            {
                Debug.LogError("Web request error: " + webRequest.error);
            }
        }
    }

    private IEnumerator InstantiateToken(TokenData tokenData, Vector3 position)
    {
        // Create the token instance first
        GameObject tokenInstance = Instantiate(tokenPrefab, position, Quaternion.identity, tokensParent);
        TokenManager tokenManager = tokenInstance.GetComponent<TokenManager>();

        if (tokenManager == null)
        {
            Debug.LogError($"TokenManager component missing on prefab for token: {tokenData.tokenAddress}");
            yield break;
        }

        UnityWebRequest textureRequest = UnityWebRequestTexture.GetTexture(tokenData.icon);
        yield return textureRequest.SendWebRequest();

        if (textureRequest.result == UnityWebRequest.Result.Success)
        {
            tokenManager.SetupToken(
                tokenData.tokenAddress,
                "Price N/A", 
                "Change N/A",
                ((DownloadHandlerTexture)textureRequest.downloadHandler).texture
            );
        }
        else
        {
            Debug.LogWarning($"Texture download failed for {tokenData.tokenAddress}, using default texture");
            tokenManager.SetupToken(
                tokenData.tokenAddress,
                "Price N/A", 
                "Change N/A",
                null  // Or use a default texture if you have one
            );
        }

        textureRequest.Dispose();
    }
    
}
