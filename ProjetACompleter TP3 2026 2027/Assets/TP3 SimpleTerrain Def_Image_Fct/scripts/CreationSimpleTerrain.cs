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

    //TP4 - ex1
    [SerializeField] private TMP_Text tmpDeformation;
    #endregion

    #region Paramétrage
    [Range(1, 2000)] 
    public float dimension = 100;
    [Range(1, 15)]
    [Tooltip("La resolution sera élevée à la puissance de cette valeur 2^n " )]
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
    private Mesh        p_mesh;
    private Vector3[]   p_vertices;
    private Vector3[]   p_normals;
    private int[]       p_triangles;
    private Vector2[]   p_uvs;
    #endregion

    // Pour chaque index de vertex, on conserve la liste des indices de ses triangles voisins
    private List<int>[] p_trianglesVoisinsParVertex;

    private bool isRotating = false;

    private Camera p_cam;
    private LayerMask maskPickingTerrain;


    #region Déformation
    [Header("Déformation par pattern")]
    public List<AnimationCurve> patterns = new List<AnimationCurve>();
    public int indexPattern = 0;
    [Range(0.1f, 50f)]  public float intensiteMax = 10f;      // unités de hauteur par seconde
    [Range(1f, 200f)]   public float rayonVoisinage = 10f;   // en unités monde (local)
    public float pasIntensite = 0.5f;
    public float pasRayon = 1f;

    private float p_longueurCarre; // pas de la grille, renseigné à la création du mesh
    private bool  p_updateCollider = false;
    private float p_dernierMajCollider = 0f;
    private const float DELAI_MAJ_COLLIDER = 0.1f;
    #endregion

    #region Aperçu de déformation
    [Header("Aperçu de déformation")]
    [Tooltip("Matériau transparent qui utilise les couleurs de vertex (sinon Sprites/Default)")]
    public Material materielApercu;
    public float decalageApercu = 0.05f;

    private GameObject p_apercuGO;
    private Mesh       p_apercuMesh;
    private Vector3[]  p_apercuVertices;
    private Color[]    p_apercuCouleurs;
    private int        p_apercuNx = -1, p_apercuNy = -1;
    #endregion

    #endregion

#if UNITY_EDITOR
    // cette méthode est appelée la première fois que le component est ajoutée l'objet (ou quand reset demandé sur le component depuis l'inspector)
    // equivalent du RequireComponent mais plus de possibilités (plusieurs components) ou intialisation automatique des paramètres public 
    void Reset()
    {
        if (GetComponent<MeshFilter>() == null)
            EditorUtility.DisplayDialog("Le component MeshFilter est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
        //else        {
        //    EditorUtility.DisplayDialog("MeshFilter initial supprimé avant génération procédurale ! ", "Le script génère un mesh procédural . Le mesh existant " + GetComponent<MeshFilter>().mesh.name + " a été disocié de  l'objet " + gameObject.name, "j'ai compris ! ");
        //    DestroyImmediate(GetComponent<MeshFilter>());
        //}
        
        gameObject.AddComponent<MeshFilter>();

        Collider[] colliders = GetComponents<Collider>();
        if (colliders.Length > 0)
        {
            foreach (Collider collider in colliders)
                DestroyImmediate(collider);
            EditorUtility.DisplayDialog("Un seul MeshCollider", "Tous les collider sont supprim�s ; seuls un meshCollider est associ� au terrain", "OK ! ");
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

        if (patterns.Count == 0)
        {
            patterns.Add(AnimationCurve.Linear(0f, 1f, 1f, 0f));      // cône
            patterns.Add(AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));   // colline douce
            patterns.Add(new AnimationCurve(                          // plateau
                new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f)));
            patterns.Add(new AnimationCurve(                          // anneau
                new Keyframe(0f, 0f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
        }

        CreerApercu();
    }

    void Start()
    {
        creerLeMeshTerrain();

        bakerVoisinageTriangles();
        
    }

    private float p_dernierTempsF11 = 0f;
    private const float DELAI_DOUBLE_CLIC = 0.5f; // Temps max entre les 2 appuis (300 ms)

    // Update is called once per frame
    void Update()
    {
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
        }


        // Gestion du double clic sur F11 pour réinitialiser le terrain
        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            if (Time.time - p_dernierTempsF11 < DELAI_DOUBLE_CLIC)
            {
                // Double clic détecté : réinitialisation du terrain
                for (int i = 0; i < p_vertices.Length; i++)
                {
                    p_vertices[i].y = 0;
                    p_normals[i] = Vector3.up;
                }
                p_mesh.vertices = p_vertices;
                p_mesh.normals = p_normals;
                p_meshCollider.sharedMesh = null;
                p_meshCollider.sharedMesh = p_mesh;
            }
            else
            {
                // Premier clic : on enregistre le temps
                p_dernierTempsF11 = Time.time;
            }
        }


        // Gestion de la rotation automatique du terrain avec la touche r
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            isRotating = !isRotating;

            Vector3 centreTerrain = new Vector3(dimension / 2f, 0, dimension / 2f);

            p_cam.GetComponent<SpectatorCamera>().enabled = !isRotating;
            p_cam.transform.position = isRotating ? new Vector3(dimension * 0.35f, hauteurMax * (dimension / 500f), dimension * 0.6f) : new Vector3(0f, hauteurMax, 0f);
            p_cam.transform.rotation = isRotating ? Quaternion.Euler(20f, 210f, 0f) : Quaternion.Euler(0f, 0f, 0f);
            p_cam.GetComponent<Camera>().farClipPlane = isRotating ? 1000000f : 1000f;
        }
        if (isRotating)
        {
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime);
        }



        // Affichage des normales si la touche n est maintenue
        if (Keyboard.current.nKey.isPressed)
        {
            DebugAllNormals(true, true, true);
        }

        //TP4 - ex1

        GererMolette();
        GererDeformation();
        MettreAJourColliderSiNecessaire();
        MettreAJourApercu();
    }
    #endregion

    #region Méthodes
    // Méthode pour créer le maillage du terrain
    private void creerLeMeshTerrain()
    {
        resolution = (ushort)Mathf.Pow(2, puissance2Resolution);

        float longueurCarre = dimension / resolution;
        p_longueurCarre = longueurCarre;
        Vector3 centreTerrain = new Vector3(dimension / 2f, 0, dimension / 2f);

        p_vertices = new Vector3[(resolution) * (resolution)];
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
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
    }

    // Méthode pour appliquer la déformation du terrain selon la fonction choisie
    private void appliquerDeformation_Fonction(Vector3? vertexCentre = null)
    {
        //position aléatoire du centre de la fonction de déformation pour les collines
        float xCentre = vertexCentre == null ? Random.Range(0, dimension) : vertexCentre.Value.x;
        float zCentre = vertexCentre == null ? Random.Range(0, dimension) : vertexCentre.Value.z;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            Vector3 vertex = p_vertices[i];

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

            p_vertices[i] = vertex;
        }

        // Mise à jour du maillage
        p_mesh.vertices = p_vertices;
        for (int i = 0; i < p_vertices.Length; i++)
        {
            calculerNormaleVertex((uint)i);
        }
        p_mesh.normals = p_normals;
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
    }

    // Méthode pour appliquer la déformation du terrain selon la texture HeightMap choisie
    private void appliquerDeformation_Texture()
    {
        // Sécurité : vérifier qu'on a bien des textures dans la liste
        if (textures == null || textures.Count == 0)
        {
            Debug.LogWarning("Aucune texture de type HeightMap n'est assign�e dans l'inspecteur !");
            return;
        }

        // Récupération de la texture courante selon l'index numTexture
        Texture2D heightmap = textures[numTexture % textures.Count];

        if (heightmap == null) return;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            // 1. On récupère les coordonnées UV du vertex courant
            Vector2 uv = p_uvs[i];

            // 2. GetPixelBilinear permet d'obtenir le niveau de gris de manière très précise 
            // à partir des coordonnées UV (entre 0.0 et 1.0), peu importe la taille ou le ratio de l'image.
            Color couleurPixel = heightmap.GetPixelBilinear(uv.x, uv.y);

            // 3. On extrait la valeur de gris (grayscale / luminosité) comprise entre 0 (noir) et 1 (blanc)
            float niveauDeGris = couleurPixel.grayscale;

            // 4. L'altitude Y devient proportionnelle au niveau de gris
            p_vertices[i].y = niveauDeGris * hauteurMax;
        }

        // 5. Mise à jour du maillage et du collider
        p_mesh.vertices = p_vertices;
        for (int i = 0; i < p_vertices.Length; i++)
        {
            calculerNormaleVertex((uint)i);
        }
        p_mesh.normals = p_normals;
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
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

        // Déformation (TP4 - Ex1)
        tmpDeformation.text =
            $"Intensité : {intensiteMax:F1}\n" +
            $"Voisinage : {rayonVoisinage:F1}\n" +
            $"Pattern : {indexPattern + 1} / {patterns.Count}";

        // Interactions
        tmpInteractions.text =
            "F1 : Afficher / masquer les informations\n" +
            "F2 : Changer de fonction\n" +
            "F3 : Changer de HeightMap\n" +
            "F10 : Changer le mode de calcul des normales\n" +
            "F11 (double click) : Réinitialiser le terrain\n" +
            "N (maintenu) : Afficher les normales\n" +
            "R : Activer / désactiver la rotation automatique du terrain" +
            "Clic Gauche : élever le terrain\n" +
            "Clic Droit : creuser le terrain\n" +
            "Molette + Shift : intensité de déformation\n" +
            "Molette + Ctrl : rayon de voisinage\n" +
            "Molette + Alt : changer de pattern\n" +
            "Espace (maintenu) : aperçu de la déformation\n";
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
                Debug.DrawRay(transform.position + vposmoyenne, vnormetri.normalized * 3.0f, Color.yellow, 0.1f,
                false);
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
                Debug.DrawRay(transform.position + vposmoyenne, vnormmoyenne.normalized * 2.0f, Color.green, 0.1f, false);
            }
        }
        if (affN_vertices)
        {
            for (int num_vert = 0; num_vert < p_vertices.Length; num_vert++)
                Debug.DrawRay(transform.position + p_vertices[num_vert], p_normals[num_vert].normalized * 1.0f, Color.red,
                0.1f, false);
        }
    }


    //TP4 - ex1
    private void GererMolette()
    {
        float molette = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Approximately(molette, 0f)) return;
        int sens = molette > 0f ? 1 : -1;
        var kb = Keyboard.current;

        if (kb.shiftKey.isPressed)
            intensiteMax = Mathf.Clamp(intensiteMax + sens * pasIntensite, 0.1f, 50f);
        else if (kb.ctrlKey.isPressed)
            rayonVoisinage = Mathf.Clamp(rayonVoisinage + sens * pasRayon, 1f, 200f);
        else if (kb.altKey.isPressed)
            indexPattern = (indexPattern + sens + patterns.Count) % patterns.Count;
    }

    private bool TrouverPointVise(out Vector3 centreLocal)
    {
        centreLocal = default;
        Ray ray = p_cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, maskPickingTerrain))
            return false;
        centreLocal = transform.InverseTransformPoint(hit.point);
        return true;
    }

    private void GererDeformation()
    {
        bool gauche = Mouse.current.leftButton.isPressed;
        bool droit  = Mouse.current.rightButton.isPressed;
        if (gauche == droit) return;
        if (!TrouverPointVise(out Vector3 centreLocal)) return;
        appliquerDeformationPattern(centreLocal, gauche ? 1f : -1f);
    }

    private void appliquerDeformationPattern(Vector3 centreLocal, float signe)
    {
        AnimationCurve courbe = patterns[indexPattern];
        float delta = signe * intensiteMax * Time.deltaTime;

        CalculerFenetre(centreLocal, out int iMin, out int iMax, out int jMin, out int jMax);

        float rayonCarre = rayonVoisinage * rayonVoisinage;

        // test précis dans la fenêtre + application de la courbe
        for (int i = iMin; i <= iMax; i++)
        {
            for (int j = jMin; j <= jMax; j++)
            {
                int index = i * resolution + j;
                float dx = p_vertices[index].x - centreLocal.x;
                float dz = p_vertices[index].z - centreLocal.z;
                float d2 = dx * dx + dz * dz;
                if (d2 >= rayonCarre) continue;

                float t = Mathf.Sqrt(d2) / rayonVoisinage;   // 0 au centre, 1 au bord
                p_vertices[index].y += delta * courbe.Evaluate(t);
            }
        }
        p_mesh.vertices = p_vertices;

        // calcul des normales : uniquement la fenêtre + 1 vertex de marge
        for (int i = Mathf.Max(0, iMin - 1); i <= Mathf.Min(resolution - 1, iMax + 1); i++)
            for (int j = Mathf.Max(0, jMin - 1); j <= Mathf.Min(resolution - 1, jMax + 1); j++)
                calculerNormaleVertex((uint)(i * resolution + j));
        p_mesh.normals = p_normals;

        p_updateCollider = true;
    }

    // Rebaker un MeshCollider est coûteux : on le limite à 10x/s pendant le clic, et on finalise au relâchement
    private void MettreAJourColliderSiNecessaire()
    {
        if (!p_updateCollider) return;
        bool boutonEnfonce = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;
        if (boutonEnfonce && Time.time - p_dernierMajCollider < DELAI_MAJ_COLLIDER) return;

        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
        p_updateCollider = false;
        p_dernierMajCollider = Time.time;
    }

    private void CreerApercu()
    {
        p_apercuGO = new GameObject("ApercuDeformation");
        p_apercuGO.transform.SetParent(transform, false);   // layer 0 : ignoré par le picking

        p_apercuMesh = new Mesh { name = "ApercuDeformation" };
        p_apercuMesh.MarkDynamic();
        p_apercuGO.AddComponent<MeshFilter>().sharedMesh = p_apercuMesh;

        if (materielApercu == null)
            materielApercu = new Material(Shader.Find("Sprites/Default"));

        var mr = p_apercuGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materielApercu;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        p_apercuGO.SetActive(false);
    }

    private void CalculerFenetre(Vector3 centre, out int iMin, out int iMax, out int jMin, out int jMax)
    {
        int jCentre = Mathf.RoundToInt((centre.x + dimension / 2f) / p_longueurCarre);
        int iCentre = Mathf.RoundToInt((centre.z + dimension / 2f) / p_longueurCarre);
        int rayonCases = Mathf.CeilToInt(rayonVoisinage / p_longueurCarre);

        jMin = Mathf.Max(0, jCentre - rayonCases);  jMax = Mathf.Min(resolution - 1, jCentre + rayonCases);
        iMin = Mathf.Max(0, iCentre - rayonCases);  iMax = Mathf.Min(resolution - 1, iCentre + rayonCases);
    }

    private void MettreAJourApercu()
    {
        Vector3 centre = default;
        bool actif = Keyboard.current.spaceKey.isPressed && TrouverPointVise(out centre);

        if (p_apercuGO.activeSelf != actif) p_apercuGO.SetActive(actif);
        if (!actif) return;

        CalculerFenetre(centre, out int iMin, out int iMax, out int jMin, out int jMax);
        int nombreColonnes = jMax - jMin + 1;
        int nombreLignes = iMax - iMin + 1;

        // topologie : reconstruite seulement si la taille de la fenêtre change
        if (nombreColonnes != p_apercuNx || nombreLignes != p_apercuNy)
        {
            p_apercuNx = nombreColonnes; p_apercuNy = nombreLignes;
            p_apercuVertices = new Vector3[nombreColonnes * nombreLignes];
            p_apercuCouleurs = new Color[nombreColonnes * nombreLignes];

            int[] tri = new int[(nombreColonnes - 1) * (nombreLignes - 1) * 6];
            int t = 0;
            for (int i = 0; i < nombreLignes - 1; i++)
            {
                for (int j = 0; j < nombreColonnes - 1; j++)
                {
                    int a = i * nombreColonnes + j;
                    tri[t++] = a;      tri[t++] = a + nombreColonnes;      tri[t++] = a + 1;
                    tri[t++] = a + nombreColonnes; tri[t++] = a + nombreColonnes + 1;  tri[t++] = a + 1;
                }
            }
            p_apercuMesh.Clear();
            p_apercuMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            p_apercuMesh.vertices = p_apercuVertices;
            p_apercuMesh.triangles = tri;
        }

        // positions et couleurs : mises à jour à chaque frame
        AnimationCurve courbe = patterns[indexPattern];
        float rayonCarre = rayonVoisinage * rayonVoisinage;
        float signe = Mouse.current.rightButton.isPressed ? -1f : 1f;

        for (int i = iMin; i <= iMax; i++)
        {
            for (int j = jMin; j <= jMax; j++)
            {
                int k = (i - iMin) * nombreColonnes + (j - jMin);
                Vector3 v = p_vertices[i * resolution + j];
                float dx = v.x - centre.x, dz = v.z - centre.z;
                float d2 = dx * dx + dz * dz;

                bool dedans = d2 < rayonCarre;
                float poids = dedans ? courbe.Evaluate(Mathf.Sqrt(d2) / rayonVoisinage) : 0f;

                v.y += signe * intensiteMax * poids + decalageApercu;
                p_apercuVertices[k] = v;

                Color col = Color.Lerp(Color.blue, Color.red, poids);
                col.a = dedans ? 0.6f : 0f;
                p_apercuCouleurs[k] = col;
            }
        }

        p_apercuMesh.vertices = p_apercuVertices;
        p_apercuMesh.colors = p_apercuCouleurs;
        p_apercuMesh.RecalculateBounds();
    }
    #endregion
}
