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
    #endregion

    #region Paramétrage
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

    #endregion

#if UNITY_EDITOR
    // cette méthode est appelée la première fois que le component est ajouté à l'objet (ou quand reset demandé sur le component depuis l'inspector)
    // equivalent du RequireComponent mais plus de possibilité (plusieurs components) ou intialisation automatique des paramètres public 
    void Reset()
    {
        if (GetComponent<MeshFilter>() == null)
            EditorUtility.DisplayDialog("Le component MeshFilter est nécessaire à la sélection", "MeshCollider ajouté au gameObject ! ", "j'ai compris ! ");
        //else        {
        //    EditorUtility.DisplayDialog("MeshFilter initial supprimé avant génération procédurale ! ", "Le script génére un mesh procédural . Le mesh existant " + GetComponent<MeshFilter>().mesh.name + " a été disocié de  l'objet " + gameObject.name, "j'ai compris ! ");
        //    DestroyImmediate(GetComponent<MeshFilter>());
        //}
        
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


        // Gestion de la déformation du terrain avec le clic gauche de la souris
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (choixModeDeformation == ChoixModeDeformation.Fonction && typeFonction == TypeFonction.Collines)
            {
                // il faut que la caméra par defaut ait le tag MainCamera pour que le système la reconnaisse comme Camera.main
                Ray ray = p_cam.ScreenPointToRay(Mouse.current.position.ReadValue());

                // lancer un rayon et trouver les objets de la bonne layer qui entre en collision avec le
                // rayon ; l’argument HIT contient en sortie toutes les informations utiles
                RaycastHit hit;
                if (!Physics.Raycast(ray, out hit, Mathf.Infinity, maskPickingTerrain))
                    return;

                // on a sélectionné un triangle du cube avec le rayon (possible si MeshCollider !
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


        // Gestion de la rotation automatique du terrain avec la touche Alt
        if (Keyboard.current.altKey.wasPressedThisFrame)
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
            Debug.LogWarning("Aucune texture de type HeightMap n'est assignée dans l'inspecteur !");
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
    #endregion
}
