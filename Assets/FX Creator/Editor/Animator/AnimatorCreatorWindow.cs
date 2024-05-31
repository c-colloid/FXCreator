using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public class AnimatorCreatorWindow : EditorWindow
{
    [SerializeField]
	private VisualTreeAsset m_VisualTreeAsset = default;
	[SerializeField]
	Manipulator m_rootManipulator = default;

	[MenuItem("Window/UI Toolkit/AnimatorCreatorWindow")]
	[MenuItem("Tools/FXCreator/AnimatorCreater")]
	public static void ShowWindow()
    {
        AnimatorCreatorWindow wnd = GetWindow<AnimatorCreatorWindow>();
	    wnd.titleContent = new GUIContent(nameof(AnimatorCreatorWindow));
    }

    public void CreateGUI()
    {
        // Each editor window contains a root VisualElement object
	    VisualElement root = rootVisualElement;
        
	    root.AddManipulator(m_rootManipulator);

        // VisualElements objects can contain other VisualElement following a tree hierarchy.
        VisualElement label = new Label("Hello World! From C#");
        root.Add(label);

        // Instantiate UXML
        VisualElement labelFromUXML = m_VisualTreeAsset.Instantiate();
        root.Add(labelFromUXML);
    }
}
