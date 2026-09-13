using System;
using System.Linq;
using NUnit.Framework;

namespace colloid.FXCreator.Tests
{
	/// <summary>
	/// Phase 0 の整理（名前空間の統一）が崩れていないことを守る回帰テスト。
	/// 併せて、テストアセンブリから FXCreator アセンブリを参照できていることも検証する。
	/// </summary>
	public class Phase0LayoutTests
	{
		private static System.Reflection.Assembly FxCreatorAssembly
			=> typeof(colloid.FXCreator.Utility.GetDefaultUSS).Assembly;

		[Test]
		public void UtilityTypesLiveInUtilityNamespace()
		{
			Assert.That(typeof(colloid.FXCreator.Utility.GetDefaultUSS).Namespace,
				Is.EqualTo("colloid.FXCreator.Utility"));
			Assert.That(typeof(colloid.FXCreator.Utility.PreviewScene).Namespace,
				Is.EqualTo("colloid.FXCreator.Utility"));
		}

		[Test]
		public void UiTypesLiveInUiNamespace()
		{
			Assert.That(typeof(colloid.FXCreator.UI.Shadow).Namespace,
				Is.EqualTo("colloid.FXCreator.UI"));
			Assert.That(typeof(colloid.FXCreator.UI.BetterTextField).Namespace,
				Is.EqualTo("colloid.FXCreator.UI"));
		}

		[Test]
		public void RenderTypesLiveInRenderNamespace()
		{
			Assert.That(typeof(colloid.FXCreator.Render.SelectObjectOutline).Namespace,
				Is.EqualTo("colloid.FXCreator.Render"));
		}

		/// <summary>
		/// グローバル名前空間に型を置かない、が Phase 0 の主目的。
		/// コンパイラ生成型（&lt;PrivateImplementationDetails&gt; 等）は対象外。
		/// </summary>
		[Test]
		public void NoTypesRemainInTheGlobalNamespace()
		{
			string[] globals = FxCreatorAssembly.GetTypes()
				.Where(t => !t.IsNested)
				.Where(t => string.IsNullOrEmpty(t.Namespace))
				.Where(t => !t.Name.StartsWith("<", StringComparison.Ordinal))
				.Select(t => t.Name)
				.OrderBy(n => n, StringComparer.Ordinal)
				.ToArray();

			Assert.That(globals, Is.Empty,
				"グローバル名前空間に型が残っています: " + string.Join(", ", globals));
		}

		/// <summary>
		/// 外部の名前空間を間借りしていない（同梱した Haï~ のコードだけは例外）。
		/// </summary>
		[Test]
		public void OnlyOwnedNamespacesArePopulated()
		{
			string[] foreign = FxCreatorAssembly.GetTypes()
				.Where(t => !t.IsNested)
				.Select(t => t.Namespace)
				.Where(ns => !string.IsNullOrEmpty(ns))
				.Distinct()
				.Where(ns => !ns.StartsWith("colloid.FXCreator", StringComparison.Ordinal))
				.Where(ns => !ns.StartsWith("Hai.", StringComparison.Ordinal))
				.OrderBy(ns => ns, StringComparer.Ordinal)
				.ToArray();

			Assert.That(foreign, Is.Empty,
				"他者の名前空間に型を置いています: " + string.Join(", ", foreign));
		}
	}
}
