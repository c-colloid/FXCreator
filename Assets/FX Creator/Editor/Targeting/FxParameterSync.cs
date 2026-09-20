using System;
using System.Collections.Generic;
using colloid.FXCreator.AnimatorGraph;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// Controller のパラメータと同期設定（Expression Parameters / MA Parameters）の
	/// つき合わせ（Docs/FXCreator-Design.md §6.2）。
	///
	/// この2つは<b>名前で結ばれているだけ</b>で、Unity も VRChat も一致を保証しない。
	/// 片方だけ改名すると、アバターは黙って「そのパラメータを動かせないメニュー」を
	/// 積んだまま出来上がる。ここが両者を揃える唯一の場所。
	/// </summary>
	public static class FxParameterSync
	{
		/// <summary>
		/// Controller 側の改名・型変更を宣言先へ追随させる。
		/// <see cref="IFxTarget.OnAfterEdit"/> から呼ぶ。
		/// </summary>
		public static void FollowControllerEdits(AcEditReport report, IFxParameterStore store)
		{
			if (report == null || store == null || store.IsReadOnly)
			{
				return;
			}

			for (int i = 0; i < report.ParameterRenames.Count; i++)
			{
				AcParameterRename rename = report.ParameterRenames[i];
				store.Rename(rename.OldName, rename.NewName);
			}

			for (int i = 0; i < report.ParameterRetypes.Count; i++)
			{
				AcParameterRetype retype = report.ParameterRetypes[i];
				FxSyncType type;
				if (!FxSyncTypes.TryFromAnimator(retype.To, out type))
				{
					// Trigger は同期できる型が無い。宣言先の行は<b>消さずに残す</b>
					// （型を戻したときに設定も戻ってほしい）。差分表示が拾う。
					continue;
				}
				store.SetType(retype.Name, type);
			}
		}

		/// <summary>宣言先にあって Controller に無いもの。</summary>
		public static List<FxSyncParameter> MissingInController(
			AnimatorController controller, IFxParameterStore store)
		{
			var result = new List<FxSyncParameter>();
			if (store == null)
			{
				return result;
			}

			var inController = new HashSet<string>(StringComparer.Ordinal);
			if (controller != null)
			{
				AnimatorControllerParameter[] parameters = controller.parameters;
				for (int i = 0; i < parameters.Length; i++)
				{
					if (parameters[i] != null)
					{
						inController.Add(parameters[i].name);
					}
				}
			}

			IReadOnlyList<FxSyncParameter> declared = store.Read();
			for (int i = 0; i < declared.Count; i++)
			{
				if (!inController.Contains(declared[i].Name))
				{
					result.Add(declared[i]);
				}
			}
			return result;
		}

		/// <summary>
		/// そのパラメータを宣言先へ足してよいか。
		///
		/// <b>差分の抽出と UI の「追加」ボタンは、必ずこの1つの判定を見ること。</b>
		/// 別々に書くと、差分一覧には出ないのに行からは追加できる（＝組み込みパラメータを
		/// 宣言して同期コストを無駄にする）といったズレが出る。
		/// §6.4 の「未参照判定の走査範囲を改名の走査と揃える」と同じ理由。
		/// </summary>
		public static bool CanDeclare(AnimatorControllerParameter parameter, out string reason)
		{
			reason = null;
			if (parameter == null)
			{
				reason = "パラメータがありません";
				return false;
			}

			FxSyncType ignored;
			if (!FxSyncTypes.TryFromAnimator(parameter.type, out ignored))
			{
				reason = "Trigger は VRChat の同期に対応する型がありません";
				return false;
			}

			if (VrcBuiltInParameters.Contains(parameter.name))
			{
				reason = "VRChat が供給する組み込みパラメータです（宣言すると同期コストの無駄になります）";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Controller にあって宣言先に無いもの。
		/// 何を対象にするかは <see cref="CanDeclare"/> が決める。
		/// </summary>
		public static List<AnimatorControllerParameter> MissingInStore(
			AnimatorController controller, IFxParameterStore store)
		{
			var result = new List<AnimatorControllerParameter>();
			if (controller == null || store == null)
			{
				return result;
			}

			var declared = new HashSet<string>(StringComparer.Ordinal);
			IReadOnlyList<FxSyncParameter> list = store.Read();
			for (int i = 0; i < list.Count; i++)
			{
				declared.Add(list[i].Name);
			}

			AnimatorControllerParameter[] parameters = controller.parameters;
			for (int i = 0; i < parameters.Length; i++)
			{
				AnimatorControllerParameter p = parameters[i];
				string ignoredReason;
				if (p == null || declared.Contains(p.name) || !CanDeclare(p, out ignoredReason))
				{
					continue;
				}
				result.Add(p);
			}
			return result;
		}

		/// <summary>Controller のパラメータを宣言先へ足す（差分解消の片道）。</summary>
		public static void DeclareInStore(AnimatorControllerParameter parameter, IFxParameterStore store)
		{
			string reason;
			if (store == null || store.IsReadOnly || !CanDeclare(parameter, out reason))
			{
				return;
			}
			FxSyncType type;
			FxSyncTypes.TryFromAnimator(parameter.type, out type);

			store.Add(new FxSyncParameter
			{
				Name = parameter.name,
				Type = type,
				// 新規行は同期・保存あり（VRC SDK が行を足すときと同じ既定）。
				// 要らないものはユーザーが外す。
				Saved = true,
				Synced = true,
				Default = DefaultOf(parameter),
			});
		}

		/// <summary>宣言先のパラメータを Controller へ足す（差分解消のもう片道）。</summary>
		public static void DeclareInController(FxSyncParameter parameter, AnimatorController controller)
		{
			if (controller == null || string.IsNullOrEmpty(parameter.Name))
			{
				return;
			}

			using (AcEdit e = AcEdit.Begin(controller, "Add Parameter"))
			{
				AnimatorControllerParameter added = e.AddParameter(
					parameter.Name, FxSyncTypes.ToAnimator(parameter.Type));
				if (added == null)
				{
					return;
				}
				// 既定値も持っていく。揃えないと、同期設定の既定と Controller の既定が
				// 食い違ってアバターの初期状態が変わる。
				e.ModifyParameter(parameter.Name, p =>
				{
					switch (parameter.Type)
					{
						case FxSyncType.Bool:
							p.defaultBool = Mathf.Abs(parameter.Default) > 0.0001f;
							break;
						case FxSyncType.Int:
							p.defaultInt = Mathf.RoundToInt(parameter.Default);
							break;
						default:
							p.defaultFloat = parameter.Default;
							break;
					}
				});
			}
		}

		private static float DefaultOf(AnimatorControllerParameter parameter)
		{
			switch (parameter.type)
			{
				case AnimatorControllerParameterType.Bool:
					return parameter.defaultBool ? 1f : 0f;
				case AnimatorControllerParameterType.Int:
					return parameter.defaultInt;
				case AnimatorControllerParameterType.Float:
					return parameter.defaultFloat;
				default:
					return 0f;
			}
		}
	}
}
