using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#if VRC
using VRC.SDK3.Avatars.Components;
#endif

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// モードの提供元。MA のようなオプション依存は<b>別アセンブリ</b>に置いて
	/// ここへ自己登録する（Docs/FXCreator-Design.md R6「MA 依存は1ファイルに閉じる」）。
	///
	/// asmdef に MA への参照を直接書くと、MA が入っていないプロジェクトで
	/// 「存在しないアセンブリへの参照」になって<b>FX Creator 本体がコンパイルできなくなる</b>。
	/// <c>defineConstraints</c> で丸ごと落ちる別アセンブリからの登録にすれば、
	/// 本体は MA を知らないままでいられる。
	/// </summary>
	public interface IFxTargetProvider
	{
		/// <summary>セレクタに並べる順。小さいほど先（Direct = 0）。</summary>
		int Order { get; }

		/// <summary>
		/// このアバター用のターゲットを作る。使えない状況でも
		/// <c>IsAvailable</c> が理由を返せるインスタンスを返すこと
		/// （UI は「選べない理由」を出す必要がある）。
		/// </summary>
		IFxTarget Create(GameObject avatar);
	}

	/// <summary>登録されたモードの一覧。</summary>
	public static class FxTargetProviders
	{
		private static readonly List<IFxTargetProvider> _providers = new List<IFxTargetProvider>();

		public static void Register(IFxTargetProvider provider)
		{
			if (provider == null)
			{
				return;
			}
			// ドメインリロードをまたいで二重登録されないよう、型で重複を弾く。
			for (int i = 0; i < _providers.Count; i++)
			{
				if (_providers[i].GetType() == provider.GetType())
				{
					_providers[i] = provider;
					return;
				}
			}
			_providers.Add(provider);
			_providers.Sort((a, b) => a.Order.CompareTo(b.Order));
		}

		public static IReadOnlyList<IFxTargetProvider> All
		{
			get { return _providers; }
		}

		/// <summary>Direct は本体が持っているので、起動時に自分で入れておく。</summary>
		[InitializeOnLoadMethod]
		private static void RegisterBuiltIn()
		{
			Register(new DirectAvatarTarget.Provider());
		}
	}

	/// <summary>
	/// シーンからアバター候補を列挙し、各モードのターゲットを作る（§2.1）。
	/// </summary>
	public static class FxTargetResolver
	{
		/// <summary>最後に使ったモード。アバターを跨いだときの既定値にする。</summary>
		private const string PreferredModeKey = "colloid.FXCreator.TargetMode";

		public static string PreferredMode
		{
			get { return EditorPrefs.GetString(PreferredModeKey, DirectAvatarTarget.ModeId); }
			set { EditorPrefs.SetString(PreferredModeKey, value ?? DirectAvatarTarget.ModeId); }
		}

		/// <summary>
		/// 開いているシーンのアバター候補。VRC アバターを先に、
		/// そうでない Animator 持ちを後ろに並べる。
		/// </summary>
		public static List<GameObject> FindAvatars()
		{
			var vrc = new List<GameObject>();
			var others = new List<GameObject>();

			for (int s = 0; s < SceneManager.sceneCount; s++)
			{
				Scene scene = SceneManager.GetSceneAt(s);
				if (!scene.IsValid() || !scene.isLoaded)
				{
					continue;
				}
				GameObject[] roots = scene.GetRootGameObjects();
				for (int i = 0; i < roots.Length; i++)
				{
					Collect(roots[i], vrc, others);
				}
			}

			// プレハブモードで開いている中身もアバターとして扱える。
			PrefabStage stage = PrefabStageUtility.GetCurrentPrefabStage();
			if (stage != null && stage.prefabContentsRoot != null)
			{
				Collect(stage.prefabContentsRoot, vrc, others);
			}

			vrc.AddRange(others);
			return vrc;
		}

		private static void Collect(GameObject root, List<GameObject> vrc, List<GameObject> others)
		{
			if (root == null)
			{
				return;
			}

#if VRC
			// アバターは入れ子にならない前提にせず、子孫まで見る
			// （シーンに複数体まとめて置いてある作業風景は珍しくない）。
			VRCAvatarDescriptor[] descriptors = root.GetComponentsInChildren<VRCAvatarDescriptor>(true);
			for (int i = 0; i < descriptors.Length; i++)
			{
				if (descriptors[i] != null && !vrc.Contains(descriptors[i].gameObject))
				{
					vrc.Add(descriptors[i].gameObject);
				}
			}
#endif

			var animators = root.GetComponentsInChildren<UnityEngine.Animator>(true);
			for (int i = 0; i < animators.Length; i++)
			{
				GameObject go = animators[i] != null ? animators[i].gameObject : null;
				if (go == null || vrc.Contains(go) || others.Contains(go))
				{
					continue;
				}
				// アバター直下の付属 Animator（顔の小物など）まで並べると邪魔なので、
				// ルート扱いできるものだけ拾う。
				if (animators[i].avatar != null || go.transform.parent == null)
				{
					others.Add(go);
				}
			}
		}

		/// <summary>
		/// アバターに対する全モードのターゲット。必ず1つ以上返る（Direct は常に作れる）。
		/// </summary>
		public static List<IFxTarget> CreateTargets(GameObject avatar)
		{
			var result = new List<IFxTarget>();
			IReadOnlyList<IFxTargetProvider> providers = FxTargetProviders.All;
			for (int i = 0; i < providers.Count; i++)
			{
				IFxTarget target = null;
				try
				{
					target = providers[i].Create(avatar);
				}
				catch (Exception e)
				{
					// 片方のモードが壊れても、もう片方でウィンドウが開けることを優先する。
					Debug.LogException(e);
				}
				if (target != null)
				{
					result.Add(target);
				}
			}
			return result;
		}

		/// <summary>
		/// 既定のモードを選ぶ。
		/// <list type="number">
		/// <item>アバターに<b>すでに支度ができている</b>モード（シーンの実体が根拠）</item>
		/// <item>前回使ったモード</item>
		/// <item>使える最初のモード</item>
		/// </list>
		/// の順。設定より実体を優先するのは、MA で組んだアバターを開いたときに
		/// Direct が選ばれて<b>別の Controller を編集し始めてしまう</b>のを防ぐため。
		/// </summary>
		public static IFxTarget ChooseDefault(IList<IFxTarget> targets, string preferredId)
		{
			if (targets == null || targets.Count == 0)
			{
				return null;
			}

			string ignored;
			for (int i = 0; i < targets.Count; i++)
			{
				if (targets[i].IsAvailable(out ignored) && targets[i].IsAlreadySetUp)
				{
					return targets[i];
				}
			}

			if (!string.IsNullOrEmpty(preferredId))
			{
				for (int i = 0; i < targets.Count; i++)
				{
					if (targets[i].Id == preferredId && targets[i].IsAvailable(out ignored))
					{
						return targets[i];
					}
				}
			}

			for (int i = 0; i < targets.Count; i++)
			{
				if (targets[i].IsAvailable(out ignored))
				{
					return targets[i];
				}
			}
			return targets[0];
		}
	}
}
