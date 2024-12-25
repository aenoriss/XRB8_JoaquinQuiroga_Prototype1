using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ImageMenu : MonoBehaviour
{
    [SerializeField] private GameObject[] Panels;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private StabilityAPI _meshyAPI;
    [SerializeField] GameObject loadingCircle;
    
    private int activePanel = -1;
    private int stage = 1;
    
    public void optionSelected(int panelID)
    {
        for (int i = 0; i < Panels.Length; i++)
        {
            if (i == panelID)
            {
                Panels[i].SetActive(true);

            }
            else
            {
                Panels[i].SetActive(false);
            }

            activePanel = panelID;
        }
    }

    public void RestorePanels()
    {
        if (stage == 1)
        {
            foreach (GameObject panel in Panels)
            {
                panel.SetActive(true);
            }

            activePanel = -1; 
        }
    }
    
    private void DeactivatePanels()
    {
        foreach (GameObject panel in Panels)
        {
            panel.SetActive(false);
        }

        activePanel = -1;
    }
    
    private IEnumerator DeactivatePanelsAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        DeactivatePanels();
    }
    
    public IEnumerator RestorePanelsAfterDelay()
    {
        yield return new WaitForSeconds(0.001f);
        RestorePanels();
    }
    
    public void GenerationActivated()
    {
        if(activePanel != -1)
        {
            stage = 2;
            Debug.Log("Generation activated in Panel " + activePanel);
            Panels[activePanel].GetComponent<OptionPanel>().GenerationActivated();
            _meshyAPI.Generate3DModel(Panels[activePanel].GetComponent<OptionPanel>().imageTexture);
            StartCoroutine(DeactivatePanelsAfterDelay(1f));
            loadingCircle.SetActive(true);
        }
    }
    

}
