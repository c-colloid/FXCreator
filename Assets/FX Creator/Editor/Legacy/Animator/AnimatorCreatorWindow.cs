using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Experimental.GraphView;
using colloid.FXCreator.Utility;
using colloid.FXCreator.UI;

namespace colloid.FXCreator.Legacy
{

public class AnimatorCreatorWindow : EditorWindow
{
    [SerializeField]
	VisualTreeAsset m_VisualTreeAsset = default;
	[SerializeField]
	StyleSheet m_StyleSheet = default;
	AnimatorCreatorRootManipulator m_rootManipulator = default;
	
	AnimatorCreatorGraph graphView;
	ObjectField m_objectField;
	public AnimatorCreatorData AnimatorCreatorData { get { return (AnimatorCreatorData)m_objectField.value; } }

	[MenuItem("Tools/FXCreator/Legacy/Animator Creator (Legacy)", priority = 1901)]
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
	
	// void LoadData()
	// {
	// 	if (AnimatorCreatorData == null) return;

	// 	graphView.DeleteAllElements();

	// 	foreach (var nodeData in AnimatorCreatorData.nodeData_list)
	// 	{
	// 		graphView.LoadNodeData(nodeData);
	// 	}
	// 	foreach (var edgeData in AnimatorCreatorData.edgeData_list)
	// 	{
	// 		graphView.LoadEdgeData(edgeData);
	// 	}

	// 	Debug.Log($"ロード完了");
	// }

	// void SaveData()
	// {
	// 	if (AnimatorCreatorData == null) return;

	// 	AnimatorCreatorData.nodeData_list.Clear();
	// 	AnimatorCreatorData.edgeData_list.Clear();

	// 	foreach (var graphElement in graphView.graphElements)
	// 	{
	// 		if (graphElement is Node) SaveData_Node(graphElement);
	// 		else if (graphElement is Edge) SaveData_Edge(graphElement);
	// 		else Debug.LogWarning($"Find a non-surported graphElement type: {graphElement.GetType()}");
	// 	}

	// 	EditorUtility.SetDirty(m_objectField.value);
	// 	AssetDatabase.SaveAssets();

	// 	Debug.Log($"保存完了");
	// }

	// void SaveData_Node(GraphElement _graphElement)
	// {
	// 	Node node = _graphElement as Node;
	// 	NodeData nodeData = new NodeData()
	// 	{
	// 		uid = node.uid,
	// 		nodeType_str = node.GetType().ToString(),
	// 		localBound = node.localBound
	// 	};
	// 	AnimatorCreatorData.nodeData_list.Add(nodeData);
	// }

	// void SaveData_Edge(GraphElement _graphElement)
	// {
	// 	Edge edge = _graphElement as Edge;

	// 	Port inputPort = edge.input;
	// 	Port outputPort = edge.output;
	// 	Node inputNode = edge.input.node as Node;
	// 	Node outputNode = edge.output.node as Node;
	// 	string uid_inputPort_target = inputNode.port_dict.FirstOrDefault(x => x.Value.Equals(inputPort)).Key;
	// 	string uid_outputPort_target = outputNode.port_dict.FirstOrDefault(x => x.Value.Equals(outputPort)).Key;

	// 	EdgeData edgeData = new EdgeData()
	// 	{
	// 		uid_outputNode = outputNode.uid,
	// 		uid_outputPort = uid_outputPort_target,
	// 		uid_inputNode = inputNode.uid,
	// 		uid_inputPort = uid_inputPort_target
	// 	};
	// 	AnimatorCreatorData.edgeData_list.Add(edgeData);
	// }
}
}
