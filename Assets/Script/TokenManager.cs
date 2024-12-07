using System;
using System.Collections;
using System.Collections.Generic;
using Oculus.Interaction.HandGrab;
using Oculus.Interaction.Input;
using TMPro;
using UnityEngine;

public class TokenManager : MonoBehaviour
{
    [SerializeField] private string TokenTicker;
    [SerializeField] private string TokenPrice;
    [SerializeField] private string TokenPriceChange;
    [SerializeField] private Texture TokenLogo;
    
    
    [SerializeField] private TMP_Text TokenTickerText;
    [SerializeField] private TMP_Text TokenPriceText;
    [SerializeField] private TMP_Text TokenPriceChangeText;
    
    [SerializeField] private GameObject TokenUI;
    [SerializeField] private float cooldownTime = 1.0f;
    
    private bool tokenStatus = false;
    private bool canActivate = true;
    private bool currentlyGrabbed = false;
    
    public void SetupToken(string ticker, string price, string priceChange, Texture logo)
    {
        TokenTickerText.text = ticker;
        TokenPriceText.text = price;
        TokenPriceChangeText.text = priceChange;
        
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        if (meshRenderer != null && logo != null)
        {
            meshRenderer.material.mainTexture = logo;
        }
    }

    public void IsTokenGrabbed(bool isGrabbed)
    {
        currentlyGrabbed = isGrabbed;
        Debug.Log("currentlyGrabbed: " + currentlyGrabbed);

        if (isGrabbed)
        {
            TokenUI.gameObject.SetActive(false); 
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
    
    

    
}
