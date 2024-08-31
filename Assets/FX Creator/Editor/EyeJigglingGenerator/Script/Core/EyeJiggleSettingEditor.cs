#if UNITY_2021_1_OR_NEWER
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace colloid.FXCreator.EyeJigglingGenerator
{
using BakeBlendshape = EyeJiggleInspectorPreview.BakeBlendshape;
	
[CustomEditor(typeof(EyeJiggleSetting))]
public class EyeJiggleSettingEditor : Editor
{
	//Get UXML files
	[SerializeField]
	VisualTreeAsset m_custumEditor;
	[SerializeField]
	VisualTreeAsset m_custumPropertyDrawer;
	
	EyeJiggleSetting m_editor;
	bool m_initialize = true;
	
	SkinnedMeshRenderer m_mesh;
	List<BlendAnimation> m_blendAnimation;
	EyeJiggleInspectorPreview m_meshPreview;
	GUIContent m_title = new GUIContent("プレビュー");
	bool m_playing = false;
	string m_blendshapeName;
	AnimationCurve m_animationCurve;
	string m_path;
	
	TimeController m_timer;
	MinMaxSlider m_minmaxSlider;
	List<MinMaxSlider> m_minmaxSliders = new List<MinMaxSlider>();
	//float m_startTime;
	//float m_endTime;
	ProgressBar m_progress;
	Slider m_timeSlider;
	List<Slider> m_timeSliders = new List<Slider>();
	
	//リスト内で登録したコールバックをUnregisterCallbackでクリーニングするアクション
	Action m_unregisterAll = null;
	
	// This function is called when the object is loaded.
	protected void OnEnable() {
		var editor = serializedObject.targetObject as EyeJiggleSetting;
		m_editor = editor;
		m_timer = new TimeController(){Loop = true};
		m_timer.startTime = m_editor.TimerClamps.x;
		m_timer.endTime = m_editor.TimerClamps.y;
		InitEditor();
	}
	
	// This function is called when the scriptable object goes out of scope.
	protected void OnDisable() {
		if (m_meshPreview != null)
			m_meshPreview.Dispose();
			
		m_minmaxSlider.SetValueWithoutNotify(Vector2.zero); //Sliderの値の更新を受け取るためリセット
		m_unregisterAll?.Invoke();　//リストのSliderの更新時にイベントが走るのを止める
		m_minmaxSlider.UnregisterValueChangedCallback(OnTimerClampChanged);
		m_minmaxSlider.RegisterValueChangedCallback(o =>{
			m_minmaxSliders.ForEach(target => {
				target.value = Vector2.zero;　//Sliderの値の更新を受け取るためリセット
				target.value = o.newValue;
			});
		});
		m_minmaxSlider.value = m_editor.TimerClamps;
		
		if (m_timer != null)
		{
			m_timer.Stop();
			m_timer = null;
		}
		EditorApplication.update -= OnPlaying;
	}
	
	// This function is called when the scriptable object will be destroyed.
	protected void OnDestroy() {
		
	}
	
	public override VisualElement CreateInspectorGUI()
	{
		VisualElement inspector = new VisualElement();
		m_custumEditor.CloneTree(inspector);
		InspectorElement.FillDefaultInspector(inspector.Q("Default_Inspector"), serializedObject, this);
		
		var meshOF = inspector.Q<ObjectField>();
		var dropdown = inspector.Q<DropdownField>();
		var curve = inspector.Q<CurveField>();
		m_minmaxSlider = inspector.Q<MinMaxSlider>();
		m_progress = inspector.Q<ProgressBar>();
		m_timeSlider = inspector.Q<Slider>();
		m_timeSlider.Q<VisualElement>("","unity-slider__input").pickingMode = PickingMode.Ignore;
		m_timeSlider.Q<VisualElement>("unity-drag-container").pickingMode = PickingMode.Ignore;
		var list = inspector.Q<ListView>();
		var tooltipBox = inspector.Q<TextField>("ToolTips");
		tooltipBox.Q<TextElement>().enableRichText = true;
		
		//編集メッシュの登録
		meshOF.RegisterValueChangedCallback(o => {
			tooltipBox.style.display = DisplayStyle.None;
			var mesh = o.newValue as SkinnedMeshRenderer;
			m_mesh = mesh;
			inspector.Query<DropdownField>().ForEach(field => {
				SetDropDownField(field , m_initialize);
			});
		});
		
		//導入先Layer番号を-1~100の間に制限
		var layerIndex = inspector.Q<IntegerField>();
		layerIndex.RegisterValueChangedCallback(o =>{
			var i = o.newValue;
			if (i < -1 | i > 100) layerIndex.value = o.previousValue;
		});
		
		//メインブレンドシェイプ・アニメーションカーブの編集
		dropdown.RegisterValueChangedCallback(o => {
			var selectingBlendShapeName = o.newValue;
			m_blendshapeName = selectingBlendShapeName;
			if (m_meshPreview == null || m_mesh == null) return;
			m_meshPreview.BlendshapeWeight = 100f;
			m_meshPreview.SetBlendshape(dropdown.index);
		});
		curve.RegisterValueChangedCallback(o => {
			var curve = o.newValue;
			tooltipBox.style.display = DisplayStyle.None;
			m_animationCurve = curve;
			m_minmaxSlider.lowLimit = curve.keys.First().time;
			m_minmaxSlider.highLimit = curve.keys.Last().time;
			m_timeSlider.lowValue = curve.keys.First().time;
			m_timeSlider.highValue = curve.keys.Last().time;
			
			RepaintPreview();
		});
		//スライダーの描写
		m_timeSlider.RegisterValueChangedCallback(OnTimerSliderChanged);
		if (m_animationCurve != null)
		{
			m_minmaxSlider.lowLimit = m_animationCurve.keys.First().time;
			m_minmaxSlider.highLimit = m_animationCurve.keys.Last().time;
		}
		m_minmaxSlider.RegisterValueChangedCallback(OnTimerClampChanged);
		
		//アニメーションリストの編集
		#region AnimationList
		list.makeItem = m_custumPropertyDrawer.CloneTree;
		list.bindItem = (ve,i) => {
			ve.Q<BindableElement>().BindProperty(list.itemsSource[i] as SerializedProperty);
			var listDropDown = ve.Q<DropdownField>();
			var listAnimationCurve = ve.Q<CurveField>();
			var listtimeSlider = ve.Q<Slider>();
			listtimeSlider.Q<VisualElement>("","unity-slider__input").pickingMode = PickingMode.Ignore;
			listtimeSlider.Q<VisualElement>("unity-drag-container").pickingMode = PickingMode.Ignore;
			m_timeSliders.Add(listtimeSlider);
			var listMinMaxSlider = ve.Q<MinMaxSlider>();
			m_minmaxSliders.Add(listMinMaxSlider);
			var listButton = ve.Q<Toggle>();
			
			if (listAnimationCurve.value.keys.Length > 0)
			{
				listMinMaxSlider.lowLimit = listAnimationCurve.value.keys.First().time;
				listMinMaxSlider.highLimit = listAnimationCurve.value.keys.Last().time;
				listtimeSlider.lowValue = listAnimationCurve.value.keys.First().time;
				listtimeSlider.highValue = listAnimationCurve.value.keys.Last().time;
			}
			
			EventCallback<ChangeEvent<bool>> toggleChangeEvent = (evt) => 
			{
				var playing = evt.newValue;
				SetBlendAnimationValue(playing, listDropDown.value, listAnimationCurve.value);
				RepaintPreview();
			};
			
			EventCallback<ChangeEvent<string>> dropdownChangeEvent = (evt) =>
			{
				var selectingBlendShapeName = evt.newValue;
				var oldBlendShapeName = evt.previousValue;
				
				if (m_meshPreview == null || m_mesh == null) return;
				
				if (!m_playing)
					m_meshPreview.SetbakeBlendshapes(new BakeBlendshape(){
						BlendShapeIndex = listDropDown.index,
						BlendShapeWeight = 100f});
				
				if(m_meshPreview.BakeBlendshapes.Count <= 0) return;
				m_meshPreview.BakeBlendshapes?.Remove(
					m_meshPreview.BakeBlendshapes.FirstOrDefault(o => o.BlendShapeIndex == m_mesh.sharedMesh.GetBlendShapeIndex(oldBlendShapeName)));
					
				SetBlendAnimationValue(listButton.value, selectingBlendShapeName, listAnimationCurve.value);
				RepaintPreview();
			};
			
			EventCallback<ChangeEvent<AnimationCurve>> animationCurveChangeEvent = (evt) =>
			{
				var curve = evt.newValue;
				
				if (curve.keys.Length > 0)
				{
					listMinMaxSlider.lowLimit = curve.keys.First().time;
					listMinMaxSlider.highLimit = curve.keys.Last().time;	
					listtimeSlider.lowValue = curve.keys.First().time;
					listtimeSlider.highValue = curve.keys.Last().time;
				}

				if (m_blendAnimation.Any(o => o.BlendShape == listDropDown.value))
					m_blendAnimation?.Remove(m_blendAnimation.FirstOrDefault(o => o.BlendShape == listDropDown.value));
				m_blendAnimation.Add(new BlendAnimation(){BlendShape = listDropDown.value,AnimationCurve = curve});
				
				RepaintPreview();
			};
			
			SetDropDownField(listDropDown);
			listButton.RegisterValueChangedCallback(toggleChangeEvent);
			m_unregisterAll += () => 
				listButton.UnregisterValueChangedCallback(toggleChangeEvent);
		
			listDropDown.RegisterValueChangedCallback(dropdownChangeEvent);
			m_unregisterAll += () => 
				listDropDown.UnregisterValueChangedCallback(dropdownChangeEvent);
			
			listAnimationCurve.RegisterValueChangedCallback(animationCurveChangeEvent);
			m_unregisterAll += () =>
				listAnimationCurve.UnregisterValueChangedCallback(animationCurveChangeEvent);
				
			listtimeSlider.RegisterValueChangedCallback(OnTimerSliderChanged);
			m_unregisterAll += () =>
				listtimeSlider.UnregisterValueChangedCallback(OnTimerSliderChanged);
				
			listMinMaxSlider.RegisterValueChangedCallback(OnTimerClampChanged);
			m_unregisterAll += () =>
				listMinMaxSlider.UnregisterValueChangedCallback(OnTimerClampChanged);
			
			
			if (list.Q<VisualElement>("unity-list-view__reorderable-item") != null)
				list.Query<VisualElement>("unity-list-view__reorderable-item").AtIndex(i).AddToClassList("reorderable-item");
			
			Repaint();
		};
		list.Q<ScrollView>().AddToClassList("sector");
		
		list.unbindItem = (ve,i) => {
			if (m_unregisterAll != null && i == 0)
			{
				m_unregisterAll?.Invoke();
				m_unregisterAll = null;
				if (m_meshPreview == null) return;
				m_meshPreview.BakeBlendshapes.Clear();
				if (m_blendAnimation != null)
					m_blendAnimation.Clear();
				if (m_timeSliders != null)
					m_timeSliders.Clear();
				if (m_minmaxSliders != null)
					m_minmaxSliders.Clear();
					
				InitEditor();
			}
		};
		#endregion
		
		//再生ボタン
		var play = inspector.Q<Button>("PlayButton");
		play.clicked += () => {
			if (m_mesh == null) {
				tooltipBox.value = 
					"<color=yellow><b>メッシュを設定してください</b></color>";
					tooltipBox.style.display = DisplayStyle.Flex;
				return;
				}
			if (m_animationCurve.keys.Count() <= 0) {
				tooltipBox.value = 
					"<color=yellow><b>カーブを設定してください</b></color>";
				tooltipBox.style.display = DisplayStyle.Flex;
				return;
				}
			
			m_playing = !m_playing;
			if (m_playing)
			{
				InitEditor();
				EditorApplication.update += OnPlaying;
				m_timer.Play();
				play.style.backgroundColor = Color.green;
			}
			else
			{
				m_meshPreview.BlendshapeWeight = m_animationCurve.Evaluate(m_timer.time)*100;
				m_meshPreview.SetBlendshape(m_meshPreview.activeBlendshape);
				EditorApplication.update -= OnPlaying;
				m_timer.Pause();
				play.style.backgroundColor = Color.white * 0.9f;
			}
		};
		
		//アニメーションクリップ作成ボタン
		var save = inspector.Q<Button>("SaveButton");
		save.clicked += () => {
			if (m_mesh == null) return;
			InitEditor();
			
			var Path = EditorUtility.SaveFilePanelInProject(
				"Save new AnimationClip",
				$"",
				"anim",
				"",
				string.IsNullOrEmpty(m_path) ? "Assets" : m_path);
			m_editor.Path = Path;
			if (string.IsNullOrEmpty(Path)) return;
			
			var Clip = GenerateClip();
			GenerateAnimClip.Save(Clip, Path);
		};
		
		//プロパティの変更をキャッチ
		//inspector.TrackSerializedObjectValue(serializedObject, CheckForValueChanged);
		//CheckForValueChanged(serializedObject);
		
		m_initialize = true;
		
		//inspectorの描写
		return inspector;
	}
	
	#region LocalMethod
	void InitEditor()
	{
		//m_mesh = m_editor.Mesh;
		//m_blendshapeName = m_editor.MainBlendShape;
		//m_animationCurve = m_editor.AnimationCurve;
		//m_timer.startTime = m_editor.TimerClump.x;
		//m_timer.endTime = m_editor.TimerClump.y;
		if (m_blendAnimation == null || m_blendAnimation.Count <= 0)
			m_blendAnimation = m_editor.BlendAnimations.Where(o => o.PlayButton == true).ToList();
		if (m_path != null)
			m_path = m_editor.Path;
		if (m_mesh == null) return;
		if (m_meshPreview == null || m_meshPreview.Scene == null)
			m_meshPreview = new EyeJiggleInspectorPreview(m_mesh);
		m_meshPreview.activeBlendshape = m_mesh.sharedMesh.GetBlendShapeIndex(m_blendshapeName);
	}
	
	void SetDropDownField(DropdownField dropdown , bool initialize = false)
	{
		if (m_meshPreview != null && initialize)
		{
			m_meshPreview.Dispose();
			m_meshPreview = null;
		}
		dropdown.choices.Clear();
		if (m_mesh == null)
		{
			//dropdown.value = "-none-";
		}
		else
		{
			if (m_meshPreview == null)
				m_meshPreview = new EyeJiggleInspectorPreview(m_mesh);
			dropdown.choices = new List<string>(m_meshPreview.BlendShapes);
		}
	}
	
	void SetBlendAnimationValue(bool playing, string blendshapeName, AnimationCurve blendshapeWeight)
	{
		if (m_blendAnimation == null) m_blendAnimation = new List<BlendAnimation>();
		if (playing)
		{
			if (m_blendAnimation.Any(o => o.BlendShape == blendshapeName)) return;
			m_blendAnimation.Add(new BlendAnimation(){
				BlendShape = blendshapeName,
				AnimationCurve = blendshapeWeight});
		}
		else
		{
			if (m_blendAnimation.Count <= 0) return;
			m_blendAnimation?.Remove(m_blendAnimation.FirstOrDefault(o => 
				o.BlendShape == blendshapeName));
		}
	}
	
	void OnTimerSliderChanged(ChangeEvent<float> evt)
	{
		var time = evt.newValue;

		m_timer.time = time;
		
		if (m_timeSlider.value != time)
			m_timeSlider.SetValueWithoutNotify(Mathf.Clamp(time, m_timeSlider.lowValue, m_timeSlider.highValue));
				
		foreach (var timeSlider in m_timeSliders)
		{
			if (timeSlider.value == time) continue;
			timeSlider.SetValueWithoutNotify(Mathf.Clamp(time, timeSlider.lowValue, timeSlider.highValue));
		}
		
		RepaintPreview();
	}


	void OnTimerClampChanged(ChangeEvent<Vector2> evt)
	{
		var val = evt.newValue;
		var target = evt.target as MinMaxSlider;

		m_timer.startTime = val.x;
		m_timer.endTime = val.y;
		
		m_minmaxSlider.SetValueWithoutNotify(SliderClampValue(target,m_minmaxSlider,val));
		
		m_minmaxSliders.ForEach(o => o.SetValueWithoutNotify(SliderClampValue(target,o,val)));
		
		m_editor.TimerClamps = val;
	}
	
	Vector2 SliderClampValue(MinMaxSlider select, MinMaxSlider target, Vector2 value)
	{
		var vec2 = target.value;
		if (select.minValue != select.lowLimit)
		{
			vec2.x = SliderClampValue(target,value.x);
		}
		if (select.maxValue != select.highLimit)
		{
			vec2.y = SliderClampValue(target,value.y);
		}
		return vec2;
	}
	float SliderClampValue(MinMaxSlider target, float value)
	{
		var clamp = Mathf.Clamp(value, target.lowLimit, target.highLimit);
		return clamp;
	}
	
	void CheckForValueChanged(SerializedObject so)
	{
		var activeEyeJiggleSetting = so.targetObject as EyeJiggleSetting;
		var duplicate = activeEyeJiggleSetting.BlendAnimations.GroupBy(o => o.BlendShape).Where(o => o.Count()> 1).Select(group => group.Key).ToList();
		Debug.Log($"ValueChanged:{string.Join(",",duplicate)}");
	}
	
	#endregion
	#region PreviewAnimation
	void RepaintPreview()
	{
		if (m_mesh == null) return;
		
		if (m_blendAnimation != null)
		{
			m_meshPreview.BakeBlendshapes = m_blendAnimation.Select((o) => {
				return new BakeBlendshape(){
					m_blendshape = m_mesh.sharedMesh.GetBlendShapeIndex(o.BlendShape),
					m_blendweight = o.AnimationCurve.Evaluate(m_timer.time)*100
				};
			}).ToList();
		}

		if (m_blendshapeName != null)
		{
			m_meshPreview.BlendshapeWeight = 
				m_animationCurve.Evaluate(m_timer.time)*
				100;
			m_meshPreview.SetBlendshape(m_mesh.sharedMesh.GetBlendShapeIndex(m_blendshapeName));
		}
	}
	
	void OnPlaying()
	{
		m_timer.EditorTime();
		RepaintPreview();
		
		m_timeSlider.value = m_timer.time;
		//m_timeSliders.ForEach(o => o.value = m_timer.time);

		this.Repaint();
	}
	#endregion	
	
	#region PublicMethod
	public AnimationClip GenerateClip()
	{
		var SaveBlendANimations = new List<BlendAnimation>(m_blendAnimation);
		SaveBlendANimations.Add(new BlendAnimation(){BlendShape = m_blendshapeName, AnimationCurve = m_animationCurve});
		return GenerateAnimClip.Generate(SaveBlendANimations,m_mesh);
	}
	#endregion
	
	#region Preview
	public override bool HasPreviewGUI() => true;
	public override GUIContent GetPreviewTitle() => m_title;
	public override void OnPreviewSettings()
	{
		if (m_meshPreview == null)
		{
			base.OnPreviewSettings();
		}
		else
		{
			m_meshPreview.OnPreviewSettings();	
		}
	}
	public override void OnInteractivePreviewGUI(Rect r, GUIStyle background) 
	{
		if (m_mesh == null) return;
		m_meshPreview.OnInteractivePreviewGUI(r,background);
	}
	
	public override string GetInfoString()
	{
		return $"{(m_timer.time).ToString("00.00")} / " +
			$"{(m_animationCurve.keys.Last().time).ToString("00.00")}(s)";
	}
	#endregion
}
#endif
}