using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using VRC.SDK3.Avatars.ScriptableObjects;
using System.Linq;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;
using UnityEditor.Animations;
using colloid.FXCreator.VRCExpressionParametersExtention.CustomUI;
using System;

using Parameter = VRC.SDK3.Avatars.ScriptableObjects.VRCExpressionParameters.Parameter;

[CustomEditor(typeof(VRCExpressionParameters))]
public class VRCExpressionParametersEditorExtention : VRCExpressionParametersEditor
{
	Parameter[] m_parameters;
	VRCExpressionParameters SO;
	[SerializeField]
	VisualTreeAsset m_tree_ParameterSet,m_inAnimatorParameters,m_inAnimatorParameter;
	
	const string m_split = "~~.#/";
	
	//リスト内で登録したコールバックをUnregisterCallbackでクリーニングするアクション
	Action m_unregisterAll = null;
	//Action m_unregisterInAnimationParameterList = null;
	
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
		var InAnimatorParametersList = InAnimatorParameters.Q<ListView>();
		var filterText = "";
		
		var root = new VisualElement();
		root.Add(new IMGUIContainer(()=> OnInspectorGUI()));
		
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
		
		var add = new Button(){text = "Add", style = {flexGrow = 1}};
		add.clicked += () => {
			System.Array.Resize(ref SO.parameters, SO.parameters.Length + 1);
		};
		
		buttonBox.Add(add);
		
		var delete = new Button(){text = "Delete", style = {flexGrow = 1,display = DisplayStyle.None}};
		delete.clicked += () => {
			System.Array.Resize(ref SO.parameters, SO.parameters.Length - 1);
		};
		
		buttonBox.Add(delete);
		
		var relode = new Button(){text = "Relode", style = {flexGrow = 1}};
		relode.clicked += () => {
			SetInAnimatorParametersList();
		};
		
		buttonBox.Add(relode);
		
		var avatarSelector = new DropdownField(){label = "Active Avatar",choices = avatars.Select(o => o.name).ToList(), index = 0};
		avatar = avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>();
		avatarSelector.RegisterValueChangedCallback(evt => avatar = avatars.SingleOrDefault(o => o.name == evt.newValue).GetComponent<VRCAvatarDescriptor>());
		
		root.Add(avatarSelector);
		
		var ParametersList = new ListView(){headerTitle = "Parameters", showFoldoutHeader = true, selectionType = SelectionType.None};
		ParametersList.bindingPath = "parameters";
		ParametersList.makeItem = () => {
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
						var parameterType = m_parameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Bool ? AnimatorControllerParameterType.Bool 
							: m_parameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Float ? AnimatorControllerParameterType.Float 
							: m_parameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Trigger;
						
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
			ve.Q<Label>().BindProperty(serializedObject.FindProperty($"parameters.Array.data[{i}].name"));
			ve.name = ve.Q<Label>().text;
			var avatar = avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>();
			ve.Query<ToggleButton>().ForEach(tg => tg.SetValueWithoutNotify(false));
			ve.Query<ToggleButton>().ForEach(tg => tg.SetEnabled(true));
			
			//void SetParametersListEvent(ChangeEvent<string> evt)
			//{
			//	ve.Query<ToggleButton>().ForEach(tg => tg.SetValueWithoutNotify(false));
			//	ve.Query<ToggleButton>().ForEach(tg => tg.SetEnabled(true));
			//	ve.name = evt.newValue;
			//	SetParametersList();
			//}
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
				
			//m_unregisterAll += () =>
			//	ve.Q<Label>().UnregisterValueChangedCallback(SetParametersListEvent);
			//ve.Q<Label>().RegisterValueChangedCallback(SetParametersListEvent);
			
			//EventCallback<ChangeEvent<bool>> SetNewParameters = (evt) =>
			//{
			//	Debug.Log((evt.target as ToggleButton).name);
			//	avatar.baseAnimationLayers
			//		.ToList().ForEach(o => {
			//			if (o.isDefault || o.animatorController == null)
			//			{
			//				ve.Query<ToggleButton>().Where(tg => tg.name == o.type.ToString())
			//					.ForEach(tg => tg.SetEnabled(false));
			//				return;
			//			}
						
			//			if (o.type.ToString() != (evt.target as ToggleButton).name) return;
						
			//			var parameters = (o.animatorController as AnimatorController).parameters;
			//			var parameterType = m_parameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Bool ? AnimatorControllerParameterType.Bool 
			//				: m_parameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Float ? AnimatorControllerParameterType.Float 
			//				: m_parameters.SingleOrDefault(p => p.name == ve.name).valueType == VRCExpressionParameters.ValueType.Int ? AnimatorControllerParameterType.Int : AnimatorControllerParameterType.Trigger;
						
			//			if (evt.newValue)
			//			(o.animatorController as AnimatorController).AddParameter(ve.name,parameterType);
			//			else
			//			(o.animatorController as AnimatorController).RemoveParameter(
			//				parameters.SingleOrDefault(p => p.name == ve.name));
			//		});
			//	SetInAnimatorParametersList();
			//};
			
			//ve.Query<ToggleButton>().ForEach(tg => tg.RegisterCallback<ChangeEvent<bool>>(SetNewParameters));
			//m_unregisterAll += () =>
			//	ve.Query<ToggleButton>().ForEach(tg => 
			//	tg.UnregisterCallback<ChangeEvent<bool>>(SetNewParameters));
		};
		ParametersList.unbindItem = (ve,i) => {
			//if (m_unregisterAll == null || i != 0) return;
			//m_unregisterAll?.Invoke();
			//m_unregisterAll = null;
		};
		
		root.Add(ParametersList);
		
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

		InAnimatorParameters.Query<ToggleButton>().ForEach(o => o.RegisterCallback<ChangeEvent<bool>>(evt => SetInAnimatorParametersList()));
		
		InAnimatorParametersList.makeItem = () => {
			VisualElement ve = m_inAnimatorParameter.CloneTree();

			void Clicked()
			{
				var newParameter = new Parameter();
				newParameter.name = ve.name;
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
			
			//m_unregisterInAnimationParameterList = () => ve.Q<Button>().clicked -= Clicked;
			ve.Q<Button>().clicked += Clicked;
			
			return ve;
		};
		InAnimatorParametersList.bindItem = (ve,i) => {
			(ve.Q<Label>() as Label).text = InAnimatorParametersList.itemsSource[i] as string;
			ve.name = ve.Q<Label>().text;
			
		
		};
		InAnimatorParametersList.unbindItem = (ve,i) => {
			//if (m_unregisterInAnimationParameterList == null) return;
			//m_unregisterInAnimationParameterList?.Invoke();
			//m_unregisterInAnimationParameterList = null;
			//EditorApplication.delayCall -= SetInAnimatorParametersList;
		};
		
		InAnimatorParametersList.itemsSourceChanged += () => {
			//Debug.Log(string.Join(m_split,avatars.SingleOrDefault(o => o.name == avatarSelector.value).GetComponent<VRCAvatarDescriptor>().baseAnimationLayers.Where(o => !o.isDefault).Select(o => string.Join(m_split,(o.animatorController as AnimatorController).parameters.Select(p => p.name)))));
		};
		
		InAnimatorParameters.Q<TextField>().RegisterValueChangedCallback(evt => {
			filterText = evt.newValue;
			SetInAnimatorParametersList();
		});
		
		root.Add(InAnimatorParameters);
		
		var fold = new Foldout(){text = "DefaultInspector", value = false};
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