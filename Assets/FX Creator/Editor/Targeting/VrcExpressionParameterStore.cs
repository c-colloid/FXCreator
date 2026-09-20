#if VRC
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// Direct モードの宣言先（<c>VRCExpressionParameters</c> アセット）。
	///
	/// これは<b>アバター同梱のアセット</b>であることが多いので、書けない場所に
	/// あるものは読み取り専用にする（グラフ側と同じ規則で判断する）。
	/// </summary>
	public sealed class VrcExpressionParameterStore : IFxParameterStore
	{
		private readonly VRCExpressionParameters _asset;

		public VrcExpressionParameterStore(VRCExpressionParameters asset)
		{
			_asset = asset;
		}

		public string DisplayName { get { return "Expression Parameters"; } }

		public UnityEngine.Object Asset { get { return _asset; } }

		public string UnavailableReason
		{
			get
			{
				return _asset == null
					? "アバターに Expression Parameters が設定されていません"
					: null;
			}
		}

		public bool IsReadOnly
		{
			get
			{
				if (_asset == null)
				{
					return true;
				}
				string path = AssetDatabase.GetAssetPath(_asset);
				return string.IsNullOrEmpty(path)
					|| path.StartsWith("Packages/", StringComparison.Ordinal);
			}
		}

		public int MaxCost { get { return VRCExpressionParameters.MAX_PARAMETER_COST; } }

		public int Cost { get { return _asset != null ? _asset.CalcTotalCost() : 0; } }

		public IReadOnlyList<FxSyncParameter> Read()
		{
			var result = new List<FxSyncParameter>();
			if (_asset == null || _asset.parameters == null)
			{
				return result;
			}

			for (int i = 0; i < _asset.parameters.Length; i++)
			{
				VRCExpressionParameters.Parameter p = _asset.parameters[i];
				if (p == null)
				{
					continue;
				}
				result.Add(new FxSyncParameter
				{
					Name = p.name,
					Type = FromValueType(p.valueType),
					Saved = p.saved,
					Synced = p.networkSynced,
					Default = p.defaultValue,
				});
			}
			return result;
		}

		#region Write

		public void Add(FxSyncParameter parameter)
		{
			if (_asset == null || IsReadOnly || Find(parameter.Name) != null)
			{
				return;
			}

			var list = new List<VRCExpressionParameters.Parameter>(
				_asset.parameters ?? new VRCExpressionParameters.Parameter[0]);
			list.Add(new VRCExpressionParameters.Parameter
			{
				name = parameter.Name,
				valueType = ToValueType(parameter.Type),
				saved = parameter.Saved,
				networkSynced = parameter.Synced,
				defaultValue = parameter.Default,
			});

			Apply("Add Expression Parameter", () => _asset.parameters = list.ToArray());
		}

		public void Remove(string name)
		{
			if (_asset == null || IsReadOnly || _asset.parameters == null)
			{
				return;
			}

			var list = new List<VRCExpressionParameters.Parameter>();
			for (int i = 0; i < _asset.parameters.Length; i++)
			{
				VRCExpressionParameters.Parameter p = _asset.parameters[i];
				if (p != null && !string.Equals(p.name, name, StringComparison.Ordinal))
				{
					list.Add(p);
				}
			}
			Apply("Remove Expression Parameter", () => _asset.parameters = list.ToArray());
		}

		public void Rename(string oldName, string newName)
		{
			if (string.IsNullOrEmpty(newName) || oldName == newName)
			{
				return;
			}
			// 同名が既にあるなら何もしない。2行が同じ名前を名乗ると、
			// VRChat 側でどちらが効くのか決まらない。
			if (Find(newName) != null)
			{
				return;
			}
			Modify(oldName, "Rename Expression Parameter", p => p.name = newName);
		}

		public void SetType(string name, FxSyncType type)
		{
			Modify(name, "Set Expression Parameter Type", p => p.valueType = ToValueType(type));
		}

		public void SetSaved(string name, bool saved)
		{
			Modify(name, "Set Saved", p => p.saved = saved);
		}

		public void SetSynced(string name, bool synced)
		{
			Modify(name, "Set Synced", p => p.networkSynced = synced);
		}

		public void SetDefault(string name, float value)
		{
			Modify(name, "Set Default", p => p.defaultValue = value);
		}

		private VRCExpressionParameters.Parameter Find(string name)
		{
			if (_asset == null || _asset.parameters == null || string.IsNullOrEmpty(name))
			{
				return null;
			}
			for (int i = 0; i < _asset.parameters.Length; i++)
			{
				VRCExpressionParameters.Parameter p = _asset.parameters[i];
				if (p != null && string.Equals(p.name, name, StringComparison.Ordinal))
				{
					return p;
				}
			}
			return null;
		}

		private void Modify(string name, string label, Action<VRCExpressionParameters.Parameter> change)
		{
			if (IsReadOnly)
			{
				return;
			}
			VRCExpressionParameters.Parameter p = Find(name);
			if (p == null)
			{
				return;
			}
			Apply(label, () => change(p));
		}

		/// <summary>
		/// Controller ではなく別アセットなので <see cref="AnimatorGraph.AcEdit"/> ではなく
		/// 直接 Undo を積む。<c>parameters</c> は参照型の配列で、要素を書き換えても
		/// 配列そのものは変わらないため <c>RegisterCompleteObjectUndo</c> を使う。
		/// </summary>
		private void Apply(string label, Action change)
		{
			Undo.RegisterCompleteObjectUndo(_asset, label);
			change();
			EditorUtility.SetDirty(_asset);
		}

		#endregion

		private static FxSyncType FromValueType(VRCExpressionParameters.ValueType type)
		{
			switch (type)
			{
				case VRCExpressionParameters.ValueType.Int: return FxSyncType.Int;
				case VRCExpressionParameters.ValueType.Float: return FxSyncType.Float;
				default: return FxSyncType.Bool;
			}
		}

		private static VRCExpressionParameters.ValueType ToValueType(FxSyncType type)
		{
			switch (type)
			{
				case FxSyncType.Int: return VRCExpressionParameters.ValueType.Int;
				case FxSyncType.Float: return VRCExpressionParameters.ValueType.Float;
				default: return VRCExpressionParameters.ValueType.Bool;
			}
		}
	}
}
#endif
