using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Threading.Tasks;
using colloid.FXCreator.UI;

namespace colloid.FXCreator.Utility{
public class FolderPathMenuItem
{
	#region Propaty
	public BetterTextField FolderPathTextField{
			//get;
			set {
				m_folderPath_TextField = value;
			}}
	#endregion
	
	private static BetterTextField m_folderPath_TextField;
	
	public FolderPathMenuItem(){}
	
	#region Methods
	[MenuItem("Assets/FX Creator/Select This Folder", false, 1050)]
	public static string GetFolderPathByProject()
	{
		int ID = Selection.activeInstanceID;
		string path = AssetDatabase.GetAssetPath(ID);
	
		SetFolderPathByProject(path);
		return path;
	}
	//MenuItemの対象がアセットの場合メニューを選択不可にする
	[MenuItem("Assets/FX Creator/Select This Folder", true)]
	public static bool ShowGetFolderPathByProject()
	{
		int ID = Selection.activeInstanceID;
		string path = AssetDatabase.GetAssetPath(ID);
		return AssetDatabase.IsValidFolder(path);
	}

	async static void SetFolderPathByProject(string path)
	{
		await Task.Delay(10);
		m_folderPath_TextField.value = FolderPathUtility.SetFolderPath(path);
	}
	#endregion
}
}