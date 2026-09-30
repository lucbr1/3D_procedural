using UnityEngine;
using UnityEngine.InputSystem;


// Ajout automatique de chunks quand la caméra s'approche d'un bord :
// plan fantôme d'avertissement, validation en s'approchant, annulation avec Echap
[RequireComponent(typeof(GestionnaireChunks))]
public class ExtensionChunks : MonoBehaviour
{
    #region Variables
    [Tooltip("Distance au bord à partir de laquelle le plan fantôme apparait")]
    public float distanceFantome = 30f;
    [Tooltip("Distance au bord à partir de laquelle le chunk est créé")]
    public float distanceCreation = 10f;

    private GestionnaireChunks p_gestionnaire;

    // un plan fantôme et un état d'annulation par direction (mêmes index que GestionnaireChunks.directions)
    private GameObject[] p_fantomes = new GameObject[4];
    private bool[] p_annulee = new bool[4];
    #endregion

    #region Unity events
    void Awake()
    {
        p_gestionnaire = GetComponent<GestionnaireChunks>();
    }

    void Start()
    {
        CreerFantomes();
    }

    void Update()
    {
        GererExtensionAutomatique();
    }
    #endregion

    #region Méthodes
    // Distance de la caméra au bord de la grille dans la direction dir
    // (float.MaxValue si la caméra n'est pas en face de ce bord)
    private float DistanceAuBord(Vector2Int dir)
    {
        float dimension = p_gestionnaire.dimension;
        Vector3 c = p_gestionnaire.transform.InverseTransformPoint(p_gestionnaire.cam.transform.position);
        float demi = dimension / 2f;
        float xMinM = p_gestionnaire.XMin * dimension - demi;
        float xMaxM = p_gestionnaire.XMax * dimension + demi;
        float zMinM = p_gestionnaire.ZMin * dimension - demi;
        float zMaxM = p_gestionnaire.ZMax * dimension + demi;

        if (dir.x != 0)
        {
            if (c.z < zMinM - distanceFantome || c.z > zMaxM + distanceFantome) return float.MaxValue;
            return dir.x > 0 ? xMaxM - c.x : c.x - xMinM;
        }

        if (c.x < xMinM - distanceFantome || c.x > xMaxM + distanceFantome) return float.MaxValue;
        return dir.y > 0 ? zMaxM - c.z : c.z - zMinM;
    }

    // Le chunk est proposé (plan fantôme) puis créé quand la caméra s'approche du bord. Echap annule.
    private void GererExtensionAutomatique()
    {
        for (int d = 0; d < 4; d++)
        {
            Vector2Int dir = p_gestionnaire.directions[d];
            float distance = DistanceAuBord(dir);

            // on réarme la direction quand la caméra s'éloigne
            if (distance > distanceFantome)
                p_annulee[d] = false;

            bool proche = distance < distanceFantome && !p_annulee[d];

            // Echap : on refuse la création du chunk prévisualisé
            if (proche && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                p_annulee[d] = true;
                proche = false;
            }

            // assez proche : le chunk est validé
            if (proche && distance < distanceCreation)
            {
                p_gestionnaire.EtendreTerrain(dir);
                proche = false;
            }

            MettreAJourFantome(d, proche);
        }
    }

    private void CreerFantomes()
    {
        Material materielFantome = new Material(Shader.Find("Sprites/Default"));
        materielFantome.color = new Color(0.2f, 0.8f, 1f, 0.4f);

        for (int d = 0; d < 4; d++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Fantome_" + d;
            Destroy(go.GetComponent<Collider>());   // le plan fantôme ne doit pas gêner le picking
            go.transform.SetParent(p_gestionnaire.transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   // le quad est couché sur le plan XZ
            go.GetComponent<MeshRenderer>().sharedMaterial = materielFantome;

            go.SetActive(false);
            p_fantomes[d] = go;
        }
    }

    // Place le plan fantôme sur la ligne / colonne de chunks qui serait créée
    private void MettreAJourFantome(int d, bool visible)
    {
        GameObject go = p_fantomes[d];
        if (go.activeSelf != visible) go.SetActive(visible);
        if (!visible) return;

        float dimension = p_gestionnaire.dimension;
        Vector2Int dir = p_gestionnaire.directions[d];
        Vector3 centre = p_gestionnaire.CentreGrille();
        float largeurGrille = p_gestionnaire.NombreColonnes * dimension;
        float longueurGrille = p_gestionnaire.NombreLignes * dimension;

        if (dir.x != 0)
        {
            go.transform.localPosition = new Vector3((dir.x > 0 ? p_gestionnaire.XMax + 1 : p_gestionnaire.XMin - 1) * dimension, 0.1f, centre.z);
            go.transform.localScale = new Vector3(dimension, longueurGrille, 1f);
        }
        else
        {
            go.transform.localPosition = new Vector3(centre.x, 0.1f, (dir.y > 0 ? p_gestionnaire.ZMax + 1 : p_gestionnaire.ZMin - 1) * dimension);
            go.transform.localScale = new Vector3(largeurGrille, dimension, 1f);
        }
    }
    #endregion
}