using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEditor.UIElements;
using colloid.FXCreater.Utility;
using CustomUI;

namespace colloid.FXCreater
{
	public class FolderPathEvents
	{
		static FolderPathMenuItem m_folderPathMenuItem = new FolderPathMenuItem();
		static BetterTextField m_folderPath_TextField;
		
		public static BetterTextField FolderPathTextField {
			get => m_folderPath_TextField;
			set {
			m_folderPath_TextField = value;
			return;
			}
		}
		
		public static void SetupEvents(VisualElement root)
		{
			// フォルダパス関連のイベントハンドラの設定
			// ...
			var folderPathTextField = m_folderPathMenuItem.FolderPathTextField
				= m_folderPath_TextField = root.Q<BetterTextField>("FolderPath");
			var resetFolderPathButton = root.Q<Button>("FolderPathReset");
			var getAnimationFolderButton = root.Q<Button>("AnimationsFolder");
			
			resetFolderPathButton.style.backgroundImage = (Texture2D)EditorGUIUtility.IconContent("clear_uielements").image;
			getAnimationFolderButton.style.backgroundImage = (Texture2D)EditorGUIUtility.Load("FolderOpened Icon");
			
			SetupFolderPathEvents(folderPathTextField, resetFolderPathButton, getAnimationFolderButton);
		}
		
		private static void SetupFolderPathEvents(BetterTextField folderPathTextField, Button resetButton, Button getFolderButton)
		{
			// フォルダパステキストフィールドのドラッグアンドドロップ機能の追加
			folderPathTextField.AddManipulator(new AddPathWithDragAndDrop());
		
			// フォルダパステキストフィールドの表示を更新
			folderPathTextField.OnValueChangedHandler += (string path) => RenderEvents.SetDropDownText(path);

			// フォルダパスリセットボタンのクリックイベントの設定
			resetButton.clicked += () => {
				folderPathTextField.value = FolderPathUtility.SetFolderPath("");
			};

			// アニメーションフォルダ取得ボタンのクリックイベントの設定
			getFolderButton.clicked += () => {
				folderPathTextField.value = FolderPathUtility.SetFolderPath(FolderPathUtility.GetFolderPath());
			};
		}
	}
}
