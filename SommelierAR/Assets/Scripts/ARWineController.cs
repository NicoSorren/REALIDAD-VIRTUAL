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

    [Header("Marcador - Botella Física")]
    [Tooltip("Nombre exacto de la imagen en la Reference Image Library")]
    public string targetRealBottle = "EtiquetaSexyFish";
    [Tooltip("Prefab HologramaVino con los textos flotantes")]
    public GameObject realBottlePrefab;

    // Diccionario para gestionar las instancias activas
    private Dictionary<string, GameObject> _spawnedInstances = new Dictionary<string, GameObject>();

    // --- Datos del vino (editables desde el Inspector de Unity) ---
    [System.Serializable]
    public struct WineData
    {
        [Tooltip("Nombre del vino y bodega")]
        public string nombreVino;
        [Tooltip("Varietal, año y origen geográfico")]
        public string varietal;
        [Tooltip("Perfil de sabor / descripción")]
        public string perfil;
        [Tooltip("Sugerencia de maridaje")]
        public string maridaje;
        [Tooltip("Temperatura de servicio y graduación alcohólica")]
        public string servicio;
    }

    [Header("Datos del Vino")]
    public WineData currentWine = new WineData
    {
        nombreVino = "Sexy Fish  ·  Norton",
        varietal   = "Malbec 2024  ·  Mendoza, Argentina",
        perfil     = "Frutal  ·  Equilibrado  ·  Final delicado",
        maridaje   = "Carnes rojas  ·  Pastas  ·  Quesos",
        servicio   = "Servir a 18 C   |   14% Alc.   |   750 ml"
    };

    // --- Debug en pantalla (se puede desactivar en producción) ---
    private string _debugLog = "Sommelier AR · Apunte a la etiqueta...";

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = Screen.height / 28;
        style.normal.textColor = Color.yellow;
        GUI.Label(new Rect(30, 120, Screen.width - 60, Screen.height - 160), _debugLog, style);
    }

    private void LogToScreen(string msg)
    {
        _debugLog = msg + "\n" + _debugLog;
        if (_debugLog.Length > 600) _debugLog = _debugLog.Substring(0, 600);
        Debug.Log("[SommelierAR] " + msg);
    }
    // ---------------------------------------------------------------

    void Awake()
    {
        _imageManager = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
#pragma warning disable 0618
        _imageManager.trackedImagesChanged += OnTrackedImagesChanged;
#pragma warning restore 0618
    }

    void OnDisable()
    {
#pragma warning disable 0618
        _imageManager.trackedImagesChanged -= OnTrackedImagesChanged;
#pragma warning restore 0618
    }

    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        // --- Imágenes recién detectadas ---
        foreach (var trackedImage in eventArgs.added)
        {
            string imgName = trackedImage.referenceImage.name;
            LogToScreen("DETECTO: [" + imgName + "]");

            if (imgName == targetRealBottle && !_spawnedInstances.ContainsKey(imgName))
            {
                if (realBottlePrefab == null)
                {
                    LogToScreen("ERROR: realBottlePrefab no asignado en el Inspector.");
                    continue;
                }

                var inst = Instantiate(realBottlePrefab,
                                       trackedImage.transform.position,
                                       trackedImage.transform.rotation);
                inst.transform.parent = trackedImage.transform;
                _spawnedInstances.Add(imgName, inst);

                // Rellenar textos con los datos del vino y hacer fade-in
                UpdateWineLabels(inst, currentWine);
                StartCoroutine(FadeInLabels(inst));
                LogToScreen("OK: Holograma anclado a la etiqueta.");
            }
        }

        // --- Imágenes actualizadas (seguimiento continuo) ---
        foreach (var trackedImage in eventArgs.updated)
        {
            string imgName = trackedImage.referenceImage.name;
            if (_spawnedInstances.TryGetValue(imgName, out GameObject inst))
            {
                bool visible = trackedImage.trackingState == TrackingState.Tracking
                            || trackedImage.trackingState == TrackingState.Limited;
                inst.SetActive(visible);
            }
        }
    }

    /// <summary>
    /// Escribe los datos reales del vino en los TextMeshPro con formato enriquecido.
    /// </summary>
    private void UpdateWineLabels(GameObject instance, WineData data)
    {
        // Nombre: grande, negrita, dorado
        SetText(instance, "Txt_Nombre",
            $"<b><color=#C9A84C>{data.nombreVino}</color></b>");

        // Varietal: itálica, crema
        SetText(instance, "Txt_Varietal",
            $"<i><color=#F5E6CC>{data.varietal}</color></i>");

        // Perfil: blanco suave
        SetText(instance, "Txt_Perfil",
            $"<color=#FFFFFF>{data.perfil}</color>");

        // Maridaje: crema con prefijo
        SetText(instance, "Txt_Maridaje",
            $"<color=#F5E6CC><b>Maridaje:</b>  {data.maridaje}</color>");

        // Servicio: gris cálido, más pequeño
        SetText(instance, "Txt_Servicio",
            $"<size=80%><color=#E0D5C5>{data.servicio}</color></size>");
    }

    private void SetText(GameObject root, string childName, string content)
    {
        Transform t = root.transform.Find(childName);
        if (t == null) { LogToScreen("WARN: no encuentro " + childName); return; }
        var tmp = t.GetComponent<TMPro.TextMeshPro>();
        if (tmp != null) tmp.text = content;
    }

    /// <summary>
    /// Aparición suave de los textos en 1.5 segundos al detectar la etiqueta.
    /// </summary>
    private System.Collections.IEnumerator FadeInLabels(GameObject instance)
    {
        var tmps = instance.GetComponentsInChildren<TMPro.TextMeshPro>();

        // Comenzar todos en transparente
        foreach (var tmp in tmps)
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, 0f);

        float duration = 1.5f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Clamp01(elapsed / duration);
            foreach (var tmp in tmps)
                tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);
            yield return null;
        }
    }
}
