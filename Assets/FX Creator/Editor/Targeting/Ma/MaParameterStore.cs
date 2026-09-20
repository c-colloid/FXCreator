// FXCreator.Ma.asmdef が MA と VRC を要求するので、Unity 上ではこのガードは常に真。
// 書いてあるのは、スクリプトゲートが staged ファイルを単一アセンブリとしてまとめて
// コンパイルするため（asmdef ごとの versionDefines が効かない）。
#if MA && VRC
using System;
using System.Collections.Generic;
using colloid.FXCreator.Targeting;
using nadena.dev.modular_avatar.core;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace colloid.FXCreator.Targeting.Ma
{
	/// <summary>
	/// NDMF(MA) モードの宣言先（<c>ModularAvatarParameters</c>）。
	///
	/// 非破壊モードの肝は「アバター同梱の <c>VRCExpressionParameters</c> を触らない」こと。
	/// 同期設定はこのコンポーネントが持ち、ビルド時に MA が Expression Parameters を組み立てる。
	/// </summary>
	public sealed class MaParameterStore : IFxParameterStore
	{
		private readonly ModularAvatarParameters _component;

		public MaParameterStore(ModularAvatarParameters component)
		{
			_component = component;
		}

		public string DisplayName { get { return "MA Parameters"; } }

		public UnityEngine.Object Asset { get { return _component; } }

		public string UnavailableReason
		{
			get
			{
				return _component == null
					? "NDMF (MA) の設定がまだ作成されていません"
					: null;
			}
		}

		public bool IsReadOnly { get { return _component == null; } }

		/// <summary>上限は Expression Parameters と同じ（最終的にそこへ組み上がる）。</summary>
		public int MaxCost { get { return VRCExpressionParameters.MAX_PARAMETER_COST; } }

		public int Cost
		{
			get
			{
				if (_component == null || _component.parameters == null)
				{
					return 0;
				}
				int cost = 0;
				for (int i = 0; i < _component.parameters.Count; i++)
				{
					ParameterConfig c = _component.parameters[i];
					// 同期しないものはコストを食わない。
					if (c.syncType == ParameterSyncType.NotSynced || c.localOnly)
					{
						continue;
					}
					cost += VRCExpressionParameters.TypeCost(ToValueType(c.syncType));
				}
				return cost;
			}
		}

		public IReadOnlyList<FxSyncParameter> Read()
		{
			var result = new List<FxSyncParameter>();
			if (_component == null || _component.parameters == null)
			{
				return result;
			}

			for (int i = 0; i < _component.parameters.Count; i++)
			{
				ParameterConfig c = _component.parameters[i];
				if (string.IsNullOrEmpty(c.nameOrPrefix) || c.isPrefix)
				{
					// 接頭辞の宣言は個別のパラメータではないので一覧に混ぜない。
					continue;
				}
				result.Add(new FxSyncParameter
				{
					Name = c.nameOrPrefix,
					Type = FromSyncType(c.syncType),
					Saved = c.saved,
					Synced = c.syncType != ParameterSyncType.NotSynced && !c.localOnly,
					Default = c.defaultValue,
				});
			}
			return result;
		}

		#region Write

		public void Add(FxSyncParameter parameter)
		{
			if (_component == null || IndexOf(parameter.Name) >= 0)
			{
				return;
			}

			var list = new List<ParameterConfig>(_component.parameters ?? new List<ParameterConfig>());
			list.Add(new ParameterConfig
			{
				nameOrPrefix = parameter.Name,
				isPrefix = false,
				internalParameter = false,
				localOnly = !parameter.Synced,
				saved = parameter.Saved,
				syncType = ToSyncType(parameter.Type),
				hasExplicitDefaultValue = true,
				defaultValue = parameter.Default,
			});
			Apply("Add MA Parameter", list);
		}

		public void Remove(string name)
		{
			int index = IndexOf(name);
			if (index < 0)
			{
				return;
			}
			var list = new List<ParameterConfig>(_component.parameters);
			list.RemoveAt(index);
			Apply("Remove MA Parameter", list);
		}

		public void Rename(string oldName, string newName)
		{
			if (string.IsNullOrEmpty(newName) || oldName == newName || IndexOf(newName) >= 0)
			{
				return;
			}
			Modify(oldName, "Rename MA Parameter", c => { c.nameOrPrefix = newName; return c; });
		}

		public void SetType(string name, FxSyncType type)
		{
			Modify(name, "Set MA Parameter Type", c =>
			{
				// 同期しない設定のまま型だけ変えられるようにする
				// （NotSynced を上書きすると、意図せず同期コストが増える）。
				if (c.syncType != ParameterSyncType.NotSynced)
				{
					c.syncType = ToSyncType(type);
				}
				return c;
			});
		}

		public void SetSaved(string name, bool saved)
		{
			Modify(name, "Set Saved", c => { c.saved = saved; return c; });
		}

		public void SetSynced(string name, bool synced)
		{
			Modify(name, "Set Synced", c =>
			{
				c.localOnly = !synced;
				if (synced && c.syncType == ParameterSyncType.NotSynced)
				{
					// 同期に戻すときは型を思い出せないので Bool から始める。
					c.syncType = ParameterSyncType.Bool;
				}
				return c;
			});
		}

		public void SetDefault(string name, float value)
		{
			Modify(name, "Set Default", c =>
			{
				c.defaultValue = value;
				c.hasExplicitDefaultValue = true;
				return c;
			});
		}

		private int IndexOf(string name)
		{
			if (_component == null || _component.parameters == null || string.IsNullOrEmpty(name))
			{
				return -1;
			}
			for (int i = 0; i < _component.parameters.Count; i++)
			{
				if (string.Equals(_component.parameters[i].nameOrPrefix, name, StringComparison.Ordinal))
				{
					return i;
				}
			}
			return -1;
		}

		/// <summary>
		/// <c>ParameterConfig</c> は<b>構造体</b>。リストから取り出した値を書き換えても
		/// リストには戻らないので、必ず入れ直す（配列コピーの罠と同じ形）。
		/// </summary>
		private void Modify(string name, string label, Func<ParameterConfig, ParameterConfig> change)
		{
			int index = IndexOf(name);
			if (index < 0)
			{
				return;
			}
			var list = new List<ParameterConfig>(_component.parameters);
			list[index] = change(list[index]);
			Apply(label, list);
		}

		private void Apply(string label, List<ParameterConfig> list)
		{
			Undo.RecordObject(_component, label);
			_component.parameters = list;
			EditorUtility.SetDirty(_component);
		}

		#endregion

		private static FxSyncType FromSyncType(ParameterSyncType type)
		{
			switch (type)
			{
				case ParameterSyncType.Int: return FxSyncType.Int;
				case ParameterSyncType.Float: return FxSyncType.Float;
				default: return FxSyncType.Bool;
			}
		}

		private static ParameterSyncType ToSyncType(FxSyncType type)
		{
			switch (type)
			{
				case FxSyncType.Int: return ParameterSyncType.Int;
				case FxSyncType.Float: return ParameterSyncType.Float;
				default: return ParameterSyncType.Bool;
			}
		}

		private static VRCExpressionParameters.ValueType ToValueType(ParameterSyncType type)
		{
			switch (type)
			{
				case ParameterSyncType.Int: return VRCExpressionParameters.ValueType.Int;
				case ParameterSyncType.Float: return VRCExpressionParameters.ValueType.Float;
				default: return VRCExpressionParameters.ValueType.Bool;
			}
		}
	}
}
#endif
