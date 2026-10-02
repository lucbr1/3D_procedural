using UnityEngine;
using System.Collections.Generic;


// Configuration partagée + grille de chunks (création, extension, raccord des bords).
// À placer sur un GameObject VIDE, avec les composants :
// DeformationChunks, ExtensionChunks, AffichageChunks et InfosChunks.
public class GestionnaireChunks : MonoBehaviour
{
    #region Variables

    #region Paramétrage

    [Header("TP5 - Jobs")]
    [Tooltip("False = version séquentielle, True = version Jobs")]
    public bool utiliserJobs = false;

    [Tooltip("Taille des lots utilisés par les IJobParallelFor")]
    [Range(1, 1024)]
    public int lotJobs = 64;
    [Range(1, 2000)]
    public float seuilModificationLOD = 0.1f;
    public float margeActivationLOD = 0.6f;
    [Range(1, 2000)]
    public float dimension = 100;   // dimension d'UN chunk
    [Range(1, 15)]
    [Tooltip("La resolution sera élevée à la puissance de cette valeur 2^n ")]
    public int puissance2Resolution = 1;
    [HideInInspector] public ushort resolution;

    [Range(0, 100)]
    public float hauteurMax = 10f;

    [Tooltip("Choix du mode de calcul des normales ")]
    public enum ModeCalculNormale
    {
        Basique,
        PondereeSurface,
        PondereeAngle
    }
    public ModeCalculNormale modeCalculNormale = ModeCalculNormale.Basique;

    public enum ChoixModeDeformation
    {
        Fonction,
        Texture
    }
    public enum TypeFonction
    {
        Sinusoide,
        Collines,
        Perlin
    }

    [Tooltip("Matériau du terrain (repris de l'ancien MeshRenderer)")]
    public Material materielTerrain;
    #endregion

    #region Déformation par pattern (lus par les chunks et l'aperçu)
    [Header("Déformation par pattern")]
    public List<AnimationCurve> patterns = new List<AnimationCurve>();
    public int indexPattern = 0;
    [Range(0.1f, 50f)] public float intensiteMax = 10f;      // unités de hauteur par seconde
    [Range(1f, 200f)] public float rayonVoisinage = 10f;   // en unités monde (local)
    public float pasIntensite = 0.5f;
    public float pasRayon = 1f;

    //TP4 - ex2
    public enum TypeDistance
    {
        Euclidienne,
        Carree,
        Manhattan,
        Chebyshev
    }
    [Header("Distance de voisinage")]
    public TypeDistance typeDistance = TypeDistance.Euclidienne;

    [Header("Aperçu de déformation")]
    [Tooltip("Matériau transparent qui utilise les couleurs de vertex (sinon Sprites/Default)")]
    public Material materielApercu;
    public float decalageApercu = 0.05f;
    #endregion

    #region Grille
    [HideInInspector] public Camera cam;
    [HideInInspector] public LayerMask maskPickingTerrain;

    // droite, gauche, haut (+z), bas (-z)
    public readonly Vector2Int[] directions = { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };

    // Grille de chunks : coordonnées entières (x, z), bornes de la grille rectangulaire
    private Dictionary<Vector2Int, CreationSimpleTerrain> p_chunks = new Dictionary<Vector2Int, CreationSimpleTerrain>();
    private int p_xMin = 0, p_xMax = 0, p_zMin = 0, p_zMax = 0;
    #endregion

    #endregion

    #region Accès à la grille
    public int XMin { get { return p_xMin; } }
    public int XMax { get { return p_xMax; } }
    public int ZMin { get { return p_zMin; } }
    public int ZMax { get { return p_zMax; } }

    public int NombreColonnes { get { return p_xMax - p_xMin + 1; } }
    public int NombreLignes { get { return p_zMax - p_zMin + 1; } }
    public int NombreChunks { get { return p_chunks.Count; } }

    // Parcours direct (ne pas créer de chunk pendant le parcours)
    public Dictionary<Vector2Int, CreationSimpleTerrain>.ValueCollection Chunks { get { return p_chunks.Values; } }

    // Copie de la liste des chunks
    public List<CreationSimpleTerrain> ObtenirListeChunks()
    {
        return new List<CreationSimpleTerrain>(p_chunks.Values);
    }

    public bool TryGetChunk(Vector2Int coord, out CreationSimpleTerrain chunk)
    {
        return p_chunks.TryGetValue(coord, out chunk);
    }

    // Centre de la grille dans le repère du gestionnaire
    public Vector3 CentreGrille()
    {
        return new Vector3((p_xMin + p_xMax) * 0.5f * dimension, 0f, (p_zMin + p_zMax) * 0.5f * dimension);
    }

    // Point visé au centre de l'écran, exprimé dans le repère du gestionnaire (commun à tous les chunks)
    public bool TrouverPointVise(out Vector3 centreGrille)
    {
        centreGrille = default;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, maskPickingTerrain))
            return false;
        centreGrille = transform.InverseTransformPoint(hit.point);
        return true;
    }
    #endregion

    public void AppliquerConfigurationJobs()
    {
        foreach (CreationSimpleTerrain chunk in p_chunks.Values)
        {
            chunk.utiliserJobs = utiliserJobs;
            chunk.lotJobs = lotJobs;
        }
    }

    #region Unity events
    void Awake()
    {
        cam = Camera.main;

        // les rayCast exploiteront cette layer pour ne tester la collision qu'avec les chunks
        maskPickingTerrain = LayerMask.GetMask("L_PickingTerrain");

        resolution = (ushort)Mathf.Pow(2, puissance2Resolution);

        if (materielTerrain == null)
            materielTerrain = new Material(Shader.Find("Standard"));

        if (patterns.Count == 0)
        {
            patterns.Add(AnimationCurve.Linear(0f, 1f, 1f, 0f));      // cône
            patterns.Add(AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));   // colline douce
            patterns.Add(new AnimationCurve(                          // plateau
                new Keyframe(0f, 1f), new Keyframe(0.6f, 1f), new Keyframe(1f, 0f)));
            patterns.Add(new AnimationCurve(                          // anneau
                new Keyframe(0f, 0f), new Keyframe(0.7f, 1f), new Keyframe(1f, 0f)));
        }
    }

    void Start()
    {
        // Premier chunk au centre de la grille
        CreerChunk(Vector2Int.zero);
    }
    #endregion

    #region Création / extension
    // Création d'un chunk aux coordonnées de grille indiquées
    private CreationSimpleTerrain CreerChunk(Vector2Int coord)
    {
        GameObject go = new GameObject("Chunk");
        go.AddComponent<MeshFilter>();
        go.AddComponent<MeshRenderer>();
        go.AddComponent<MeshCollider>();

        CreationSimpleTerrain chunk = go.AddComponent<CreationSimpleTerrain>();
        p_chunks.Add(coord, chunk);
        chunk.Initialiser(this, coord);

        RaccorderAuxVoisins(chunk, coord);

        return chunk;
    }

    // Etend la grille d'une ligne / colonne de chunks dans la direction demandée
    public void EtendreTerrain(Vector2Int dir)
    {
        int x0 = p_xMin, x1 = p_xMax, z0 = p_zMin, z1 = p_zMax;

        if (dir.x > 0) { p_xMax++; x0 = x1 = p_xMax; }
        else if (dir.x < 0) { p_xMin--; x0 = x1 = p_xMin; }
        else if (dir.y > 0) { p_zMax++; z0 = z1 = p_zMax; }
        else { p_zMin--; z0 = z1 = p_zMin; }

        for (int x = x0; x <= x1; x++)
            for (int z = z0; z <= z1; z++)
                CreerChunk(new Vector2Int(x, z));
    }
    #endregion

    #region Distance de voisinage (TP4 - ex2)
    // Teste si un vertex (écart dx, dz au centre) est dans le voisinage selon la distance choisie.
    // t : position normalisée sur le rayon (0 au centre, 1 au bord), utilisée par la courbe du pattern.
    public bool EstDansVoisinage(float dx, float dz, out float t)
    {
        float abs_dx = Mathf.Abs(dx);
        float abs_dz = Mathf.Abs(dz);

        switch (typeDistance)
        {
            case TypeDistance.Carree:
                {
                    float d2 = dx * dx + dz * dz;
                    if (d2 >= rayonVoisinage * rayonVoisinage) { t = 0f; return false; }
                    t = Mathf.Sqrt(d2) / rayonVoisinage;   // sqrt calculé seulement pour les vertices retenus
                    return true;
                }
            case TypeDistance.Manhattan:
                {
                    float d = abs_dx + abs_dz;
                    if (d >= rayonVoisinage) { t = 0f; return false; }
                    t = d / rayonVoisinage;
                    return true;
                }
            case TypeDistance.Chebyshev:
                {
                    float d = Mathf.Max(abs_dx, abs_dz);
                    if (d >= rayonVoisinage) { t = 0f; return false; }
                    t = d / rayonVoisinage;
                    return true;
                }
            default: // Euclidienne
                {
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d >= rayonVoisinage) { t = 0f; return false; }
                    t = d / rayonVoisinage;
                    return true;
                }
        }
    }
    #endregion

    #region Continuité entre chunks
    // Le nouveau chunk recopie la hauteur des bords partagés avec ses voisins existants, puis les normales sont lissées
    private void RaccorderAuxVoisins(CreationSimpleTerrain chunk, Vector2Int coord)
    {
        List<CreationSimpleTerrain> concernes = new List<CreationSimpleTerrain>();
        concernes.Add(chunk);

        foreach (Vector2Int dir in directions)
        {
            if (!p_chunks.TryGetValue(coord + dir, out CreationSimpleTerrain voisin)) continue;

            // le voisin est la source, le nouveau chunk est à (-dir) du voisin
            RaccorderBord(voisin, chunk, -dir, false);
            concernes.Add(voisin);
        }

        if (concernes.Count == 1) return;

        chunk.RecalculerToutesLesNormales();
        HarmoniserChunks(concernes);

        chunk.FinaliserDeformation(float.MaxValue);
        for (int i = 1; i < concernes.Count; i++)
            concernes[i].FinaliserDeformation(0f);
    }

    // Parcourt les vertices communs à deux chunks contigus (b est le voisin de a dans la direction dir)
    // moyenne = true : hauteur et normale moyennées ; false : b recopie la hauteur de a
    private void RaccorderBord(CreationSimpleTerrain a, CreationSimpleTerrain b, Vector2Int dir, bool moyenne)
    {
        Vector3[] vA = a.Vertices;
        Vector3[] vB = b.Vertices;
        Vector3[] nA = a.Normales;
        Vector3[] nB = b.Normales;
        int res = resolution;

        for (int k = 0; k < res; k++)
        {
            int indexA, indexB;

            if (dir.x != 0)
            {
                // bord en x : j = res-1 d'un côté, j = 0 de l'autre
                indexA = k * res + (dir.x > 0 ? res - 1 : 0);
                indexB = k * res + (dir.x > 0 ? 0 : res - 1);
            }
            else
            {
                // bord en z : i = res-1 d'un côté, i = 0 de l'autre
                indexA = (dir.y > 0 ? res - 1 : 0) * res + k;
                indexB = (dir.y > 0 ? 0 : res - 1) * res + k;
            }

            if (moyenne)
            {
                float y = (vA[indexA].y + vB[indexB].y) * 0.5f;
                vA[indexA].y = y;
                vB[indexB].y = y;

                Vector3 n = (nA[indexA] + nB[indexB]).normalized;
                nA[indexA] = n;
                nB[indexB] = n;
            }
            else
            {
                vB[indexB].y = vA[indexA].y;
            }
        }
    }

    // Raccorde les bords communs des chunks de la liste (chaque frontière n'est traitée qu'une fois)
    public void HarmoniserChunks(List<CreationSimpleTerrain> chunks)
    {
        foreach (CreationSimpleTerrain chunk in chunks)
        {
            if (p_chunks.TryGetValue(chunk.coordonnees + Vector2Int.right, out CreationSimpleTerrain droite) && chunks.Contains(droite))
                RaccorderBord(chunk, droite, Vector2Int.right, true);

            if (p_chunks.TryGetValue(chunk.coordonnees + Vector2Int.up, out CreationSimpleTerrain haut) && chunks.Contains(haut))
                RaccorderBord(chunk, haut, Vector2Int.up, true);
        }
    }
    #endregion
}
