using UnityEngine;
using System.Collections.Generic;


[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
[RequireComponent(typeof(MeshCollider))]


// Représente UN chunk : son mesh, ses normales et les déformations appliquées à ses vertices.
// Le LOD est géré par LODChunk, l'aperçu par ApercuDeformation.
// Les entrées clavier/souris et la grille sont gérées par GestionnaireChunks et ses composants.
public class CreationSimpleTerrain : MonoBehaviour
{
    #region Variables

    private GestionnaireChunks p_gestionnaire;

    // Position du chunk dans la grille (x, z)
    public Vector2Int coordonnees;
    [HideInInspector] public ushort resolution;

    #region Mesh
    private MeshCollider p_meshCollider;

    private Mesh p_mesh;

    private Vector3[] p_vertices;
    private Vector3[] p_normals;
    private int[] p_triangles;
    private Vector2[] p_uvs;
    #endregion

    // Pour chaque index de vertex, on conserve la liste des indices de ses triangles voisins
    private List<int>[] p_trianglesVoisinsParVertex;

    private LODChunk p_lod;
    private ApercuDeformation p_apercu;

    #region Déformation
    private float p_longueurCarre; // pas de la grille, renseigné à la création du mesh
    private bool p_updateCollider = false;
    private float p_dernierMajCollider = 0f;
    private const float DELAI_MAJ_COLLIDER = 0.1f;
    #endregion

    #endregion

    #region Accès pour les autres scripts
    // Les tableaux sont partagés (pas de copie)
    public Vector3[] Vertices { get { return p_vertices; } }
    public Vector3[] Normales { get { return p_normals; } }

    public int NombreVertices { get { return p_vertices.Length; } }
    public int NombreTriangles { get { return p_triangles.Length / 3; } }

    public int PoidsMemoire
    {
        get
        {
            return p_vertices.Length * sizeof(float) * 3
                 + p_normals.Length * sizeof(float) * 3
                 + p_uvs.Length * sizeof(float) * 2
                 + p_triangles.Length * sizeof(int);
        }
    }

    public int[] ResolutionsLOD { get { return p_lod.Resolutions; } }

    // Retourne l'index du LOD affiché (3 = hors champ, -1 = pas de LOD sur ce chunk)
    public int ObtenirLODActuel()
    {
        return p_lod.ObtenirLODActuel();
    }
    #endregion

    #region Unity events
    void Update()
    {
        // Vérifie si un LOD doit être actualisé
        if (p_lod != null)
            p_lod.VerifierActualisation();
    }
    #endregion

    #region Création
    // Appelée par le gestionnaire juste après la création du GameObject (remplace Awake / Start)
    public void Initialiser(GestionnaireChunks gestionnaire, Vector2Int coord)
    {
        p_gestionnaire = gestionnaire;
        coordonnees = coord;
        resolution = gestionnaire.resolution;

        gameObject.name = "Chunk_" + coord.x + "_" + coord.y;

        // les rayCast exploiteront cette layer pour ne tester la collision qu'avec les chunks
        gameObject.layer = LayerMask.NameToLayer("L_PickingTerrain");

        transform.SetParent(gestionnaire.transform, false);
        transform.localPosition = new Vector3(coord.x * gestionnaire.dimension, 0, coord.y * gestionnaire.dimension);

        p_mesh = new Mesh();
        p_meshCollider = GetComponent<MeshCollider>();
        p_meshCollider.sharedMesh = p_mesh;
        GetComponent<MeshFilter>().mesh = p_mesh;
        GetComponent<MeshRenderer>().sharedMaterial = gestionnaire.materielTerrain;

        creerLeMeshTerrain();

        bakerVoisinageTriangles();

        p_lod = new LODChunk(this, gestionnaire);
        p_lod.Creer();

        p_apercu = new ApercuDeformation(this, gestionnaire);
    }

    // Méthode pour créer le maillage du terrain
    private void creerLeMeshTerrain()
    {
        float dimension = p_gestionnaire.dimension;

        // (resolution - 1) pour que les vertices du bord tombent exactement sur ceux du chunk voisin
        float longueurCarre = dimension / (resolution - 1);
        p_longueurCarre = longueurCarre;
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
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;

        p_mesh.RecalculateBounds();
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
    #endregion

    #region Mesh / matériau
    public void DefinirMateriel(Material materiel)
    {
        GetComponent<MeshRenderer>().sharedMaterial = materiel;
        p_lod.DefinirMateriel(materiel);
    }

    // Envoie les vertices / normales au mesh
    public void MettreAJourMesh(bool colliderDiffere)
    {
        p_mesh.vertices = p_vertices;
        p_mesh.normals = p_normals;

        if (colliderDiffere)
            p_updateCollider = true;
    }

    // Fin d'une déformation globale (fonction, texture, reset, raccord) : mesh + collider + LOD
    public void FinaliserDeformation(float variationMax)
    {
        MettreAJourMesh(false);
        p_mesh.RecalculateBounds();
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;

        // Mise à jour intelligente des LOD
        p_lod.GererActualisation(variationMax);
    }

    // Fin d'un changement de mode de calcul des normales
    public void FinaliserNormales()
    {
        p_mesh.normals = p_normals;
        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;

        // Les normales des LOD utilisent les nouvelles normales
        p_lod.MarquerAActualiser();
    }

    // Rebaker un MeshCollider est coûteux : on le limite à 10x/s pendant le clic, et on finalise au relâchement
    public void MettreAJourColliderSiNecessaire(bool boutonEnfonce)
    {
        if (!p_updateCollider) return;
        if (boutonEnfonce && Time.time - p_dernierMajCollider < DELAI_MAJ_COLLIDER) return;

        p_meshCollider.sharedMesh = null;
        p_meshCollider.sharedMesh = p_mesh;
        p_updateCollider = false;
        p_dernierMajCollider = Time.time;
    }
    #endregion

    #region Normales
    public void RecalculerToutesLesNormales()
    {
        for (int i = 0; i < p_vertices.Length; i++)
        {
            calculerNormaleVertex((uint)i);
        }
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

            switch (p_gestionnaire.modeCalculNormale)
            {
                case GestionnaireChunks.ModeCalculNormale.Basique:
                    normaleSomme += normaleTriangleNorm;
                    sommePoids += 1.0f;
                    break;

                case GestionnaireChunks.ModeCalculNormale.PondereeSurface:
                    float surface = doubleSurface * 0.5f;
                    normaleSomme += normaleTriangleNorm * surface;
                    sommePoids += surface;
                    break;

                case GestionnaireChunks.ModeCalculNormale.PondereeAngle:
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

    // Méthode pour déboguer les normales
    public void DebugAllNormals(bool affN_Orientation, bool affN_Eclairage, bool affN_vertices)
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

    #region Déformations
    // Déformation par fonction. Les coordonnées passées à la fonction sont celles de la GRILLE (pas du chunk)
    // afin que la fonction soit continue d'un chunk à l'autre. Retourne la variation max.
    public float AppliquerFonction(GestionnaireChunks.TypeFonction typeFonction, float xCentre, float zCentre)
    {
        float variationMax = 0f;
        Vector3 decalage = transform.localPosition;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            Vector3 vertex = p_vertices[i];
            float ancienneHauteur = vertex.y;

            float x = vertex.x + decalage.x;
            float z = vertex.z + decalage.z;

            switch (typeFonction)
            {
                case GestionnaireChunks.TypeFonction.Sinusoide:
                    // Fonction globale : Ondulation synchrone sur X et Z
                    float freqSin = 0.1f;// plus elle est petite, plus les vagues sont larges.
                    vertex.y = Mathf.Sin(x * freqSin) * Mathf.Cos(z * freqSin) * p_gestionnaire.hauteurMax;
                    break;

                case GestionnaireChunks.TypeFonction.Collines:
                    // Gaussienne centrée en (xCentre, zCentre)
                    float distanceAuCentreSqr = (x - xCentre) * (x - xCentre) + (z - zCentre) * (z - zCentre);
                    float sigma = p_gestionnaire.dimension / 4f; // Largeur de la colline

                    vertex.y = p_gestionnaire.hauteurMax * Mathf.Exp(-distanceAuCentreSqr / (2 * sigma * sigma));
                    break;

                case GestionnaireChunks.TypeFonction.Perlin:
                    // Bruit de Perlin pseudo-aléatoire fluide
                    float scalePerlin = 0.05f;
                    vertex.y = Mathf.PerlinNoise(x * scalePerlin, z * scalePerlin) * p_gestionnaire.hauteurMax;
                    break;
            }

            variationMax = Mathf.Max(variationMax, Mathf.Abs(vertex.y - ancienneHauteur));
            p_vertices[i] = vertex;
        }

        RecalculerToutesLesNormales();

        return variationMax;
    }

    // Déformation par HeightMap : la texture recouvre toute la grille de chunks, comme s'il n'y avait qu'un seul terrain.
    // xMinGrille / zMinGrille : coin de la grille, largeurGrille / longueurGrille : sa taille (repère du gestionnaire).
    // Retourne la variation max.
    public float AppliquerTexture(Texture2D heightmap, float xMinGrille, float zMinGrille, float largeurGrille, float longueurGrille)
    {
        float variationMax = 0f;
        Vector3 decalage = transform.localPosition;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            // 1. Coordonnées UV du vertex dans l'ensemble de la grille (0 -> 1 sur toute la grille)
            float u = (p_vertices[i].x + decalage.x - xMinGrille) / largeurGrille;
            float v = (p_vertices[i].z + decalage.z - zMinGrille) / longueurGrille;

            // 2. GetPixelBilinear permet d'obtenir le niveau
            // de gris à partir des coordonnées UV
            float niveauDeGris = heightmap.GetPixelBilinear(u, v).grayscale;

            // 3. On conserve l'ancienne hauteur
            float ancienneHauteur = p_vertices[i].y;

            // 4. L'altitude Y devient proportionnelle au niveau de gris
            p_vertices[i].y = niveauDeGris * p_gestionnaire.hauteurMax;

            variationMax = Mathf.Max(variationMax, Mathf.Abs(p_vertices[i].y - ancienneHauteur));
        }

        RecalculerToutesLesNormales();

        return variationMax;
    }

    // Remet le chunk à plat. Retourne la variation max.
    public float Reinitialiser()
    {
        float variationMax = 0f;

        for (int i = 0; i < p_vertices.Length; i++)
        {
            variationMax = Mathf.Max(variationMax, Mathf.Abs(p_vertices[i].y));

            p_vertices[i].y = 0;
            p_normals[i] = Vector3.up;
        }

        return variationMax;
    }

    //TP4 - ex1
    // centreGrille : point visé exprimé dans le repère du gestionnaire (commun à tous les chunks).
    // Retourne false si le voisinage ne touche aucun vertex de ce chunk.
    public bool AppliquerDeformationPattern(Vector3 centreGrille, float delta, AnimationCurve courbe)
    {
        Vector3 centreLocal = centreGrille - transform.localPosition;

        CalculerFenetre(centreLocal, out int iMin, out int iMax, out int jMin, out int jMax);

        // le voisinage ne touche pas ce chunk
        if (iMin > iMax || jMin > jMax) return false;

        float variationMax = 0f;

        // test précis dans la fenêtre + application de la courbe
        for (int i = iMin; i <= iMax; i++)
        {
            for (int j = jMin; j <= jMax; j++)
            {
                int index = i * resolution + j;
                float dx = p_vertices[index].x - centreLocal.x;
                float dz = p_vertices[index].z - centreLocal.z;

                // TP4 - ex2 : le test dépend de la distance choisie (t : 0 au centre, 1 au bord)
                if (!p_gestionnaire.EstDansVoisinage(dx, dz, out float t)) continue;

                float deplacement = delta * courbe.Evaluate(t);
                p_vertices[index].y += deplacement;
                variationMax = Mathf.Max(variationMax, Mathf.Abs(deplacement));
            }
        }

        // calcul des normales : uniquement la fenêtre + 1 vertex de marge
        for (int i = Mathf.Max(0, iMin - 1); i <= Mathf.Min(resolution - 1, iMax + 1); i++)
            for (int j = Mathf.Max(0, jMin - 1); j <= Mathf.Min(resolution - 1, jMax + 1); j++)
                calculerNormaleVertex((uint)(i * resolution + j));

        // Mise à jour intelligente des LOD
        p_lod.GererActualisation(variationMax);

        return true;
    }

    // Fenêtre de vertices (indices i, j) touchée par un voisinage centré en "centre" (repère du chunk)
    // Si le centre est très loin du chunk, min > max : la fenêtre est vide
    public void CalculerFenetre(Vector3 centre, out int iMin, out int iMax, out int jMin, out int jMax)
    {
        float dimension = p_gestionnaire.dimension;

        int jCentre = Mathf.RoundToInt((centre.x + dimension / 2f) / p_longueurCarre);
        int iCentre = Mathf.RoundToInt((centre.z + dimension / 2f) / p_longueurCarre);
        int rayonCases = Mathf.CeilToInt(p_gestionnaire.rayonVoisinage / p_longueurCarre);

        jMin = Mathf.Max(0, jCentre - rayonCases); jMax = Mathf.Min(resolution - 1, jCentre + rayonCases);
        iMin = Mathf.Max(0, iCentre - rayonCases); iMax = Mathf.Min(resolution - 1, iCentre + rayonCases);
    }

    // centreGrille : point visé dans le repère du gestionnaire. Chaque chunk affiche sa partie de l'aperçu.
    public void MettreAJourApercu(bool actif, Vector3 centreGrille, float signe)
    {
        Vector3 centre = centreGrille - transform.localPosition;
        int iMin = 0, iMax = -1, jMin = 0, jMax = -1;

        if (actif)
        {
            CalculerFenetre(centre, out iMin, out iMax, out jMin, out jMax);
            actif = iMin <= iMax && jMin <= jMax;
        }

        p_apercu.MettreAJour(actif, centre, iMin, iMax, jMin, jMax, signe);
    }
    #endregion
}