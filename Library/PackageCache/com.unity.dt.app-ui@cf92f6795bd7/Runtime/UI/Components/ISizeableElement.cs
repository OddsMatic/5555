using System;

namespace Unity.AppUI.UI
{
    /// <summary>
    /// Interface used on UI elements which handle user-defined sizing.
    /// </summary>
    public interface ISizeableElement
    {
        /// <summary>
        /// The current size of the UI element.
        /// </summary>
        Size size { get; set; }
    }
}
