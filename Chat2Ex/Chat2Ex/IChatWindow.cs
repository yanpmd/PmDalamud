using System.Numerics;
using Chat2Ex.GameFunctions.Types;

namespace Chat2Ex;

/// <summary>
///     State that the hide-state helper needs from a chat window.
/// </summary>
public interface IChatWindow
{
    Vector2 LastWindowPos { get; set; }
    Vector2 LastWindowSize { get; set; }
    HideState CurrentHideState { get; set; }
}
