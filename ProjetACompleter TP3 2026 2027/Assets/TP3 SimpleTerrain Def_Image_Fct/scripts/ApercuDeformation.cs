using UnityEngine;


public class ApercuDeformation
{
    private CreationSimpleTerrain p_chunk;
    private GestionnaireChunks p_gestionnaire;

    private GameObject p_apercuGO;
    private Mesh p_apercuMesh;
    private Vector3[] p_apercuVertices;
    private Color[] p_apercuCouleurs;
    private int p_apercuNx = -1, p_apercuNy = -1;

    public ApercuDeformation(CreationSimpleTerrain chunk, GestionnaireChunks gestionnaire)
    {
        p_chunk = chunk;
        p_gestionnaire = gestionnaire;

        p_apercuGO = new GameObject("ApercuDeformation");
        p_apercuGO.transform.SetParent(chunk.transform, false);   // layer 0 : ignoré par le picking

        p_apercuMesh = new Mesh { name = "ApercuDeformation" };
        p_apercuMesh.MarkDynamic();
        p_apercuGO.AddComponent<MeshFilter>().sharedMesh = p_apercuMesh;

        Material materielApercu = gestionnaire.materielApercu;
        if (materielApercu == null)
            materielApercu = new Material(Shader.Find("Sprites/Default"));

        var mr = p_apercuGO.AddComponent<MeshRenderer>();
        mr.sharedMaterial = materielApercu;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        p_apercuGO.SetActive(false);
    }

    // centre : point visé dans le repère du chunk ; i/j Min/Max : fenêtre de vertices concernée
    public void MettreAJour(bool actif, Vector3 centre, int iMin, int iMax, int jMin, int jMax, float signe)
    {
        if (p_apercuGO.activeSelf != actif) p_apercuGO.SetActive(actif);
        if (!actif) return;

        Vector3[] vertices = p_chunk.Vertices;
        int resolution = p_chunk.resolution;

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
                    tri[t++] = a; tri[t++] = a + nombreColonnes; tri[t++] = a + 1;
                    tri[t++] = a + nombreColonnes; tri[t++] = a + nombreColonnes + 1; tri[t++] = a + 1;
                }
            }
            p_apercuMesh.Clear();
            p_apercuMesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            p_apercuMesh.vertices = p_apercuVertices;
            p_apercuMesh.triangles = tri;
        }

        // positions et couleurs : mises à jour à chaque frame
        AnimationCurve courbe = p_gestionnaire.patterns[p_gestionnaire.indexPattern];

        for (int i = iMin; i <= iMax; i++)
        {
            for (int j = jMin; j <= jMax; j++)
            {
                int k = (i - iMin) * nombreColonnes + (j - jMin);
                Vector3 v = vertices[i * resolution + j];
                float dx = v.x - centre.x, dz = v.z - centre.z;

                bool dedans = p_gestionnaire.EstDansVoisinage(dx, dz, out float t);
                float poids = dedans ? courbe.Evaluate(t) : 0f;

                v.y += signe * p_gestionnaire.intensiteMax * poids + p_gestionnaire.decalageApercu;
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
}