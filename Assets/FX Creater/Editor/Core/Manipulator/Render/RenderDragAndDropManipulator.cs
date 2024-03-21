using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using System;
using CustomUI;

namespace colloid.FXCreater.Utility
{
	public class RenderDragAndDropManipulator : DragAndDropManipulator
	{
		private DropDownField m_ve;
		private FXCreater m_instance;
		
		public RenderDragAndDropManipulator(FXCreater instance, DropDownField ve, Type visual){
			m_type = visual;
			m_ve = ve;
			m_instance = instance;
		}
		
		protected override void RegisterCallbacksOnTarget() {
			base.RegisterCallbacksOnTarget();
			target.RegisterCallback<PointerDownEvent>(OnPointerDown);
			target.RegisterCallback<PointerMoveEvent>(OnPointerMove);
			target.RegisterCallback<PointerUpEvent>(OnPointerUp);
		}
		protected override void UnregisterCallbacksFromTarget() {
			base.UnregisterCallbacksFromTarget();
			target.UnregisterCallback<PointerDownEvent>(OnPointerDown);
			target.UnregisterCallback<PointerMoveEvent>(OnPointerMove);
			target.UnregisterCallback<PointerUpEvent>(OnPointerUp);
		}
		
		void OnPointerDown(PointerDownEvent evt)
		{
			evt.target.CapturePointer(evt.pointerId);
		}
		
		void OnPointerMove(PointerMoveEvent evt)
		{
			if (evt.target.HasPointerCapture(evt.pointerId))
			{
				
			}
		}
		
		void OnPointerUp(PointerUpEvent evt)
		{
			if (evt.target.HasPointerCapture(evt.pointerId)) evt.target.ReleasePointer(evt.pointerId);
		}
		
		public override void OnDragAndDropEvent(DragAndDropEventBase<DragPerformEvent> evt)
		{
			foreach (var item in DragAndDrop.objectReferences)
			{
				if (m_instance.clips.Contains(AnimationClipsUtility.GetClip(AssetDatabase.GetAssetPath(item)))) continue;
				m_ve.Popupvalues.Add($"Drag and Drop/{item.name}.anim");
				m_instance.clips.Add(AnimationClipsUtility.GetClip(AssetDatabase.GetAssetPath(item)));	
			}
		}
	}
}