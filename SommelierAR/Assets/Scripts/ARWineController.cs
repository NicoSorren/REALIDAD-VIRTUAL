using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARTrackedImageManager))]
public class ARWineController : MonoBehaviour
{
    // ── Configuración AR ─────────────────────────────────────────────────────
    [Header("AR Configuration")]
    private ARTrackedImageManager _imageManager;

    [Header("Escenario 1: Marcador Menu (Carta)")]
    [Tooltip("Tracking Name: Ej. EtiquetaVino")]
    public string targetMenu = "EtiquetaVino";
    [Tooltip("Prefab del Holograma 3D + Textos")]
    public GameObject menuPrefab;

    [Header("Escenario 2: Marcador Botella Fisica")]
    [Tooltip("Tracking Name: Ej. EtiquetaSexyFish")]
    public string targetRealBottle = "EtiquetaVinoTinto";
    [Tooltip("Prefab original que ya venías usando (HologramaVino)")]
    public GameObject realBottlePrefab;

    // ── Estructura de una página ──────────────────────────────────────────────
    [System.Serializable]
    public struct WinePage
    {
        [Tooltip("Título principal (Txt_Nombre)")]
        public string titulo;
        [Tooltip("Subtítulo (Txt_Varietal)")]
        public string subtitulo;
        [Tooltip("Línea 1 (Txt_Perfil)")]
        public string linea1;
        [Tooltip("Línea 2 (Txt_Maridaje)")]
        public string linea2;
        [Tooltip("Línea 3 (Txt_Servicio)")]
        public string linea3;
    }

    [Header("Escenario 1: Datos Cabernet (Menú)")]
    public WinePage[] pagesMenu;

    [Header("Escenario 2: Datos Malbec (Botella Física)")]
    public WinePage[] pagesRealBottle;

    // ── Estado interno ────────────────────────────────────────────────────────
    private Dictionary<string, GameObject> _spawnedInstances = new Dictionary<string, GameObject>();
    private int  _currentPage  = 0;
    private bool _isAnimating  = false;

    // ── Debug (solo consola) ──────────────────────────────────────────────────
    private void LogToScreen(string msg)
    {
        Debug.Log("[SommelierAR] " + msg);
    }

    // ── Ciclo de vida ─────────────────────────────────────────────────────────
    void Awake()
    {
        _imageManager = GetComponent<ARTrackedImageManager>();
        InitDefaultPages();
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

    void Update()
    {
        bool hasActiveInstances = false;
        foreach (var kvp in _spawnedInstances)
        {
            if (kvp.Value.activeSelf) hasActiveInstances = true;
        }

        // Paginación activa para cualquioer escenario que esté visible
        if (hasActiveInstances && !_isAnimating)
        {
            bool isTouched = (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began);
            
#if UNITY_EDITOR
            bool spacePressed = Input.GetKeyDown(KeyCode.Space);
            if (isTouched || spacePressed)
#else
            if (isTouched)
#endif
            {
                StartCoroutine(ChangePage());
            }
        }
    }

    // ── Detección de imágenes ─────────────────────────────────────────────────
    private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            string imgName = trackedImage.referenceImage.name;
            LogToScreen("Detectando: " + imgName);

            // ==========================================
            // ESCENARIO 1: DETECCIÓN DE LA CARTA / MENÚ
            // ==========================================
            if (imgName == targetMenu && !_spawnedInstances.ContainsKey(imgName))
            {
                if (menuPrefab != null)
                {
                    var inst = Instantiate(menuPrefab, trackedImage.transform.position, trackedImage.transform.rotation);
                    inst.transform.parent = trackedImage.transform;
                    _spawnedInstances.Add(imgName, inst);

                    _currentPage = 0;
                    ApplyPage(inst, pagesMenu, _currentPage);
                    StartCoroutine(FadeAllTo(inst, 0f, 1f, 1.5f));
                }
            }
            // ==========================================
            // ESCENARIO 2: DETECCIÓN BOTELLA FÍSICA
            // ==========================================
            else if (imgName == targetRealBottle && !_spawnedInstances.ContainsKey(imgName))
            {
                if (realBottlePrefab != null)
                {
                    var inst = Instantiate(realBottlePrefab, trackedImage.transform.position, trackedImage.transform.rotation);
                    inst.transform.parent = trackedImage.transform;
                    _spawnedInstances.Add(imgName, inst);

                    _currentPage = 0;
                    ApplyPage(inst, pagesRealBottle, _currentPage); // VOLVEMOS A APLICAR LA PAGINACIÓN COMPLETAMENTE INDEPENDIENTE
                    StartCoroutine(FadeAllTo(inst, 0f, 1f, 1.5f));
                }
            }
        }

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

    // ── Cambio de página (Aplica a CUALQUIER escenario instanciado) ───────────
    private IEnumerator ChangePage()
    {
        _isAnimating = true;

        // Fade out
        foreach (var kvp in _spawnedInstances)
        {
            if (kvp.Value.activeSelf) yield return FadeAllTo(kvp.Value, 1f, 0f, 0.35f);
        }

        // Avanzar contador general
        _currentPage++;

        // Actualizar contenido dependiendo de a qué escenario pertenece el prefab activo
        foreach (var kvp in _spawnedInstances)
        {
            if (kvp.Value.activeSelf) 
            {
                WinePage[] activePages = (kvp.Key == targetMenu) ? pagesMenu : pagesRealBottle;
                int pageIndex = _currentPage % activePages.Length;
                ApplyPage(kvp.Value, activePages, pageIndex);
            }
        }

        // Fade in
        foreach (var kvp in _spawnedInstances)
        {
            if (kvp.Value.activeSelf) yield return FadeAllTo(kvp.Value, 0f, 1f, 0.8f);
        }

        LogToScreen("Cambio de página ejecutado.");
        _isAnimating = false;
    }

    // ── Aplicar contenido a los textos ────────────────────────────────────────
    private void ApplyPage(GameObject instance, WinePage[] arrayRef, int pageIndex)
    {
        WinePage page = arrayRef[pageIndex];

        SetText(instance, "Txt_Nombre",   $"<b><color=#C9A84C>{page.titulo}</color></b>");
        SetText(instance, "Txt_Varietal", $"<i><color=#F5E6CC>{page.subtitulo}</color></i>");
        SetText(instance, "Txt_Perfil",   $"<color=#FFFFFF>{page.linea1}</color>");
        SetText(instance, "Txt_Maridaje", $"<color=#F5E6CC>{page.linea2}</color>");
        SetText(instance, "Txt_Servicio", $"<size=80%><color=#E0D5C5>{page.linea3}</color></size>");

        // Paginador visual de puntitos
        int total = arrayRef.Length;
        string dots = "";
        for (int i = 0; i < total; i++)
            dots += (i == pageIndex) ? "<color=#C9A84C>●</color> " : "<color=#888888>○</color> ";
        SetText(instance, "Txt_Paginas", dots.TrimEnd());

        // Mostrar mapa solo en página 2 (El Terroir)
        Transform mapa = instance.transform.Find("Panel_Mapa");
        if (mapa != null) mapa.gameObject.SetActive(pageIndex == 1);
    }

    private void SetText(GameObject root, string childName, string content)
    {
        Transform t = root.transform.Find(childName);
        if (t == null) return; 
        var tmp = t.GetComponent<TMPro.TextMeshPro>();
        if (tmp != null) tmp.text = content;
    }

    // ── Animación de fade limpia ──────────────────────────────────────────────
    private IEnumerator FadeAllTo(GameObject instance, float from, float to, float duration)
    {
        if (instance == null) yield break;
        
        var tmps = instance.GetComponentsInChildren<TMPro.TextMeshPro>(true);

        foreach (var tmp in tmps)
            tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, from);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            foreach (var tmp in tmps)
            {
                if (tmp != null) tmp.color = new Color(tmp.color.r, tmp.color.g, tmp.color.b, alpha);
            }
            yield return null;
        }
    }

    // ── Datos por defecto ───────────────────────────
    private void InitDefaultPages()
    {
        if (pagesMenu == null || pagesMenu.Length == 0)
        {
            pagesMenu = new WinePage[]
            {
                new WinePage
                {
                    titulo    = "Cabernet Sauvignon",
                    subtitulo = "Reserva 2022  ·  Mendoza, Argentina",
                    linea1    = "Intenso  ·  Estructurado  ·  Elegante",
                    linea2    = "Maridaje:  Carnes asadas  ·  Caza  ·  Quesos",
                    linea3    = "Servir a 18°C   |   14.5% Alc.   |   750 ml"
                },
                new WinePage
                {
                    titulo    = "El Terroir",
                    subtitulo = "Valle de Uco  ·  Mendoza",
                    linea1    = "Altitud:  1.100 msnm",
                    linea2    = "Suelo:  Calcáreo y pedregoso",
                    linea3    = "Gran amplitud térmica  ·  Vendimia manual"
                },
                new WinePage
                {
                    titulo    = "La Elaboración",
                    subtitulo = "Fermentación a 28°C  ·  15 días",
                    linea1    = "Maceración prolongada",
                    linea2    = "Crianza:  Roble francés  ·  12 meses",
                    linea3    = "Alcohol 14.5% vol.  ·  Potencial de guarda"
                },
                new WinePage
                {
                    titulo    = "Notas del Sommelier",
                    subtitulo = "Color:  Rojo rubí profundo",
                    linea1    = "Nariz:  Casis  ·  Pimiento  ·  Tabaco",
                    linea2    = "Boca:  Taninos firmes  ·  Final persistente",
                    linea3    = "Estructura compleja  ·  pH 3.7"
                }
            };
        }

        if (pagesRealBottle == null || pagesRealBottle.Length == 0)
        {
            pagesRealBottle = new WinePage[]
            {
                new WinePage
                {
                    titulo    = "Viñas de Balbo  ·  Bodega Balbo",
                    subtitulo = "Malbec 2024  ·  Mendoza, Argentina",
                    linea1    = "Frutal  ·  Equilibrado  ·  Final delicado",
                    linea2    = "Maridaje:  Carnes rojas  ·  Pastas  ·  Quesos",
                    linea3    = "Servir a 18°C   |   14% Alc.   |   750 ml"
                },
                new WinePage
                {
                    titulo    = "El Terroir",
                    subtitulo = "Luján de Cuyo  ·  Mendoza",
                    linea1    = "Altitud:  900 – 1.200 msnm",
                    linea2    = "Suelo:  Aluvial pedregoso",
                    linea3    = "Clima árido continental  ·  Vendimia Abril 2024"
                },
                new WinePage
                {
                    titulo    = "La Elaboración",
                    subtitulo = "Fermentación a 26°C  ·  12 días",
                    linea1    = "Maceración pelicular larga",
                    linea2    = "Crianza:  Roble francés  ·  6 meses",
                    linea3    = "Alcohol 14% vol.  ·  750 ml"
                },
                new WinePage
                {
                    titulo    = "Notas del Sommelier",
                    subtitulo = "Color:  Rojo violáceo intenso",
                    linea1    = "Nariz:  Ciruelas  ·  Frambuesas  ·  Vainilla",
                    linea2    = "Boca:  Pleno  ·  Taninos maduros  ·  Final largo",
                    linea3    = "Acidez media-alta  ·  pH 3.6"
                }
            };
        }
    }
}
