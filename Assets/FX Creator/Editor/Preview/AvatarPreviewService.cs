using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace colloid.FXCreator.Preview
{
	/// <summary>
	/// ノード内プレビューの描画元（Docs/FXCreator-Design.md §5.2）。
	///
	/// <b>1アバター / 1プレビューシーン / 1カメラを全ノードで共有する。</b>
	/// 試作（§5.1）はノードごとに <c>PreviewRenderUtility</c> とアバターの複製を
	/// 作っていて、ノード20個で RenderTexture・カメラ・シーンが20セットできていた。
	/// ここではアバターの複製は1体だけで、ノードは「この clip のこの時刻が欲しい」と
	/// 要求を出すだけ。
	///
	/// 負荷の配り方:
	/// <list type="bullet">
	/// <item>要求はキューに積み、<c>EditorApplication.update</c> で<b>1フレーム最大2件</b>処理する</item>
	/// <item>キューが空なら何もしない（アイドル時の GPU 負荷ゼロ）</item>
	/// <item>結果は <see cref="PreviewCache"/>（LRU）に入れ、同じ要求は描き直さない</item>
	/// <item><c>AnimationMode</c> の開始・終了は<b>サービスが</b>行う。ノードは触らない
	/// （試作はノードごとに <c>StartAnimationMode</c> を呼んでいて干渉していた）</item>
	/// </list>
	/// </summary>
	public sealed class AvatarPreviewService : IDisposable
	{
		/// <summary>1フレームに処理する最大件数（§5.2）。</summary>
		private const int PerFrameBudget = 2;

		/// <summary>
		/// 1フレームに使ってよい時間（ms）。件数だけで区切ると、重いアバターや
		/// 初回描画（シェーダの準備で実測 10.8ms）で 16ms を超える。
		/// 1件処理するたびに見て、超えていたら残りは次フレームへ回す。
		/// </summary>
		private const double PerFrameMillisecondBudget = 8.0;

		private static AvatarPreviewService _current;

		/// <summary>
		/// アバターに紐づくサービスを返す。アバターが変わったら前のものは畳む。
		/// 同時に必要なアバターは1体なので、共有は1つで足りる。
		/// </summary>
		public static AvatarPreviewService ForAvatar(GameObject avatarRoot)
		{
			if (avatarRoot == null)
			{
				Shutdown();
				return null;
			}

			if (_current != null && _current._source == avatarRoot && _current.IsUsable)
			{
				return _current;
			}

			Shutdown();
			var service = new AvatarPreviewService(avatarRoot);
			if (!service.IsUsable)
			{
				service.Dispose();
				return null;
			}
			_current = service;
			return _current;
		}

		public static void Shutdown()
		{
			if (_current == null)
			{
				return;
			}
			_current.Dispose();
			_current = null;
		}

		private readonly GameObject _source;
		private readonly PreviewCache _cache = new PreviewCache();
		private readonly List<PreviewRequest> _queue = new List<PreviewRequest>();

		private Scene _scene;
		private GameObject _instance;
		private Camera _camera;
		private UnityEngine.Animator _animator;
		private bool _sceneCreated;
		private bool _animationModeOwned;
		private bool _disposed;
		private bool _updateHooked;

		/// <summary>プレビューを出せる状態か。アバターが人型でないなどの場合 false。</summary>
		public bool IsUsable { get; private set; }

		/// <summary>使えない理由（UI に出す）。使えるなら null。</summary>
		public string UnusableReason { get; private set; }

		public PreviewCache Cache => _cache;

		/// <summary>まだ描いていない要求の数（ゲートの確認用）。</summary>
		public int PendingCount => _queue.Count;

		private AvatarPreviewService(GameObject avatarRoot)
		{
			_source = avatarRoot;
			try
			{
				Build(avatarRoot);
				IsUsable = true;
			}
			catch (Exception e)
			{
				// プレビューが作れなくてもグラフ編集は続けられる（§5.3）。
				// 例外を外へ出さず、理由だけ持って無効化する。
				IsUsable = false;
				UnusableReason = e.Message;
			}
		}

		#region Setup

		private void Build(GameObject avatarRoot)
		{
			_scene = EditorSceneManager.NewPreviewScene();
			_sceneCreated = true;

			_instance = UnityEngine.Object.Instantiate(avatarRoot);
			_instance.name = "FXC Preview Avatar";
			_instance.hideFlags = HideFlags.HideAndDontSave;
			SceneManager.MoveGameObjectToScene(_instance, _scene);
			_instance.transform.position = Vector3.zero;
			_instance.transform.rotation = Quaternion.identity;
			_instance.SetActive(true);

			// 複製したアバターが実行時スクリプトで動き出さないよう、
			// Animator は切っておく（姿勢はこちらがサンプリングで決める）。
			_animator = _instance.GetComponentInChildren<UnityEngine.Animator>();
			if (_animator == null)
			{
				throw new InvalidOperationException("アバターに Animator がありません");
			}
			_animator.enabled = false;

			var cameraGo = new GameObject("FXC Preview Camera", typeof(Camera))
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			SceneManager.MoveGameObjectToScene(cameraGo, _scene);
			_camera = cameraGo.GetComponent<Camera>();
			_camera.cameraType = CameraType.Preview;
			_camera.scene = _scene;
			_camera.enabled = false;                 // Game View に影響させない
			_camera.forceIntoRenderTexture = true;
			_camera.clearFlags = CameraClearFlags.SolidColor;
			_camera.backgroundColor = new Color(0.16f, 0.16f, 0.18f, 1f);
			_camera.nearClipPlane = 0.01f;
			_camera.farClipPlane = 30f;

			var lightGo = new GameObject("FXC Preview Light", typeof(Light))
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			SceneManager.MoveGameObjectToScene(lightGo, _scene);
			lightGo.transform.rotation = Quaternion.Euler(35f, -35f, 0f);
			Light light = lightGo.GetComponent<Light>();
			light.type = LightType.Directional;
			light.intensity = 1.1f;

			WarmUp();
		}

		/// <summary>
		/// 一度だけ捨て描きして、アバターのメッシュとシェーダを GPU に載せておく。
		/// 実測で初回の <c>Camera.Render</c> は約 200ms かかり、これは分割できない。
		/// スクロール中に来ると目に見えて引っかかるので、アバターを選んだ時点
		/// （＝ここ）で払ってしまう。以降の描画は 2ms 前後で収まる。
		/// </summary>
		private void WarmUp()
		{
			PlaceCamera(PreviewFraming.Default);
			var warm = new RenderTexture(32, 32, 24, GraphicsFormat.R8G8B8A8_UNorm)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			_camera.targetTexture = warm;
			bool previous = Unsupported.useScriptableRenderPipeline;
			Unsupported.useScriptableRenderPipeline = GraphicsSettings.currentRenderPipeline != null;
			try
			{
				_camera.Render();
			}
			finally
			{
				Unsupported.useScriptableRenderPipeline = previous;
				_camera.targetTexture = null;
				warm.Release();
				UnityEngine.Object.DestroyImmediate(warm);
			}
		}

		#endregion

		#region Requests

		/// <summary>
		/// プレビューを要求する。キャッシュに当たれば<b>その場で</b>コールバックし、
		/// 無ければキューに積む。同じ要求元・同じキーの重複要求は積み直さない。
		/// </summary>
		public void Request(PreviewRequest request)
		{
			if (_disposed || !IsUsable || request.Clip == null || request.OnRendered == null)
			{
				return;
			}

			PreviewKey key = request.Key;
			RenderTexture cached;
			if (_cache.TryGet(key, out cached))
			{
				request.OnRendered(cached);
				return;
			}

			for (int i = 0; i < _queue.Count; i++)
			{
				if (_queue[i].Owner == request.Owner && _queue[i].Key.Equals(key))
				{
					return;
				}
			}

			_queue.Add(request);
			HookUpdate();
		}

		/// <summary>
		/// この要求元の未処理ぶんを取り下げる。ノードが画面外へ出た（カリングされた）
		/// ときに呼ぶので、スクロールしても要求が溜まり続けない。
		/// </summary>
		public void CancelAll(object owner)
		{
			if (owner == null)
			{
				return;
			}
			for (int i = _queue.Count - 1; i >= 0; i--)
			{
				if (_queue[i].Owner == owner)
				{
					_queue.RemoveAt(i);
				}
			}
		}

		private void HookUpdate()
		{
			if (_updateHooked || _disposed)
			{
				return;
			}
			_updateHooked = true;
			EditorApplication.update += OnEditorUpdate;
		}

		private void UnhookUpdate()
		{
			if (!_updateHooked)
			{
				return;
			}
			_updateHooked = false;
			EditorApplication.update -= OnEditorUpdate;
		}

		/// <summary>
		/// キューを少しずつ捌く。空になったら update から外れ、
		/// <c>AnimationMode</c> も抜ける（アイドル時に何も抱えない）。
		/// </summary>
		private void OnEditorUpdate()
		{
			if (_disposed)
			{
				return;
			}

			// 再生は毎フレーム1コマ。静止プレビューより優先する（見ている当人だから）。
			AdvancePlayback();

			if (_queue.Count == 0)
			{
				if (_playingOwner == null)
				{
					StopAnimationMode();
					UnhookUpdate();
				}
				return;
			}

			// 件数と時間の両方で区切る。どちらかに達したら残りは次フレームへ。
			System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
			int processed = 0;
			while (_queue.Count > 0 && processed < PerFrameBudget)
			{
				PreviewRequest request = _queue[0];
				_queue.RemoveAt(0);
				processed++;
				Process(request);

				if (clock.Elapsed.TotalMilliseconds >= PerFrameMillisecondBudget)
				{
					break;
				}
			}
		}

		private void Process(PreviewRequest request)
		{
			if (request.Clip == null || request.OnRendered == null)
			{
				return;
			}

			PreviewKey key = request.Key;
			RenderTexture cached;
			if (_cache.TryGet(key, out cached))
			{
				request.OnRendered(cached);
				return;
			}

			RenderTexture texture = Render(request);
			if (texture == null)
			{
				return;
			}

			_cache.Add(key, texture);
			request.OnRendered(texture);
		}

		#endregion

		#region Playback

		/// <summary>
		/// 再生中のノード。<b>1つだけ</b>（§5.2-5）。他のノードは静止フレームのまま。
		/// </summary>
		private object _playingOwner;
		private AnimationClip _playingClip;
		private PreviewFraming _playingFraming;
		private Action<Texture> _playingOnFrame;
		private RenderTexture _playingTexture;
		private float _playingTime;
		private double _lastTick;

		public bool IsPlaying(object owner)
		{
			return owner != null && _playingOwner == owner;
		}

		/// <summary>
		/// このノードの再生を始める。既に別のノードが再生していれば、そちらは止まる。
		/// 再生フレームは<b>キャッシュに入れない</b>。時刻が毎フレーム変わる＝毎回別のキーになり、
		/// LRU が静止プレビューを丸ごと押し出してしまうため、専用のテクスチャを使い回す。
		/// </summary>
		public void StartPlaying(
			object owner, AnimationClip clip, Vector2Int size, PreviewFraming framing, Action<Texture> onFrame)
		{
			if (_disposed || !IsUsable || owner == null || clip == null || onFrame == null)
			{
				return;
			}
			if (clip.length <= 0f)
			{
				return;
			}

			if (_playingOwner != owner)
			{
				ReleasePlayingTexture();
				_playingTime = 0f;
			}

			_playingOwner = owner;
			_playingClip = clip;
			_playingFraming = framing;
			_playingOnFrame = onFrame;
			_lastTick = EditorApplication.timeSinceStartup;

			int width = Mathf.Max(8, size.x);
			int height = Mathf.Max(8, size.y);
			if (_playingTexture == null || _playingTexture.width != width || _playingTexture.height != height)
			{
				ReleasePlayingTexture();
				_playingTexture = new RenderTexture(width, height, 24, GraphicsFormat.R8G8B8A8_UNorm)
				{
					hideFlags = HideFlags.HideAndDontSave,
					name = "FXC Preview (playing)"
				};
			}

			HookUpdate();
		}

		public void StopPlaying(object owner)
		{
			if (owner == null || _playingOwner != owner)
			{
				return;
			}
			_playingOwner = null;
			_playingClip = null;
			_playingOnFrame = null;
			ReleasePlayingTexture();
		}

		private void ReleasePlayingTexture()
		{
			if (_playingTexture == null)
			{
				return;
			}
			_playingTexture.Release();
			UnityEngine.Object.DestroyImmediate(_playingTexture);
			_playingTexture = null;
		}

		/// <summary>再生を1コマ進める。クリップ末尾で先頭へ戻る。</summary>
		private void AdvancePlayback()
		{
			if (_playingOwner == null || _playingClip == null || _playingTexture == null)
			{
				return;
			}

			double now = EditorApplication.timeSinceStartup;
			float delta = (float)(now - _lastTick);
			_lastTick = now;
			// ウィンドウが止まっていた間の巨大な delta で飛ばないように上限をかける。
			delta = Mathf.Clamp(delta, 0f, 0.1f);

			_playingTime += delta;
			if (_playingClip.length > 0f)
			{
				_playingTime %= _playingClip.length;
			}

			Sample(_playingClip, _playingTime);
			PlaceCamera(_playingFraming);
			RenderInto(_playingTexture);
			_playingOnFrame(_playingTexture);
		}

		#endregion

		#region Rendering

		private RenderTexture Render(PreviewRequest request)
		{
			if (_instance == null || _camera == null)
			{
				return null;
			}

			Sample(request.Clip, request.Time);
			PlaceCamera(new PreviewFraming { Focus = request.Focus, Fov = request.Fov });

			var texture = new RenderTexture(
				Mathf.Max(8, request.Size.x),
				Mathf.Max(8, request.Size.y),
				24,
				GraphicsFormat.R8G8B8A8_UNorm)
			{
				hideFlags = HideFlags.HideAndDontSave,
				name = "FXC Preview"
			};

			RenderInto(texture);
			return texture;
		}

		/// <summary>アバターをその時刻の姿勢にする。<c>AnimationMode</c> の出入りはここだけ。</summary>
		private void Sample(AnimationClip clip, float time)
		{
			StartAnimationMode();
			AnimationMode.BeginSampling();
			try
			{
				AnimationMode.SampleAnimationClip(_instance, clip, time);
			}
			finally
			{
				AnimationMode.EndSampling();
			}
		}

		private void RenderInto(RenderTexture texture)
		{
			_camera.targetTexture = texture;
			bool previous = Unsupported.useScriptableRenderPipeline;
			Unsupported.useScriptableRenderPipeline = GraphicsSettings.currentRenderPipeline != null;
			try
			{
				_camera.Render();
			}
			finally
			{
				Unsupported.useScriptableRenderPipeline = previous;
				_camera.targetTexture = null;
			}
		}

		/// <summary>
		/// 指定のボーンが画面に収まる位置にカメラを置く。
		/// アバターの正面（root の forward）側から見る。
		/// </summary>
		private void PlaceCamera(PreviewFraming framing)
		{
			Transform bone = null;
			if (_animator != null && _animator.isHuman)
			{
				bone = _animator.GetBoneTransform(framing.Focus);
			}

			Vector3 target = bone != null ? bone.position : _instance.transform.position + Vector3.up;
			float radius = FramingRadius(framing.Focus);

			float fov = Mathf.Clamp(framing.Fov, 5f, 120f);
			_camera.fieldOfView = fov;
			float distance = radius / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

			Vector3 forward = _instance.transform.forward;
			if (forward.sqrMagnitude < 0.001f)
			{
				forward = Vector3.forward;
			}

			_camera.transform.position = target + forward * distance;
			_camera.transform.rotation = Quaternion.LookRotation(target - _camera.transform.position, Vector3.up);
		}

		/// <summary>収めたい範囲の半径（m）。顔は寄りたいが全身は引きたい。</summary>
		private static float FramingRadius(HumanBodyBones focus)
		{
			switch (focus)
			{
				case HumanBodyBones.Head:
				case HumanBodyBones.Neck:
					return 0.16f;
				case HumanBodyBones.LeftHand:
				case HumanBodyBones.RightHand:
					return 0.14f;
				case HumanBodyBones.Chest:
				case HumanBodyBones.UpperChest:
				case HumanBodyBones.Spine:
					return 0.45f;
				default:
					return 0.8f;
			}
		}

		private void StartAnimationMode()
		{
			if (AnimationMode.InAnimationMode())
			{
				return;
			}
			AnimationMode.StartAnimationMode();
			_animationModeOwned = true;
		}

		private void StopAnimationMode()
		{
			if (!_animationModeOwned)
			{
				return;
			}
			_animationModeOwned = false;
			if (AnimationMode.InAnimationMode())
			{
				AnimationMode.StopAnimationMode();
			}
		}

		#endregion

		public void Dispose()
		{
			if (_disposed)
			{
				return;
			}
			_disposed = true;

			UnhookUpdate();
			_queue.Clear();
			_playingOwner = null;
			_playingClip = null;
			_playingOnFrame = null;
			ReleasePlayingTexture();
			StopAnimationMode();
			_cache.Dispose();

			if (_camera != null)
			{
				_camera.targetTexture = null;
			}
			if (_instance != null)
			{
				UnityEngine.Object.DestroyImmediate(_instance);
				_instance = null;
			}
			if (_sceneCreated)
			{
				EditorSceneManager.ClosePreviewScene(_scene);
				_sceneCreated = false;
			}
			_camera = null;
			_animator = null;

			if (_current == this)
			{
				_current = null;
			}
		}
	}
}
