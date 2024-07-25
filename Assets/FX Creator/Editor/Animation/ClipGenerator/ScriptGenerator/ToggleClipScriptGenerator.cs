#if UNITY_2022_3_OR_NEWER
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using System.Linq;
using System.IO;

namespace colloid.FXCreator.Animation.Generator
{
	public class ToggleClipScriptGenerator : EditorWindow
	{
		private static string m_component = "Component";
		private static string m_type = "Component";
		
		private static string CODE = 
$@"using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{{
	public class ToggleClip_#m_component# : ToggleClip
	{{
		const string m_componentMenuItem = m_componentMenuItemPath + nameof(#m_type#);
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		static bool VaridationGenerate#m_component#Clip()
		{{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<#m_type#>(out var result));
		}}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		static void Generate#m_component#Clip()
		{{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<#m_type#>()));
		}}
	}}
}}";
	
		private static string METHOD =
$@"const string m_componentMenuItem = m_componentMenuItemPath + nameof(#m_type#);

[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
static bool VaridationGenerate#m_component#Clip()
{{
	return Selection.gameObjects.Any()
		&& Selection.gameObjects.All(o => o.TryGetComponent<#m_type#>(out var result));
}}

[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
static void Generate#m_component#Clip()
{{
	GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<#m_type#>()));
}}";
	
		[MenuItem("Tools/ClipGen/Settings/GenerateScript")]
		static void GenerateScript()
		{
			var window = EditorWindow.CreateInstance<ToggleClipScriptGenerator>();
			window.title = "GenerateScript";
			window.ShowAuxWindow();
		}
		
		void CreateGUI()
		{
			var root = rootVisualElement;
			var dropdown = new DropdownField(){value = "select Component",style = {flexGrow = 1}};
			dropdown.choices = MenuItemInternals.GetMenuItems(nameof(Component),false,false).Select(o => o.Path).ToList();
			dropdown.RegisterValueChangedCallback(evt =>{
				var path = evt.newValue;
				m_component = path.Split("/").Last().Replace(" ","");
				m_type = m_component;
			});
			
			root.Add(dropdown);
			
			root.Add(new VisualElement{style = {flexGrow = 2}});
			
			var buttonsBox = new VisualElement(){style = {flexDirection = FlexDirection.RowReverse, flexGrow = 1}};
			
			root.Add(buttonsBox);
			
			var ok = new Button(){text = "OK"};
			ok.clicked += () => {
				if (dropdown.index < 0) return;
				var code = CODE.Replace("#m_component#",m_component).Replace("#m_type#",m_type);
				
				// 作成するアセットのパス
				var filePath = $"Assets/FX Creator/Editor/Animation/ClipGenerator/Gen/ToggleClip_{m_component}.cs";

				// もし名前(パス)が重複していた場合に、自動で語尾に「Sample1.cs」みたく数字をつけてくれる
				var assetPath = AssetDatabase.GenerateUniqueAssetPath(filePath);

				// アセット(.cs)を作成する
				File.WriteAllText(filePath, code);
        
				// 変更があったアセットをインポートする(UnityEditorの更新)
				AssetDatabase.Refresh();
			};
			
			buttonsBox.Add(ok);
			
			var copy = new Button(){text = "Copy"};
			copy.clicked += () => {
				var method = METHOD.Replace("#m_component#",m_component).Replace("#m_type#",m_type);
				
				EditorGUIUtility.systemCopyBuffer = method;
			};
			
			buttonsBox.Add(copy);
			
			var cansel = new Button(){text = "Cancel"};
			cansel.clicked += () => this.Close();
			
			buttonsBox.Add(cansel);
		}
	}
}
#endif