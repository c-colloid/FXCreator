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
	/// Targeting（Docs/FXCreator-Design.md §8 の <c>TargetingTests</c>）。
	///
	/// 見張るのは3つ。
	/// <list type="number">
	/// <item>Direct の解決と、支度（作成・複製）が<b>実際にアバターへ差さる</b>こと</item>
	/// <item>使えないモードが既定に選ばれないこと（＝MA 未導入時のフォールバック）</item>
	/// <item>「書けるか」の判定がグラフと Targeting で<b>同じ</b>であること</item>
	/// </list>
	///
	/// MA が入っているかどうかで結果が変わるテストは書かない。
	/// CI と手元で違う結果が出るテストは、落ちたときに原因を指せない。
	/// </summary>
	public class TargetingTests
	{
		/// <summary>実在のアバターと紛れない名前にする（後始末でフォルダごと消すため）。</summary>
		private const string AvatarName = "FXC_Targeting_Test";

		private GameObject _avatar;
		private string _savedPreferredMode;

		[SetUp]
		public void SetUp()
		{
			_savedPreferredMode = FxTargetResolver.PreferredMode;
			_avatar = new GameObject(AvatarName);
			_avatar.AddComponent<UnityEngine.Animator>();
		}

		[TearDown]
		public void TearDown()
		{
			FxTargetResolver.PreferredMode = _savedPreferredMode;

			if (_avatar != null)
			{
				Object.DestroyImmediate(_avatar);
				_avatar = null;
			}

			// 支度のテストが作ったアセットを片付ける。
			string folder = FxSavePaths.Root + "/" + AvatarName;
			if (AssetDatabase.IsValidFolder(folder))
			{
				AssetDatabase.DeleteAsset(folder);
			}
			AssetDatabase.Refresh();

			// 別テストの PerformUndo がここの操作を巻き戻さないように。
			Undo.ClearAll();
		}

		private static DirectAvatarTarget Direct(GameObject avatar)
		{
			return new DirectAvatarTarget(avatar);
		}

		#region Providers / resolver

		[Test]
		public void CreateTargets_AlwaysIncludesDirect()
		{
			List<IFxTarget> targets = FxTargetResolver.CreateTargets(_avatar);

			Assert.That(targets, Is.Not.Empty, "モードが1つも作られなかった");
			Assert.That(
				targets.Exists(t => t.Id == DirectAvatarTarget.ModeId),
				Is.True,
				"Direct は常に作れるはず");
		}

		[Test]
		public void CreateTargets_IdsAreUnique()
		{
			List<IFxTarget> targets = FxTargetResolver.CreateTargets(_avatar);

			var seen = new HashSet<string>();
			for (int i = 0; i < targets.Count; i++)
			{
				Assert.That(
					seen.Add(targets[i].Id),
					Is.True,
					"モード ID が重複している: " + targets[i].Id);
			}
		}

		/// <summary>
		/// MA 未導入時のフォールバック。VRC Avatar Descriptor が無いアバターでは
		/// NDMF(MA) は使えないので、既定は Direct に落ちる。
		/// MA が入っていても入っていなくても同じ結果になる書き方にしてある。
		/// </summary>
		[Test]
		public void ChooseDefault_FallsBackToDirect_WhenOtherModesAreUnavailable()
		{
			List<IFxTarget> targets = FxTargetResolver.CreateTargets(_avatar);

			string reason;
			for (int i = 0; i < targets.Count; i++)
			{
				if (targets[i].Id == DirectAvatarTarget.ModeId)
				{
					continue;
				}
				Assert.That(
					targets[i].IsAvailable(out reason),
					Is.False,
					targets[i].DisplayName + " は Descriptor の無いアバターで使えると答えた");
				Assert.That(reason, Is.Not.Null.And.Not.Empty, "使えない理由が空だと UI に出せない");
			}

			IFxTarget chosen = FxTargetResolver.ChooseDefault(targets, "ndmf-ma");
			Assert.That(chosen, Is.Not.Null);
			Assert.That(chosen.Id, Is.EqualTo(DirectAvatarTarget.ModeId));
		}

		/// <summary>設定より<b>シーンの実体</b>を優先する（既に支度のあるモードが勝つ）。</summary>
		[Test]
		public void ChooseDefault_PrefersAlreadySetUpMode()
		{
			var controller = CreateTempController();
			_avatar.GetComponent<UnityEngine.Animator>().runtimeAnimatorController = controller;

			List<IFxTarget> targets = FxTargetResolver.CreateTargets(_avatar);
			IFxTarget chosen = FxTargetResolver.ChooseDefault(targets, "存在しないモード");

			Assert.That(chosen.Id, Is.EqualTo(DirectAvatarTarget.ModeId));
			Assert.That(chosen.IsAlreadySetUp, Is.True);
		}

		[Test]
		public void FindAvatars_IncludesSceneAnimatorRoot()
		{
			List<GameObject> avatars = FxTargetResolver.FindAvatars();

			Assert.That(avatars.Contains(_avatar), Is.True, "シーンに置いたアバターが候補に出ない");
		}

		#endregion

		#region Direct

		[Test]
		public void Direct_ResolvesAssignedController()
		{
			var controller = CreateTempController();
			_avatar.GetComponent<UnityEngine.Animator>().runtimeAnimatorController = controller;

			Assert.That(Direct(_avatar).ResolveController(), Is.SameAs(controller));
		}

		[Test]
		public void Direct_ResolvesNull_WhenNothingAssigned()
		{
			Assert.That(Direct(_avatar).ResolveController(), Is.Null);
			Assert.That(Direct(_avatar).IsAlreadySetUp, Is.False);
		}

		/// <summary>解決は副作用なし。ダイアログも作成も <c>TryPrepareForEditing</c> の担当。</summary>
		[Test]
		public void Direct_Resolve_DoesNotCreateAssets()
		{
			Direct(_avatar).ResolveController();

			Assert.That(
				AssetDatabase.IsValidFolder(FxSavePaths.Root + "/" + AvatarName),
				Is.False,
				"ResolveController がフォルダを作った");
		}

		[Test]
		public void Direct_Prepare_CreatesControllerAndAssignsIt()
		{
			DirectAvatarTarget target = Direct(_avatar);

			string reason;
			Assert.That(target.TryPrepareForEditing(false, out reason), Is.True, reason);

			AnimatorController created = target.ResolveController();
			Assert.That(created, Is.Not.Null, "作成した Controller が解決できない");

			// アバター側の参照が実際に差し替わっていること（ここが抜けると
			// 「編集はできるが VRChat では何も起きない」になる）。
			Assert.That(
				_avatar.GetComponent<UnityEngine.Animator>().runtimeAnimatorController,
				Is.SameAs(created));

			// 置き場は FX Creator の管理下（§2.3）。
			string path = AssetDatabase.GetAssetPath(created);
			Assert.That(FxSavePaths.IsManaged(path), Is.True, "管理外に作られた: " + path);
			Assert.That(path, Does.Contain(AvatarName));
		}

		[Test]
		public void Direct_Prepare_IsIdempotent()
		{
			DirectAvatarTarget target = Direct(_avatar);

			string reason;
			Assert.That(target.TryPrepareForEditing(false, out reason), Is.True, reason);
			AnimatorController first = target.ResolveController();

			// 2回目は既に書ける Controller があるので、何も作らず true。
			Assert.That(target.TryPrepareForEditing(false, out reason), Is.True, reason);
			Assert.That(target.ResolveController(), Is.SameAs(first), "2回目で別の Controller を作った");
		}

		/// <summary>
		/// アセットですらない Controller は複製で救えない。黙って編集させずに断ること。
		/// </summary>
		[Test]
		public void Direct_Prepare_RefusesUnsavedController()
		{
			var inMemory = new AnimatorController();
			try
			{
				_avatar.GetComponent<UnityEngine.Animator>().runtimeAnimatorController = inMemory;

				string reason;
				Assert.That(Direct(_avatar).TryPrepareForEditing(false, out reason), Is.False);
				Assert.That(reason, Is.Not.Null.And.Not.Empty);
			}
			finally
			{
				Object.DestroyImmediate(inMemory);
			}
		}

		[Test]
		public void Direct_AfterEdit_DoesNotThrowOnNullReport()
		{
			Assert.DoesNotThrow(() => Direct(_avatar).OnAfterEdit(null));
		}

		[Test]
		public void Direct_WithoutAvatar_IsUnavailable()
		{
			string reason;
			Assert.That(Direct(null).IsAvailable(out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
			Assert.That(Direct(null).ResolveController(), Is.Null);
		}

		#endregion

		#region Writability / paths

		/// <summary>
		/// グラフの読み取り専用判定と Targeting の複製判断が同じ規則を見ていること。
		/// 片方だけが「編集できる」と思っていると、消える変更を作らせる。
		/// </summary>
		[Test]
		public void IsWritable_AgreesWithGraphSource()
		{
			var controller = CreateTempController();

			string reason;
			Assert.That(AcControllerAccess.IsWritable(controller, out reason), Is.True, reason);

			using (var source = new AcGraphSource())
			{
				source.SetController(controller);
				Assert.That(source.CanEdit, Is.True, source.ReadOnlyReason);
			}
		}

		[Test]
		public void IsWritable_RejectsUnsavedController()
		{
			var inMemory = new AnimatorController();
			try
			{
				string reason;
				Assert.That(AcControllerAccess.IsWritable(inMemory, out reason), Is.False);
				Assert.That(reason, Is.Not.Null.And.Not.Empty);
			}
			finally
			{
				Object.DestroyImmediate(inMemory);
			}
		}

		[Test]
		public void IsWritable_RejectsNull()
		{
			string reason;
			Assert.That(AcControllerAccess.IsWritable(null, out reason), Is.False);
			Assert.That(reason, Is.Not.Null.And.Not.Empty);
		}

		[Test]
		public void Sanitize_ReplacesPathSeparators()
		{
			Assert.That(FxSavePaths.Sanitize("a/b:c*d"), Is.EqualTo("a_b_c_d"));
			Assert.That(FxSavePaths.Sanitize(null), Is.EqualTo("Unnamed"));
			Assert.That(FxSavePaths.Sanitize("   "), Is.EqualTo("Unnamed"));
		}

		[Test]
		public void IsManaged_OnlyMatchesSaveFolder()
		{
			Assert.That(FxSavePaths.IsManaged(FxSavePaths.Root + "/X/Y.controller"), Is.True);
			Assert.That(FxSavePaths.IsManaged("Assets/Other/Y.controller"), Is.False);
			Assert.That(FxSavePaths.IsManaged(null), Is.False);
		}

		/// <summary>2回目の複製で1回目を黙って上書きしないこと。</summary>
		[Test]
		public void UniqueAssetPath_DoesNotCollide()
		{
			string folder = FxSavePaths.EnsureAvatarFolder(_avatar);

			string first = FxSavePaths.UniqueAssetPath(folder, "FXC_FX", ".controller");
			AnimatorController.CreateAnimatorControllerAtPath(first);

			string second = FxSavePaths.UniqueAssetPath(folder, "FXC_FX", ".controller");
			Assert.That(second, Is.Not.EqualTo(first));
		}

		#endregion

		private AnimatorController CreateTempController()
		{
			string folder = FxSavePaths.EnsureAvatarFolder(_avatar);
			string path = FxSavePaths.UniqueAssetPath(folder, "Temp", ".controller");
			return AnimatorController.CreateAnimatorControllerAtPath(path);
		}
	}
}
