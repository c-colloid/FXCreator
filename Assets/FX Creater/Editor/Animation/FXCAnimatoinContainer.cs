using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreater
{
	
public class FXCAnimatoinContainer : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

	[MenuItem("Tools/FXCreater/Debug/", priority = 1021)]
	
	[MenuItem("Window/UI Toolkit/FXCAnimatoinContainer"), MenuItem("Tools/FXCreater/Debug/FXCAnimationContainer", priority = 1021)]
    public static void ShowExample()
    {
        FXCAnimatoinContainer wnd = GetWindow<FXCAnimatoinContainer>();
        wnd.titleContent = new GUIContent("FXCAnimatoinContainer");
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
        VisualElement root = rootVisualElement;

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
    }
}

}