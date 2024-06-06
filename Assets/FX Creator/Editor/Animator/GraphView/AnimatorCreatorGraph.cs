using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;
using UnityEditor;

public class AnimatorCreatorGraph : GraphView
{
	[SerializeField]
	StyleSheet m_styleSheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath("f76461d4486bab64a84805c6c291e65b"));
	public AnimatorCreatorGraph() : base()
	{
		SetupZoom(ContentZoomer.DefaultMinScale,ContentZoomer.DefaultMaxScale * 1.5f);
		this.AddManipulator(new SelectionDragger());
		this.AddManipulator(new ContentDragger());
		this.AddManipulator(new RectangleSelector());
		var gridbackground = new GridBackground();
		// this.styleSheets.Clear();
		if (m_styleSheet != null)
			this.styleSheets.Add(m_styleSheet);
		if (GetDefaultUSS.DefaultCommonDarkStyleSheet != null)
			this.styleSheets.Add(GetDefaultUSS.DefaultCommonDarkStyleSheet);
		gridbackground.RegisterCallback<CustomStyleResolvedEvent>(evt => 
			gridbackground
				.GetType()
				.GetField("m_ThickLines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
				?.SetValue(gridbackground, 10)
		);
		Insert(0,gridbackground);
		
		var blackbord = new Blackboard(this);
		Add(blackbord);

		var mousePos = Vector2.zero;
		this.RegisterCallback<MouseDownEvent>(evt => {
			mousePos = this.ChangeCoordinatesTo(contentViewContainer,evt.localMousePosition);
		});

		nodeCreationRequest += Context =>
		{
			AddElement(new AnimatorCreaterNode(){
				style = {left = mousePos.x, top = mousePos.y, width = 200, height = 300}
			});
		};
	}

    public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
    {
		var compatiblePorts = new List<Port>();
		foreach (var port in ports.ToList())
		{
			if (startPort.node == port.node) continue;
			if (startPort.direction == port.direction) continue;
			if (startPort.portType != port.portType) continue;

			compatiblePorts.Add(port);
		}
	    return compatiblePorts;
    }
}
