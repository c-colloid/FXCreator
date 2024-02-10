using UnityEngine;
using UnityEditor;

public class ClipPropertyName : EditorWindow {
	[MenuItem("Window/Disp curve.propertyName")]
	static void ShowWindow() {
		EditorWindow.GetWindow<ClipPropertyName>();
	}

	void OnSelectionChange() {
		if (Selection.objects.Length > 1) {
			Debug.Log("Length: " + Selection.objects.Length);
		} else if (Selection.activeObject is AnimationClip) {
			foreach (var curve in AnimationUtility.GetCurveBindings((AnimationClip)Selection.activeObject)) {
				Debug.Log(curve.propertyName);
			}
		}
	}

	void OnGUI() {
		GUILayout.Label("Please select an Animation Clip");
	}
}