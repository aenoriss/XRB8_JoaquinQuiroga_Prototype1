using System;
using System.Collections;
using System.Collections.Generic;
using Meta.WitAi.Json;
using Oculus.Platform.Models;
using UnityEngine;
using UnityEngine.Networking;
using System.Globalization;

[Serializable]
public class DexScreenerToken
{
    public string chainId;
    public long creationDate;
    public string description;
    public long dexId;
    public string header;
    public string icon;
    public string name;
    public string symbol;
    public string tokenAddress;
    public string url;
    public string price;
    public Dictionary<string, float> priceChange;  
    public float volume;                          
    public TimePeriod txns;                  
    public List<LinkData> links;                 
}
public class LinkData
{
    public string label;  
    public string type;     
    public string url;
}


[Serializable]
public class DexScreenerPair
{
    public string chainId;
    public string dexId;
    public string pairAddress;
    public DexScreenerToken baseToken;
    public DexScreenerToken quoteToken;
    public string priceUsd;
    public PriceChange priceChange;
    public float volume; // Add this property to match the JSON structure
    public TimePeriod txns; // Ensure this is also included if referenced
}

public class DexScreenerPair2
{
    public string chainId;
    public string dexId;
    public string pairAddress;
    public DexScreenerToken baseToken;
    public DexScreenerToken quoteToken;
    public string priceUsd;
    public PriceChange priceChange;
    public VolumeData volume; // Add this property to match the JSON structure
    public TransactionData txns; // Ensure this is also included if referenced
}

public class VolumeData
{
    public float h1;
    public float h6;
    public float h24;
    public float m5;
    public TransactionData h1Txns;  // TransactionData for h1
    public TransactionData h6Txns;  // TransactionData for h6
    public TransactionData h24Txns; // TransactionData for h24
    public TransactionData m5Txns;  // TransactionData for m5
}

public class TransactionData
{
    public TimePeriod h1;
    public TimePeriod h6;
    public TimePeriod h24;
    public TimePeriod m5;
}

// New TimePeriod class to represent buys and sells for each period
public class TimePeriod
{
    public int buys;
    public int sells;
}

[Serializable]
public class PriceChange
{
    public float h6;
    public float h24;
}

[Serializable]
public class DexScreenerResponse
{
    public string schemaVersion;
    public List<DexScreenerPair> pairs;
}

public class DexScreenerResponse2
{
    public string schemaVersion;
    public List<DexScreenerPair2> pairs;
}

public class APIManager : MonoBehaviour
{
    [SerializeField] private GameObject tokenPrefab;
    [SerializeField] private Transform tokensParent;
    [SerializeField] private float spacingBetweenTokens = 1.0f;

    private List<Coroutine> activeCoroutines = new List<Coroutine>();

    private string _currentlyWatchedTokenAddress;
    public string  currentlyWatchedTokenPrice;
    public float  currentlyWatchedTokenPriceChange;
    public float  currentlyWatchedTokenVolume;
    public float currentlyWatchedTokenTXBalBuys;
    public float currentlyWatchedTokenTXBalSells;

    public bool showWatchedTokenUpdate = false;
    
    public event Action OnTokensLoaded;

    void Start()
    {
        StartCoroutine(GetDexScreenerData());
        StartCoroutine(PeriodicFinancialUpdate());
    }

    private IEnumerator PeriodicFinancialUpdate()
    {
        while (true)
        {
            if (_currentlyWatchedTokenAddress != null)
            {
                yield return StartCoroutine(GetFinancialData());
                Debug.Log("retrieving data");
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private IEnumerator GetDexScreenerData()
    {
        activeCoroutines.Clear();
        StartCoroutine(WaitForTokens());

        string url = "https://xrb8-joaquinquiroga-prototype1-backendd-28244255329.us-central1.run.app/getTokensIndex";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Response: " + webRequest.downloadHandler.text);
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    var tokens =
                        JsonConvert.DeserializeObject<Dictionary<string, DexScreenerToken>>(webRequest.downloadHandler.text);

                    int i = 0;
                    foreach (var token in tokens.Values)
                    {
                        Vector3 position = tokensParent.position + new Vector3(i * spacingBetweenTokens, 0, 0);
                        Coroutine coroutine = StartCoroutine(InstantiateToken(token, position));
                        activeCoroutines.Add(coroutine);
                        i++;

                    }
                }
            }

            StartCoroutine(WaitForTokens());
        }
    }

    private IEnumerator WaitForTokens()
    {
        foreach (var coroutine in activeCoroutines)
        {
            yield return coroutine;
        }

        activeCoroutines.Clear();
        OnTokensLoaded?.Invoke();
    }

    public void setWatchedToken(string newWatchedToken)
    {
        Debug.Log("NEW WATCHED TOKEN:" + newWatchedToken);
        _currentlyWatchedTokenAddress = newWatchedToken;
    }

    private IEnumerator InstantiateToken(DexScreenerToken tokenData, Vector3 position)
    {
        UnityWebRequest textureRequest = UnityWebRequestTexture.GetTexture(tokenData.icon);
        yield return textureRequest.SendWebRequest();

        GameObject tokenInstance = Instantiate(tokenPrefab, position, Quaternion.identity, tokensParent);
        TokenManager tokenManager = tokenInstance.GetComponent<TokenManager>();

        if (tokenManager == null)
        {
            Debug.LogError($"TokenManager component missing on prefab for token: {tokenData.tokenAddress}");
            yield break;
        }

        Debug.Log($"Attempting to download texture from URL: {tokenData.icon}");

        if (textureRequest.result == UnityWebRequest.Result.Success)
        {
            Debug.Log($"Texture download successful for token: {tokenData.tokenAddress}");
            Texture2D texture = ((DownloadHandlerTexture)textureRequest.downloadHandler).texture;
            
            tokenManager.SetupToken(
                tokenData.name,
                tokenData.tokenAddress,
                tokenData.price,
                tokenData.priceChange["h6"] / 100,
                texture,
                (float)tokenData.txns.buys,
                (float)tokenData.txns.sells,
                tokenData.volume
            );
        }

        textureRequest.Dispose();
    }

    public IEnumerator GetFinancialData(string tokenAddress = null)
    {
        if (string.IsNullOrEmpty(_currentlyWatchedTokenAddress)) yield break;

        string tokenRequest = tokenAddress ?? _currentlyWatchedTokenAddress;
        string url = $"https://api.dexscreener.com/latest/dex/tokens/{tokenRequest}";

        using (UnityWebRequest webRequest = UnityWebRequest.Get(url))
        {
            yield return webRequest.SendWebRequest();

            if (tokenAddress == null)
            {
                showWatchedTokenUpdate = true;
            }

            if (webRequest.result == UnityWebRequest.Result.Success)
            {
                if (!string.IsNullOrEmpty(webRequest.downloadHandler.text))
                {
                    try
                    {
                        var response = JsonConvert.DeserializeObject<DexScreenerResponse2>(webRequest.downloadHandler.text);

                        if (response?.pairs != null && response.pairs.Count > 0)
                        {
                            var pair = response.pairs[0];
                            
                            currentlyWatchedTokenPrice = pair.priceUsd ?? "N/A";
                            currentlyWatchedTokenPriceChange = pair.priceChange.h6 / 100;
                            currentlyWatchedTokenVolume = pair.volume.h6;
                            currentlyWatchedTokenTXBalBuys = (float)pair.txns.h6.buys;
                            currentlyWatchedTokenTXBalSells= (float)pair.txns.h6.sells;
                            
                            Debug.Log($"Updated price for {pair.baseToken.symbol}: ${currentlyWatchedTokenPrice} ({currentlyWatchedTokenPriceChange}%)");
                        }
                        else
                        {
                            Debug.LogWarning($"No pairs found for token {_currentlyWatchedTokenAddress}");
                        }
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error parsing DexScreener data: {e.Message}");
                    }
                }
            }
            else
            {
                Debug.LogError($"Error fetching DexScreener data: {webRequest.error}");
            }
        }
    }
}
