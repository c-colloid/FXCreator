using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace colloid.FXCreator.Legacy
{

public class AnimatorCreatorRootManipulator : Manipulator
{
	AnimatorCreatorWindow m_window;
	VisualElement m_targetVE;
	VisualElement m_targetBG;
	
	Vector2 m_offset = Vector2.zero;
	float m_Zoom = 1;
	
	public AnimatorCreatorRootManipulator()
	{
		
	}
	
	public AnimatorCreatorRootManipulator(AnimatorCreatorWindow window)
	{
		m_window = window;
	}
	
	protected override void RegisterCallbacksOnTarget()
	{
		Debug.Log("RegisterManipulator");
		target.RegisterCallback<WheelEvent>(OnWheelEvent);
		target.RegisterCallback<MouseDownEvent>(OnMouseDown);
		target.RegisterCallback<MouseUpEvent>(OnMouseUp);
		target.RegisterCallback<MouseMoveEvent>(OnMouseMove);
		
		m_targetVE = target?.Q<VisualElement>("StateMachineContainer");
		m_targetBG = target?.Q<VisualElement>("Background");
	}
	
	protected override void UnregisterCallbacksFromTarget()
	{
		Debug.Log("UnregisterManipulator");
		target.UnregisterCallback<WheelEvent>(OnWheelEvent);
		target.UnregisterCallback<MouseDownEvent>(OnMouseDown);
		target.UnregisterCallback<MouseUpEvent>(OnMouseUp);
		target.UnregisterCallback<MouseMoveEvent>(OnMouseMove);
	}
	
	void OnWheelEvent(WheelEvent evt)
	{
		var wheeldelta = evt.delta;
		var targetVE = target?.Q<VisualElement>("StateMachineContainer");
		var targetBG = target?.Q<VisualElement>("Background");
		if (targetVE == null || targetBG == null) return;
		m_targetVE = targetVE;
		m_targetBG = targetBG;
		var scale = m_targetVE.transform.scale;
		var newOrigin = m_targetVE.ChangeCoordinatesTo(m_targetVE, evt.localMousePosition);
		
		var oldBound = m_targetVE.worldBound;
		//m_targetVE.style.transformOrigin = new TransformOrigin(newOrigin.x,newOrigin.y);
		var deltaPosition = oldBound.position - m_targetVE.worldBound.position;
		m_targetVE.transform.position += (Vector3)deltaPosition;
		
		var newZoom = Mathf.Clamp(scale.y - scale.y * wheeldelta.y * .1f, .1f, 1f);
		m_targetVE.transform.scale = Vector3.one * newZoom;
		m_targetBG.style.backgroundSize = new BackgroundSize(Length.Percent(100 * newZoom),Length.Percent(100 * newZoom));
		
		m_Zoom = newZoom;
	}
	
	void OnMouseDown(MouseDownEvent evt)
	{
		var mouseButton = evt.button;
		if (mouseButton != 1) return;

		m_targetVE = target?.Q<VisualElement>("StateMachineContainer");
		m_targetBG = target?.Q<VisualElement>("Background");
		if (m_targetVE == null || m_targetBG == null) return;
		if (evt.localMousePosition.x > m_targetVE.layout.width) return;
		if (evt.localMousePosition.y > m_targetVE.layout.height) return;
		var mousePos = (Vector3)evt.localMousePosition;
		var menu = new GenericMenu();
		menu.AddItem(new GUIContent("Create new state"),false,() => {
			var box = new Box();
			box.style.width = 100;
			box.style.height = 100;
			box.style.position = Position.Absolute;
			box.transform.position = m_targetBG.ChangeCoordinatesTo(m_targetVE,(Vector3)mousePos);
			m_targetVE.Add(box);
		});
		menu.ShowAsContext();
	}
	
	void OnMouseUp(MouseUpEvent evt)
	{
		
	}
	
	void OnMouseMove(MouseMoveEvent evt)
	{
		m_targetVE = target?.Q<VisualElement>("StateMachineContainer");
		m_targetBG = target?.Q<VisualElement>("Background");
		if (m_targetVE == null || m_targetBG == null) return;
		if (evt.localMousePosition.x > m_targetVE.layout.width) return;
		if (evt.localMousePosition.y > m_targetVE.layout.height) return;
		var current = Event.current;
		var mouseDelta = evt.mouseDelta;
		if (current.button != 2) return;
		m_targetVE.transform.position += (Vector3)mouseDelta;
		m_offset += mouseDelta;
		m_targetBG.style.backgroundPositionX =
			new BackgroundPosition(BackgroundPositionKeyword.Top,
			new Length(m_targetBG.style.backgroundPositionX.value.offset.value + mouseDelta.x));
		m_targetBG.style.backgroundPositionY =
			new BackgroundPosition(BackgroundPositionKeyword.Top,
			new Length(m_targetBG.style.backgroundPositionY.value.offset.value + mouseDelta.y));
	}
}
}
