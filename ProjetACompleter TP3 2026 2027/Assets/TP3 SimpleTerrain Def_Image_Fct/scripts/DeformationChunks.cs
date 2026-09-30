using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;


// Entrées de déformation (F2, F3, F10, F11, clics, molette, espace) appliquées à toute la grille de chunks
[RequireComponent(typeof(GestionnaireChunks))]
public class DeformationChunks : MonoBehaviour
{
    #region Variables
    [Tooltip("Information sur le mode de génération :  ")]
    public GestionnaireChunks.ChoixModeDeformation choixModeDeformation;

    [Tooltip("Choix de la première fonction à appliquer ")]
    public GestionnaireChunks.TypeFonction typeFonction;

    [Tooltip("Choix de la première HeightMap à appliquer ")]
    public int numTexture;
    [Tooltip("Les HeightMaps disponibles ")]
    public List<Texture2D> textures;

    private GestionnaireChunks p_gestionnaire;

    private List<CreationSimpleTerrain> p_chunksTouches = new List<CreationSimpleTerrain>();

    private float p_dernierTempsF11 = 0f;

    private const float DELAI_DOUBLE_CLIC = 0.5f;
    #endregion

    #region Unity events
    void Awake()
    {
        p_gestionnaire = GetComponent<GestionnaireChunks>();
    }

    void Update()
    {
        // Gestion du changement de mode de déformation des fonctions avec la touche F2
        if (Keyboard.current.f2Key.wasPressedThisFrame)
        {
            choixModeDeformation = GestionnaireChunks.ChoixModeDeformation.Fonction;
            appliquerDeformation_Fonction();
            // la fonction suivante sera utilisée au prochain appel
            typeFonction = (GestionnaireChunks.TypeFonction)(((int)typeFonction + 1) % 3);
        }

        // Gestion de la déformation du terrain avec le clic gauche de la souris
        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            if (choixModeDeformation == GestionnaireChunks.ChoixModeDeformation.Fonction && typeFonction == GestionnaireChunks.TypeFonction.Collines)
            {
                // il faut que la caméra par defaut ait le tag MainCamera pour que le système la reconnaisse comme Camera.main
                Ray ray = p_gestionnaire.cam.ScreenPointToRay(Mouse.current.position.ReadValue());

                // lancer un rayon
                if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, p_gestionnaire.maskPickingTerrain))
                {
                    // point touché exprimé dans le repère de la grille
                    appliquerDeformation_Fonction(p_gestionnaire.transform.InverseTransformPoint(hit.point));
                }
            }
        }

        // Gestion du changement de mode de déformation des textures avec la touche F3
        if (Keyboard.current.f3Key.wasPressedThisFrame)
        {
            choixModeDeformation = GestionnaireChunks.ChoixModeDeformation.Texture;
            appliquerDeformation_Texture();
            // l'image'suivante sera utilisée au prochain appel
            numTexture = (numTexture + 1) % textures.Count;
        }

        // Gestion du changement de mode de calcul des normales avec la touche F10
        if (Keyboard.current.f10Key.wasPressedThisFrame)
        {
            changerModeCalculNormales();
        }

        // Gestion du double clic sur F11 pour réinitialiser le terrain
        if (Keyboard.current.f11Key.wasPressedThisFrame)
        {
            if (Time.time - p_dernierTempsF11 < DELAI_DOUBLE_CLIC)
            {
                // Double clic détecté : réinitialisation du terrain
                reinitialiserTerrain();
            }
            else
            {
                // Premier clic : on enregistre le temps
                p_dernierTempsF11 = Time.time;
            }
        }

        //TP4 - ex1

        GererMolette();
        GererDeformation();
        MettreAJourCollidersSiNecessaire();
        MettreAJourApercu();

        //TP4 - ex2
        // Gestion du changement de distance de voisinage avec la touche D
        if (Keyboard.current.dKey.wasPressedThisFrame)
            p_gestionnaire.typeDistance = (GestionnaireChunks.TypeDistance)(((int)p_gestionnaire.typeDistance + 1) % 4);
    }
    #endregion

    #region Méthodes
    // Méthode pour appliquer la déformation du terrain selon la fonction choisie
    private void appliquerDeformation_Fonction(Vector3? centre = null)
    {
        float dimension = p_gestionnaire.dimension;

        //position aléatoire du centre de la fonction de déformation pour les collines (sur toute la grille)
        float xCentre = centre == null ? Random.Range(p_gestionnaire.XMin * dimension - dimension / 2f, p_gestionnaire.XMax * dimension + dimension / 2f) : centre.Value.x;
        float zCentre = centre == null ? Random.Range(p_gestionnaire.ZMin * dimension - dimension / 2f, p_gestionnaire.ZMax * dimension + dimension / 2f) : centre.Value.z;

        List<CreationSimpleTerrain> liste = p_gestionnaire.ObtenirListeChunks();
        float[] variations = new float[liste.Count];

        for (int i = 0; i < liste.Count; i++)
            variations[i] = liste[i].AppliquerFonction(typeFonction, xCentre, zCentre);

        p_gestionnaire.HarmoniserChunks(liste);

        for (int i = 0; i < liste.Count; i++)
            liste[i].FinaliserDeformation(variations[i]);
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

        // La HeightMap recouvre toute la grille de chunks
        float dimension = p_gestionnaire.dimension;
        float xMinGrille = p_gestionnaire.XMin * dimension - dimension / 2f;
        float zMinGrille = p_gestionnaire.ZMin * dimension - dimension / 2f;
        float largeurGrille = p_gestionnaire.NombreColonnes * dimension;
        float longueurGrille = p_gestionnaire.NombreLignes * dimension;

        List<CreationSimpleTerrain> liste = p_gestionnaire.ObtenirListeChunks();
        float[] variations = new float[liste.Count];

        for (int i = 0; i < liste.Count; i++)
            variations[i] = liste[i].AppliquerTexture(heightmap, xMinGrille, zMinGrille, largeurGrille, longueurGrille);

        p_gestionnaire.HarmoniserChunks(liste);

        for (int i = 0; i < liste.Count; i++)
            liste[i].FinaliserDeformation(variations[i]);
    }

    private void changerModeCalculNormales()
    {
        p_gestionnaire.modeCalculNormale = (GestionnaireChunks.ModeCalculNormale)(((int)p_gestionnaire.modeCalculNormale + 1) % 3);

        List<CreationSimpleTerrain> liste = p_gestionnaire.ObtenirListeChunks();

        foreach (CreationSimpleTerrain chunk in liste)
            chunk.RecalculerToutesLesNormales();

        p_gestionnaire.HarmoniserChunks(liste);

        foreach (CreationSimpleTerrain chunk in liste)
            chunk.FinaliserNormales();
    }

    private void reinitialiserTerrain()
    {
        foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
        {
            float variationMax = chunk.Reinitialiser();
            chunk.FinaliserDeformation(variationMax);
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
            p_gestionnaire.intensiteMax = Mathf.Clamp(p_gestionnaire.intensiteMax + sens * p_gestionnaire.pasIntensite, 0.1f, 50f);
        else if (kb.ctrlKey.isPressed)
            p_gestionnaire.rayonVoisinage = Mathf.Clamp(p_gestionnaire.rayonVoisinage + sens * p_gestionnaire.pasRayon, 1f, 200f);
        else if (kb.altKey.isPressed)
            p_gestionnaire.indexPattern = (p_gestionnaire.indexPattern + sens + p_gestionnaire.patterns.Count) % p_gestionnaire.patterns.Count;
    }

    private void GererDeformation()
    {
        bool gauche = Mouse.current.leftButton.isPressed;
        bool droit = Mouse.current.rightButton.isPressed;
        if (gauche == droit) return;
        if (!p_gestionnaire.TrouverPointVise(out Vector3 centreGrille)) return;
        appliquerDeformationPattern(centreGrille, gauche ? 1f : -1f);
    }

    // La déformation peut concerner jusqu'à 4 chunks (picking dans un coin)
    private void appliquerDeformationPattern(Vector3 centreGrille, float signe)
    {
        float dimension = p_gestionnaire.dimension;
        float rayonVoisinage = p_gestionnaire.rayonVoisinage;
        AnimationCurve courbe = p_gestionnaire.patterns[p_gestionnaire.indexPattern];
        float delta = signe * p_gestionnaire.intensiteMax * Time.deltaTime;

        // Plage de chunks dont l'emprise croise le disque de déformation
        int xMinChunk = Mathf.FloorToInt((centreGrille.x - rayonVoisinage + dimension / 2f) / dimension);
        int xMaxChunk = Mathf.FloorToInt((centreGrille.x + rayonVoisinage + dimension / 2f) / dimension);
        int zMinChunk = Mathf.FloorToInt((centreGrille.z - rayonVoisinage + dimension / 2f) / dimension);
        int zMaxChunk = Mathf.FloorToInt((centreGrille.z + rayonVoisinage + dimension / 2f) / dimension);

        p_chunksTouches.Clear();

        for (int x = xMinChunk; x <= xMaxChunk; x++)
        {
            for (int z = zMinChunk; z <= zMaxChunk; z++)
            {
                if (!p_gestionnaire.TryGetChunk(new Vector2Int(x, z), out CreationSimpleTerrain chunk)) continue;

                if (chunk.AppliquerDeformationPattern(centreGrille, delta, courbe))
                    p_chunksTouches.Add(chunk);
            }
        }

        // continuité des hauteurs et des normales sur les frontières entre chunks touchés
        p_gestionnaire.HarmoniserChunks(p_chunksTouches);

        foreach (CreationSimpleTerrain chunk in p_chunksTouches)
            chunk.MettreAJourMesh(true);
    }

    private void MettreAJourCollidersSiNecessaire()
    {
        bool boutonEnfonce = Mouse.current.leftButton.isPressed || Mouse.current.rightButton.isPressed;

        foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
            chunk.MettreAJourColliderSiNecessaire(boutonEnfonce);
    }

    private void MettreAJourApercu()
    {
        Vector3 centre = default;
        bool actif = Keyboard.current.spaceKey.isPressed && p_gestionnaire.TrouverPointVise(out centre);
        float signe = Mouse.current.rightButton.isPressed ? -1f : 1f;

        foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
            chunk.MettreAJourApercu(actif, centre, signe);
    }
    #endregion
}