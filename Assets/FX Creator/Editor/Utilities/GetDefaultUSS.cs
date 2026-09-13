using System.Reflection;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEditor;

namespace colloid.FXCreator.Utility
{

public static class GetDefaultUSS
{
	private static StyleSheet defaultCommonDarkStyleSheet;
	private static StyleSheet defaultCommonLightStyleSheet;
	
	public static StyleSheet DefaultCommonDarkStyleSheet {get {
		if (defaultCommonDarkStyleSheet == null)
		{
			GetSkins();
			if (defaultCommonDarkStyleSheet == null)
			{
				defaultCommonDarkStyleSheet = EditorGUIUtility.Load("StyleSheets/Generated/DefaultCommonDark.uss.asset") as StyleSheet;
			}
		}
		return defaultCommonDarkStyleSheet;
	}}
	public static StyleSheet DefaultCommonLightStyleSheet {get {
		if (defaultCommonLightStyleSheet == null)
		{
			GetSkins();
			if (defaultCommonLightStyleSheet == null)
			{
				defaultCommonLightStyleSheet = EditorGUIUtility.Load("StyleSheets/Generated/DefaultCommonLight.uss.asset") as StyleSheet;
			}
		}
		return defaultCommonLightStyleSheet;
	}}

	private static void GetSkins()
	{
		//UnityEditor.UIElementsのアセンブリを取得
		var assembly = typeof(Toolbar).Assembly;

		//UIElementsEditorUtilityのTypeを取得
		var type = assembly.GetType("UnityEditor.UIElements.UIElementsEditorUtility");
		if (type == null) return;

		//StyleSheetを格納するフィールドを取得
		var darkField = type.GetField("s_DefaultCommonDarkStyleSheet", BindingFlags.Static | BindingFlags.NonPublic);
		var lightField = type.GetField("s_DefaultCommonLightStyleSheet", BindingFlags.Static | BindingFlags.NonPublic);

		if (darkField == null || lightField == null) return;

		//staticなフィールドとして値を取得
		defaultCommonDarkStyleSheet = (StyleSheet) darkField.GetValue(null);
		defaultCommonLightStyleSheet = (StyleSheet) lightField.GetValue(null);
	}
}
}
