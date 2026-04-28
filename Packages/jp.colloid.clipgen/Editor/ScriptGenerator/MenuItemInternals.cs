#if UNITY_2022_3_OR_NEWER
using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEditor;

static class MenuItemInternals
{
	public struct MenuItemInfo
	{
		public string Path { get; set; }
		public bool IsSeparator { get; set; }
		public int Priority { get; set; }
	}
	public static IEnumerable<MenuItemInfo> GetMenuItems(string menuPath, bool includeSeparators, bool localized)
	{
		if (s_GetMenuItems?.Invoke(null, new object[] { menuPath, includeSeparators, localized })
			is not Array result)
		{
			Debug.LogError("(Editor) It could not get menu items. Please check the Unity version!");
			return Enumerable.Empty<MenuItemInfo>();
		}

		return result.Cast<object>().Select(e =>
		{
			return new MenuItemInfo
			{
				Path = s_ScriptingMenuItem_path?.GetValue(e) as string,
				IsSeparator = s_ScriptingMenuItem_isSeparator?.GetValue(e) as bool? ?? false,
				Priority = s_ScriptingMenuItem_priority?.GetValue(e) as int? ?? 0
			};
		});
	}

	private readonly static MethodInfo s_GetMenuItems;
	private readonly static PropertyInfo s_ScriptingMenuItem_path;
	private readonly static PropertyInfo s_ScriptingMenuItem_priority;
	private readonly static PropertyInfo s_ScriptingMenuItem_isSeparator;

	static MenuItemInternals()
	{
		var eapm = Array.Empty<ParameterModifier>();
		var eat = Array.Empty<Type>();
		s_GetMenuItems = typeof(Menu).GetMethod("GetMenuItems", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(bool), typeof(bool) }, eapm);
		if (s_GetMenuItems == null) Debug.LogError("(Editor) s_GetMenuItems == null");

		var scriptingMenuItemType = Type.GetType("UnityEditor.ScriptingMenuItem, UnityEditor.CoreModule");
		if (scriptingMenuItemType == null) Debug.LogError("(Editor) scriptingMenuItemType == null");

		s_ScriptingMenuItem_path = scriptingMenuItemType?.GetProperty("path", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance, null, typeof(string), eat, eapm);
		if (s_ScriptingMenuItem_path == null) Debug.LogError("(Editor) s_ScriptingMenuItem_path == null");

		s_ScriptingMenuItem_isSeparator = scriptingMenuItemType?.GetProperty("isSeparator", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance, null, typeof(bool), eat, eapm);
		if (s_ScriptingMenuItem_isSeparator == null) Debug.LogError("(Editor) s_ScriptingMenuItem_isSeparator == null");

		s_ScriptingMenuItem_priority = scriptingMenuItemType?.GetProperty("priority", BindingFlags.GetProperty | BindingFlags.Public | BindingFlags.Instance, null, typeof(int), eat, eapm);
		if (s_ScriptingMenuItem_priority == null) Debug.LogError("(Editor) s_ScriptingMenuItem_priority == null");
	}
}
#endif