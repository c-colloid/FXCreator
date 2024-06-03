using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.Experimental.GraphView;

public class AnimatorCreaterNode : Node
{
	private float GridSize = 20;
    private Vector2 MinimumSize = new Vector2(10,20);

    public AnimatorCreaterNode()
    {
        capabilities |= Capabilities.Resizable;

	    title = "Sample";

        var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Port));
	    inputPort.portName = "In";
	    inputPort.portColor = Color.blue;
        inputContainer.Add(inputPort);

        var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Port));
	    outputPort.portName = "Out";
	    outputPort.portColor = Color.red;
	    outputContainer.Add(outputPort);
	    outputPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(Port));
	    outputPort.portName = "Out";
	    outputPort.portColor = Color.red;
	    outputContainer.Add(outputPort);
        

        var textField = new TextField();
        textField.style.flexGrow = 1;
        textField.multiline = true;
        textField.RegisterCallback<FocusInEvent>(evt => { Input.imeCompositionMode = IMECompositionMode.On; });
        textField.RegisterCallback<FocusOutEvent>(evt => { Input.imeCompositionMode = IMECompositionMode.Auto; });
        this.mainContainer.Add(textField);

        RegisterCallback<GeometryChangedEvent>(evt =>{
            var rect = evt.newRect;
            SetPosition(
                new Rect(
                    Mathf.Floor(rect.x/ GridSize) * GridSize,
                    Mathf.Floor(rect.y/ GridSize) * GridSize,
                    Mathf.Floor(rect.width/ GridSize) * GridSize,
                    Mathf.Floor(rect.height/ GridSize) * GridSize
                )
            );

            style.width = Mathf.Max(
                Mathf.Floor(rect.width / GridSize) * GridSize,
                MinimumSize.x
            );
            style.height = Mathf.Max(
                Mathf.Floor(rect.height/ GridSize) * GridSize,
                MinimumSize.y
            );
        });
    }
}
