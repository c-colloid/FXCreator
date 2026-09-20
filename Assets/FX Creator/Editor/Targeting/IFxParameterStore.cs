using System.Collections.Generic;
using UnityEngine;

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// 同期パラメータの型。VRChat が同期できるのはこの3つだけで、
	/// <c>Trigger</c> に対応するものは無い（§6.2）。
	/// </summary>
	public enum FxSyncType
	{
		Bool,
		Int,
		Float,
	}

	/// <summary>同期パラメータ1件。保存先がどこであっても、パネルはこの形で扱う。</summary>
	public struct FxSyncParameter
	{
		public string Name;
		public FxSyncType Type;

		/// <summary>ワールドを移動しても値を保つ。</summary>
		public bool Saved;

		/// <summary>他の人に同期する（同期コストを消費する）。</summary>
		public bool Synced;

		public float Default;
	}

	/// <summary>
	/// 同期パラメータの宣言先（Docs/FXCreator-Design.md §2.3 / §6.2）。
	///
	/// Direct モードは <c>VRCExpressionParameters</c> アセット、
	/// NDMF(MA) モードはアバター上の <c>ModularAvatarParameters</c> と、
	/// <b>置き場所も型も全く違う</b>。パネルにその違いを持ち込むと、
	/// 「MA モードでは何もできない」か「MA モードなのにアバター同梱アセットを
	/// 書き換える」のどちらかになる。
	///
	/// 保存先の違いはこの境界で吸収し、パネルは<b>どのモードで動いているかを
	/// 知らないまま</b>同じ操作を提供する。
	/// </summary>
	public interface IFxParameterStore
	{
		/// <summary>パネルの見出しに出す名前（"Expression Parameters" / "MA Parameters"）。</summary>
		string DisplayName { get; }

		/// <summary>実体。null なら宣言先そのものが無い（その理由は <see cref="UnavailableReason"/>）。</summary>
		Object Asset { get; }

		/// <summary>宣言先が無い・触れない理由。使えるときは null。</summary>
		string UnavailableReason { get; }

		bool IsReadOnly { get; }

		IReadOnlyList<FxSyncParameter> Read();

		/// <summary>同期コストの合計（同期するものだけ数える）。</summary>
		int Cost { get; }

		/// <summary>上限。SDK の定数を見るのでハードコードしない（§6.2）。</summary>
		int MaxCost { get; }

		void Add(FxSyncParameter parameter);
		void Remove(string name);

		/// <summary>改名。宣言先に同名が既にあるときは何もしない（重複は作らない）。</summary>
		void Rename(string oldName, string newName);

		void SetType(string name, FxSyncType type);
		void SetSaved(string name, bool saved);
		void SetSynced(string name, bool synced);
		void SetDefault(string name, float value);
	}

	/// <summary>Controller の型と同期パラメータの型の対応。</summary>
	public static class FxSyncTypes
	{
		/// <summary>
		/// Controller のパラメータ型を同期できる型へ。
		/// <c>Trigger</c> は VRChat の同期に対応する型が無いので false。
		/// </summary>
		public static bool TryFromAnimator(UnityEngine.AnimatorControllerParameterType type, out FxSyncType result)
		{
			switch (type)
			{
				case UnityEngine.AnimatorControllerParameterType.Bool:
					result = FxSyncType.Bool;
					return true;
				case UnityEngine.AnimatorControllerParameterType.Int:
					result = FxSyncType.Int;
					return true;
				case UnityEngine.AnimatorControllerParameterType.Float:
					result = FxSyncType.Float;
					return true;
				default:
					result = FxSyncType.Bool;
					return false;
			}
		}

		public static UnityEngine.AnimatorControllerParameterType ToAnimator(FxSyncType type)
		{
			switch (type)
			{
				case FxSyncType.Int: return UnityEngine.AnimatorControllerParameterType.Int;
				case FxSyncType.Float: return UnityEngine.AnimatorControllerParameterType.Float;
				default: return UnityEngine.AnimatorControllerParameterType.Bool;
			}
		}
	}
}
