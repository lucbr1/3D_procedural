using UnityEngine;


public class LODChunk
{
    #region Variables
    private CreationSimpleTerrain p_chunk;
    private GestionnaireChunks p_gestionnaire;
    private Camera p_cam;
    private int p_resolution;

    private Vector3[] p_vertices_LOD1;
    private Vector3[] p_vertices_LOD2;

    private Mesh p_meshLOD1;
    private Mesh p_meshLOD2;

    private int p_resolutionLOD1;
    private int p_resolutionLOD2;

    private GameObject p_objetLOD1;
    private GameObject p_objetLOD2;

    private MeshRenderer p_rendererLOD1;
    private MeshRenderer p_rendererLOD2;

    private LODGroup p_lodGroup;

    private bool p_lodsAActualiser = false;
    #endregion

    public int[] Resolutions { get { return new int[] { p_resolution, p_resolutionLOD1, p_resolutionLOD2 }; } }

    public LODChunk(CreationSimpleTerrain chunk, GestionnaireChunks gestionnaire)
    {
        p_chunk = chunk;
        p_gestionnaire = gestionnaire;
        p_cam = gestionnaire.cam;
        p_resolution = chunk.resolution;
    }

    #region Création
    // Création des deux maillages de résolution inférieure
    public void Creer()
    {
        if (p_gestionnaire.puissance2Resolution < 6)
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

        p_resolutionLOD1 = Mathf.Max(32, p_resolution / 8);
        p_meshLOD1 = creerMeshLOD(p_resolutionLOD1);
        p_meshLOD2 = creerMeshLOD(p_resolutionLOD2);
        p_vertices_LOD1 = p_meshLOD1.vertices;
        p_vertices_LOD2 = p_meshLOD2.vertices;

        // Création des objets enfants
        p_objetLOD1 = new GameObject("LOD1_" + p_resolutionLOD1);
        p_objetLOD2 = new GameObject("LOD2_" + p_resolutionLOD2);
        p_objetLOD1.transform.SetParent(p_chunk.transform, false);
        p_objetLOD2.transform.SetParent(p_chunk.transform, false);

        MeshRenderer rendererPrincipal = p_chunk.GetComponent<MeshRenderer>();


        MeshFilter meshFilterLOD1 = p_objetLOD1.AddComponent<MeshFilter>();
        meshFilterLOD1.mesh = p_meshLOD1;
        p_rendererLOD1 = p_objetLOD1.AddComponent<MeshRenderer>();
        p_rendererLOD1.sharedMaterials = rendererPrincipal.sharedMaterials;


        MeshFilter meshFilterLOD2 = p_objetLOD2.AddComponent<MeshFilter>();
        meshFilterLOD2.mesh = p_meshLOD2;
        p_rendererLOD2 = p_objetLOD2.AddComponent<MeshRenderer>();
        p_rendererLOD2.sharedMaterials = rendererPrincipal.sharedMaterials;

        // Création du LODGroup
        p_lodGroup = p_chunk.gameObject.AddComponent<LODGroup>();

        LOD[] lods = new LOD[3];

        lods[0] = new LOD(0.8f, new MeshRenderer[] { rendererPrincipal });
        lods[1] = new LOD(0.6f, new MeshRenderer[] { p_rendererLOD1 });
        lods[2] = new LOD(0.2f, new MeshRenderer[] { p_rendererLOD2 });

        p_lodGroup.SetLODs(lods);
        p_lodGroup.RecalculateBounds();
        p_lodGroup.fadeMode = LODFadeMode.None;

        mettreAJourLesLODs();
    }

    //création d'un mesh LOD à partir du terrain principal
    private Mesh creerMeshLOD(int resolutionLOD)
    {
        Vector3[] vertices = p_chunk.Vertices;
        Vector3[] normals = p_chunk.Normales;

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
                verticesLOD[indexLOD] = vertices[indexPrincipal];
                normalsLOD[indexLOD] = normals[indexPrincipal];
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

    private int trouverIndexVertexPrincipal(int xLOD, int zLOD, int resolutionLOD)
    {
        // On utilise les coordonnées normalisées.
        // Le premier et le dernier vertex du LOD correspondent exactement
        // au premier et au dernier vertex du terrain principal
        int xPrincipal = Mathf.RoundToInt((float)xLOD / (resolutionLOD - 1) * (p_resolution - 1));
        int zPrincipal = Mathf.RoundToInt((float)zLOD / (resolutionLOD - 1) * (p_resolution - 1));
        return xPrincipal * p_resolution + zPrincipal;
    }
    #endregion

    #region Mise à jour
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


    private void mettreAJourUnLOD(Mesh meshLOD, Vector3[] verticesLOD, int resolutionLOD)
    {
        Vector3[] vertices = p_chunk.Vertices;
        Vector3[] normals = p_chunk.Normales;

        for (int i = 0; i < resolutionLOD; i++)
        {
            for (int j = 0; j < resolutionLOD; j++)
            {
                int indexLOD = i * resolutionLOD + j;
                int indexPrincipal = trouverIndexVertexPrincipal(i, j, resolutionLOD);

                // On ne modifie le vertex que si
                // sa hauteur a réellement changé
                if (Mathf.Abs(verticesLOD[indexLOD].y - vertices[indexPrincipal].y) > 0.0001f)
                {
                    verticesLOD[indexLOD] = vertices[indexPrincipal];
                }
            }
        }

        //les normales sont récupérées depuis le terrain principal
        Vector3[] normalsLOD = new Vector3[verticesLOD.Length];

        for (int i = 0; i < resolutionLOD; i++)
        {
            for (int j = 0; j < resolutionLOD; j++)
            {
                int indexLOD = i * resolutionLOD + j;
                int indexPrincipal = trouverIndexVertexPrincipal(i, j, resolutionLOD);
                normalsLOD[indexLOD] = normals[indexPrincipal];
            }
        }

        meshLOD.vertices = verticesLOD;
        meshLOD.normals = normalsLOD;
        meshLOD.RecalculateBounds();
    }

    //vérifie si le terrain est suffisamment proche pour justifier la mise à jour des LOD
    public void VerifierActualisation()
    {
        if (!p_lodsAActualiser)
            return;

        if (p_cam == null)
            return;

        if (lodsProchesOuActifs())
            mettreAJourLesLODs();
    }

    public void GererActualisation(float variationMax)
    {
        if (p_meshLOD1 == null || p_meshLOD2 == null)
            return;

        // si la variation maximale de hauteur est inférieure au seuil, on ne met pas à jour les LOD
        if (variationMax < p_gestionnaire.seuilModificationLOD)
            return;

        if (lodsProchesOuActifs())
            mettreAJourLesLODs();
        else
            p_lodsAActualiser = true;
    }

    // demande une actualisation dès que le terrain sera assez proche
    public void MarquerAActualiser()
    {
        if (p_meshLOD1 != null || p_meshLOD2 != null)
        {
            p_lodsAActualiser = true;
        }
    }

    private bool lodsProchesOuActifs()
    {
        if (p_cam == null || p_lodGroup == null) return false;

        Transform transform = p_chunk.transform;

        float echelle = Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z);
        float tanDemiFov = Mathf.Tan(p_cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float distanceLOD1 = p_lodGroup.size * echelle * QualitySettings.lodBias / tanDemiFov;

        float distance = Vector3.Distance(p_cam.transform.position, transform.position);

        //on met à jour un peu avant que le LOD 1 s'active
        return distance >= distanceLOD1 * p_gestionnaire.margeActivationLOD;
    }
    #endregion

    #region Infos / matériau
    public int ObtenirLODActuel()
    {
        if (p_lodGroup == null || p_cam == null) return -1;

        Transform transform = p_chunk.transform;

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

    public void DefinirMateriel(Material materiel)
    {
        if (p_rendererLOD1 != null)
            p_rendererLOD1.sharedMaterial = materiel;

        if (p_rendererLOD2 != null)
            p_rendererLOD2.sharedMaterial = materiel;
    }
    #endregion
}