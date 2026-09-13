using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using UnityEditor;
using System.Reflection;

namespace colloid.FXCreator.UI
{
internal class ReorderableListView : ListView
{
	public const string UssClassName = "reorderable-list-view";
	
	public List<string> veList = new List<string>();
	
	public Action<VisualElement> OnValueChangedHandler;
	
	public ReorderableListView() : base()
	{
		AddToClassList(UssClassName);
		
		RegisterCallback<AttachToPanelEvent>(evt =>{
			var target = (ReorderableListView)evt.target;
			Debug.Log(nameof(AttachToPanelEvent));
			var json = EditorUserSettings.GetConfigValue(nameof(veList));
			veList = JsonUtility.FromJson<ReorderableListView>(json).veList;
			
			var viewDataKey = 0;
			foreach (var item in this.Children().ToList())
			{
				item.AddToClassList("reorderable-list-view__contents");
				item.style.borderBottomColor =
					item.style.borderLeftColor =
					item.style.borderRightColor =
					item.style.borderTopColor = BorderColor;
				// item.style.borderColor = BorderColor;
				item.style.borderBottomWidth = 
					item.style.borderLeftWidth = 
					item.style.borderRightWidth = 
					item.style.borderTopWidth = BorderWidth;
				item.AddManipulator(new ReorderList(this));
				item.viewDataKey = viewDataKey.ToString();
				viewDataKey++;
				if (veList.IndexOf(item.viewDataKey) >= viewDataKey -1 || veList.IndexOf(item.viewDataKey) == -1) continue;
				this.Insert(veList.IndexOf(item.viewDataKey),item);
			}
		});
		
		RegisterCallback<DetachFromPanelEvent>(evt =>{
			Debug.Log(nameof(DetachFromPanelEvent));
			veList = this.Children().Select(o=> o.viewDataKey).ToList();
			EditorUserSettings.SetConfigValue(nameof(veList),JsonUtility.ToJson(this, true));
		});
	}
	
	public new class UxmlFactory : UxmlFactory<ReorderableListView, UxmlTraits> { }
	
	public new class UxmlTraits : VisualElement.UxmlTraits
	{
		readonly UxmlColorAttributeDescription m_BorderColor = new UxmlColorAttributeDescription { name = "BorderColor" };
		readonly UxmlFloatAttributeDescription m_BorderWidth = new UxmlFloatAttributeDescription { name = "BorderWidth"};
		
		public override void Init(VisualElement ve, IUxmlAttributes bag, CreationContext cc)
		{
			base.Init(ve, bag, cc);
			var RLV = ve as ReorderableListView;
			RLV.BorderColor = m_BorderColor.GetValueFromBag(bag, cc);
			RLV.BorderWidth = m_BorderWidth.GetValueFromBag(bag, cc);
			foreach (var item in RLV.Children())
			{
				var vestyle = item.style;
				vestyle.borderBottomColor =
					vestyle.borderLeftColor =
					vestyle.borderRightColor =
					vestyle.borderTopColor = RLV.BorderColor;
				// vestyle.borderColor = RLV.BorderColor;
				vestyle.borderBottomWidth =
					vestyle.borderLeftWidth =
					vestyle.borderRightWidth =
					vestyle.borderTopWidth = RLV.BorderWidth;
			}
		}
	}
	
	class ReorderList : Manipulator
	{
		public ReorderList(ReorderableListView target)
		{
			root = target;
			defaltBorderColor = root.BorderColor;
		}
		
		protected override void RegisterCallbacksOnTarget() {
			//throw new NotImplementedException("Register");
			target.RegisterCallback<MouseDownEvent>(OnDragStartEvent);
			target.RegisterCallback<MouseMoveEvent>(OnDragEvent);
			target.RegisterCallback<MouseUpEvent>(OnDropEvent);
		}
		
		protected override void UnregisterCallbacksFromTarget() {
			//throw new NotImplementedException("Unregister");
			target.UnregisterCallback<MouseDownEvent>(OnDragStartEvent);
			target.UnregisterCallback<MouseMoveEvent>(OnDragEvent);
			target.UnregisterCallback<MouseUpEvent>(OnDropEvent);
		}
		
		private Vector2 targetStartPosition { get; set; }

		private Vector2 pointerStartPosition { get; set; }

		private static bool enabled { get; set; }
		
		private Color defaltBorderColor { get; }
		
		private IStyle defaultStyle { get; set; } = new VisualElement().style;

		private ReorderableListView root { get; }
		
		void OnDragStartEvent(MouseDownEvent evt)
		{
			if (evt.button != 0) return;
			if (enabled)
			{
				evt.StopImmediatePropagation();
				return;
			}
			
			targetStartPosition = target.layout.position;
			//Debug.Log("DragStart:" +targetStartWorldPosition);
			pointerStartPosition = evt.localMousePosition;
			foreach (var item in defaultStyle.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
			{
				var prop = item.GetValue(target.style);
				if (prop.ToString() != "Null")
					//Debug.Log(item.Name + " : " + prop.ToString());
				if (item.Name == "ve") continue;
				item.SetValue(defaultStyle, item.GetValue(target.style));
			}
			
			root.SetSelection(root.IndexOf(target));
			target.CaptureMouse();
			OnDragElement(evt.mousePosition);
			target.style.opacity = 0.6f;
			defaultStyle.opacity = 1.0f;
			enabled = true;
			evt.StopPropagation();
		}
		
		void OnDragEvent(MouseMoveEvent evt)
		{
			if (!enabled || !target.HasMouseCapture()) return;
			
			//Debug.Log("Drag");
			OnDragElement(evt.mousePosition);
			foreach (var item in root.Children())
			{
				item.style.borderBottomColor =
					item.style.borderLeftColor =
					item.style.borderRightColor =
					item.style.borderTopColor = item == OnNerlestElemtent(target.transform.position) && item != target ? Color.red : defaltBorderColor;
				// item.style.borderColor = item == OnNerlestElemtent(target.transform.position) && item != target ? Color.red : defaltBorderColor;
			}
			evt.StopPropagation();
		}
		
		void OnDropEvent(MouseUpEvent evt)
		{
			if (!enabled || !target.HasMouseCapture() || evt.button != 0) return;
			
			target.ReleaseMouse();
			
			//Debug.Log("Drop");
			var result = OnNerlestElemtent(target.transform.position);
			target.transform.position = Vector2.zero;
			root.Insert(root.IndexOf(result), target);
			result.style.borderBottomColor =
				result.style.borderLeftColor =
				result.style.borderRightColor =
				result.style.borderTopColor = defaltBorderColor;
			// result.style.borderColor = defaltBorderColor;
			ResetStyle();
			enabled = false;
			evt.StopPropagation();
		}
		
		void OnDragElement(Vector2 mouseP)
		{
			Vector2 pointerDelta = root.Q<VisualElement>("unity-content-container").WorldToLocal(mouseP) - pointerStartPosition;
			target.transform.position = new Vector2(
				Mathf.Clamp
				(
				-targetStartPosition.x + pointerDelta.x +1,
				-targetStartPosition.x -1,
				root.layout.width - 10
				),
				Mathf.Clamp
				(
				-targetStartPosition.y + pointerDelta.y -1,
				-targetStartPosition.y -1,
				root.Children().Last().layout.y - targetStartPosition.y +1
				)
			);
		}
		
		VisualElement OnNerlestElemtent(Vector2 targetP)
		{
			var result = root.Children().Aggregate((x, y) => 
			(Vector2.Distance(x.layout.position, targetP + targetStartPosition) < 
				Vector2.Distance(y.layout.position, targetP + targetStartPosition)) ?
				x : y );
			return result;
		}
		
		public void ResetStyle()
		{
			foreach (var item in target.style.GetType().GetProperties(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance))
			{
				if (item.Name == "ve" || item.GetValue(defaultStyle).ToString() == "Null") continue;
				item.SetValue(target.style, item.GetValue(defaultStyle));
			}
		}
	}
	
	public Color BorderColor { get;set; }
	public float BorderWidth { get;set; }
}
}
