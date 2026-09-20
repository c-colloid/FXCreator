using colloid.FXCreator.AnimatorGraph;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
#if VRC
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
#endif

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// アバターの FX レイヤーに刺さっている AnimatorController をそのまま編集するモード（§2.3）。
	///
	/// 非破壊ではない代わりに依存が VRC SDK だけで済み、標準 Animator ウィンドウや
	/// AV3Manager と同じアセットを見る。
	///
	/// 安全装置は<b>書けない場所にあるときだけ</b>働く。自分の <c>Assets/</c> 配下にある
	/// Controller は今までどおり黙って直接編集する（毎回ダイアログを出すと、
	/// 普通の使い方が一番うるさくなる）。
	/// </summary>
	public sealed class DirectAvatarTarget : IFxTarget
	{
		public const string ModeId = "direct";

		internal sealed class Provider : IFxTargetProvider
		{
			public int Order { get { return 0; } }

			public IFxTarget Create(GameObject avatar)
			{
				return new DirectAvatarTarget(avatar);
			}
		}

		private readonly GameObject _avatar;

		public DirectAvatarTarget(GameObject avatar)
		{
			_avatar = avatar;
		}

		public string Id { get { return ModeId; } }
		public string DisplayName { get { return "Direct"; } }

		public string Description
		{
			get { return "アバターの FX レイヤーの Controller を直接編集します（非破壊ではありません）"; }
		}

		public GameObject Avatar { get { return _avatar; } }

		public bool IsAvailable(out string reason)
		{
			if (_avatar == null)
			{
				reason = "アバターが選ばれていません";
				return false;
			}
			reason = null;
			return true;
		}

		/// <summary>FX レイヤーに書き込める Controller が既に刺さっているか。</summary>
		public bool IsAlreadySetUp
		{
			get
			{
				AnimatorController controller = ResolveController();
				string ignored;
				return controller != null && AcControllerAccess.IsWritable(controller, out ignored);
			}
		}

		public AnimatorController ResolveController()
		{
			if (_avatar == null)
			{
				return null;
			}

#if VRC
			var descriptor = _avatar.GetComponent<VRCAvatarDescriptor>();
			if (descriptor != null)
			{
				// isDefault の枠は SDK 同梱の既定 Controller を指しているだけで、
				// 「このアバター用の FX」ではない。編集対象にはしない。
				VRCAvatarDescriptor.CustomAnimLayer[] layers = descriptor.baseAnimationLayers;
				for (int i = 0; layers != null && i < layers.Length; i++)
				{
					if (layers[i].type != VRCAvatarDescriptor.AnimLayerType.FX || layers[i].isDefault)
					{
						continue;
					}
					var controller = layers[i].animatorController as AnimatorController;
					if (controller != null)
					{
						return controller;
					}
				}
				return null;
			}
#endif

			var animator = _avatar.GetComponent<UnityEngine.Animator>();
			return animator != null ? animator.runtimeAnimatorController as AnimatorController : null;
		}

#if VRC
		public IFxParameterStore ResolveParameterStore()
		{
			var descriptor = _avatar != null ? _avatar.GetComponent<VRCAvatarDescriptor>() : null;
			return new VrcExpressionParameterStore(
				descriptor != null ? descriptor.expressionParameters : null);
		}

		public VRCExpressionsMenu ResolveMenu()
		{
			var descriptor = _avatar != null ? _avatar.GetComponent<VRCAvatarDescriptor>() : null;
			return descriptor != null ? descriptor.expressionsMenu : null;
		}
#else
		public IFxParameterStore ResolveParameterStore()
		{
			return null;
		}

		public Object ResolveMenu()
		{
			return null;
		}
#endif

		public bool TryPrepareForEditing(bool interactive, out string reason)
		{
			reason = null;
			if (_avatar == null)
			{
				reason = "アバターが選ばれていません";
				return false;
			}

			AnimatorController current = ResolveController();

			if (current == null)
			{
				return CreateAndAssign(interactive, out reason);
			}

			string writeReason;
			if (AcControllerAccess.IsWritable(current, out writeReason))
			{
				return true;
			}

			return DuplicateAndAssign(current, writeReason, interactive, out reason);
		}

		/// <summary>FX レイヤーが空のアバター用に、新しい Controller を作って割り当てる。</summary>
		private bool CreateAndAssign(bool interactive, out string reason)
		{
			if (interactive)
			{
				bool ok = EditorUtility.DisplayDialog(
					"FX Creator",
					"このアバターには FX レイヤーの AnimatorController がありません。\n\n"
					+ FxSavePaths.AvatarFolder(_avatar) + "/ に新しく作成して割り当てますか？",
					"作成して割り当てる",
					"何もしない");
				if (!ok)
				{
					reason = "FX レイヤーの Controller がありません";
					return false;
				}
			}

			string folder = FxSavePaths.EnsureAvatarFolder(_avatar);
			string path = FxSavePaths.UniqueAssetPath(folder, "FXC_FX", ".controller");
			AnimatorController created = AnimatorController.CreateAnimatorControllerAtPath(path);
			if (created == null)
			{
				reason = "Controller を作成できませんでした（" + path + "）";
				return false;
			}

			if (!Assign(created, "Create FX Controller", out reason))
			{
				return false;
			}

			reason = null;
			return true;
		}

		/// <summary>
		/// 書き込めない場所にある Controller を FX Creator の管理下へ複製して差し替える。
		/// 断られたら読み取り専用のまま開く（§2.3）。
		/// </summary>
		private bool DuplicateAndAssign(
			AnimatorController source, string writeReason, bool interactive, out string reason)
		{
			string sourcePath = AssetDatabase.GetAssetPath(source);
			if (string.IsNullOrEmpty(sourcePath))
			{
				// アセットですらないのでコピー元が無い。複製で救えるのはファイルがある場合だけ。
				reason = writeReason;
				return false;
			}

			if (interactive)
			{
				bool ok = EditorUtility.DisplayDialog(
					"FX Creator",
					source.name + " は" + writeReason + "。\n\n"
					+ FxSavePaths.AvatarFolder(_avatar) + "/ に複製して、\n"
					+ "アバターの FX レイヤーを複製の方へ差し替えますか？\n\n"
					+ "「そのまま開く」を選ぶと読み取り専用で表示します。",
					"複製して差し替える",
					"そのまま開く");
				if (!ok)
				{
					reason = writeReason;
					return false;
				}
			}

			string folder = FxSavePaths.EnsureAvatarFolder(_avatar);
			string destination = FxSavePaths.UniqueAssetPath(folder, source.name, ".controller");
			if (!AssetDatabase.CopyAsset(sourcePath, destination))
			{
				reason = "複製に失敗しました（" + destination + "）";
				return false;
			}
			AssetDatabase.ImportAsset(destination);

			var copy = AssetDatabase.LoadAssetAtPath<AnimatorController>(destination);
			if (copy == null)
			{
				reason = "複製した Controller を読み込めませんでした（" + destination + "）";
				return false;
			}

			return Assign(copy, "Replace FX Controller", out reason);
		}

		/// <summary>アバター側の参照を差し替える。</summary>
		private bool Assign(AnimatorController controller, string operation, out string reason)
		{
			reason = null;

#if VRC
			var descriptor = _avatar.GetComponent<VRCAvatarDescriptor>();
			if (descriptor != null)
			{
				Undo.RecordObject(descriptor, operation);

				// baseAnimationLayers は<b>配列のコピー</b>を返す（§4.3 の罠と同じ）。
				// 要素を書き換えただけでは反映されないので、配列ごと入れ直す。
				VRCAvatarDescriptor.CustomAnimLayer[] layers = descriptor.baseAnimationLayers;
				bool assigned = false;
				for (int i = 0; layers != null && i < layers.Length; i++)
				{
					if (layers[i].type != VRCAvatarDescriptor.AnimLayerType.FX)
					{
						continue;
					}
					VRCAvatarDescriptor.CustomAnimLayer layer = layers[i];
					layer.isDefault = false;
					layer.animatorController = controller;
					layers[i] = layer;
					assigned = true;
				}

				if (!assigned)
				{
					reason = "この Avatar Descriptor に FX レイヤーの枠がありません";
					return false;
				}

				descriptor.baseAnimationLayers = layers;
				// これが false だと、割り当てた Controller は VRChat 側で使われない。
				descriptor.customizeAnimationLayers = true;
				EditorUtility.SetDirty(descriptor);
				return true;
			}
#endif

			var animator = _avatar.GetComponent<UnityEngine.Animator>();
			if (animator == null)
			{
				reason = "アバターに Avatar Descriptor も Animator もありません";
				return false;
			}
			Undo.RecordObject(animator, operation);
			animator.runtimeAnimatorController = controller;
			EditorUtility.SetDirty(animator);
			return true;
		}

		/// <summary>
		/// Direct は Controller 自体が最終形なので、やることは汚れを立てることと、
		/// 改名・型変更を Expression Parameters へ追随させること（§6.2）。
		/// 追随させないと、名前が一致しなくなって VRChat 側で動かなくなる。
		/// </summary>
		public void OnAfterEdit(AcEditReport report)
		{
			if (report == null || report.Controller == null)
			{
				return;
			}
			EditorUtility.SetDirty(report.Controller);
			FxParameterSync.FollowControllerEdits(report, ResolveParameterStore());
		}
	}
}
