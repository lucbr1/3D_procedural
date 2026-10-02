using UnityEngine;

public class SpectatorCamera : MonoBehaviour
{
    [Header("Mouvement")]
    public float movementSpeed = 5f;
    public float fastSpeed = 30f;
    public float freeLookSensitivity = 3f;

    [Header("Touches")]
    public KeyCode boostKey = KeyCode.LeftShift;
    public KeyCode upKey = KeyCode.E;
    public KeyCode downKey = KeyCode.Q;

    [SerializeField]
    private GestionnaireChunks p_gestionnaire;

    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        // Verrouille le curseur de la souris au centre de l'�cran
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;


        // Initialise la rotation actuelle bas�e sur la position de d�part de la cam�ra
        Vector3 rotation = transform.localRotation.eulerAngles;
        rotationX = rotation.y;
        rotationY = -rotation.x;
    }

    void Update()
    {
        if (p_gestionnaire.affichageInfos) return;
        
        // --- ROTATION (SOURIS) ---
        rotationX += Input.GetAxis("Mouse X") * freeLookSensitivity;
        rotationY += Input.GetAxis("Mouse Y") * freeLookSensitivity;
        rotationY = Mathf.Clamp(rotationY, -90f, 90f); // Emp�che de faire des loopings verticaux

        transform.localRotation = Quaternion.AngleAxis(rotationX, Vector3.up) * Quaternion.AngleAxis(rotationY, Vector3.left);

        // --- D�PLACEMENT (CLAVIER) ---
        // Choix de la vitesse si Shift gauche est enfonc�
        float currentSpeed = Input.GetKey(boostKey) ? fastSpeed : movementSpeed;

        // Inputs horizontaux (Q/D ou A/D selon clavier) et verticaux (Z/S ou W/S)
        float moveForward = Input.GetAxis("Vertical");
        float moveSide = Input.GetAxis("Horizontal");

        Vector3 direction = new Vector3(moveSide, 0, moveForward);

        // Gestion de la mont�e / descente
        if (Input.GetKey(upKey)) direction.y += 1f;
        if (Input.GetKey(downKey)) direction.y -= 1f;

        // D�placement par rapport � l'orientation de la cam�ra
        transform.Translate(direction * currentSpeed * Time.deltaTime);
    }
}