#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]


public class CreationSimpleTerrain : MonoBehaviour
{
    #region Variables

    #region Fenêtre infos
    [SerializeField] private GameObject fenetreInfo;

    [SerializeField] private TMP_Text tmpVertices;
    [SerializeField] private TMP_Text tmpTriangles;
    [SerializeField] private TMP_Text tmpMemoire;
    [SerializeField] private TMP_Text tmpModeNormales;
    [SerializeField] private TMP_Text tmpFPS;
    [SerializeField] private TMP_Text tmpInteractions;
    [SerializeField] private TMP_Text tmpLOD;
    #endregion

    #region Paramétrage
    [SerializeField] private float seuilModificationLOD = 0.1f;
    [SerializeField] private float margeActivationLOD = 0.6f;
    [Range(1, 2000)]
    public float dimension = 100;
    [Range(1, 15)]
    [Tooltip("La resolution sera 2 élévé à la puissance de cette valeur 2^n " )]
    public int puissance2Resolution = 1;
    [HideInInspector] public ushort resolution;

    [Range(0, 100)]
    public float hauteurMax = 10f;
    [Range(0, 100)]
    public float rotationSpeed = 20f; // Vitesse de rotation en degrés par seconde

    [Tooltip("Choix du mode de calcul des normales ")]
    public enum ModeCalculNormale
    {
        Basique,
        PondereeSurface,
        PondereeAngle
    }
    public ModeCalculNormale modeCalculNormale = ModeCalculNormale.Basique;

    [Tooltip("Information sur le mode de génération :  ")]
    public enum ChoixModeDeformation
    {
        Fonction,
        Texture
    }
    public ChoixModeDeformation choixModeDeformation;
    public enum TypeFonction
    {
        Sinusoide,
        Collines,
        Perlin
    }
    [Tooltip("Choix de la première fonction à appliquer ")]
    public TypeFonction typeFonction;

    [Tooltip("Choix de la première HeightMap à appliquer ")] 
    public int numTexture;
    [Tooltip("Les HeightMaps disponibles ")]
    public List<Texture2D>  textures;
    #endregion

    #region Mesh
    private MeshCollider p_meshCollider;

    private Mesh p_mesh;

    private Vector3[] p_vertices;
    private Vector3[] p_normals;
    private int[] p_triangles;
    private Vector2[] p_uvs;

    private Vector3[] p_vertices_LOD1;
    private Vector3[] p_vertices_LOD2;

    private Mesh p_meshLOD1;
    private Mesh p_meshLOD2;

    // Résolutions des différents LOD
    private int p_resolutionLOD1;
    private int p_resolutionLOD2;

    // Objets utilisés pour les MeshRenderer des LOD
    private GameObject p_objetLOD1;
    private GameObject p_objetLOD2;

    private MeshRenderer p_rendererLOD1;
    private MeshRenderer p_rendererLOD2;

    private LODGroup p_lodGroup;

    private bool p_lodsAActualiser = false;

    #endregion

    // Pour chaque index de vertex, on conserve la liste des indices de ses triangles voisins
    private List<int>[] p_trianglesVoisinsParVertex;

    private bool isRotating = false;

    private Camera p_cam;
    private LayerMask maskPickingTerrain;

    #endregion

#if UNITY_EDITOR
    // cette méthode est appelée la première fois que le component est ajouté à l'objet (ou quand reset demandé sur le component depuis l'inspector)
    // equivalent du RequireComponent mais plus de possibilité (plusieurs components) ou intialisation automatique des paramètres public 
    void Reset()
    {
        if (GetComponent<MeshFilter>() == null)
            EditorUtility.DisplayDialog("Le component MeshFilter est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");

        gameObject.AddComponent<MeshFilter>();

        Collider[] colliders = GetComponents<Collider>();
        if (colliders.Length > 0)
        {
            foreach (Collider collider in colliders)
                DestroyImmediate(collider);
            EditorUtility.DisplayDialog("Un seul MeshCollider", "Tous les collider sont supprimés ; seuls un meshCollider est associé au terrain", "OK ! ");
        }
        else
            EditorUtility.DisplayDialog("Le component MeshCollider est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
        p_meshCollider = gameObject.AddComponent<MeshCollider>();

        if (GetComponent<MeshRenderer>() == null)
        {
            EditorUtility.DisplayDialog("Le component MeshRenderer est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
            gameObject.AddComponent<MeshRenderer>();
        }
    }
#endif

    #region Unity events
    void Awake()
    {
        p_cam = Camera.main;

        // les rayCast exploiteront cette layer pour ne tester la collision qu'avec l'objet terrain
        gameObject.layer = LayerMask.NameToLayer("L_PickingTerrain");
        maskPickingTerrain = LayerMask.GetMask("L_PickingTerrain");

        p_mesh = new Mesh();
        p_meshCollider = GetComponent<MeshCollider>();
        p_meshCollider.sharedMesh = p_mesh;
        GetComponent<MeshFilter>().mesh = p_mesh;
    }

    void Start()
    {
        creerLeMeshTerrain();

        bakerVoisinageTriangles();

        creerLesLODs();
    }

    private float p_dernierTempsF11 = 0f;

    private const float DELAI_DOUBLE_CLIC = 0.5f;

    // Update is called once per frame
    void Update()
    {
        // Vérifie si un LOD doit être actualisé
        verifierActualisationLODs();

        // Gestion de l'affichage de la fenêtre d'informations avec la touche F1
        if (Keyboard.current.f1Key.wasPressedThisFrame)
        {
            bool afficher = !fenetreInfo.activeSelf;

            fenetreInfo.SetActive(afficher);

            if (afficher)
            {
                AfficherInfos();
            }
        }

        // Gestion du changement de mode de déformation des fonctions avec la touche F2
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            choixModeDeformation = ChoixModeDeformation.Fonction;
            appliquerDeformation_Fonction();
            // la fonction suivante sera utilisée au prochain appel
            typeFonction = (TypeFonction)(((int)typeFonction + 1) % 3);
        }

        // Gestion de la déformation du terrain avec le clic gauche de la souris
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (choixModeDeformation == ChoixModeDeformation.Fonction && typeFonction == TypeFonction.Collines)
            {
                // il faut que la caméra par defaut ait le tag MainCamera pour que le système la reconnaisse comme Camera.main
                Ray ray = p_cam.ScreenPointToRay(Mouse.current.position.ReadValue());

                // lancer un rayon
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, Mathf.Infinity, maskPickingTerrain))
                    return;

                // récupérer le numéro du triangle touché
                int numSelectedTri = hit.triangleIndex;

                Vector3 vertex = p_vertices[p_triangles[numSelectedTri * 3]];

                appliquerDeformation_Fonction(vertex);
            }
        }

        // Gestion du changement de mode de déformation des textures avec la touche F3
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            choixModeDeformation = ChoixModeDeformation.Texture;
            appliquerDeformation_Texture();
            // l'image'suivante sera utilisée au prochain appel     
            numTexture = (numTexture + 1) % textures.Count;
        }

        // Gestion du changement de mode de calcul des normales avec la touche F10
        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            modeCalculNormale = (ModeCalculNormale)(((int)modeCalculNormale + 1) % 3);
            for (int i = 0; i < p_vertices.Length; i++)
            {
                calculerNormaleVertex((uint)i);
            }
            p_mesh.normals = p_normals;
            p_meshCollider.sharedMesh = null;
            p_meshCollider.sharedMesh = p_mesh;

            // Les normales des LOD utilisent les nouvelles normales
            if (p_meshLOD1 != null || p_meshLOD2 != null)
            {
                p_lodsAActualiser = true;
            }
        }

        // Gestion du double clic sur F11 pour réinitialiser le terrain
        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            if (Time.time - p_dernierTempsF11 < DELAI_DOUBLE_CLIC)
            {
                // Double clic détecté : réinitialisation du terrain
                float variationMax = 0f;

                for (int i = 0; i < p_vertices.Length; i++)
                {
                    variationMax = Mathf.Max(variationMax, Mathf.Abs(p_vertices[i].y));

                    p_vertices[i].y = 0;
                    p_normals[i] = Vector3.up;
                }
                p_mesh.vertices = p_vertices;
                p_mesh.normals = p_normals;

                p_mesh.RecalculateBounds();

                p_meshCollider.sharedMesh = null;
                p_meshCollider.sharedMesh = p_mesh;

                gererActualisationLODs(variationMax);
            }
            else
            {
                // Premier clic : on enregistre le temps
                p_dernierTempsF11 = Time.time;
            }
        }

        // Gestion de la rotation automatique du terrain avec la touche Alt
        if (Keyboard.current.altKey.wasPressedThisFrame)
        {
            isRotating = !isRotating;

            Vector3 centreTerrain = new Vector3(dimension / 2f, 0, dimension / 2f);

            p_cam.GetComponent<SpectatorCamera>().enabled = !isRotating;
            p_cam.transform.position = isRotating ? new Vector3(dimension * 0.35f, hauteurMax * (dimension / 500f), dimension * 0.6f) : new Vector3(0f, hauteurMax, 0f);
            p_cam.transform.rotation = isRotating ? Quaternion.Euler(20f, 210f, 0f) : Quaternion.Euler(0f, 0f, 0f);
        }
        if (isRotating)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }

        // Affichage des normales si la touche espace est maintenue
        if (Keyboard.current.spaceKey.isPressed)
        {
            DebugAllNormals(true, true, true);
        }
    }
    #endregion

    #region Méthodes
    // Méthode pour créer le maillage du terrain
    private void creerLeMeshTerrain()
    {
        resolution = (ushort)Mathf.Pow(2, puissance2Resolution);

        float longueurCarre = dimension / resolution;
        Vector3 centreTerrain = new Vector3(dimension / 2f, 0, dimension / 2f);

        p_vertices = new Vector3[resolution * resolution];

        p_triangles = new int[(resolution - 1) * (resolution - 1) * 2 * 3];
        p_normals = new Vector3[p_vertices.Length];
        p_uvs = new Vector2[p_vertices.Length];

        for (int i = 0; i < resolution; i++)
        {
            for (int j = 0; j < resolution; j++)
            {
                p_vertices[i * resolution + j] = new Vector3(j * longueurCarre, 0, i * longueurCarre) - centreTerrain;

                p_uvs[i * resolution + j] = new Vector2((float)j / (resolution - 1), (float)i / (resolution - 1));

                p_normals[i * resolution + j] = Vector3.up;
            }
        }

        int indexTriangle = 0;
        for (int i = 0; i < resolution - 1; i++)
        {
            for (int j = 0; j < resolution - 1; j++)
            {
                p_triangles[indexTriangle++] = i * resolution + j;
                p_triangles[indexTriangle++] = (i + 1) * resolution + j;
                p_triangles[indexTriangle++] = i * resolution + (j + 1);

                p_triangles[indexTriangle++] = (i + 1) * resolution + j;
                p_triangles[indexTriangle++] = (i + 1) * resolution + (j + 1);
                p_triangles[indexTriangle++] = i * resolution + (j + 1);
            }
        }

        if (p_vertices.Length > ushort.MaxValue)
        {
            p_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        }

        p_mesh.Clear();
        p_mesh.vertices = p_vertices;
        p_mesh.triangles = p_triangles;
        p_mesh.normals = p_normals;
        p_mesh.uv = p_uvs;

        p_mesh.RecalculateBounds();
    }

    // Création des deux maillages de résolution inférieure
    private void creerLesLODs()
    {
        if (puissance2Resolution < 6)
        {
            return;
        }

        p_resolutionLOD2 = 16;

        // Pour 256 :
        // 256 -> 32 -> 16
        // Pour 128 :
        // 128 -> 32 -> 16
        // Pour 64 :
        // 64 -> 32 -> 16

        p_resolutionLOD1 = Mathf.Max(32, resolution / 8);
        p_meshLOD1 = creerMeshLOD(p_resolutionLOD1);
        p_meshLOD2 = creerMeshLOD(p_resolutionLOD2);
        p_vertices_LOD1 = p_meshLOD1.vertices;
        p_vertices_LOD2 = p_meshLOD2.vertices;

        // Création des objets enfants
        p_objetLOD1 = new GameObject("LOD1_" + p_resolutionLOD1);
        p_objetLOD2 = new GameObject("LOD2_" + p_resolutionLOD2);
        p_objetLOD1.transform.SetParent(transform, false);
        p_objetLOD2.transform.SetParent(transform, false);

        // MeshFilter LOD1
        MeshFilter meshFilterLOD1 = p_objetLOD1.AddComponent<MeshFilter>();
        meshFilterLOD1.mesh = p_meshLOD1;

        // MeshFilter LOD2
        MeshFilter meshFilterLOD2 = p_objetLOD2.AddComponent<MeshFilter>();
        meshFilterLOD2.mesh = p_meshLOD2;

        // MeshRenderer LOD1
        p_rendererLOD1 = p_objetLOD1.AddComponent<MeshRenderer>();

        // MeshRenderer LOD2
        p_rendererLOD2 = p_objetLOD2.AddComponent<MeshRenderer>();

        // On utilise le même matériau que le terrain principal
        MeshRenderer rendererPrincipal = GetComponent<MeshRenderer>();

        p_rendererLOD1.sharedMaterials = rendererPrincipal.sharedMaterials;

        p_rendererLOD2.sharedMaterials = rendererPrincipal.sharedMaterials;

        // Création du LODGroup
        p_lodGroup = gameObject.AddComponent<LODGroup>();

        LOD[] lods = new LOD[3];

        lods[0] = new LOD(0.8f, new MeshRenderer[] { rendererPrincipal });
        lods[1] = new LOD(0.6f, new MeshRenderer[] { p_rendererLOD1 });
        lods[2] = new LOD(0.4f, new MeshRenderer[] { p_rendererLOD2 });

        p_lodGroup.SetLODs(lods);
        p_lodGroup.RecalculateBounds();
        p_lodGroup.fadeMode = LODFadeMode.None;

        mettreAJourLesLODs();
    }

    // Création d'un mesh LOD à partir du terrain principal
    private Mesh creerMeshLOD(int resolutionLOD)
    {
        Mesh meshLOD = new Mesh();
        Vector3[] verticesLOD = new Vector3[resolutionLOD * resolutionLOD];
        Vector2[] uvsLOD = new Vector2[resolutionLOD * resolutionLOD];
        Vector3[] normalsLOD = new Vector3[resolutionLOD * resolutionLOD];
        int[] trianglesLOD = new int[(resolutionLOD - 1) * (resolutionLOD - 1) * 6];

        // Création des vertices
        for (int i = 0; i < resolutionLOD; i++)
        {
            for (int j = 0; j < resolutionLOD; j++)
            {
                int indexLOD = i * resolutionLOD + j;

                int indexPrincipal = trouverIndexVertexPrincipal(i, j, resolutionLOD);
                verticesLOD[indexLOD] = p_vertices[indexPrincipal];
                normalsLOD[indexLOD] = p_normals[indexPrincipal];
                uvsLOD[indexLOD] = new Vector2((float)j / (resolutionLOD - 1), (float)i / (resolutionLOD - 1));
            }
        }

        // Création des triangles
        int indexTriangle = 0;
        for (int i = 0; i < resolutionLOD - 1; i++)
        {
            for (int j = 0; j < resolutionLOD - 1; j++)
            {
                trianglesLOD[indexTriangle++] = i * resolutionLOD + j;
                trianglesLOD[indexTriangle++] = (i + 1) * resolutionLOD + j;
                trianglesLOD[indexTriangle++] = i * resolutionLOD + (j + 1);
                trianglesLOD[indexTriangle++] = (i + 1) * resolutionLOD + j;
                trianglesLOD[indexTriangle++] = (i + 1) * resolutionLOD + (j + 1);
                trianglesLOD[indexTriangle++] = i * resolutionLOD + (j + 1);
            }
        }

        if (verticesLOD.Length > ushort.MaxValue)
            meshLOD.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

        meshLOD.vertices = verticesLOD;
        meshLOD.triangles = trianglesLOD;
        meshLOD.normals = normalsLOD;
        meshLOD.uv = uvsLOD;
        meshLOD.RecalculateBounds();

        return meshLOD;
    }

    // Retourne l'index du vertex du terrain principal
    // correspondant au vertex du LOD
    private int trouverIndexVertexPrincipal(int xLOD, int zLOD, int resolutionLOD)
    {
        // On utilise les coordonnées normalisées.

        // Exemple :
        // 256 -> 32
        // le premier vertex :
        // 0 -> 0
        // le dernier :
        // 31 -> 255
        // Cela permet de conserver exactement
        // les mêmes dimensions pour les deux terrains.

        int xPrincipal = Mathf.RoundToInt((float)xLOD / (resolutionLOD - 1) * (resolution - 1));
        int zPrincipal = Mathf.RoundToInt((float)zLOD / (resolutionLOD - 1) * (resolution - 1));
        return xPrincipal * resolution + zPrincipal;
    }

    // Met à jour les deux meshes LOD
    private void mettreAJourLesLODs()
    {
        if (p_meshLOD1 == null || p_meshLOD2 == null)
            return;

        mettreAJourUnLOD(p_meshLOD1, p_vertices_LOD1, p_resolutionLOD1);

        mettreAJourUnLOD(p_meshLOD2, p_vertices_LOD2, p_resolutionLOD2);

        p_lodsAActualiser = false;

        if (p_lodGroup != null)
            p_lodGroup.RecalculateBounds();
    }

    // Mise à jour d'un seul LOD
    private void mettreAJourUnLOD(Mesh meshLOD, Vector3[] verticesLOD, int resolutionLOD)
    {
        for (int i = 0; i < resolutionLOD; i++)
        {
            for (int j = 0; j < resolutionLOD; j++)
            {
                int indexLOD = i * resolutionLOD + j;
                int indexPrincipal = trouverIndexVertexPrincipal(i, j, resolutionLOD);

                // On ne modifie le vertex que si
                // sa hauteur a réellement changé.
                if (Mathf.Abs(verticesLOD[indexLOD].y - p_vertices[indexPrincipal].y) > 0.0001f)
                {
                    verticesLOD[indexLOD].y = p_vertices[indexPrincipal].y;
                    verticesLOD[indexLOD].x = p_vertices[indexPrincipal].x;
                    verticesLOD[indexLOD].z = p_vertices[indexPrincipal].z;
                    // On récupère aussi la normale correspondante
                    // au terrain principal.
                    verticesLOD[indexLOD] = p_vertices[indexPrincipal];
                }
            }
        }

        // Les normales sont récupérées depuis
        // le terrain principal.
        Vector3[] normalsLOD = new Vector3[verticesLOD.Length];

        for (int i = 0; i < resolutionLOD; i++)
        {
            for (int j = 0; j < resolutionLOD; j++)
            {
                int indexLOD = i * resolutionLOD + j;
                int indexPrincipal = trouverIndexVertexPrincipal(i, j, resolutionLOD);
                normalsLOD[indexLOD] = p_normals[indexPrincipal];
            }
        }

        meshLOD.vertices = verticesLOD;
        meshLOD.normals = normalsLOD;
        meshLOD.RecalculateBounds();
    }

    // Vérifie si le terrain est suffisamment proche
    // pour justifier la mise à jour des LOD
    private void verifierActualisationLODs()
    {
        if (!p_lodsAActualiser)
            return;

        if (p_cam == null)
            return;

        if (lodsProchesOuActifs())
            mettreAJourLesLODs();
    }

    // Détermine si la modification du terrain
    // est suffisamment importante pour actualiser les LOD
    private void gererActualisationLODs(float variationMax)
    {
        if (p_meshLOD1 == null || p_meshLOD2 == null)
            return;

        // Modification trop faible :
        // les LOD ne sont pas actualisés.
        if (variationMax < seuilModificationLOD)
            return;

        if (lodsProchesOuActifs())
            mettreAJourLesLODs();
        else
            p_lodsAActualiser = true;
    }

    private bool lodsProchesOuActifs()
    {
        if (p_cam == null || p_lodGroup == null) return false;

        float echelle = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float tanDemiFov = Mathf.Tan(p_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distanceLOD1 = p_lodGroup.size * echelle * QualitySettings.lodBias / tanDemiFov;

        float distance = Vector3.Distance(p_cam.transform.position, transform.position);

        // 0.8 = on anticipe : on met à jour un peu avant que le LOD 1 s'active
        return distance >= distanceLOD1 * margeActivationLOD;
    }

    // Méthode pour appliquer la déformation du terrain selon la fonction choisie
    private void appliquerDeformation_Fonction(Vector3? vertexCentre = null)
    {
        //position aléatoire du centre de la fonction de déformation pour les collines
        float xCentre = vertexCentre == null ? Random.Range(0, dimension) : vertexCentre.Value.x;
        float zCentre = vertexCentre == null ? Random.Range(0, dimension) : vertexCentre.Value.z;
        float variationMax = 0f;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            Vector3 vertex = p_vertices[i];
            float ancienneHauteur = vertex.y;

            switch (typeFonction)
            {
                case TypeFonction.Sinusoide:
                    // Fonction globale : Ondulation synchrone sur X et Z
                    float freqSin = 0.1f;// plus elle est petite, plus les vagues sont larges.
                    vertex.y = Mathf.Sin(vertex.x * freqSin) * Mathf.Cos(vertex.z * freqSin) * hauteurMax;
                    break;

                case TypeFonction.Collines:
                    // Gaussienne centrée au milieu du terrain (x0, z0)
                    float distanceAuCentreSqr = (vertex.x - xCentre) * (vertex.x - xCentre) + (vertex.z - zCentre) * (vertex.z - zCentre);
                    float sigma = dimension / 4f; // Largeur de la colline

                    vertex.y = hauteurMax * Mathf.Exp(-distanceAuCentreSqr / (2 * sigma * sigma));
                    break;

                case TypeFonction.Perlin:
                    // Bruit de Perlin pseudo-aléatoire fluide
                    float scalePerlin = 0.05f;
                    vertex.y = Mathf.PerlinNoise(vertex.x * scalePerlin, vertex.z * scalePerlin) * hauteurMax;
                    break;
            }

            variationMax = Mathf.Max(variationMax, Mathf.Abs(vertex.y - ancienneHauteur));
            p_vertices[i] = vertex;
        }

        // Mise à jour du maillage
        p_mesh.vertices = p_vertices;
        for (int i = 0; i < p_vertices.Length; i++)
        {
            calculerNormaleVertex((uint)i);
        }
        p_mesh.normals = p_normals;
        p_mesh.RecalculateBounds();
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;

        // Mise à jour intelligente des LOD
        gererActualisationLODs(variationMax);
    }

    // Méthode pour appliquer la déformation du terrain selon la texture HeightMap choisie
    private void appliquerDeformation_Texture()
    {
        // Sécurité : vérifier qu'on a bien des textures dans la liste
        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("Aucune texture de type HeightMap n'est assignée dans l'inspecteur !");
            return;
        }

        // Récupération de la texture courante selon l'index numTexture
        Texture2D heightmap = textures[numTexture % textures.Count];

        if (heightmap == null) return;

        float variationMax = 0f;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            // 1. On récupère les coordonnées UV du vertex courant
            Vector2 uv = p_uvs[i];

            // 2. GetPixelBilinear permet d'obtenir le niveau
            // de gris à partir des coordonnées UV
            Color couleurPixel = heightmap.GetPixelBilinear(uv.x, uv.y);

            // 3. On extrait la valeur de gris
            float niveauDeGris = couleurPixel.grayscale;

            // 4. On conserve l'ancienne hauteur
            float ancienneHauteur = p_vertices[i].y;

            // 5. L'altitude Y devient proportionnelle
            // au niveau de gris
            p_vertices[i].y = niveauDeGris * hauteurMax;

            variationMax = Mathf.Max(variationMax, Mathf.Abs(p_vertices[i].y - ancienneHauteur));
        }

        // 5. Mise à jour du maillage et du collider
        p_mesh.vertices = p_vertices;
        for (int i = 0; i < p_vertices.Length; i++)
        {
            calculerNormaleVertex((uint)i);
        }
        p_mesh.normals = p_normals;
        p_mesh.RecalculateBounds();
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;

        // Mise à jour intelligente des LOD
        gererActualisationLODs(variationMax);
    }

    // Méthode pour calculer la normale d'un vertex en fonction des triangles voisins
    private void calculerNormaleVertex(uint num_Vertex)
    {
        Vector3 vPosition = p_vertices[num_Vertex];
        Vector3 normaleSomme = Vector3.zero;
        float sommePoids = 0f;

        // Récupération directe des triangles voisins pré-calculés
        List<int> trianglesAdjacents = p_trianglesVoisinsParVertex[num_Vertex];

        foreach (int idxTriangle in trianglesAdjacents)
        {
            int idxA = p_triangles[idxTriangle];
            int idxB = p_triangles[idxTriangle + 1];
            int idxC = p_triangles[idxTriangle + 2];

            Vector3 A = p_vertices[idxA];
            Vector3 B = p_vertices[idxB];
            Vector3 C = p_vertices[idxC];

            Vector3 u = B - A;
            Vector3 v = C - A;
            Vector3 normaleTriangle = Vector3.Cross(u, v);

            float doubleSurface = normaleTriangle.magnitude;
            if (doubleSurface < 0.00001f) continue;

            Vector3 normaleTriangleNorm = normaleTriangle / doubleSurface;

            switch (modeCalculNormale)
            {
                case ModeCalculNormale.Basique:
                    normaleSomme += normaleTriangleNorm;
                    sommePoids += 1.0f;
                    break;

                case ModeCalculNormale.PondereeSurface:
                    float surface = doubleSurface * 0.5f;
                    normaleSomme += normaleTriangleNorm * surface;
                    sommePoids += surface;
                    break;

                case ModeCalculNormale.PondereeAngle:
                    Vector3 pV = vPosition;
                    Vector3 p1 = (idxA == num_Vertex) ? B : A;
                    Vector3 p2 = (idxC == num_Vertex) ? B : C;

                    Vector3 vec1 = (p1 - pV).normalized;
                    Vector3 vec2 = (p2 - pV).normalized;

                    float angle = Mathf.Acos(Mathf.Clamp(Vector3.Dot(vec1, vec2), -1.0f, 1.0f));

                    normaleSomme += normaleTriangleNorm * angle;
                    sommePoids += angle;
                    break;
            }
        }

        if (sommePoids > 0f)
        {
            p_normals[num_Vertex] = (normaleSomme / sommePoids).normalized;
        }
    }

    // Méthode pour pré-calculer le voisinage des triangles pour chaque vertex
    private void bakerVoisinageTriangles()
    {
        // Initialisation du tableau pour chaque vertex
        p_trianglesVoisinsParVertex = new List<int>[p_vertices.Length];
        for (int i = 0; i < p_vertices.Length; i++)
        {
            p_trianglesVoisinsParVertex[i] = new List<int>();
        }

        // Parcours de tous les triangles par bloc de 3 indices
        for (int idxTriangle = 0; idxTriangle < p_triangles.Length; idxTriangle += 3)
        {
            int vA = p_triangles[idxTriangle];
            int vB = p_triangles[idxTriangle + 1];
            int vC = p_triangles[idxTriangle + 2];

            // On enregistre l'index de départ du triangle (idxTriangle) pour les 3 sommets concernés
            p_trianglesVoisinsParVertex[vA].Add(idxTriangle);
            p_trianglesVoisinsParVertex[vB].Add(idxTriangle);
            p_trianglesVoisinsParVertex[vC].Add(idxTriangle);
        }
    }
    private int obtenirLODActuel()
    {
        float echelle = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float distance = Vector3.Distance(p_cam.transform.position, transform.TransformPoint(p_lodGroup.localReferencePoint));
        float hauteurRelative = p_lodGroup.size * echelle * QualitySettings.lodBias / (2f * distance * Mathf.Tan(p_cam.fieldOfView * 0.5f * Mathf.Deg2Rad));

        LOD[] lods = p_lodGroup.GetLODs();
        for (int i = 0; i < lods.Length; i++)
        {
            if (hauteurRelative >= lods[i].screenRelativeTransitionHeight) return i;
        }
        return lods.Length;
    }

    // Méthode pour afficher les informations dans la fenêtre
    private void AfficherInfos()
    {
        // Informations sur le maillage
        tmpVertices.text = $"Vertices : {p_vertices.Length:N0}";
        tmpTriangles.text = $"Triangles : {p_triangles.Length / 3:N0}";

        // Poids mémoire approximatif
        int memoireVertices = p_vertices.Length * sizeof(float) * 3;
        int memoireNormales = p_normals.Length * sizeof(float) * 3;
        int memoireUV = p_uvs.Length * sizeof(float) * 2;
        int memoireTriangles = p_triangles.Length * sizeof(int);

        int memoireTotale = memoireVertices
                          + memoireNormales
                          + memoireUV
                          + memoireTriangles;

        tmpMemoire.text = $"Mémoire : {memoireTotale / 1024f:F2} Ko";

        // Paramètres
        tmpModeNormales.text = $"Mode normales : {modeCalculNormale}";

        // FPS
        float fps = 1f / Time.unscaledDeltaTime;
        tmpFPS.text = $"FPS : {fps:F1}";

        if (p_lodGroup == null)
        {
            tmpLOD.text = "LOD : désactivé (puissance < 6)";
        }
        else
        {
            int lodActuel = obtenirLODActuel();
            int[] resolutions = { resolution, p_resolutionLOD1, p_resolutionLOD2 };
            string actuel = lodActuel < 3 ? $"LOD {lodActuel} ({resolutions[lodActuel]}x{resolutions[lodActuel]})" : "aucun (hors champ)";
            tmpLOD.text = $"LOD : activé ({resolution} / {p_resolutionLOD1} / {p_resolutionLOD2})\nLOD affiché : {actuel}";
        }
        // Interactions
        tmpInteractions.text =
            "F1 : Afficher / masquer les informations\n" +
            "F2 : Changer de fonction\n" +
            "F3 : Changer de HeightMap\n" +
            "F10 : Changer le mode de calcul des normales\n" +
            "F11 (double click) : Réinitialiser le terrain\n" +
            "Espace (maintenu) : Afficher les normales\n" +
            "Alt : Activer / désactiver la rotation automatique du terrain";
    }

    // Méthode pour déboguer les normales
    private void DebugAllNormals(bool affN_Orientation, bool affN_Eclairage, bool affN_vertices)
    {
        if (affN_Orientation)
        {
            Vector3 vposmoyenne, v1, v2, vnormetri;
            for (int num_tri = 0; num_tri < p_triangles.Length; num_tri += 3)
            { // calcul de vnormetri comme le produit vectoriel de V01^V02
                vposmoyenne =
                p_vertices[p_triangles[num_tri]] + p_vertices[p_triangles[num_tri + 1]] +
                p_vertices[p_triangles[num_tri + 2]];
                vposmoyenne /= 3.0f;
                v1 = p_vertices[p_triangles[num_tri + 1]] - p_vertices[p_triangles[num_tri + 0]];
                v2 = p_vertices[p_triangles[num_tri + 2]] - p_vertices[p_triangles[num_tri + 0]];
                vnormetri = Vector3.Cross(v1, v2);
                Debug.DrawRay(transform.TransformPoint(vposmoyenne), transform.TransformDirection(vnormetri.normalized) * 3.0f, Color.yellow, 0.1f, false);
            }
        }
        if (affN_Eclairage)
        {
            Vector3 vposmoyenne, vnormmoyenne;
            for (int num_tri = 0; num_tri < p_triangles.Length; num_tri += 3)
            {
                vposmoyenne = (
                p_vertices[p_triangles[num_tri]] + p_vertices[p_triangles[num_tri + 1]] +
                p_vertices[p_triangles[num_tri + 2]]) / 3.0f;
                vnormmoyenne = (
                p_normals[p_triangles[num_tri]] + p_normals[p_triangles[num_tri + 1]] +
                p_normals[p_triangles[num_tri + 2]]) / 3.0f;
                Debug.DrawRay(transform.TransformPoint(vposmoyenne), transform.TransformDirection(vnormmoyenne.normalized) * 2.0f, Color.green, 0.1f, false);
            }
        }
        if (affN_vertices)
        {
            for (int num_vert = 0; num_vert < p_vertices.Length; num_vert++)
                Debug.DrawRay(transform.TransformPoint(p_vertices[num_vert]), transform.TransformDirection(p_normals[num_vert].normalized) * 1.0f, Color.red, 0.1f, false);
        }
    }
    #endregion
}