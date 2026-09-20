// FXCreator.Ma.asmdef の defineConstraints が MA と VRC を要求するので、
// Unity 上ではこのガードは常に真になる。書いてあるのは、単一アセンブリとして
// まとめてコンパイルする検証（スクリプトゲート）では asmdef ごとの
// versionDefines が効かず、MA / VRC が未定義のまま読まれるため。
#if MA && VRC
using System;
using System.Collections.Generic;
using colloid.FXCreator.AnimatorGraph;
using colloid.FXCreator.Targeting;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace colloid.FXCreator.Targeting.Ma
{
	/// <summary>
	/// Modular Avatar 経由の非破壊モード（Docs/FXCreator-Design.md D1 / §2.3）。
	///
	/// FX Creator が<b>自分専用の</b> Controller を
	/// <c>Assets/FX Creator/Save/&lt;Avatar&gt;/FXC_FX.controller</c> に持ち、
	/// アバターには <c>ModularAvatarMergeAnimator</c> だけを置く。
	/// アバター同梱の FX Controller は最後まで触らない。
	///
	/// このファイル（とこのアセンブリ）が MA への依存を全部引き受ける（R6）。
	/// <c>FXCreator.Ma.asmdef</c> は <c>defineConstraints</c> で MA と VRC SDK が
	/// 揃っているときだけコンパイルされるので、MA が無いプロジェクトでは
	/// アセンブリごと存在しなくなる。本体は
	/// <see cref="FxTargetProviders"/> に何も登録されないだけで、普通に動く。
	/// </summary>
	public sealed class NdmfMaTarget : IFxTarget
	{
		public const string ModeId = "ndmf-ma";

		/// <summary>アバター直下に置く入れ物の名前。</summary>
		private const string ObjectName = "FX Creator";

		internal sealed class Provider : IFxTargetProvider
		{
			public int Order { get { return 10; } }

			public IFxTarget Create(GameObject avatar)
			{
				return new NdmfMaTarget(avatar);
			}
		}

		[InitializeOnLoadMethod]
		private static void Register()
		{
			FxTargetProviders.Register(new Provider());
		}

		private readonly GameObject _avatar;

		public NdmfMaTarget(GameObject avatar)
		{
			_avatar = avatar;
		}

		public string Id { get { return ModeId; } }
		public string DisplayName { get { return "NDMF (MA)"; } }

		public string Description
		{
			get { return "FX Creator 専用の Controller を Modular Avatar でビルド時にマージします（非破壊）"; }
		}

		public GameObject Avatar { get { return _avatar; } }

		public bool IsAvailable(out string reason)
		{
			if (_avatar == null)
			{
				reason = "アバターが選ばれていません";
				return false;
			}
			if (_avatar.GetComponent<VRCAvatarDescriptor>() == null)
			{
				// MergeAnimator は VRC アバターのビルドに乗って初めて意味を持つ。
				reason = "VRC Avatar Descriptor が無いアバターでは使えません";
				return false;
			}
			reason = null;
			return true;
		}

		public bool IsAlreadySetUp
		{
			get { return FindMergeAnimator() != null; }
		}

#region Resolve

		/// <summary>
		/// FX Creator が置いた MergeAnimator を探す。
		///
		/// 名前ではなく<b>実体</b>で見分ける（FX レイヤー向けで、かつ差している Controller が
		/// FX Creator の管理フォルダにある）。ユーザーがオブジェクトを改名しても見失わないし、
		/// 他のツールや手作業で置かれた MergeAnimator を自分のものと誤認しない。
		/// </summary>
		private ModularAvatarMergeAnimator FindMergeAnimator()
		{
			if (_avatar == null)
			{
				return null;
			}

			ModularAvatarMergeAnimator[] all =
				_avatar.GetComponentsInChildren<ModularAvatarMergeAnimator>(true);
			for (int i = 0; i < all.Length; i++)
			{
				ModularAvatarMergeAnimator merge = all[i];
				if (merge == null || merge.layerType != VRCAvatarDescriptor.AnimLayerType.FX)
				{
					continue;
				}
				var controller = merge.animator as AnimatorController;
				if (controller == null)
				{
					continue;
				}
				if (FxSavePaths.IsManaged(AssetDatabase.GetAssetPath(controller)))
				{
					return merge;
				}
			}
			return null;
		}

		public AnimatorController ResolveController()
		{
			ModularAvatarMergeAnimator merge = FindMergeAnimator();
			return merge != null ? merge.animator as AnimatorController : null;
		}

		public IFxParameterStore ResolveParameterStore()
		{
			// MA モードの同期設定は ModularAvatarParameters が持つ（§2.3 の表）。
			// アバターの VRCExpressionParameters を返すと、非破壊モードのつもりで
			// アバター同梱アセットを書き換えさせてしまう。
			ModularAvatarMergeAnimator merge = FindMergeAnimator();
			var component = merge != null
				? merge.GetComponent<ModularAvatarParameters>()
				: null;
			return new MaParameterStore(component);
		}

		public VRCExpressionsMenu ResolveMenu()
		{
			if (_avatar == null)
			{
				return null;
			}
			// メニューは v0.1 では表示のみ（§6.3 / R4）。こちらから作りはせず、
			// ユーザーが MenuInstaller を置いていればその中身を見せる。
			ModularAvatarMenuInstaller[] installers =
				_avatar.GetComponentsInChildren<ModularAvatarMenuInstaller>(true);
			for (int i = 0; i < installers.Length; i++)
			{
				if (installers[i] != null && installers[i].menuToAppend != null)
				{
					return installers[i].menuToAppend;
				}
			}
			return null;
		}

		#endregion

		#region Prepare

		public bool TryPrepareForEditing(bool interactive, out string reason)
		{
			reason = null;
			if (!IsAvailable(out reason))
			{
				return false;
			}

			ModularAvatarMergeAnimator merge = FindMergeAnimator();
			if (merge != null)
			{
				// null も IsWritable が理由付きで断るので、ここで分岐を足さない。
				var existing = merge.animator as AnimatorController;
				string writeReason;
				if (AcControllerAccess.IsWritable(existing, out writeReason))
				{
					return true;
				}
				reason = writeReason;
				return false;
			}

			if (interactive)
			{
				bool ok = EditorUtility.DisplayDialog(
					"FX Creator",
					"このアバターに FX Creator の Modular Avatar 設定を作成します。\n\n"
					+ "・" + FxSavePaths.AvatarFolder(_avatar) + "/FXC_FX.controller を作成\n"
					+ "・アバター直下に \"" + ObjectName + "\" を作り、MA Merge Animator と\n"
					+ "　MA Parameters を追加\n\n"
					+ "アバター同梱の FX Controller は変更しません。",
					"作成する",
					"キャンセル");
				if (!ok)
				{
					reason = "NDMF (MA) の設定が作成されていません";
					return false;
				}
			}

			return Setup(out reason);
		}

		private bool Setup(out string reason)
		{
			string folder = FxSavePaths.EnsureAvatarFolder(_avatar);
			string path = FxSavePaths.UniqueAssetPath(folder, "FXC_FX", ".controller");
			AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
			if (controller == null)
			{
				reason = "Controller を作成できませんでした（" + path + "）";
				return false;
			}

			var holder = new GameObject(ObjectName);
			Undo.RegisterCreatedObjectUndo(holder, "Create FX Creator (MA)");
			Undo.SetTransformParent(holder.transform, _avatar.transform, "Create FX Creator (MA)");
			holder.transform.localPosition = Vector3.zero;
			holder.transform.localRotation = Quaternion.identity;
			holder.transform.localScale = Vector3.one;

			var merge = Undo.AddComponent<ModularAvatarMergeAnimator>(holder);
			merge.animator = controller;
			merge.layerType = VRCAvatarDescriptor.AnimLayerType.FX;
			// FX Creator が作るクリップのパスは<b>アバタールート基準</b>なので Absolute。
			// 既定の Relative だと、このオブジェクトからの相対パスとして解釈されて
			// どのオブジェクトにも当たらなくなる。
			merge.pathMode = MergeAnimatorPathMode.Absolute;
			// WriteDefaults はアバター側の流儀に合わせる（混在は VRChat で事故のもと）。
			merge.matchAvatarWriteDefaults = true;

			Undo.AddComponent<ModularAvatarParameters>(holder);

			EditorUtility.SetDirty(holder);
			reason = null;
			return true;
		}

		#endregion

		#region After edit

		/// <summary>
		/// Controller のパラメータ定義を <c>ModularAvatarParameters</c> へ同期する（§2.3 の表）。
		///
		/// <b>足すだけで、既存の行は触らない。</b> 同期種別や saved はユーザーが
		/// 調整するところなので、こちらが編集のたびに上書きすると設定が戻らない。
		/// 使わなくなった行も残す（消すと、まだ書いていないレイヤーの分まで巻き添えになる）。
		/// </summary>
		public void OnAfterEdit(AcEditReport report)
		{
			if (report == null || report.Controller == null || !report.ParametersChanged)
			{
				return;
			}

			ModularAvatarMergeAnimator merge = FindMergeAnimator();
			if (merge == null || (object)merge.animator != report.Controller)
			{
				return;
			}

			// 改名・型変更は「足す」より先に流す。先に足すと、改名前の名前で
			// 1行、改名後の名前でもう1行という重複ができる（§6.2）。
			FxParameterSync.FollowControllerEdits(report, ResolveParameterStore());

			var holder = merge.gameObject;
			var parameters = holder.GetComponent<ModularAvatarParameters>();
			if (parameters == null)
			{
				parameters = Undo.AddComponent<ModularAvatarParameters>(holder);
			}

			var declared = new HashSet<string>(StringComparer.Ordinal);
			List<ParameterConfig> list = parameters.parameters != null
				? new List<ParameterConfig>(parameters.parameters)
				: new List<ParameterConfig>();
			for (int i = 0; i < list.Count; i++)
			{
				if (!string.IsNullOrEmpty(list[i].nameOrPrefix))
				{
					declared.Add(list[i].nameOrPrefix);
				}
			}

			bool changed = false;
			AnimatorControllerParameter[] source = report.Controller.parameters;
			for (int i = 0; source != null && i < source.Length; i++)
			{
				AnimatorControllerParameter p = source[i];
				if (p == null || string.IsNullOrEmpty(p.name) || declared.Contains(p.name))
				{
					continue;
				}
				// VRChat が供給するパラメータを宣言すると、使わない同期コストを取られる。
				if (VrcBuiltInParameters.Contains(p.name))
				{
					continue;
				}

				list.Add(NewConfig(p));
				declared.Add(p.name);
				changed = true;
			}

			if (!changed)
			{
				return;
			}

			Undo.RecordObject(parameters, "Sync MA Parameters");
			parameters.parameters = list;
			EditorUtility.SetDirty(parameters);
		}

		private static ParameterConfig NewConfig(AnimatorControllerParameter p)
		{
			var config = new ParameterConfig
			{
				nameOrPrefix = p.name,
				isPrefix = false,
				internalParameter = false,
				localOnly = false,
				// 新規行は同期・保存あり（VRC SDK が Expression Parameters に
				// 行を足すときと同じ既定）。要らないものはユーザーが外す。
				saved = true,
				syncType = SyncTypeFor(p.type),
				hasExplicitDefaultValue = true,
				defaultValue = DefaultValueFor(p),
			};
			return config;
		}

		private static ParameterSyncType SyncTypeFor(AnimatorControllerParameterType type)
		{
			switch (type)
			{
				case AnimatorControllerParameterType.Bool:
					return ParameterSyncType.Bool;
				case AnimatorControllerParameterType.Int:
					return ParameterSyncType.Int;
				case AnimatorControllerParameterType.Float:
					return ParameterSyncType.Float;
				default:
					// Trigger は VRChat の同期に対応する型が無い（Expression Parameters にも無い）。
					return ParameterSyncType.NotSynced;
			}
		}

		private static float DefaultValueFor(AnimatorControllerParameter p)
		{
			switch (p.type)
			{
				case AnimatorControllerParameterType.Bool:
					return p.defaultBool ? 1f : 0f;
				case AnimatorControllerParameterType.Int:
					return p.defaultInt;
				case AnimatorControllerParameterType.Float:
					return p.defaultFloat;
				default:
					return 0f;
			}
		}

		#endregion
	}
}

#endif
