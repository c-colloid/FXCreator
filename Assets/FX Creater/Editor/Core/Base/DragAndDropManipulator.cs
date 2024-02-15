using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;

namespace colloid.FXCreater.Utility
{
	public class DragAndDropManipulator : Manipulator
	{
		public Type m_type = typeof(DefaultAsset);
		public DragAndDropManipulator(){}
		public DragAndDropManipulator(Type visual){
			m_type = visual;
		}

		protected override void RegisterCallbacksOnTarget() {
			//throw new System.NotImplementedException();
			target.RegisterCallback<DragPerformEvent>(OnDragAndDropEvent);
			target.RegisterCallback<DragUpdatedEvent>(OnDragEnterEvent);
		}
	
		protected override void UnregisterCallbacksFromTarget() {
			//throw new System.NotImplementedException();
			target.UnregisterCallback<DragPerformEvent>(OnDragAndDropEvent);
			target.UnregisterCallback<DragUpdatedEvent>(OnDragEnterEvent);
		}
	
		public virtual void OnDragAndDropEvent(DragAndDropEventBase<DragPerformEvent> evt)
		{
			var pathField = (TextField)evt.currentTarget;
			pathField.value = FolderPathUtility.SetFolderPath(DragAndDrop.paths[0]);
		}
	
		void OnDragEnterEvent(DragUpdatedEvent evt)
		{
			DragAndDrop.visualMode = 
				DragAndDrop.objectReferences[0].GetType() == 
				m_type ? 
				DragAndDropVisualMode.Generic : 
				DragAndDropVisualMode.Rejected;
		}
	}
}