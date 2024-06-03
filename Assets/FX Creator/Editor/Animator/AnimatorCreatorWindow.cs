using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

public class AnimatorCreatorWindow : EditorWindow
{
    [SerializeField]
	VisualTreeAsset m_VisualTreeAsset = default;
	[SerializeField]
	StyleSheet m_StyleSheet = default;
	AnimatorCreatorRootManipulator m_rootManipulator = default;

	[MenuItem("Window/UI Toolkit/AnimatorCreatorWindow")]
	[MenuItem("Tools/FXCreator/AnimatorCreater",priority = 1001)]
	public static void ShowWindow()
    {
        AnimatorCreatorWindow wnd = GetWindow<AnimatorCreatorWindow>();
	    wnd.titleContent = new GUIContent(nameof(AnimatorCreatorWindow));
	    wnd.minSize = new Vector2(700,430);
    }

    public void CreateGUI()
    {
	    m_rootManipulator = new AnimatorCreatorRootManipulator(GetWindow<AnimatorCreatorWindow>());
        // Each editor window contains a root VisualElement object
	    VisualElement root = rootVisualElement;
        
        // VisualElements objects can contain other VisualElement following a tree hierarchy.
		

        // Instantiate UXML
	    VisualElement UXML = m_VisualTreeAsset.Instantiate();
	    UXML.StretchToParentSize();
	    root.Add(UXML);
	    
	    TwoPaneSplitView twopanel = root.Q<TwoPaneSplitView>();
	    SetIcon();
	    SetToolbarButton();
	    root.Q<VisualElement>("Background").AddManipulator(m_rootManipulator);
	    root.Q<Shadow>().Add(new AnimatorCreatorGraph(){ style = {flexGrow = 1} });
	    SetRootStyle(root);
    }
    
	void SetIcon()
	{
		rootVisualElement.Q<ToolbarButton>("DisableTwoPanel").style.backgroundImage =
			(Texture2D)EditorGUIUtility.Load(EditorGUIUtility.isProSkin ?
			"d_animationvisibilitytoggleon" :
			"animationvisibilitytoggleon");
		rootVisualElement.Q<ToolbarButton>("EnableTwoPanel").style.backgroundImage =
			(Texture2D)EditorGUIUtility.Load(EditorGUIUtility.isProSkin ?
			"d_animationvisibilitytoggleoff" :	
			"animationvisibilitytoggleoff");
	}
	
	void SetToolbarButton()
	{
		var TwoPanel = rootVisualElement.Q<TwoPaneSplitView>();
		var Dragline = rootVisualElement.Q<VisualElement>("unity-dragline-anchor");
		var DisableTwoPanel = rootVisualElement.Q<ToolbarButton>("DisableTwoPanel");
		var EnableTwoPanel = rootVisualElement.Q<ToolbarButton>("EnableTwoPanel");
		DisableTwoPanel.clicked += () => {
			TwoPanel.fixedPane.style.display = Dragline.style.display = DisplayStyle.None;
			EnableTwoPanel.style.display = DisplayStyle.Flex;
		};
		EnableTwoPanel.clicked += () => {
			TwoPanel.fixedPane.style.display = Dragline.style.display = DisplayStyle.Flex;
			EnableTwoPanel.style.display = DisplayStyle.None;
		};
	}
	
	void SetRootStyle(VisualElement root)
	{
		root.styleSheets.Clear();
		root.styleSheets.Add(EditorGUIUtility.isProSkin ?
			GetDefaultUSS.DefaultCommonDarkStyleSheet :
			GetDefaultUSS.DefaultCommonLightStyleSheet);
	}
}
