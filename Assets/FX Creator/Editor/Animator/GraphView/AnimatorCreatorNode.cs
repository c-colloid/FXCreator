using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEditorInternal;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using VRC.SDK3.Avatars.Components;

public class AnimatorCreatorNode : Node
{
	[SerializeField]
	VisualTreeAsset m_virtualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(AssetDatabase.GUIDToAssetPath("32350547bc91dde4fb1d5c3aaf2b6d27"));
	
	private float GridSize = 20;
	private Vector2 MinimumSize = new Vector2(200,100);
    
	Motion m_motion;
	float m_speed,
		m_speedMultiplier,
		m_cycleOffset;
	bool m_mittor,
		m_footIK,
		m_writeDefaults;
	string m_motionTimeChoice;
	List<string> m_motionTime = new List<string>();
	List<UnityEditor.Animations.AnimatorTransition> m_transitions = new List<UnityEditor.Animations.AnimatorTransition>();
	
	float m_fieldsBackgroundHeight;
	float m_settingsFieldsHeight = 123;
	
	PreviewRenderUtility m_preview = new PreviewRenderUtility();
	GameObject m_targetGO;
	Animator m_target;
	float m_avatarEyesHight;

    public AnimatorCreatorNode()
    {
	    capabilities |= Capabilities.Resizable;
	    this.style.width = 130;

	    title = "";
	    var titleTextField = new TextField(){style = {display = DisplayStyle.None, flexGrow = 1, fontSize = 12}};
	    titleTextField.RegisterCallback<KeyDownEvent>(evt =>
	    {
	    	if (evt.keyCode != KeyCode.Return) return;
	    	title = titleTextField.value;
	    	titleTextField.style.display = DisplayStyle.None;
	    	titleContainer.Q<Label>().style.flexGrow = 1;
		    titleContainer.Q<Label>().style.display = DisplayStyle.Flex;
	    });
	    titleTextField.RegisterCallback<FocusOutEvent>(evt =>{
	    	title = titleTextField.value;
	    	titleTextField.style.display =
		    	DisplayStyle.None;
	    	titleContainer.Q<Label>().style.flexGrow = 1;
		    titleContainer.Q<Label>().style.display = 
		    	DisplayStyle.Flex;
	    });
	    titleContainer.Insert(0, titleTextField);
	    titleContainer.Q<Label>().style.flexGrow = 1;
	    titleContainer.Q<Label>().enableRichText = true;
	    titleContainer.RegisterCallback<MouseDownEvent>(evt =>
	    {
	    	if (evt.button != 0 || evt.clickCount != 2) return;
	    	titleTextField.style.display =
		    	titleTextField.style.display == DisplayStyle.None ?
		    	DisplayStyle.Flex :
		    	DisplayStyle.None;
	    	titleTextField.Focus();
	    	titleContainer.Q<Label>().style.flexGrow = titleTextField.style.display == DisplayStyle.None ?
		    	1 : 0;
		    titleContainer.Q<Label>().style.display = titleTextField.style.display == DisplayStyle.None ?
		    	DisplayStyle.Flex :
		    	DisplayStyle.None;
	    });

        var inputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(Port));
	    inputPort.portName = "In";
	    inputPort.portColor = Color.blue;
        inputContainer.Add(inputPort);

        var outputPort = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(Port));
	    outputPort.portName = "Out";
	    outputPort.portColor = Color.red;
	    outputContainer.Add(outputPort);
	    outputPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(Port));
	    outputPort.portName = "Out";
	    outputPort.portColor = Color.red;
	    outputContainer.Add(outputPort);
        
	    var UXML = m_virtualTreeAsset.Instantiate();
	    mainContainer.Add(UXML);
	    var background = UXML.Q<VisualElement>("FieldsBackground");
	    var render = UXML.Q<VisualElement>("Render");
	    //var textField = UXML.Q<TextField>("FieldsTitle");
        //textField.style.flexGrow = 1;
        //textField.RegisterCallback<FocusInEvent>(evt => { Input.imeCompositionMode = IMECompositionMode.On; });
        //textField.RegisterCallback<FocusOutEvent>(evt => { Input.imeCompositionMode = IMECompositionMode.Auto; });
	    //this.mainContainer.Add(textField);
	    var motionField = UXML.Q<ObjectField>("Motion");
	    motionField.RegisterValueChangedCallback(evt =>
	    {
	    	var newvalue = evt.newValue as Motion;
	    	m_motion = newvalue;
	    	
	    	if (m_preview != null) m_preview.Cleanup();
	    	m_preview = new PreviewRenderUtility();
	    	var targetGO = EditorSceneManager.GetActiveScene().GetRootGameObjects().Single(obj => obj.TryGetComponent<Animator>(out var target));
	    	targetGO = GameObject.Instantiate(targetGO);
	    	targetGO.hideFlags = HideFlags.HideAndDontSave;
	    	
	    	m_preview.AddSingleGO(targetGO);
	    	PlayClipWithAnimationMode(targetGO);
	    	
	    	var target = targetGO.GetComponent<Animator>();
	    	var animator = new UnityEditor.Animations.AnimatorController();
	    	
	    	var avatar = targetGO.GetComponent<VRCAvatarDescriptor>();
	    	
	    	m_targetGO = targetGO;
	    	m_target = target;
	    	m_avatarEyesHight = avatar.ViewPosition.y;
	    	
	    	var camera = m_preview.camera;
	    	camera.transform.position = new Vector3(0,m_avatarEyesHight,0.6f);
	    	camera.transform.rotation = Quaternion.identity * Quaternion.Euler(0,180,0);
	    	camera.fieldOfView = 30;
	    	camera.nearClipPlane = 0.3f;
	    	
	    	RepaintPreview();
	    	
	    });
	    //mainContainer.Add(motionField);
	    var settingsField = UXML.Q<Foldout>("Settings");
	    settingsField.RegisterValueChangedCallback(evt =>
	    {
	    	if (evt.target != settingsField) return;
	    	var toggle = evt.newValue;
	    	toggleFields(toggle, settingsField.Q<VisualElement>("unity-content"), ref m_settingsFieldsHeight);
	    });
	    settingsField.value = false;
	    var speedField = UXML.Q<FloatField>("Speed");
	    speedField.RegisterValueChangedCallback(evt =>
	    {
	    	var newvalue = evt.newValue;
	    	m_speed = newvalue;
	    });
	    //mainContainer.Add(speedField);
	    var motionTimeField = UXML.Q<DropdownField>("MotionTime");
	    motionTimeField.choices = m_motionTime;
	    motionTimeField.RegisterValueChangedCallback(evt =>
	    {
	    	var newvalue = evt.newValue;
	    	m_motionTimeChoice = newvalue;
	    });
	    //mainContainer.Add(motionTimeField);
	    var writeDefaultsField = UXML.Q<Toggle>("WriteDefaults");
	    writeDefaultsField.RegisterValueChangedCallback(evt =>
	    {
	    	var newvalue = evt.newValue;
	    	m_writeDefaults = newvalue;
	    });
	    //mainContainer.Add(writeDefaultsField);
	    var transitionsField = UXML.Q<ListView>("Transitions");
	    transitionsField.itemsSource = m_transitions;
	    //transitionsField.itemsAdded(BindNewOutPutPort);
	    //mainContainer.Add(transitionsField);
	    var toggleFieldsButton = UXML.Q<Button>("ToggleFields");
	    toggleFieldsButton.clicked += () => {
	    	background.style.display = toggleFields(background.style.display == DisplayStyle.None, background, ref m_fieldsBackgroundHeight);
	    };
	    var cameraTargetField = UXML.Q<EnumField>("CameraTarget");
	    cameraTargetField.RegisterValueChangedCallback(evt =>
	    {
	    	var newEnum = (HumanBodyBones)evt.newValue;
	    	RepaintPreview(newEnum);
	    });

        RegisterCallback<GeometryChangedEvent>(evt =>{
            var rect = evt.newRect;
            SetPosition(
                new Rect(
                    Mathf.Floor(rect.x/ GridSize) * GridSize,
                    Mathf.Floor(rect.y/ GridSize) * GridSize,
                    Mathf.Floor(rect.width/ GridSize) * GridSize,
                    Mathf.Floor(rect.height/ GridSize) * GridSize
                )
            );

            style.width = Mathf.Max(
                Mathf.Floor(rect.width / GridSize) * GridSize,
	            MinimumSize.x
            );
            style.height = Mathf.Max(
                Mathf.Floor(rect.height/ GridSize) * GridSize,
                MinimumSize.y
            );
            
	        RepaintPreview();
        });
        
	    RegisterCallback<DetachFromPanelEvent>(evt =>{
	    	AnimationMode.StopAnimationMode();
	    	m_preview.Cleanup();
	    	EditorApplication.update -= UpdateAnimation;
	    });
    }
    
	void BindNewOutPutPort(string name = "Out",Color color = new Color())
	{
		var outputPort = Port.Create<Edge>(Orientation.Vertical, Direction.Output, Port.Capacity.Multi, typeof(Port));
		outputPort.portName = name;
		outputPort.portColor = color;
		outputContainer.Add(outputPort);
	}
    
	void PlayClipWithAnimationMode(GameObject target = null, AnimationClip clip = null, float time = 0f)
	{
		if (target == null && m_targetGO == null) return;
		if (target == null)
			target = m_targetGO;
		if (m_motion.GetType() != typeof(AnimationClip)) return;
		if (clip == null)
			clip = m_motion as AnimationClip;
		
		AnimationMode.StartAnimationMode();
		AnimationMode.BeginSampling();
		AnimationMode.SampleAnimationClip(target,clip,time);
		AnimationMode.EndSampling();
	}
    
	void UpdateAnimation()
	{
		m_target.Update(Time.deltaTime);
		RepaintPreview();
	}
	
	void RepaintPreview(HumanBodyBones cameraYtarget = HumanBodyBones.Head)
	{
		m_preview.camera.transform.position = new Vector3(0,
			m_target == null ? 0.5f :
			m_target.GetBoneTransform(cameraYtarget).position.y,
			0.6f);
		var render = this.Q<VisualElement>("Render");
		if (render.layout.height <= 0) return;
		m_preview.BeginPreview(new Rect(render.layout), GUIStyle.none);
		m_preview.Render();
		var tex = m_preview.EndPreview();
		render.style.backgroundImage = Background.FromRenderTexture(tex as RenderTexture);
	}
    
	DisplayStyle toggleFields(bool current, VisualElement target, ref float heightvariavle)
	{
		var currentHeight = this.layout.height;
		if (!current) heightvariavle = target.layout.height;
		currentHeight += current ?
			Mathf.Round(heightvariavle/ GridSize) * GridSize :
			Mathf.Round(-heightvariavle/ GridSize) * GridSize;
		this.style.height = currentHeight;
		return current ? DisplayStyle.Flex : DisplayStyle.None;
	}
}
