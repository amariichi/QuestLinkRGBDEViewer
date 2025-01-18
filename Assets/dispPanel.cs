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

    void Awake()
    {
      valueText  = showValue.GetComponent<TextMeshProUGUI>();
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
        content += " - Y / Z: Expand / Compress of Z\n\n";
        content += "Controller R:\n";
        content += " - A / B: Set Z to be Linear / Log\n";
        content += " - Trigger + Move: Change Depth Magnification\n";
        content += " - Stick L / R: Change Scale";

        valueText.text = content;
    }
}
