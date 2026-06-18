using UnityEngine;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class Door : MonoBehaviour
{
    bool trig, open;
    public float smooth = 2.0f;
    public float DoorOpenAngle = 90.0f;
    private Vector3 defaulRot;
    private Vector3 openRot;
    public Text txt;

    void Start()
    {
        defaulRot = transform.eulerAngles;
        openRot = new Vector3(defaulRot.x, defaulRot.y + DoorOpenAngle, defaulRot.z);
    }

    void Update()
    {
        if (open)
        {
            transform.eulerAngles = Vector3.Slerp(transform.eulerAngles, openRot, Time.deltaTime * smooth);
        }
        else
        {
            transform.eulerAngles = Vector3.Slerp(transform.eulerAngles, defaulRot, Time.deltaTime * smooth);
        }

        if (IsInteractPressedThisFrame() && trig)
        {
            open = !open;
        }

        if (trig && txt != null)
        {
            txt.text = open ? "Close E" : "Open E";
        }
    }

    private void OnTriggerEnter(Collider coll)
    {
        if (!coll.CompareTag("Player"))
        {
            return;
        }

        if (txt != null)
        {
            txt.text = open ? "Close E" : "Open E";
        }

        trig = true;
    }

    private void OnTriggerExit(Collider coll)
    {
        if (!coll.CompareTag("Player"))
        {
            return;
        }

        if (txt != null)
        {
            txt.text = " ";
        }

        trig = false;
    }

#if ENABLE_INPUT_SYSTEM
    static bool IsInteractPressedThisFrame() => Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame;
#else
    static bool IsInteractPressedThisFrame() => Input.GetKeyDown(KeyCode.E);
#endif
}
