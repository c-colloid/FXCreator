using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;

/// <summary>
/// AssetDatabaseの拡張クラス
/// </summary>
public static class AssetDatabaseExtension{

	/// <summary>
	/// アセットを上書きで作成する(metaデータはそのまま)
	/// </summary>
	public static void CreateAssetWithOverwrite(UnityEngine.Object asset, string exportPath){
		//アセットが存在しない場合はそのまま作成(metaファイルも新規作成)
		if (!File.Exists(exportPath)) {
			AssetDatabase.CreateAsset(asset, exportPath);
			return;
		}
    
		//仮ファイルを作るためのディレクトリを作成
		var fileName = Path.GetFileName(exportPath);
		var tmpDirectoryPath = Path.Combine(exportPath.Replace(fileName, ""), "tmpDirectory");
		Directory.CreateDirectory(tmpDirectoryPath);

		//仮ファイルを保存
		var tmpFilePath = Path.Combine(tmpDirectoryPath, fileName);
		AssetDatabase.CreateAsset(asset, tmpFilePath);
      
		//仮ファイルを既存のファイルに上書き(metaデータはそのまま)
		FileUtil.ReplaceFile(tmpFilePath, exportPath);
      
		//仮ディレクトリとファイルを削除
		AssetDatabase.DeleteAsset (tmpDirectoryPath);
      
		//データ変更をUnityに伝えるためインポートしなおし
		AssetDatabase.ImportAsset (exportPath);
	}
  
}