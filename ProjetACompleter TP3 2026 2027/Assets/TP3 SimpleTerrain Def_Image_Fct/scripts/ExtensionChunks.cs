using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(GestionnaireChunks))]
public class ExtensionChunks : MonoBehaviour
{
    #region Variables
    [Tooltip("Distance au bord à partir de laquelle le plan fantôme apparait")]
    public float distanceFantome = 30f;
    [Tooltip("Distance au bord à partir de laquelle le chunk est créé")]
    public float distanceCreation = 10f;

    private GestionnaireChunks p_gestionnaire;

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
    //distance de la caméra au bord du chunk dans la direction donnée
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

    private void GererExtensionAutomatique()
    {
        for (int d = 0; d < 4; d++)
        {
            Vector2Int dir = p_gestionnaire.directions[d];
            float distance = DistanceAuBord(dir);

            if (distance > distanceFantome)
                p_annulee[d] = false;

            bool proche = distance < distanceFantome && !p_annulee[d];

            if (proche && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                p_annulee[d] = true;
                proche = false;
            }

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
            Destroy(go.GetComponent<Collider>());   
            go.transform.SetParent(p_gestionnaire.transform, false);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);   
            go.GetComponent<MeshRenderer>().sharedMaterial = materielFantome;

            go.SetActive(false);
            p_fantomes[d] = go;
        }
    }

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