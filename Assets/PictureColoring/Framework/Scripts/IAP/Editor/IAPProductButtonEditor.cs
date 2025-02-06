using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace BizzyBeeGames
{
	[CustomEditor(typeof(IAPProductButton))]
	public class IAPProductButtonEditor : Editor
	{
		#region Public Methods

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			EditorGUILayout.Space();

			DrawProductIdsDropdown();

			EditorGUILayout.PropertyField(serializedObject.FindProperty("titleText"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("descriptionText"));
			EditorGUILayout.PropertyField(serializedObject.FindProperty("priceText"));

			EditorGUILayout.Space();

			serializedObject.ApplyModifiedProperties();
		}

		#endregion

		#region Private Methods

		private void DrawProductIdsDropdown()
		{
			SerializedProperty productIdProp = serializedObject.FindProperty("productId");

			int				selectedIndex	= 0;
			List<string>	productIds		= new List<string>();

			if (!IAPSettings.IsIAPEnabled)
			{
				EditorGUILayout.HelpBox("IAP is not enabled. Please open the IAP Settings window and enable IAP.", MessageType.Warning);

				if (GUILayout.Button("Open IAP Settings Window"))
				{
					IAPSettingsWindow.Open();
				}
			}

			for (int i = 0; i < IAPSettings.Instance.productInfos.Count; i++)
			{
				string productId = IAPSettings.Instance.productInfos[i].productId;

				if (!string.IsNullOrEmpty(productId))
				{
					productIds.Add(productId);

					if (productIdProp.stringValue == productId)
					{
						selectedIndex = productIds.Count - 1;
					}
				}
			}

			if (productIds.Count == 0)
			{
				EditorGUILayout.HelpBox("There are no product ids set in the IAP Settings. Open the IAP Settings window to add your product ids.", MessageType.Warning);

				if (GUILayout.Button("Open IAP Settings Window"))
				{
					IAPSettingsWindow.Open();
				}

				EditorGUILayout.Popup("Product Id", selectedIndex, productIds.ToArray());
			}
			else
			{
				selectedIndex = EditorGUILayout.Popup("Product Id", selectedIndex, productIds.ToArray());

				productIdProp.stringValue = productIds[selectedIndex];
			}

			GUI.enabled = true;
		}

		#endregion
	}
}
