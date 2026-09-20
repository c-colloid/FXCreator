using System.Collections.Generic;
using colloid.FXCreator.AnimatorGraph;
using colloid.FXCreator.Targeting;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// VAR（Controller のパラメータ）と同期設定の連携（§6.2）。
	///
	/// この2つは<b>名前で結ばれているだけ</b>で、Unity も VRChat も一致を保証しない。
	/// ずれた状態は「メニューは出るのに何も起きない」として現れ、
	/// Controller を見ても原因が分からない。ここで揃うことを固定する。
	///
	/// 宣言先は <see cref="IFxParameterStore"/> 越しなので、VRC SDK にも MA にも
	/// 依存しない差し替え可能な実装でテストできる。
	/// </summary>
	public class FxParameterSyncTests
	{
		private const string TempFolder = "Assets/FXCreatorSyncTests";

		private AnimatorController _controller;
		private FakeStore _store;

		/// <summary>宣言先の代役。Expression Parameters / MA Parameters と同じ口を持つ。</summary>
		private sealed class FakeStore : IFxParameterStore
		{
			public readonly List<FxSyncParameter> Items = new List<FxSyncParameter>();

			public string DisplayName { get { return "Fake"; } }
			public Object Asset { get { return null; } }
			public string UnavailableReason { get; set; }
			public bool IsReadOnly { get; set; }
			public int MaxCost { get { return 256; } }

			public int Cost
			{
				get
				{
					int cost = 0;
					for (int i = 0; i < Items.Count; i++)
					{
						if (Items[i].Synced)
						{
							cost += Items[i].Type == FxSyncType.Bool ? 1 : 8;
						}
					}
					return cost;
				}
			}

			public IReadOnlyList<FxSyncParameter> Read() { return Items; }

			private int IndexOf(string name)
			{
				return Items.FindIndex(p => p.Name == name);
			}

			public void Add(FxSyncParameter parameter)
			{
				if (IndexOf(parameter.Name) < 0)
				{
					Items.Add(parameter);
				}
			}

			public void Remove(string name)
			{
				int i = IndexOf(name);
				if (i >= 0)
				{
					Items.RemoveAt(i);
				}
			}

			public void Rename(string oldName, string newName)
			{
				if (IndexOf(newName) >= 0)
				{
					return;
				}
				Modify(oldName, p => { p.Name = newName; return p; });
			}

			public void SetType(string name, FxSyncType type)
			{
				Modify(name, p => { p.Type = type; return p; });
			}

			public void SetSaved(string name, bool saved)
			{
				Modify(name, p => { p.Saved = saved; return p; });
			}

			public void SetSynced(string name, bool synced)
			{
				Modify(name, p => { p.Synced = synced; return p; });
			}

			public void SetDefault(string name, float value)
			{
				Modify(name, p => { p.Default = value; return p; });
			}

			private void Modify(string name, System.Func<FxSyncParameter, FxSyncParameter> change)
			{
				int i = IndexOf(name);
				if (i >= 0)
				{
					Items[i] = change(Items[i]);
				}
			}
		}

		[SetUp]
		public void SetUp()
		{
			if (!AssetDatabase.IsValidFolder(TempFolder))
			{
				AssetDatabase.CreateFolder("Assets", "FXCreatorSyncTests");
			}
			_controller = AnimatorController.CreateAnimatorControllerAtPath(TempFolder + "/Sync.controller");
			_store = new FakeStore();
		}

		[TearDown]
		public void TearDown()
		{
			Undo.ClearAll();
			_controller = null;
			AssetDatabase.DeleteAsset(TempFolder);
			AssetDatabase.Refresh();
		}

		private static FxSyncParameter Declared(string name, FxSyncType type)
		{
			return new FxSyncParameter { Name = name, Type = type, Saved = true, Synced = true };
		}

		#region CanDeclare

		[Test]
		public void CanDeclare_RefusesTrigger()
		{
			_controller.AddParameter("t", AnimatorControllerParameterType.Trigger);

			string reason;
			Assert.That(FxParameterSync.CanDeclare(_controller.parameters[0], out reason), Is.False);
			Assert.That(reason, Does.Contain("Trigger"));
		}

		[Test]
		public void CanDeclare_RefusesVrcBuiltIns()
		{
			// 組み込みを宣言すると、使いもしない同期コストを取られる。
			_controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);

			string reason;
			Assert.That(FxParameterSync.CanDeclare(_controller.parameters[0], out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
		}

		/// <summary>
		/// 差分一覧と「追加」の可否が<b>同じ判定</b>を見ていること。
		/// 別々に書くと、一覧には出ないのに行からは追加できる、というズレが出る。
		/// </summary>
		[Test]
		public void MissingInStore_AgreesWithCanDeclare()
		{
			_controller.AddParameter("Toggle", AnimatorControllerParameterType.Bool);
			_controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);
			_controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

			List<AnimatorControllerParameter> missing =
				FxParameterSync.MissingInStore(_controller, _store);

			var names = missing.ConvertAll(p => p.name);
			Assert.That(names, Is.EquivalentTo(new[] { "Toggle" }));

			foreach (AnimatorControllerParameter p in _controller.parameters)
			{
				string reason;
				bool canDeclare = FxParameterSync.CanDeclare(p, out reason);
				Assert.That(names.Contains(p.name), Is.EqualTo(canDeclare),
					p.name + ": 差分一覧と CanDeclare が食い違っている");
			}
		}

		[Test]
		public void DeclareInStore_IgnoresRefusedParameters()
		{
			_controller.AddParameter("GestureLeft", AnimatorControllerParameterType.Int);
			_controller.AddParameter("Fire", AnimatorControllerParameterType.Trigger);

			foreach (AnimatorControllerParameter p in _controller.parameters)
			{
				FxParameterSync.DeclareInStore(p, _store);
			}

			Assert.That(_store.Items, Is.Empty, "宣言してはいけないものが入った");
		}

		#endregion

		#region Diff

		[Test]
		public void MissingInController_FindsDeclaredOnlyParameters()
		{
			_store.Add(Declared("OnlyDeclared", FxSyncType.Bool));
			_controller.AddParameter("Both", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("Both", FxSyncType.Bool));

			List<FxSyncParameter> missing =
				FxParameterSync.MissingInController(_controller, _store);

			Assert.That(missing.Count, Is.EqualTo(1));
			Assert.That(missing[0].Name, Is.EqualTo("OnlyDeclared"));
		}

		[Test]
		public void DeclareInController_AddsWithCarriedDefault()
		{
			var parameter = new FxSyncParameter
			{
				Name = "Level",
				Type = FxSyncType.Int,
				Default = 3f,
				Saved = true,
				Synced = true
			};

			FxParameterSync.DeclareInController(parameter, _controller);

			AnimatorControllerParameter[] parameters = _controller.parameters;
			Assert.That(parameters.Length, Is.EqualTo(1));
			Assert.That(parameters[0].name, Is.EqualTo("Level"));
			Assert.That(parameters[0].type, Is.EqualTo(AnimatorControllerParameterType.Int));
			// 既定値がずれると、アバターの初期状態が宣言側と食い違う。
			Assert.That(parameters[0].defaultInt, Is.EqualTo(3));
		}

		[Test]
		public void DeclareBothWays_ClearsTheDiff()
		{
			_controller.AddParameter("FromController", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("FromStore", FxSyncType.Float));

			foreach (AnimatorControllerParameter p in
				FxParameterSync.MissingInStore(_controller, _store).ToArray())
			{
				FxParameterSync.DeclareInStore(p, _store);
			}
			foreach (FxSyncParameter p in
				FxParameterSync.MissingInController(_controller, _store).ToArray())
			{
				FxParameterSync.DeclareInController(p, _controller);
			}

			Assert.That(FxParameterSync.MissingInStore(_controller, _store), Is.Empty);
			Assert.That(FxParameterSync.MissingInController(_controller, _store), Is.Empty);
		}

		#endregion

		#region Follow

		[Test]
		public void FollowControllerEdits_FollowsRename()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("Old", FxSyncType.Bool));

			using (AcEdit e = AcEdit.Begin(_controller, "rename"))
			{
				e.RenameParameter("Old", "New");
				FxParameterSync.FollowControllerEdits(e.Report, _store);
			}

			Assert.That(_store.Items.Count, Is.EqualTo(1), "行が増えている（改名ではなく追加になった）");
			Assert.That(_store.Items[0].Name, Is.EqualTo("New"));
		}

		[Test]
		public void FollowControllerEdits_FollowsTypeChange()
		{
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("p", FxSyncType.Bool));

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Int);
				FxParameterSync.FollowControllerEdits(e.Report, _store);
			}

			Assert.That(_store.Items[0].Type, Is.EqualTo(FxSyncType.Int));
		}

		[Test]
		public void FollowControllerEdits_LeavesRowAloneWhenTypeBecomesTrigger()
		{
			// Trigger は同期できる型が無い。行を消すと、型を戻したときに
			// saved / sync の設定が失われる。差分表示が拾えばよい。
			_controller.AddParameter("p", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("p", FxSyncType.Bool));

			using (AcEdit e = AcEdit.Begin(_controller, "type"))
			{
				e.ChangeParameterType("p", AnimatorControllerParameterType.Trigger);
				FxParameterSync.FollowControllerEdits(e.Report, _store);
			}

			Assert.That(_store.Items.Count, Is.EqualTo(1));
			Assert.That(_store.Items[0].Type, Is.EqualTo(FxSyncType.Bool));
		}

		[Test]
		public void FollowControllerEdits_DoesNothingWhenReadOnly()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("Old", FxSyncType.Bool));
			_store.IsReadOnly = true;

			using (AcEdit e = AcEdit.Begin(_controller, "rename"))
			{
				e.RenameParameter("Old", "New");
				FxParameterSync.FollowControllerEdits(e.Report, _store);
			}

			Assert.That(_store.Items[0].Name, Is.EqualTo("Old"), "読み取り専用の宣言先を書き換えた");
		}

		/// <summary>改名先が既にあるときは重複を作らない（どちらが効くか決まらなくなる）。</summary>
		[Test]
		public void FollowControllerEdits_RenameDoesNotCreateDuplicates()
		{
			_controller.AddParameter("Old", AnimatorControllerParameterType.Bool);
			_store.Add(Declared("Old", FxSyncType.Bool));
			_store.Add(Declared("Taken", FxSyncType.Bool));

			using (AcEdit e = AcEdit.Begin(_controller, "rename"))
			{
				e.RenameParameter("Old", "Taken");
				FxParameterSync.FollowControllerEdits(e.Report, _store);
			}

			var names = _store.Items.ConvertAll(p => p.Name);
			Assert.That(names.Count, Is.EqualTo(names.FindAll(n => n == "Taken").Count == 1 ? 2 : names.Count));
			Assert.That(names.FindAll(n => n == "Taken").Count, Is.EqualTo(1), "同名の行が2つできた");
		}

		#endregion
	}
}
