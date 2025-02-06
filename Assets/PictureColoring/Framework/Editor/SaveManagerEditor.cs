using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BizzyBeeGames
{
	[CustomEditor(typeof(SaveManager))]
	public class SaveManagerEditor : Editor
	{
		#region Public Methods

		public override void OnInspectorGUI()
		{
			base.OnInspectorGUI();

			if (GUILayout.Button("Delete Save File"))
			{
				DeleteSaveData();
			}

			if (GUILayout.Button("Print Save Data To Console"))
			{
				PrintSaveFileToConsole();
			}
		}

		#endregion

		#region Menu Items

		[UnityEditor.MenuItem("Tools/Bizzy Bee Games/Delete Save Data", priority = 0)]
		public static void DeleteSaveData()
		{
			if (!System.IO.File.Exists(SaveManager.Instance.SaveFilePath))
			{
				UnityEditor.EditorUtility.DisplayDialog("Delete Save File", "There is no save file.", "Ok");

				return;
			}

			bool delete = UnityEditor.EditorUtility.DisplayDialog("Delete Save File", "Delete the save file located at " + SaveManager.Instance.SaveFilePath, "Yes", "No");

			if (delete)
			{
				System.IO.File.Delete(SaveManager.Instance.SaveFilePath);

				Debug.Log("Save file deleted");
			}
		}

		[UnityEditor.MenuItem("Tools/Bizzy Bee Games/Print Save Data To Console", priority = 1)] 
		public static void PrintSaveFileToConsole()
		{
			SaveManager.PrintSaveDataToConsole();
		}

		#endregion
	}
}
