using UnityEngine;
using TMPro;

public class DispPanel : MonoBehaviour
{
    [SerializeField]
    private GridMesh _gridMesh;

    [SerializeField]
    private GameObject _showValue;

    private TextMeshProUGUI _valueText = null;

    private string _content;
    private int _showPanel = 1;
    private Vector3 _initialPosition = Vector3.zero;

    private void Awake()
    {
        _valueText = _showValue.GetComponent<TextMeshProUGUI>();
        _initialPosition = transform.localPosition;
    }

    private void Update()
    {
        _content = "== QuestLinkRGBDEViewer ==\n\n";
        _content += "Linearity: " + _gridMesh.Linearity + "\n\n";
        _content += "Depth Magnified: " + _gridMesh.MagnificationZ + "\n";
        _content += "Depth PowerNum in Log: " + _gridMesh.PowerFactor + "\n\n";
        _content += "== Usage ==\n";
        _content += "Controller L:\n";
        _content += " - Start: Open File Browser\n";
        _content += " - Trigger + Move / Move Stick: Move Object\n";
        _content += " - Y / Z: Expand / Compress of Z\n";
        _content += " - Hand Trigger: Show / Hide this panel\n\n";
        _content += "Controller R:\n";
        _content += " - A / B: Set Z to be Linear / Log\n";
        _content += " - Trigger + Move: Change Depth Magnification\n";
        _content += " - Stick L / R: Change Scale\n";
        _content += " - Hand Trigger + Move L / R, Angle Narrower, Wider";

        _valueText.text = _content;

        bool isLeftHandTriggerPressed = OVRInput.GetDown(OVRInput.RawButton.LHandTrigger);
        if (isLeftHandTriggerPressed)
        {
            _showPanel *= -1;
        }

        if (_showPanel == 1)
        {
            transform.localPosition = _initialPosition;
        }
        else
        {
            Vector3 hiddenPosition = _initialPosition;
            hiddenPosition.x = -10000f;
            transform.localPosition = hiddenPosition;
        }
    }
}
