using System;
using System.Collections.Generic;

namespace colloid.FXCreator.Targeting
{
	/// <summary>
	/// VRChat が最初から供給するアバターパラメータ。
	///
	/// これらを Expression Parameters や <c>ModularAvatarParameters</c> に宣言すると、
	/// 使いもしない同期コストを取られるうえ、VRChat 側の供給値と衝突する。
	/// Controller のパラメータを機械的に同期するときは必ず除外する。
	///
	/// <c>jp.colloid.vrc-expression-params-extension</c> が同じ一覧を持っているが、
	/// 別 VPM パッケージへの asmdef 参照は FX Creator の配布時にそのパッケージを
	/// <b>必須化</b>してしまう（§6.4 の結論）。共有するなら、両方が依存する
	/// 小さなパッケージへ移すのが筋で、それは v0.2 のパッケージ化と一緒にやる。
	/// </summary>
	public static class VrcBuiltInParameters
	{
		private static readonly HashSet<string> _names = new HashSet<string>(StringComparer.Ordinal)
		{
			"IsLocal",
			"PreviewMode",
			"Viseme",
			"Voice",
			"GestureLeft",
			"GestureRight",
			"GestureLeftWeight",
			"GestureRightWeight",
			"AngularY",
			"VelocityX",
			"VelocityY",
			"VelocityZ",
			"VelocityMagnitude",
			"Upright",
			"Grounded",
			"Seated",
			"AFK",
			"TrackingType",
			"VRMode",
			"MuteSelf",
			"InStation",
			"Earmuffs",
			"IsOnFriendsList",
			"AvatarVersion",
			"ScaleModified",
			"ScaleFactor",
			"ScaleFactorInverse",
			"EyeHeightAsMeters",
			"EyeHeightAsPercent",
		};

		public static bool Contains(string name)
		{
			return !string.IsNullOrEmpty(name) && _names.Contains(name);
		}
	}
}
