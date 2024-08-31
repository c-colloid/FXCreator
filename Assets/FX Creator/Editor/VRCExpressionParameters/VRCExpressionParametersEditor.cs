using System.Collections;
using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDK3.Editor;
using colloid.FXCreator.VRCExpressionParametersExtention.CustomUI;

using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;

namespace colloid.FXCreator.VRCExpressionParametersExtention
{

[CustomEditor(typeof(VRCExpressionParameters))]
public class VRCExpressionParametersEditorExtention : VRCExpressionParametersEditor
{
	Parameter[] m_parameters;
	VRCExpressionParameters SO;
	[SerializeField]
	VisualTreeAsset m_tree_ParameterSet,m_inAnimatorParameters,m_inAnimatorParameter;
	[SerializeField]
	StyleSheet m_foldoutUSS;
	
	static readonly List<Parameter> VRCParameters = VRCDefaultParameters.VRCParameters;
	
	const string m_split = "~~.#/";
	
	new void OnEnable()
	{
		base.OnEnable();
		m_foldoutUSS = EditorGUIUtility.isProSkin ? AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath("bd6ed3062a3676740831ef55bfd77e69")) : AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath("44427f82ec64ac6459f3ae6e67566eb2"));
		
		if (!EditorApplication.isPlaying) return;
		if (m_tree_ParameterSet == null)
			m_tree_ParameterSet = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("1fde1cc766ec22d4798067b54a4dfa90"));
		if (m_inAnimatorParameters == null)
			m_inAnimatorParameters = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("c300baf15eef91942a0acde60878ab8f"));
		if (m_inAnimatorParameter == null)
			m_inAnimatorParameter = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("20f4032d8d7d596459dcc880d9427c7c"));
		if ( m_foldoutUSS == null)
			m_foldoutUSS = AssetDatabase.LoadAssetAtPath<StyleSheet>(AssetDatabase.GUIDToAssetPath("44427f82ec64ac6459f3ae6e67566eb2"));
	}
	
	public override void OnInspectorGUI()
	{
		serializedObject.Update();
		
		base.OnInspectorGUI();
		m_parameters = SO.parameters;
		
		serializedObject.ApplyModifiedProperties();
	}
	
	public override VisualElement CreateInspectorGUI()
	{
		SO = serializedObject.targetObject as VRCExpressionParameters;
		m_parameters = SO.parameters;
		var avatars = EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(o => o.TryGetComponent<VRCAvatarDescriptor>(out var result) && o.active);
		var avatar = new VRCAvatarDescriptor();
		var InAnimatorParameters = m_inAnimatorParameters.CloneTree();
		InAnimatorParameters.styleSheets.Add(m_foldoutUSS);
		var InAnimatorParametersList = InAnimatorParameters.Q<ListView>();
		var filterText = "";
		
		var root = new VisualElement();
		#if VRCSDK_370_OR_NEWER
		root.Add(base.CreateInspectorGUI());
		#else
		root.Add(new IMGUIContainer(()=> OnInspectorGUI()));
		#endif
		
		if (avatars.Count() < 1)
		{
			var exceptionBox = new Box();
			exceptionBox.style.paddingBottom
				= exceptionBox.style.paddingLeft
				= exceptionBox.style.paddingRight
				= exceptionBox.style.paddingTop
				= 5;
			root.Add(exceptionBox);
			
			var nullAvatarsException = new Label();
			nullAvatarsException.text = @"No active avatar descriptor found in scene.";
			exceptionBox.Add(nullAvatarsException);
			
			return root;
		}
		
		var buttonBox = new Box(){style = {flexDirection = FlexDirection.Row}};
		root.Add(buttonBox);
		
		var add = new Button(){text = "Add", style = {flexGrow = 1,display = DisplayStyle.None}};
		add.clicked += () => {
			System.Array.Resize(ref SO.parameters, SO.parameters.Length + 1);
		};
		
		buttonBox.Add(add);
		
		var delete = new Button(){text = "Delete", style = {flexGrow = 1,display = DisplayStyle.None}};
		delete.clicked += () => {
			System.Array.Resize(ref SO.parameters, SO.parameters.Length - 1);
		};
		
		buttonBox.Add(delete);
		
		var VRCParametersList = new ListView(){headerTitle = "VRC Parameters", showFoldoutHeader = true, selectionType = SelectionType.None , style = {display = DisplayStyle.None}};
		VRCParametersList.styleSheets.Add(m_foldoutUSS);
		var viewVRCParameters = new ToggleButton(){text = "View VRC Parameters", style = {flexGrow = 1}};
		viewVRCParameters.SetValueWithoutNotify(false);
		viewVRCParameters.RegisterCallback<ChangeEvent<bool>>(evt => {
			VRCParametersList.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None;
		});
		
		buttonBox.Add(viewVRCParameters);
		
		var relode = new Button(){text = "Relode", style = {flexGrow = 1}};
		relode.clicked += () => {
			SetInAnimatorParametersList();
			VRCParametersList.Rebuild();
		};
		
		buttonBox.Add(relode);
		
		var avatarSelector = new DropdownField(){label = "Active Avatar",choices = avatars.Select(o => o.name).ToList(), index = 0};
		avatar = avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>();
		avatarSelector.RegisterValueChangedCallback(evt => avatar = avatars.SingleOrDefault(o => o.name == evt.newValue).GetComponent<VRCAvatarDescriptor>());
		
		root.Add(avatarSelector);
		
		var ParametersList = new ListView(){headerTitle = "Parameters", showFoldoutHeader = true, selectionType = SelectionType.None};
		ParametersList.styleSheets.Add(m_foldoutUSS);
		ParametersList.bindingPath = "parameters";
		ParametersList.makeItem = MakeParametersListItem;
		VisualElement MakeParametersListItem()
		{
			var ve = m_tree_ParameterSet.CloneTree();
			void SetParametersListEvent(ChangeEvent<string> evt)
			{
				ve.Query<ToggleButton>().ForEach(tg => tg.SetValueWithoutNotify(false));
				ve.Query<ToggleButton>().ForEach(tg => tg.SetEnabled(true));
				ve.name = evt.newValue;
				SetParametersList();
			}
			void SetParametersList()
			{
				avatar.baseAnimationLayers
					.ToList().ForEach(o => {
						if (o.isDefault || o.animatorController == null)
						{
							ve.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
								.ForEach(tg => tg.SetEnabled(false));
							return;
						}
				
						var parameters = (o.animatorController as AnimatorController).parameters;
					
						ve.Query<ToggleButton>().Where(tg => parameters.Select(i => i.name).Contains(ve.Q<Label>().text))
							.Where(tg => tg.name == o.type.ToString())
							.ForEach(tg => tg.SetValueWithoutNotify(true))
							;
					});	
			}
			ve.Q<Label>().RegisterValueChangedCallback(SetParametersListEvent);
			
			EventCallback<ChangeEvent<bool>> SetNewParameters = (evt) =>
			{
				avatar.baseAnimationLayers
					.ToList().ForEach(o => {
						if (o.isDefault || o.animatorController == null)
						{
							ve.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
								.ForEach(tg => tg.SetEnabled(false));
							return;
						}
						
						if (o.type.ToString() != (evt.target as ToggleButton).name) return;
						
						var parameters = (o.animatorController as AnimatorController).parameters;
						var AllParameters = new List<Parameter>(m_parameters);
						AllParameters.AddRange(
							VRCParameters.Where(o => AllParameters.All(p => p.name != o.name)));
						var parameterType = AllParameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Bool ? AnimatorControllerParameterType.Bool 
							: AllParameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Float ? AnimatorControllerParameterType.Float 
							: AllParameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Trigger;
						
						if (evt.newValue)
						(o.animatorController as AnimatorController).AddParameter(ve.name,parameterType);
						else
						(o.animatorController as AnimatorController).RemoveParameter(
							parameters.SingleOrDefault(p => p.name == ve.name));
					});
				SetInAnimatorParametersList();
			};
			ve.Query<ToggleButton>().ForEach(tg => tg.RegisterCallback<ChangeEvent<bool>>(SetNewParameters));
			
			return ve;
		};
		ParametersList.bindItem = (ve,i) =>{

			//旧仕様
			//ve.Q<Label>().BindProperty(serializedObject.FindProperty($"parameters.Array.data[{i}].name"));
			//新仕様
			ve.Q<Label>().BindProperty((ParametersList.itemsSource[i] as SerializedProperty).FindPropertyRelative("name"));

			ve.name = ve.Q<Label>().text;
			var avatar = avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>();
			ve.Query<ToggleButton>().ForEach(tg => tg.SetValueWithoutNotify(false));
			ve.Query<ToggleButton>().ForEach(tg => tg.SetEnabled(true));
			VRCParametersList.RefreshItem(VRCParameters.IndexOf(VRCParameters.SingleOrDefault(o => o.name == ve.name)));
			
			void SetParametersList()
			{
				avatar.baseAnimationLayers
					.ToList().ForEach(o => {
						if (o.isDefault || o.animatorController == null)
						{
							ve.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
								.ForEach(tg => tg.SetEnabled(false));
							return;
						}
				
						var parameters = (o.animatorController as AnimatorController).parameters;
					
						ve.Query<ToggleButton>().Where(tg => parameters.Select(i => i.name).Contains(ve.Q<Label>().text))
							.Where(tg => tg.name == o.type.ToString())
							.ForEach(tg => tg.SetValueWithoutNotify(true))
							;
					});	
			}
			SetParametersList();
				
		};
		ParametersList.unbindItem = (ve,i) => {
			VRCParametersList.RefreshItem(VRCParameters.IndexOf(VRCParameters.SingleOrDefault(o => o.name == ve.name)));
		};
		
		root.Add(ParametersList);
		
		VRCParametersList.itemsSource = VRCParameters;
		VRCParametersList.makeItem = MakeParametersListItem;
		VRCParametersList.bindItem = (ve,i) =>{

			//旧仕様
			//ve.Q<Label>().BindProperty((VRCParametersList.itemsSource[i] as SerializedProperty).FindPropertyRelative("name"));
			//新仕様
			ve.SetEnabled(m_parameters.All(o => o.name != VRCParameters[i].name));

			ve.style.backgroundColor = m_parameters.All(o => o.name != VRCParameters[i].name) ? default : Color.gray*0.1f;
			ve.Q<Label>().text = VRCParameters[i].name;
			ve.name = ve.Q<Label>().text;
			var avatar = avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>();
			ve.Query<ToggleButton>().ForEach(tg => tg.SetValueWithoutNotify(false));
			ve.Query<ToggleButton>().ForEach(tg => tg.SetEnabled(true));
			
			void SetParametersList()
			{
				avatar.baseAnimationLayers
					.ToList().ForEach(o => {
						if (o.isDefault || o.animatorController == null)
						{
							ve.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
								.ForEach(tg => tg.SetEnabled(false));
							return;
						}
				
						var parameters = (o.animatorController as AnimatorController).parameters;
					
						ve.Query<ToggleButton>().Where(tg => parameters.Select(i => i.name).Contains(ve.Q<Label>().text))
							.Where(tg => tg.name == o.type.ToString())
							.ForEach(tg => tg.SetValueWithoutNotify(true))
							;
					});	
			}
			SetParametersList();
		};
		
		root.Add(VRCParametersList);
		
		void SetInAnimatorParametersList()
		{
			InAnimatorParametersList.itemsSource = string.Join(m_split,
				avatars.SingleOrDefault(o => o.name == avatar.name)
				.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers
				.Where(o => !o.isDefault && o.animatorController != null)
				.Where(o => InAnimatorParameters.Query<ToggleButton>().Where(tg => tg.value).ToList().Any(tg => tg.name == o.type.ToString()))
				.Select(o => string.Join(m_split,(o.animatorController as AnimatorController).parameters
				.Where(p => m_parameters.All(s => s.name != p.name))
				.Where(p => filterText.Split(" ").All(s => p.name.Contains(s)))
				.Select(p => p.name)))).Split(m_split,StringSplitOptions.RemoveEmptyEntries).Distinct().ToList();
				
			EditorApplication.delayCall -= SetInAnimatorParametersList;
		}
		SetInAnimatorParametersList();

		InAnimatorParameters.Query<ToggleButton>().ForEach(o => {
			o.tooltip = $"click:Toggle{Environment.NewLine}ctrl+click:SingleSelect{Environment.NewLine}ctrl+dubleclick:Disselect";
			o.RegisterCallback<MouseDownEvent>(evt => {
				if (evt.ctrlKey)
				{
					o.value = true;
					InAnimatorParameters.Query<ToggleButton>().Where(tb => tb != o && tb.value == o.value).ForEach(tb => tb.value = !tb.value);
					if (evt.clickCount > 1)
						InAnimatorParameters.Query<ToggleButton>().ForEach(tb => tb.value = !tb.value);
				}
			});
			o.RegisterCallback<ChangeEvent<bool>>(evt => SetInAnimatorParametersList());
		});
		
		InAnimatorParametersList.makeItem = () => {
			VisualElement ve = m_inAnimatorParameter.CloneTree();

			void Clicked()
			{
				var newParameter = new Parameter();
				newParameter.name = ve.name;

				//確認用
				//Debug.Log(avatars.SingleOrDefault(o => o.name == avatarSelector.value)
				//	.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers.Where(o => !o.isDefault)
				//	.Select(o => (o.animatorController as AnimatorController).parameters)
				//	.Where(o => o.Any(p => p.name == ve.name)).First().SingleOrDefault(p => p.name == ve.name).type);

				var layers = avatars.SingleOrDefault(o => o.name == avatarSelector.value)
					.GetComponent<VRCAvatarDescriptor>().baseAnimationLayers.Where(o => !o.isDefault);
				var type = layers.Select(o => (o.animatorController as AnimatorController).parameters)
					.Where(o => o.Any(p => p.name == ve.name)).First().SingleOrDefault(p => p.name == ve.name).type;
				newParameter.valueType = type == AnimatorControllerParameterType.Int ? VRCExpressionParameters.ValueType.Int
					: type == AnimatorControllerParameterType.Float ? VRCExpressionParameters.ValueType.Float
					: VRCExpressionParameters.ValueType.Bool;
				Array.Resize(ref SO.parameters,SO.parameters.Length +1);
				SO.parameters[SO.parameters.Length - 1] = newParameter;
				
				EditorApplication.delayCall += SetInAnimatorParametersList;
			}
			
			ve.Q<Button>().clicked += Clicked;
			
			return ve;
		};
		InAnimatorParametersList.bindItem = (ve,i) => {
			(ve.Q<Label>() as Label).text = InAnimatorParametersList.itemsSource[i] as string;
			ve.name = ve.Q<Label>().text;
			
		
		};
		InAnimatorParametersList.unbindItem = (ve,i) => {

		};
		
		InAnimatorParametersList.itemsSourceChanged += () => {
			//確認用
			//Debug.Log(string.Join(m_split,avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>().baseAnimationLayers.Where(o => !o.isDefault).Select(o => string.Join(m_split,(o.animatorController as AnimatorController).parameters.Select(p => p.name)))));
		};
		
		InAnimatorParameters.Q<TextField>().RegisterValueChangedCallback(evt => {
			filterText = evt.newValue;
			SetInAnimatorParametersList();
		});
		
		root.Add(InAnimatorParameters);
		
		var fold = new Foldout(){text = "DefaultInspector", value = false, style = {display = DisplayStyle.None}};
		fold.Add((new IMGUIContainer(()=> DrawDefaultInspector())));
		root.Add(fold);
		
		avatarSelector.RegisterValueChangedCallback(evt =>{
			var avatar = avatars.SingleOrDefault(o => o.name == evt.newValue).GetComponent<VRCAvatarDescriptor>();
			ParametersList.Query<ToggleButton>().ForEach(tg => tg.SetValueWithoutNotify(false));
			ParametersList.Query<ToggleButton>().ForEach(tg => tg.SetEnabled(true));
			avatar.baseAnimationLayers
				.ToList().ForEach(o => {
					if (o.isDefault || o.animatorController == null)
					{
						ParametersList.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
							.ForEach(tg => tg.SetEnabled(false));
						return;
					}
				
					var parameters = (o.animatorController as AnimatorController).parameters;
					
					var containParametersList = ParametersList.Query<VisualElement>("ParameterSet")
						.Where(ve =>
							parameters.Select(i => i.name).Contains(ve.Q<Label>().text)
						);
					containParametersList.ForEach(ve => 
							ve.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
						.ForEach(tg => tg.SetValueWithoutNotify(true))
					);
				});
			
			SetInAnimatorParametersList();
		});
		
		return root;
	}
}
}