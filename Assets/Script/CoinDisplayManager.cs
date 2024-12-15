using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class CoinDisplayManager : MonoBehaviour
{
    [SerializeField] private GameObject CoinList;
    [SerializeField] private Transform[] CoinPos;
    [SerializeField] private OVRHand  RightHandPos;
    [SerializeField] private OVRHand  LeftHandPos;
    
    private List<GameObject> currentCoins = new List<GameObject>();
    
    private int startIndex = 0; 
    private int endIndex = 0; 
    
    private bool isDraggedLeft = false;
    private bool isDraggedRight = false;
    private int leftCounter = 0;
    private int rightCounter = 0;
    private Vector3? prevDraggedPosition;
    private Vector3? isDraggedDirection;
    
    private bool isTokenMoving = false;
    
    void Start()
    {
        APIManager _apimanager = FindObjectOfType<APIManager>();
        _apimanager.OnTokensLoaded += HandleTokensLoaded;
    }

    private void HandleTokensLoaded()
    {
        for (int i = 0; i < CoinPos.Length; i++)
        { 
            GameObject childTransform = CoinList.transform.GetChild(i).gameObject;
            
            Debug.Log("childTransform"+childTransform);
            
            GameObject newCoin = Instantiate(childTransform, CoinPos[i].position, childTransform.transform.rotation); 
            currentCoins.Add(newCoin);
            newCoin.gameObject.GetComponent<TokenManager>().updateCarrouselPosition(CoinPos[i].transform, i);
            endIndex++;
        }
    }

    public void IsBeingDraggedRight(bool newState)
    {
        if (newState)
        {
            isDraggedRight = true;
        }
        else
        {
            isDraggedRight = false;
            rightCounter = 0;
            leftCounter = 0;
        }
    }

    public void IsBeingDraggedLeft(bool newState)
    {
        if (newState)
        {
            isDraggedLeft = true;
        }
        else
        {
            isDraggedLeft = false;
            rightCounter = 0;
            leftCounter = 0;
        }
    }

private void ShiftCarrousel(bool direction) 
{
    if (!isTokenMoving)
    {
        StartCoroutine(ShiftCarrouselCoroutine(direction));
    }
}

private IEnumerator ShiftCarrouselCoroutine(bool isShiftingRight)
{
    isTokenMoving = true;

    int removeIndex = isShiftingRight ? currentCoins.Count - 1 : 0;
    GameObject lastToken = currentCoins[removeIndex];
    currentCoins.RemoveAt(removeIndex);
    Destroy(lastToken.gameObject);

    if (isShiftingRight)
    {
        startIndex = (startIndex + 1) % CoinList.transform.childCount;
        endIndex = (endIndex + 1) % CoinList.transform.childCount;
    }
    else
    {
        startIndex = ((startIndex - 1 + CoinList.transform.childCount) % CoinList.transform.childCount);
        endIndex = ((endIndex - 1 + CoinList.transform.childCount) % CoinList.transform.childCount);
    }

    if (isShiftingRight)
    {
        for (int i = 0; i < currentCoins.Count; i++)
        {
            TokenManager tokenManager = currentCoins[i].GetComponent<TokenManager>();
            if (tokenManager != null)
            {
                Debug.Log(CoinPos[i + 1]);
                tokenManager.updateCarrouselPosition(CoinPos[i + 1].transform, i + 1);
            }
        }
    }
    else
    {
        for (int i = currentCoins.Count - 1; i >= 0; i--)
        {
            TokenManager tokenManager = currentCoins[i].GetComponent<TokenManager>();
            if (tokenManager != null)
            {
                tokenManager.updateCarrouselPosition(CoinPos[i].transform, i);
            }
        }
    }

    int newTokenIndex = isShiftingRight ? startIndex : endIndex;
    GameObject childTransform = CoinList.transform.GetChild(newTokenIndex).gameObject;
    Vector3 spawnPosition = isShiftingRight ? CoinPos[0].position : CoinPos[4].position;
    GameObject newCoin = Instantiate(childTransform, spawnPosition, childTransform.transform.rotation);

    if (isShiftingRight)
    {
        currentCoins.Insert(0, newCoin);
        newCoin.GetComponent<TokenManager>().updateCarrouselPosition(CoinPos[0].transform, 0);
    }
    else
    {
        currentCoins.Add(newCoin);
        newCoin.GetComponent<TokenManager>().updateCarrouselPosition(CoinPos[4].transform, 4);
    }

    yield return new WaitForSeconds(1f);
    isTokenMoving = false;
}


    private void Update()
    {

        if (isDraggedLeft || isDraggedRight)
        {
            Vector3 currentPosition = isDraggedRight ? RightHandPos.PointerPose.position : LeftHandPos.PointerPose.position;
        
            if (!prevDraggedPosition.HasValue)
            {
                prevDraggedPosition = currentPosition;
                return;
            }
            
            if (currentPosition.x < prevDraggedPosition.Value.x)
            {
                leftCounter++;
            }
            else if (currentPosition.x > prevDraggedPosition.Value.x)
            {
                rightCounter++;
            }

            if (leftCounter >= 10)
            {
                rightCounter = 0;
                Debug.Log("shifting carrousel left");
                ShiftCarrousel(false);

            }else if(rightCounter >= 10)
            {
                leftCounter = 0;
                Debug.Log("shifting carrousel right");
                ShiftCarrousel(true);
            }
        
            prevDraggedPosition = currentPosition;

        }
       
    }
}
