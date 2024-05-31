using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CustomUI;
using UnityEditor;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace colloid.FXCreator.Utility
{
public sealed class FolderPathUtility
{
	private FolderPathUtility(){}

#region Variable
	private static string m_folderPath = "";
	private static string m_assetsRootPath = "Assets/";
#endregion

#region Method
	public static string SetFolderPath(string newpath)
	{
		return newpath;
	}

	public static string GetFolderPath()
	{
		var FolderPath = string.IsNullOrEmpty(m_folderPath) ? m_assetsRootPath : m_folderPath;
		FolderPath = EditorUtility.OpenFolderPanel("Animations Folder", FolderPath, "");
	
		if (FolderPath == string.Empty) return m_folderPath;
	
		var split = Regex.Split(FolderPath,m_assetsRootPath);
	
		if (split.Length == 1) return m_folderPath = m_assetsRootPath;
	
		FolderPath = split[split.Length - 1];
		
		return m_folderPath = m_assetsRootPath + FolderPath;
	}
#endregion
}
}
