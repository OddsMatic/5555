using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BizzyBeeGames.PictureColoring
{
	public class MainScreenSubNavButton : MonoBehaviour
	{
		#region Inspector Variables

		[SerializeField] private Image	buttonIcon		= null;
		[SerializeField] private Text	buttonText		= null;
		[SerializeField] private Color	normalColor		= Color.white;
		[SerializeField] private Color	selectedColor	= Color.white;

		#endregion

		#region Unity Methods

		public void SetSelected(bool isSelected)
		{
			buttonIcon.color = isSelected ? selectedColor : normalColor;
			buttonText.color = isSelected ? selectedColor : normalColor;
		}

		#endregion
	}
}
