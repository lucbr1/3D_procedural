using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;


// Fenêtre d'informations (F1)
[RequireComponent(typeof(GestionnaireChunks))]
public class InfosChunks : MonoBehaviour
{
    #region Variables
    [SerializeField] private GameObject fenetreInfo;

    [SerializeField] private TMP_Text tmpVertices;
    [SerializeField] private TMP_Text tmpTriangles;
    [SerializeField] private TMP_Text tmpMemoire;
    [SerializeField] private TMP_Text tmpModeNormales;
    [SerializeField] private TMP_Text tmpFPS;
    [SerializeField] private TMP_Text tmpInteractions;

    //TP4 - ex1
    [SerializeField] private TMP_Text tmpDeformation;
    [SerializeField] private TMP_Text tmpInteractions2;

    //TP4 - ex3
    [SerializeField] private TMP_Text tmpLOD;

    //TP5 - ex5
    [SerializeField] private TMP_Text tmpChunks;

    private GestionnaireChunks p_gestionnaire;
    #endregion

    #region Unity events
    void Awake()
    {
        p_gestionnaire = GetComponent<GestionnaireChunks>();
    }

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
    }
    #endregion

    #region Méthodes
    // Méthode pour afficher les informations dans la fenêtre
    private void AfficherInfos()
    {
        CreationSimpleTerrain reference = null;
        p_gestionnaire.TryGetChunk(Vector2Int.zero, out reference);

        long verticesTotal = 0;
        long trianglesTotal = 0;
        long memoireTotale = 0;

        // LOD 0, LOD 1, LOD 2, hors champ
        int[] compteurLOD = new int[4];
        bool lodActif = false;

        foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
        {
            verticesTotal += chunk.NombreVertices;
            trianglesTotal += chunk.NombreTriangles;
            memoireTotale += chunk.PoidsMemoire;

            int lod = chunk.ObtenirLODActuel();
            if (lod >= 0)
            {
                lodActif = true;
                compteurLOD[Mathf.Min(lod, 3)]++;
            }
        }

        // TP5 : dimensions de la grille
        tmpChunks.text = $"Grille de chunks : {p_gestionnaire.NombreColonnes} x {p_gestionnaire.NombreLignes} ({p_gestionnaire.NombreChunks} chunks)";

        // Informations sur le maillage : un chunk / l'ensemble
        tmpVertices.text = $"Vertices : {reference.NombreVertices:N0} / chunk ({verticesTotal:N0} au total)";
        tmpTriangles.text = $"Triangles : {reference.NombreTriangles:N0} / chunk ({trianglesTotal:N0} au total)";
        tmpMemoire.text = $"Mémoire : {reference.PoidsMemoire / 1024f:F2} Ko / chunk ({memoireTotale / 1024f:F2} Ko au total)";

        // Paramètres
        tmpModeNormales.text = $"Mode normales : {p_gestionnaire.modeCalculNormale}";

        // FPS
        float fps = 1f / Time.unscaledDeltaTime;
        tmpFPS.text = $"FPS : {fps:F1}";

        // Déformation (TP4 - Ex1)
        tmpDeformation.text =
            $"Intensité : {p_gestionnaire.intensiteMax:F1}\n" +
            $"Voisinage : {p_gestionnaire.rayonVoisinage:F1}\n" +
            $"Pattern : {p_gestionnaire.indexPattern + 1} / {p_gestionnaire.patterns.Count}\n" +
            $"Distance : {p_gestionnaire.typeDistance}";

        if (!lodActif)
        {
            tmpLOD.text = "LOD : désactivé (puissance < 6)";
        }
        else
        {
            int[] resolutions = reference.ResolutionsLOD;
            tmpLOD.text = $"LOD : activé ({resolutions[0]} / {resolutions[1]} / {resolutions[2]})\n" +
                          $"Chunks affichés : LOD 0 : {compteurLOD[0]} | LOD 1 : {compteurLOD[1]} | LOD 2 : {compteurLOD[2]} | hors champ : {compteurLOD[3]}";
        }

        // Interactions
        tmpInteractions.text =
            "F1 : Afficher / masquer les informations\n" +
            "F2 : Changer de fonction\n" +
            "F3 : Changer de HeightMap\n" +
            "F10 : Changer le mode de calcul des normales\n" +
            "F11 (double click) : Réinitialiser le terrain\n" +
            "N (maintenu) : Afficher les normales\n" +
            "R : Activer / désactiver la rotation automatique\n" +
            "C : Mettre en évidence les chunks\n" +
            "Echap : Annuler la création du chunk proposé";

        tmpInteractions2.text = "Clic Gauche : élever le terrain\n" +
            "Clic Droit : creuser le terrain\n" +
            "Molette + Shift : intensité de déformation\n" +
            "Molette + Ctrl : rayon de voisinage\n" +
            "Molette + Alt : changer de pattern\n" +
            "Espace (maintenu) : aperçu de la déformation\n" +
            "D : changer de distance de voisinage";
    }
    #endregion
}