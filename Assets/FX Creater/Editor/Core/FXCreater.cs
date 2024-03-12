using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.SceneManagement;
using UnityEngine.Playables;
using UnityEngine.Animations;
using UnityEditorInternal;
using System;
using System.Text.RegularExpressions;
using System.IO;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CustomUI;
using Hai.VisualExpressionsEditor.Scripts.Editor;
using colloid.FXCreater.Utility;

namespace colloid.FXCreater
{
public class FXCreater : EditorWindow
{
#region Variable
	#region Serialize
	[SerializeField]
	VisualTreeAsset _visualTree;
	[SerializeField]
	AnimationClip m_t_pose;
	#endregion
	
	#region Path
	//string m_folderPath = "";
	//string m_assetsRootPath = "Assets/";
	FolderPathMenuItem m_folderPathMenuItem = new FolderPathMenuItem();
	BetterTextField m_folderPath_TextField;
	#endregion
	
	#region VisualElement
	DropDownField clipsDropdown;
	ToggleinButton playButton;
	Slider playSlider;
	#endregion
	
	#region PreviewWindow
	PreviewRenderUtility preRenderUtil;
	PreviewScene m_previewScene;
	bool m_didInitialize = false;
	#endregion
	
	#region Selection
	SelectObjectOutline selection;
	GameObject m_selectObject;
	GameObject m_oldSelectObject;
	int m_selectIndex;
	List<GameObject> m_selectObjectsList = new List<GameObject>();
	List<GameObject> m_objectIsActiveList = new List<GameObject>();
	#endregion
	
	#region AnimationGraph
	public List<AnimationClip> clips;
	bool m_playing = false;
	PlayableGraph graph;
	PlayableGraph defaultGraph;
	PlayableGraph defaultHumanoidGraph;
	AnimationClipPlayable clipPlayable;
	#endregion
	
	#region Avatars
	GameObject originalAvatar;
	List<SkinnedMeshRenderer> originalAvatarSMRs = new List<SkinnedMeshRenderer>();
	List<SkinnedMeshRenderer> previewAvatarSMRs = new List<SkinnedMeshRenderer>();
	Dictionary<GameObject,bool> defaultPreviewAvatarObjectsIsActiveList = new Dictionary<GameObject,bool>();
	//GameObject cloneAvatar;
	//List<SkinnedMeshRenderer> cloneAvatarSMRs = new List<SkinnedMeshRenderer>();
	#endregion
	
	enum SaveType
	{
		Empty = 0,
		IsActive  = 1,
		BlendShape = 2
	}
#endregion
#region CreateWindow
	[MenuItem("Tools/FXCreater/", priority = 1000)]

	[MenuItem("Tools/FXCreater/ShowPanel", priority = 1000)]
    public static void ShowWindow()
    {
        FXCreater wnd = GetWindow<FXCreater>();
        wnd.titleContent = new GUIContent("FXCreater");
	    //wnd.position = Rect.MinMaxRect(0,0,300,300);
	    wnd.minSize = new Vector2(200,200);
    }
    
	public void OnEnable()
	{
		clips = new List<AnimationClip>();
		if (preRenderUtil != null)
		{
			preRenderUtil.Cleanup();	
		}
		preRenderUtil = new PreviewRenderUtility(true);
		m_didInitialize = false;
		InitializeIfNeeded();
		InitAvatar();
		
		defaultGraph = PlayableGraph.Create();
		defaultHumanoidGraph = PlayableGraph.Create();
		graph = PlayableGraph.Create();
	}
    
	public void OnDisable()
	{
		//OnRegisterUpdate(false);
		//m_previewScene.Dispose();
		//m_didInitialize = false;
		//graph.Destroy();
		preRenderUtil.Cleanup();
		clips.Clear();
		originalAvatarSMRs.Clear();
		previewAvatarSMRs.Clear();
		//cloneAvatarSMRs.Clear();
		//GameObject.DestroyImmediate(cloneAvatar);
	}
    
    public void OnDestroy()
    {
    	//EditorUserSettings.SetConfigValue(nameof(ReorderableListView),JsonUtility.ToJson(rootVisualElement.Q<ReorderableListView>().Children()));
	    //OnRegisterUpdate(false);
    	//m_previewScene.Dispose();
	    //m_didInitialize = false;
	    //graph.Destroy();
    }
    
	void InitAvatar()
	{
		/*OriginalAvatar*/
		originalAvatar = EditorSceneManager.GetActiveScene().GetRootGameObjects().ToList().Where(o => o.GetComponent<Animator>()).First();
		foreach (var item in originalAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			originalAvatarSMRs.Add(item);
		}
		
		/*PreviewAvatar*/
		SetPreviewAvatar();
		
		//CloneAvatar
		//cloneAvatar = GameObject.Instantiate(m_previewScene.Avatar.gameObject);
		//EditorSceneManager.MoveGameObjectToScene(cloneAvatar,EditorSceneManager.GetActiveScene());
		//foreach (var item in cloneAvatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		//{
		//	cloneAvatarSMRs.Add(item);
		//}
		//cloneAvatar.hideFlags = HideFlags.HideAndDontSave;
		//SceneVisibilityManager.instance.Hide(cloneAvatar,true);
		//cloneAvatar.tag = "EditorOnly";
	}
	
	void SetPreviewAvatar()
	{
		previewAvatarSMRs.Clear();
		defaultPreviewAvatarObjectsIsActiveList.Clear();
		foreach (var item in m_previewScene.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			previewAvatarSMRs.Add(item);
			var meshcollider = item.gameObject.AddComponent<MeshCollider>();
			meshcollider.sharedMesh = item.sharedMesh;
			defaultPreviewAvatarObjectsIsActiveList.Add(item.gameObject,item.gameObject.active);
		}
	}
    
	VisualTreeAsset InitVTA()
	{
		var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/FX Creater/Editor/Core/FXCreater.uxml");
		return visualTree;
	}
#endregion

#region CreateGUI
    public void CreateGUI()
    {
	    // Each editor window contains a root VisualElement object
	    VisualElement root = rootVisualElement;
	    
	    if (_visualTree == null) _visualTree = InitVTA();
	    
	    // Import UXML
	    root.Add(_visualTree.CloneTree());
	    root.Q<TemplateContainer>().StretchToParentSize();
	    root.style.marginBottom = root.style.marginLeft = root.style.marginRight = root.style.marginTop = 5;
	    
	    #region SetUIElements
	    m_folderPath_TextField = root.Q<BetterTextField>("FolderPath");
	    var resetFolderPathButton = root.Q<Button>("FolderPathReset");
	    var getAnimationFolderButton = root.Q<Button>("AnimationsFolder");
	    var newAnimationName = root.Q<BetterTextField>("AnimationName");
	    var resetAnimationNameButton = root.Q<Button>("ResetAnimationName");
	    var saveNewAnimationClipButton = root.Q<Button>("SaveAnimationClip");
	    var saveNewAnimationClipDropdown = root.Q<DropdownField>("SaveAnimationClip");
	    var getVisualExpressionEditorButton = root.Q<Button>("AnimationFile");
	    clipsDropdown = root.Q<DropDownField>("ClipsDropDown");
	    var render = root.Q<VisualElement>("Render");
	    playButton = root.Q<ToggleinButton>("PlayButton");
	    playSlider = root.Q<Slider>("PlaySlider");
		#endregion
	    #region SetIcon
	    resetFolderPathButton.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("clear_uielements").image;
	    getAnimationFolderButton.style.backgroundImage = (Texture2D)EditorGUIUtility.Load("FolderOpened Icon");
	    resetAnimationNameButton.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("clear_uielements").image;
	    saveNewAnimationClipButton.style.backgroundImage = (Texture2D)EditorGUIUtility.Load("CreateAddNew");
	    getVisualExpressionEditorButton.style.backgroundImage = (Texture2D)EditorGUIUtility.Load("AnimationClip Icon");
	    playButton.Q<Button>().style.backgroundImage = (Texture2D)EditorGUIUtility.Load("Animation Icon");
        #endregion
        #region SetKeyEvent
	    root.RegisterCallback<KeyDownEvent>(evt => {
		    switch (evt.keyCode)
		    {
		    case KeyCode.UpArrow:	clipsDropdown.index -= clipsDropdown.index == 0 ? 0 : 1;
			    break;
		    case KeyCode.DownArrow:	clipsDropdown.index += clipsDropdown.index == clips.Count ? 0 : 1;
			    break;
		    case KeyCode.Space:
		    case KeyCode.Return:	playButton.value = !playButton.value;
			    break;
		    case KeyCode.Tab: {
		    	//m_selectIndex = m_selectIndex < previewAvatarSMRs.Count -1 ? m_selectIndex + 1 : 0;
		    	m_selectIndex = previewAvatarSMRs.IndexOf(m_selectObjectsList.ElementAt(m_selectObjectsList.IndexOf(previewAvatarSMRs.ElementAt(m_selectIndex).gameObject) < m_selectObjectsList.Count -1 ? m_selectObjectsList.IndexOf(previewAvatarSMRs.ElementAt(m_selectIndex).gameObject) +1 : 0).GetComponent<SkinnedMeshRenderer>());
		    	Debug.Log(m_selectIndex);
		    	m_selectObject = previewAvatarSMRs.ElementAt(m_selectIndex).gameObject;
		    	Selection.activeTransform = m_selectObject.transform;
			    m_oldSelectObject = m_selectObject;
			    selection.SetCommandBuffer(previewAvatarSMRs.ElementAt(m_selectIndex));
			    OnShotRepaint();
		    	}
			    	break;
		    case KeyCode.H:	{
		    	if (evt.altKey)
		    	{
			    	previewAvatarSMRs.ForEach(o => o.gameObject.active = true);
		    	}
		    	else if (m_selectObject != null)
		    	{
			    	m_selectObject.active = !m_selectObject.active;
		    	}
			    OnShotRepaint();
		    }
			    break;
		    default:Debug.Log("KeyDown");
			    break;
		    }
	    });
	    //root.RegisterCallback<KeyUpEvent>(evt => {
		//    switch (evt.keyCode)
		//    {
		//    case KeyCode.LeftAlt: alt = false;
		//	    break;
		//    default:
		//    	break;
		//    }
	    //});
	    #endregion
	    
	    #region FolderPath
	    m_folderPath_TextField.AddManipulator(new AddPathWithDragAndDrop());
	    m_folderPathMenuItem.FolderPathTextField = m_folderPath_TextField;
	    
	    m_folderPath_TextField.OnValueChangedHandler += (string path) => SetDropDownText(path);
        
        resetFolderPathButton.clicked += () => {
        	m_folderPath_TextField.value = FolderPathUtility.SetFolderPath("");
        };
        
        getAnimationFolderButton.clicked += () => {
        	m_folderPath_TextField.value = FolderPathUtility.SetFolderPath(FolderPathUtility.GetFolderPath());
        };
        #endregion
        #region ButtonEvents
	    resetAnimationNameButton.clicked += () => {
	    	newAnimationName.value = "";
	    };
	    
	    saveNewAnimationClipButton.clicked += () => {
	    	var newClip = AnimationClipsUtility.SaveNewClip(newAnimationName.value, m_folderPath_TextField.value);
	    	if (newClip == null) return;
	    	if (clips.Contains(newClip)) return;
	    	clips.Add(newClip);
	    	clipsDropdown.Popupvalues.Add($"Create/{newClip.name}.anim");
	    };
	    
	    saveNewAnimationClipDropdown.RegisterValueChangedCallback(evt => {
	    	if (evt.newValue == null) return;
	    	Debug.Log(evt.newValue);
	    	switch (saveNewAnimationClipDropdown.index)
	    	{
	    	case 0: var newClip = AnimationClipsUtility.SaveNewClip(newAnimationName.value, m_folderPath_TextField.value);
		    	if (newClip == null) break;
		    	if (clips.Contains(newClip)) break;
		    	clips.Add(newClip);
		    	clipsDropdown.Popupvalues.Add($"Create/{newClip.name}.anim");
		    	break;
	    	case 1: newClip = new AnimationClip();
		    	AnimationClipsUtility.CreateAnimationClip(newClip, m_objectIsActiveList);
		    	newClip = AnimationClipsUtility.SaveNewClip(newAnimationName.value, m_folderPath_TextField.value, newClip);
		    	if (newClip == null) break;
		    	if (clips.Contains(newClip)) break;
		    	clips.Add(newClip);
		    	clipsDropdown.Popupvalues.Add($"Create/{newClip.name}.anim");
		    	break;
	    	case 2:
		    	break;
		    	
	    	default:
	    		break;
	    	}
	    	saveNewAnimationClipDropdown.index = -1;
	    });
        
        getVisualExpressionEditorButton.clicked += () => {
        	VisualExpressionsEditorWindow.ShowWindow();
        	var window = GetWindow<VisualExpressionsEditorWindow>();
        	window.ChangeAnimator((Animator)EditorSceneManager.GetActiveScene().GetRootGameObjects().Where(o => o.GetComponent<Animator>()).First().GetComponent<Animator>());
        	Debug.Log(clipsDropdown.index);
        	var clip = clipsDropdown.index == 0 ? null : clips.ElementAt(clipsDropdown.index - 1);
        	window.ChangeClip(clip);
        };
        #endregion
	    //RenderPreviewの作成
	    m_previewScene.Render();
	    render.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(m_previewScene.RenderTexture));
        
	    //DefaultAnimationClipの作成
	    AnimationClip defaultclip = new AnimationClip();
	    //CreateDefaultAnimationClip(defaultclip);
	    AnimationClipsUtility.CreateAnimationClip(defaultclip,previewAvatarSMRs);
	    
	    //DropDownの更新
	    var clipname = "";
	    clipsDropdown.RegisterValueChangedCallback(newclipname => SetClipsDropDown(defaultclip, newclipname.newValue));
	    
	    #region RenderEvent
	    Rect rect = new Rect();
	    render.RegisterCallback<GeometryChangedEvent>(evt => {
	    	rect = render.contentRect;
		    rect = new Rect(0,0,rect.width,rect.height);
	    });
	    
	    render.RegisterCallback<MouseDownEvent>(evt => {
	    	//右クリック
	    	if (evt.button == 1)
	    	{
	    		GenericMenu menu = new GenericMenu();
	    		menu.AddItem(new GUIContent("Refresh"), false, () => {
	    			m_didInitialize = false;
	    			InitializeIfNeeded();
	    			m_previewScene.Render();
		    		render.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(m_previewScene.RenderTexture));
	    			SetPreviewAvatar();
	    		});
	    		menu.ShowAsContext();
	    	}
	    	//左クリック
	    	else if (evt.button == 0)
	    	{
	    		m_selectObjectsList.Clear();
	    		var mousePos = evt.mousePosition;
	    		//mousePos.y = rect.height - mousePos.y;
	    		//Vector2 ratio = new Vector2(rect.width / m_previewScene.Camera.pixelWidth, rect.height / m_previewScene.Camera.pixelHeight);
	    		//mousePos.x /= ratio.x;
	    		//mousePos.y /= ratio.y;
	    		
	    		mousePos = mousePos - rect.min;
	    		mousePos.y = rect.height - mousePos.y + m_previewScene.Camera.fieldOfView;
	    		mousePos *= new Vector2(m_previewScene.Camera.pixelWidth, m_previewScene.Camera.pixelHeight) / rect.size;
	    		//mousePos.y = m_previewScene.Camera.pixelHeight - mousePos.y;
	    		Debug.Log(rect.min+","+rect.height);
	    		
	    		var ray = m_previewScene.Camera.ScreenPointToRay(mousePos);
	    		var screneXRatio = m_previewScene.Camera.pixelWidth;
	    		//Debug.Log(ratio.x);
	    		ray.origin = new Vector3(ray.origin.x, ray.origin.y, ray.origin.z);
	    		ray.direction = new Vector3(ray.direction.x, ray.direction.y, ray.direction.z);
	    		Debug.DrawRay(ray.origin,ray.direction,Color.white,10f);
	    		//var hit = new RaycastHit();
	    		GameObject hit;
	    		Vector3 oldvert = Vector3.zero;
	    		Vector3 vert;
	    		
	    		//var physicsScene = PhysicsSceneExtensions.GetPhysicsScene(m_previewScene.Scene);
		    	//if (physicsScene.Raycast(ray.origin, ray.direction,out hit,Mathf.Infinity))
		    	//{
			    	//m_selectIndex = cloneAvatarSMRs.IndexOf(hit.transform.GetComponent<SkinnedMeshRenderer>());
			    	//m_selectIndex = previewAvatarSMRs.IndexOf(hit.transform.GetComponent<SkinnedMeshRenderer>());
			    	//m_selectObject = hit.gameObject;
			    	//Selection.activeTransform = m_selectObject.transform;
			    	//selection.SetCommandBuffer(previewAvatarSMRs.ElementAt(m_selectIndex));
			    	//OnShotRepaint();
			    	//return;
		    	//}
	    		foreach (var item in previewAvatarSMRs)
	    		{
	    			if (!item.gameObject.active || item.gameObject == m_oldSelectObject) continue;
		    		var deformMeshRayCast = new DeformeMeshRayCast(item.gameObject);
		    		
		    		if (deformMeshRayCast.GetRayCast(ray, out hit, out vert))
		    		{
		    			m_selectObjectsList.Add(item.gameObject);
		    			m_selectObject = oldvert == Vector3.zero ? hit : Vector3.Distance(ray.origin,oldvert) <= Vector3.Distance(ray.origin,vert) ? m_selectObject : hit;
			    		//Debug.Log(hit.transform.name+" : "+vert+" * "+Vector3.Distance(ray.origin,vert)+" = "+m_selectObject);
			    		oldvert = Vector3.Distance(ray.origin,oldvert) <= Vector3.Distance(ray.origin,vert) ? oldvert : vert;
			    		Selection.activeTransform = m_selectObject.transform;
		    		}
	    		}
		    	m_selectIndex = m_selectObject == null ? -1 : previewAvatarSMRs.IndexOf(m_selectObject.GetComponent<SkinnedMeshRenderer>());
	    		m_oldSelectObject = m_selectObject;
		    	selection.SetCommandBuffer(previewAvatarSMRs.ElementAtOrDefault(m_selectIndex));
	    		OnShotRepaint();
	    	}
	    });
	    
	    render.RegisterCallback<WheelEvent>(evt =>{
	    	//Zoom from Distance
	    	//m_previewScene.Camera.nearClipPlane = 0.001f;
	    	//var delta =  Mathf.Pow(1.1f,Vector3.Distance(m_previewScene.Camera.transform.position,m_previewScene.Avatar.transform.position)/20)-1;
	    	
	    	//Zoom from FOV
	    	m_previewScene.Camera.fieldOfView += 0.01f * evt.delta.y * m_previewScene.Camera.fieldOfView;
	    	m_previewScene.Render();
	    	Repaint();
	    	//OnShotRepaint();
	    });
	    
	    render.AddManipulator(new RenderDragAndDropManipulator(this, clipsDropdown, typeof(AnimationClip)));
	    #endregion
	    
	    playButton.RegisterCallback<ChangeEvent<bool>>(x => {
	    	OnPlayerUpdate(x.newValue);
	    });
	    
	    playSlider.RegisterValueChangedCallback(x => {
		    var time = x.newValue;
		    Debug.Log(time);
		    graph.Evaluate(time);
		    m_previewScene.Render();
		    Repaint();
	    });
	    
    }
#endregion    
    
#region Methods
	#region DropDownFieldMethods
	private void SetDropDownText(string path){
		clipsDropdown.Popupvalues.Clear();
		clips.Clear();
		clipsDropdown.Popupvalues.Add("-select-");
		if (string.IsNullOrEmpty(path)) return;
        	
		clips = AnimationClipsUtility.GetClips(path);
		foreach (var clip in clips) clipsDropdown.Popupvalues.Add(AssetDatabase.GetAssetPath(clip).Replace($"{m_folderPath_TextField.text}/",""));
	}
	
	private void CreateDefaultAnimationClip(AnimationClip defaultclip){
		//AnimationClip defaultclip = new AnimationClip();
		var KeyValue = 0f;
		var Key = new Keyframe(0f,KeyValue);
		var Curve = new AnimationCurve(Key);
		foreach (var SMR in m_previewScene.Avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true))
		{
			//SkinnedMeshRendererからのアニメーションパス
			var SMRPath = new StringBuilder(SMR.transform.name);
			var current = SMR.transform.parent;
			while (current != null)
			{
				if (current.TryGetComponent(out Animator animator)) break;
				SMRPath.Insert(0, $"{current.name}/");
				current = current.parent;
			}
        	
			//GameObjectアニメーション
			KeyValue = Convert.ToInt16(SMR.gameObject.active);
			Key.value = KeyValue;
			Curve.ClearKeys();
			Curve.AddKey(Key);
			defaultclip.SetCurve(SMRPath.ToString(),typeof(GameObject),$"m_IsActive",Curve);
        	
			//BlendShapeアニメーション
			for (int i = 0; i < SMR.sharedMesh.blendShapeCount; i++) 
			{
				KeyValue = SMR.GetBlendShapeWeight(i);
				Key.value = KeyValue;
				Curve.ClearKeys();
				Curve.AddKey(Key);
				defaultclip.SetCurve(SMRPath.ToString(),typeof(SkinnedMeshRenderer),$"blendShape.{SMR.sharedMesh.GetBlendShapeName(i)}",Curve);
			}
		}
		
		//Clip保存(テスト用)
		//AssetDatabase.CreateAsset(defaultclip,"Assets/defaultanim.anim");
	}
	
	//アニメーション切り替え
	private void SetClipsDropDown(AnimationClip defaultclip, string newclipname){
		if (graph.IsValid())
		{
			graph.Destroy();	
		}
		if (defaultGraph.IsValid() && defaultHumanoidGraph.IsValid())
		{
			defaultGraph.Destroy();
			defaultHumanoidGraph.Destroy();
		}

		AnimationPlayableUtilities.PlayClip(m_previewScene.Avatar.GetComponent<Animator>(),defaultclip,out defaultGraph);
		clipPlayable = AnimationPlayableUtilities.PlayClip(m_previewScene.Avatar.GetComponent<Animator>(),m_t_pose,out defaultHumanoidGraph);
		
		if(clipsDropdown.index == 0)return;
		
		//削除したClipファイルにアクセスした時のダイアログ
		if (!AssetDatabase.Contains(clips.ElementAt(clipsDropdown.index - 1)))
		{
			EditorUtility.DisplayDialog($"Missing",$"{Path.GetFileName(clipsDropdown.value)} is Missing","OK");
			clips.RemoveAt(clipsDropdown.index - 1);
			clipsDropdown.Popupvalues.RemoveAt(clipsDropdown.index);
			clipsDropdown.index = 0;
			return;
		}
        	
		//var clipPlayable = AnimationClipPlayable.Create(graph,clips.ElementAt(clipsDropdown.index - 1));
		//var playableOutput = AnimationPlayableOutput.Create(graph,"Animation",m_previewScene.Avatar.GetComponent<Animator>());
		//playableOutput.SetSourcePlayable(clipPlayable);
		//graph.Play();
        	
		clipPlayable = AnimationPlayableUtilities.PlayClip(m_previewScene.Avatar.GetComponent<Animator>(),clips.ElementAt(clipsDropdown.index - 1),out graph);
		playSlider.highValue = clips.ElementAt(clipsDropdown.index - 1).length;
		    
		playButton.value = true;
	}
	#endregion
	#region RenderMethods
	private void OnPlayerUpdate(bool value){
		if (value)
		{
			rootVisualElement.Q<ToggleinButton>().Q<Button>().style.backgroundImage = (Texture2D)EditorGUIUtility.Load("PauseButton");
			rootVisualElement.Q<ToggleinButton>().tooltip = "Stop";
			defaultGraph.Play();
			defaultHumanoidGraph.Play();
			graph.Play();
			m_playing = true;
			EditorApplication.update += OnUpdate;
		}
		else
		{
			rootVisualElement.Q<ToggleinButton>().Q<Button>().style.backgroundImage = (Texture2D)EditorGUIUtility.Load("Animation Icon");
			rootVisualElement.Q<ToggleinButton>().tooltip = "Play";
			if (graph.IsValid())
			{
				graph.Stop();	
			}
			defaultGraph.Stop();
			defaultHumanoidGraph.Stop();
			EditorApplication.update -= OnUpdate;
			EditorApplication.update += OnShotRepaint;
		}
		Debug.Log(value);
	}
	
	private void OnUpdate(){
		m_previewScene.Render();
		Repaint();
		Mathf.Repeat(rootVisualElement.Q<Slider>("PlaySlider").value,rootVisualElement.Q<Slider>("PlaySlider").highValue);
	}
	
	private void OnShotRepaint(){
		defaultGraph.Play();
		defaultHumanoidGraph.Play();
		m_previewScene.Render();
		Repaint();
		if (m_playing) {m_playing = false;return;}
		defaultGraph.Stop();
		defaultHumanoidGraph.Stop();
		RepaintRenderBorder();
		Debug.Log("ShotRepaint");
		EditorApplication.update -= OnShotRepaint;
	}
	
	private void RepaintRenderBorder(){
		m_objectIsActiveList = defaultPreviewAvatarObjectsIsActiveList.Where(o => previewAvatarSMRs.Single(p => p.gameObject == o.Key).gameObject.active != o.Value).Select(o => o.Key).ToList();
		var render = rootVisualElement.Q<VisualElement>("Render");
		if (m_objectIsActiveList.Count == 0)
		{
			render.style.borderTopColor = Color.clear;
			render.style.borderBottomColor = Color.clear;
			render.style.borderLeftColor = Color.clear;
			render.style.borderRightColor = Color.clear;
		}
		else
		{
			render.style.borderTopColor = Color.red;
			render.style.borderBottomColor = Color.red;
			render.style.borderLeftColor = Color.red;
			render.style.borderRightColor = Color.red;
		}
	}
    #endregion
    
	void InitializeIfNeeded()
	{
		Debug.Log($"befor Initilize + {m_didInitialize}");
		if (m_didInitialize) return;
		if (m_previewScene != null)
		{
			m_previewScene.Dispose();
			Debug.Log("Dispose");
		}
		m_previewScene = new PreviewScene(EditorSceneManager.GetActiveScene().path);
		selection = new SelectObjectOutline(m_previewScene.Camera);
		m_didInitialize = true;
		Debug.Log("Initialaize");
	}
	
	async static void DelayRepaint(VisualElement ve)
	{
		await Task.Delay(10);
		ve.MarkDirtyRepaint();
	}
#endregion
}
}