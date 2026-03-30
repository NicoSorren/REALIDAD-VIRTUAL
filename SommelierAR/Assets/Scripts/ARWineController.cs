    using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

// Requerimos que el GameObject tenga un ARTrackedImageManager
[RequireComponent(typeof(ARTrackedImageManager))]
public class ARWineController : MonoBehaviour
{
    [Header("AR Configuration")]
    private ARTrackedImageManager _imageManager;
    
    [Header("Prefabs y Modelos")]
    [Tooltip("El nombre de la imagen de referencia en la Reference Image Library")]
    public string targetImageName = "EtiquetaVino";
    [Tooltip("Prefab base que contiene ambos escenarios (A y B)")]
    public GameObject virtualBottlePrefab;
    
    // Instancia del objeto 3D que aparecerá anclado a la botella real
    private GameObject _spawnedBottle;

    [Header("Escenarios / Modos")]
    public ScenarioMode currentScenario = ScenarioMode.EscenarioA_Labels;

    // Referencias a los subcontenedores u objetos gráficos de cada escenario
    private GameObject _scenarioAContainer;
    private GameObject _scenarioBContainer;

    public enum ScenarioMode
    {
        EscenarioA_Labels,
        EscenarioB_XRay
    }

    // Estructura de datos requerida para los parámetros enológicos
    [System.Serializable]
    public struct EnologicalData
    {
        public float pH;
        public float brixDegree;
        public float fermentTemperature;
        public string originHectares;
    }

    [Header("Datos Enológicos Actuales")]
    public EnologicalData currentData = new EnologicalData
    {
        pH = 3.5f,
        brixDegree = 24.0f,
        fermentTemperature = 26.5f,
        originHectares = "Valle de Uco - Finca 1"
    };

    void Awake()
    {
        _imageManager = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        // Suscribirse a los eventos de detección de imágenes
#pragma warning disable 0618
        _imageManager.trackedImagesChanged += OnTrackedImagesChanged;
#pragma warning restore 0618
    }

    void OnDisable()
    {
        // Desuscribirse para evitar fugas de memoria y errores
#pragma warning disable 0618
        _imageManager.trackedImagesChanged -= OnTrackedImagesChanged;
#pragma warning restore 0618
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // 1. Manejo de nuevas imágenes detectadas (marcador encontrado)
        foreach (var trackedImage in eventArgs.added)
        {
            if (trackedImage.referenceImage.name == targetImageName)
            {
                // Si aún no hemos instanciado la botella virtual, lo hacemos
                if (_spawnedBottle == null)
                {
                    _spawnedBottle = Instantiate(virtualBottlePrefab, trackedImage.transform.position, trackedImage.transform.rotation);
                    _spawnedBottle.transform.parent = trackedImage.transform; // Anclar toda la geometría al marcador (iPhone pose data)
                    
                    // Supongamos que el prefab tiene dos hijos principales, uno para cada escenario
                    Transform containerA = _spawnedBottle.transform.Find("ScenarioA_Labels");
                    Transform containerB = _spawnedBottle.transform.Find("ScenarioB_XRay");

                    if (containerA != null) _scenarioAContainer = containerA.gameObject;
                    if (containerB != null) _scenarioBContainer = containerB.gameObject;

                    UpdateScenarioVisuals();
                    UpdateEnologicalLabels(currentData);
                }
            }
        }

        // 2. Manejo de imágenes actualizadas (se mueven o giran respecto a la cámara del iPhone)
        foreach (var trackedImage in eventArgs.updated)
        {
            if (trackedImage.referenceImage.name == targetImageName && _spawnedBottle != null)
            {
                // La alineación de las posiciones AR ya está gobernada por ARFoundation.
                // Como _spawnedBottle.transform.parent = trackedImage.transform, 
                // mantiene estricta consistencia con la geometría del iPhone.
                
                // Si perdemos nivel de rastreo al ocultar el marcador, ocultamos el modelo
                if (trackedImage.trackingState == TrackingState.Tracking || trackedImage.trackingState == TrackingState.Limited)
                {
                    _spawnedBottle.SetActive(true);
                }
                else
                {
                    _spawnedBottle.SetActive(false); // Ocultar si se deja de ver por completo
                }
            }
        }
    }

    /// <summary>
    /// Cambia entre el escenario A (Datos) y B (Rayos X).
    /// Esta función es un "listener" ideal para tu UI o el trigger que leas desde nivel hardware (Acelerómetro/Táctil).
    /// </summary>
    public void ToggleScenario()
    {
        if (currentScenario == ScenarioMode.EscenarioA_Labels)
        {
            currentScenario = ScenarioMode.EscenarioB_XRay;
        }
        else
        {
            currentScenario = ScenarioMode.EscenarioA_Labels;
        }
        
        UpdateScenarioVisuals();
    }

    private void UpdateScenarioVisuals()
    {
        if (_scenarioAContainer == null || _scenarioBContainer == null) 
        {
            Debug.LogWarning("No se encontraron los contenedores de los escenarios en el prefab.");
            return;
        }

        switch (currentScenario)
        {
            case ScenarioMode.EscenarioA_Labels:
                _scenarioAContainer.SetActive(true);
                _scenarioBContainer.SetActive(false);
                break;
            case ScenarioMode.EscenarioB_XRay:
                _scenarioAContainer.SetActive(false);
                _scenarioBContainer.SetActive(true);
                break;
        }
    }

    /// <summary>
    /// Actualiza los textos / primitivas 3D del Escenario A con los datos enológicos.
    /// </summary>
    public void UpdateEnologicalLabels(EnologicalData data)
    {
        // Aquí debes enlazar los atributos de EnologicalData con tus componentes de UI o TextMeshPro 3D.
        // Ej:
        // var textPh = _scenarioAContainer.transform.Find("Label_pH").GetComponent<TMPro.TextMeshPro>();
        // textPh.text = "pH: " + data.pH.ToString("F2");
        
        Debug.Log($"[Sommelier AR] Actualizando datos: pH={data.pH}, Brix={data.brixDegree}, Temp={data.fermentTemperature}°C");
    }
}
