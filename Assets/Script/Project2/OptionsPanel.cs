using UnityEngine;
using System.Collections;

public class OptionPanel : MonoBehaviour
{
    [SerializeField] private int _panelID;
    [SerializeField] private MeshFilter _meshFilter;
    [SerializeField] private BoxCollider _boxCollider;
    [SerializeField] private GameObject _panel;
    [SerializeField] private ParticleSystem _crushingEffect;
    [SerializeField] private MeshRenderer _meshRenderer;
    [SerializeField] private ImageMenu _imageMenu;
    [SerializeField] private Material _cubeMaterial;
    
    private static Mesh _cubeMesh;
    private Mesh _panelMesh;
    private Vector3 _panelColliderSize;
    private Vector3 _cubeColliderSize;
    private Vector3 _initialPosition;
    private Quaternion _initialRotation;
    private Material _originalMaterial;  // Store the original material
    public Texture imageTexture;
    public int stage = 1;
    
    private void Awake()
    {
        if (_crushingEffect != null)
        {
            _crushingEffect.Stop();
            var emission = _crushingEffect.emission;
            emission.enabled = false;
        }
        
    }
    
    private void Start()
    {
        _panelMesh = _meshFilter.mesh;
        _panelColliderSize = _boxCollider.size;
        
        if (_cubeMesh == null)
        {
            _cubeMesh = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
        }
        
        _cubeColliderSize = Vector3.one;
        _initialPosition = transform.position;
        _initialRotation = transform.rotation;
    }
    
    public void SwitchToCube()
    {
        if (_meshRenderer != null)
        {
            _originalMaterial = _meshRenderer.material; 
            imageTexture = _meshRenderer.material.mainTexture;
        }
        
        _meshFilter.mesh = _cubeMesh;
        _boxCollider.size = _cubeColliderSize;
        _imageMenu.optionSelected(_panelID);
        
        if (_meshRenderer != null && _cubeMaterial != null)
        {
            _meshRenderer.material = _cubeMaterial;
        }
    }
    
    public void SwitchToPanel()
    {
        if (stage == 1)
        {
            StartCoroutine(SwitchToPanelWithDelay());
            _imageMenu.StartCoroutine(_imageMenu.RestorePanelsAfterDelay()); 
        }
    }
    
    public void GenerationActivated()
    {
        Debug.Log("Generation activated in Panel " + _panelID);
        stage = 2;
        if (_crushingEffect != null)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = false;
            }
            
            var emission = _crushingEffect.emission;
            emission.enabled = true;
            _crushingEffect.Play();
        }
    }

    private IEnumerator SwitchToPanelWithDelay()
    {
        _boxCollider.enabled = false;
        yield return new WaitForSeconds(0.1f);
        _meshFilter.mesh = _panelMesh;
        _boxCollider.size = _panelColliderSize;
        transform.position = _initialPosition;
        transform.rotation = _initialRotation;
        
        if (_meshRenderer != null && imageTexture != null)
        {
            _meshRenderer.material = _originalMaterial;  // Restore original material
            _meshRenderer.material.mainTexture = imageTexture;  // Set the texture
        }
        
        _boxCollider.enabled = true;
    }
}