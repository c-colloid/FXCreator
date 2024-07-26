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
		private static string m_groupPath = "";
		
		private static string CODE = 
$@"using UnityEngine;
using UnityEditor;
using System.Linq;

namespace colloid.FXCreator.Animation
{{
	public class ToggleClip_#m_component# : ToggleClip
	{{
		const string m_componentMenuItem = m_componentMenuItemPath + ""#m_groupPath#"" + nameof(#m_type#);
		const string m_contextMenuItem = ""CONTEXT/"" + nameof(#m_type#) +""/""+ m_menuItem;
	
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_componentMenuItem)]
		[MenuItem(m_menuItem ,validate = true ,menuItem = m_contextMenuItem)]
		static bool VaridationGenerate#m_component#Clip()
		{{
			return Selection.gameObjects.Any()
				&& Selection.gameObjects.All(o => o.TryGetComponent<#m_type#>(out var result));
		}}
		
		[MenuItem(m_menuItem ,menuItem = m_componentMenuItem, priority = 1011)]
		[MenuItem(m_menuItem ,menuItem = m_contextMenuItem)]
		static void Generate#m_component#Clip()
		{{
			GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<#m_type#>()));
		}}
	}}
}}";
	
		private static string METHOD =
$@"const string m_#m_component#MenuItem = m_componentMenuItemPath + ""#m_groupPath#"" + nameof(#m_type#);
const string m_#m_component#ContextMenuItem = ""CONTEXT/"" + nameof(#m_type#) +""/""+ m_menuItem;

[MenuItem(m_menuItem ,validate = true ,menuItem = m_#m_component#MenuItem)]
[MenuItem(m_menuItem ,validate = true ,menuItem = m_#m_component#ContextMenuItem)]
static bool VaridationGenerate#m_component#Clip()
{{
	return Selection.gameObjects.Any()
		&& Selection.gameObjects.All(o => o.TryGetComponent<#m_type#>(out var result));
}}

[MenuItem(m_menuItem ,menuItem = m_#m_component#MenuItem, priority = 1011)]
[MenuItem(m_menuItem ,menuItem = m_#m_component#ContextMenuItem)]
static void Generate#m_component#Clip()
{{
	GenerateToggelActiveComponentsClip(Selection.gameObjects.Select(o => o.GetComponent<#m_type#>()));
}}";
	
		[MenuItem("Tools/ClipGen/Settings/GenerateScript")]
		static void GenerateScript()
		{
			var window = EditorWindow.CreateInstance<ToggleClipScriptGenerator>();
			window.title = "GenerateScript";
			window.ShowUtility();
		}
		
		void CreateGUI()
		{
			var root = rootVisualElement;
			root.style.marginBottom = root.style.marginLeft = root.style.marginRight = root.style.marginTop = 5;
			var dropdown = new DropdownField(){value = "select Component",style = {flexGrow = 0}};
			dropdown.choices = MenuItemInternals.GetMenuItems(nameof(Component),false,false).Select(o => o.Path).ToList();
			
			root.Add(dropdown);
			
			var togglesBox = new VisualElement{style = {flexDirection = FlexDirection.Row, flexGrow = 2, justifyContent = Justify.SpaceAround}};
			
			root.Add(togglesBox);
			
			dropdown.RegisterValueChangedCallback(evt =>{
				var path = evt.newValue;
				m_component = path.Split("/").Last().Replace(" ","");
				m_type = m_component;
				
				togglesBox.Clear();
				path.Split("/").Last().Split(" ").ToList().ForEach(o=> togglesBox.Add(new CustomUI.ToggleButton(o){style = {flexShrink = 1, flexGrow = 1, alignItems = Align.Center, marginBottom = 5, marginLeft = 5, marginRight = 5, marginTop = 5}}));
			});
			
			
			var buttonsBox = new VisualElement(){style = {flexDirection = FlexDirection.RowReverse, flexGrow = 0}};
			
			root.Add(buttonsBox);
			
			var ok = new Button(){text = "OK", style = {flexShrink = 1}};
			ok.clicked += () => {
				if (dropdown.index < 0) return;
				m_groupPath = string.Join("",
					togglesBox.Query<CustomUI.ToggleButton>().ToList()
					.Where(o => o.value)
					.Select(o => o.text));
					
				var code = CODE.Replace("#m_component#",m_component)
					.Replace("#m_type#",m_type)
					.Replace($"#{nameof(m_groupPath)}#",string.IsNullOrEmpty(m_groupPath) ? "" : m_groupPath + "/");
					
				var path = $"Assets/FX Creator/Editor/Animation/ClipGenerator/Gen/{m_groupPath}";
				
				// 作成するアセットのパス
				var filePath =
					$"{path}/ToggleClip_{m_component}.cs";

				// もし名前(パス)が重複していた場合に、自動で語尾に「Sample1.cs」みたく数字をつけてくれる
				var assetPath = AssetDatabase.GenerateUniqueAssetPath(filePath);
				
				if(!Directory.Exists(path))
					Directory.CreateDirectory(path);

				// アセット(.cs)を作成する
				File.WriteAllText(filePath, code);
        
				// 変更があったアセットをインポートする(UnityEditorの更新)
				AssetDatabase.Refresh();
			};
			
			buttonsBox.Add(ok);
			
			var copy = new Button(){text = "Copy", style = {flexShrink = 1}};
			copy.clicked += () => {
				m_groupPath = string.Join("",
					togglesBox.Query<CustomUI.ToggleButton>().ToList()
					.Where(o => o.value)
					.Select(o => o.text));
				
				var method = METHOD.Replace($"#{nameof(m_component)}#",m_component)
					.Replace($"#{nameof(m_type)}#",m_type)
					.Replace($"#{nameof(m_groupPath)}#",string.IsNullOrEmpty(m_groupPath) ? "" : m_groupPath + "/");
				
				EditorGUIUtility.systemCopyBuffer = method;
			};
			
			buttonsBox.Add(copy);
			
			var cansel = new Button(){text = "Cancel", style = {flexShrink = 1}};
			cansel.clicked += () => this.Close();
			
			buttonsBox.Add(cansel);
		}
	}
}
#endif