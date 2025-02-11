using UnityEngine;
using TMPro;

public class dispPanel : MonoBehaviour
{
    [SerializeField]
    private GridMesh gridMesh;

    [SerializeField]
    public GameObject showValue;

    TextMeshProUGUI valueText = null;

    private string content;
    private int showPanel = 1;
    UnityEngine.Vector3 pos = Vector3.zero;

    void Awake()
    {
      valueText  = showValue.GetComponent<TextMeshProUGUI>();
      pos = transform.localPosition;
    }

    void Update()
    {
        content = "== QuestLinkRGBDEViewer ==\n\n";
        content += "Linearity: " + gridMesh.Linearity + "\n\n";
        content += "Depth Magnified: " + gridMesh.MagnificationZ + "\n";
        content += "Depth PowerNum in Log: " + gridMesh.PowerFig + "\n\n";
        content += "== Usage ==\n";
        content += "Controller L:\n";
        content += " - Start: Open File Browser\n";
        content += " - Trigger + Move / Move Stick: Move Object\n";
        content += " - Y / Z: Expand / Compress of Z\n";
        content += " - Hand Trigger: Show / Hide this panel\n\n";
        content += "Controller R:\n";
        content += " - A / B: Set Z to be Linear / Log\n";
        content += " - Trigger + Move: Change Depth Magnification\n";
        content += " - Stick L / R: Change Scale\n";
        content += " - Hand Trigger + Move L / R, Angle Narrower, Wider";

        valueText.text = content;

        bool handTriggerL = OVRInput.GetDown(OVRInput.RawButton.LHandTrigger);
        if (handTriggerL) { showPanel *= -1;}
        if (showPanel == 1)
        {
            transform.localPosition = pos;
        }
        else
        {
            UnityEngine.Vector3 _pos = pos;
            _pos.x = -10000f;
            transform.localPosition = _pos;
        }
    }
}
