using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.InputSystem;


[RequireComponent(typeof(GestionnaireChunks))]
public class AffichageChunks : MonoBehaviour
{
    #region Variables
    [Range(0, 100)]
    public float rotationSpeed = 20f; // Vitesse de rotation en degrés par seconde

    [Tooltip("Matériaux utilisés par la touche C (si vide : couleurs générées automatiquement)")]
    public List<Material> materielsChunks = new List<Material>();
    public float dureeEvidence = 3f;

    private GestionnaireChunks p_gestionnaire;

    private bool isRotating = false;

    private List<Material> p_materielsEvidence = new List<Material>();
    private Coroutine p_coroutineEvidence;
    #endregion

    #region Unity events
    void Awake()
    {
        p_gestionnaire = GetComponent<GestionnaireChunks>();
    }

    void Update()
    {
        // Gestion de la rotation automatique du terrain avec la touche r
        if (Keyboard.current.rKey.wasPressedThisFrame)
        {
            isRotating = !isRotating;

            Camera cam = p_gestionnaire.cam;
            float dimension = p_gestionnaire.dimension;
            float hauteurMax = p_gestionnaire.hauteurMax;

            cam.GetComponent<SpectatorCamera>().enabled = !isRotating;
            cam.transform.position = isRotating ? new Vector3(dimension * 0.35f, hauteurMax * (dimension / 500f), dimension * 0.6f) : new Vector3(0f, hauteurMax, 0f);
            cam.transform.rotation = isRotating ? Quaternion.Euler(20f, 210f, 0f) : Quaternion.Euler(0f, 0f, 0f);
        }
        if (isRotating)
        {
            // rotation autour du centre de la grille de chunks
            Transform t = p_gestionnaire.transform;
            t.RotateAround(t.TransformPoint(p_gestionnaire.CentreGrille()), Vector3.up, rotationSpeed * Time.deltaTime);
        }

        // Affichage des normales si la touche n est maintenue
        if (Keyboard.current.nKey.isPressed)
        {
            foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
                chunk.DebugAllNormals(true, true, true);
        }

        // Mise en évidence des chunks avec la touche c
        if (Keyboard.current.cKey.wasPressedThisFrame)
        {
            if (p_coroutineEvidence != null)
                StopCoroutine(p_coroutineEvidence);

            p_coroutineEvidence = StartCoroutine(MettreEnEvidenceChunks());
        }
    }
    #endregion

    #region Méthodes
    private Material ObtenirMaterielEvidence(int index)
    {
        if (materielsChunks != null && materielsChunks.Count > 0)
            return materielsChunks[index % materielsChunks.Count];

        // Sinon on génère une couleur par chunk à partir du matériau du terrain
        while (p_materielsEvidence.Count <= index)
        {
            Material materiel = new Material(p_gestionnaire.materielTerrain);
            materiel.color = Color.HSVToRGB((p_materielsEvidence.Count * 0.618f) % 1f, 0.7f, 1f);
            p_materielsEvidence.Add(materiel);
        }
        return p_materielsEvidence[index];
    }

    // Un matériau différent par chunk pendant quelques secondes
    private IEnumerator MettreEnEvidenceChunks()
    {
        int numChunk = 0;
        foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
        {
            chunk.DefinirMateriel(ObtenirMaterielEvidence(numChunk));
            numChunk++;
        }

        yield return new WaitForSeconds(dureeEvidence);

        foreach (CreationSimpleTerrain chunk in p_gestionnaire.Chunks)
            chunk.DefinirMateriel(p_gestionnaire.materielTerrain);

        p_coroutineEvidence = null;
    }
    #endregion
}