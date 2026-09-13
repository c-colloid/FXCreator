using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace colloid.FXCreator
{
	
public class FxcAnimationContainer : EditorWindow
{
    [SerializeField]
    private VisualTreeAsset m_VisualTreeAsset = default;

	
	[MenuItem("Tools/FXCreator/Debug/FXC Animation Container", priority = 1921)]
    public static void ShowExample()
    {
        FxcAnimationContainer wnd = GetWindow<FxcAnimationContainer>();
        wnd.titleContent = new GUIContent("FXC Animation Container");
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