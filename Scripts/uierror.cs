using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Utilities.Editor.PlayMode
{
    /// <summary>
    /// Temporary workaround for a UnityEditor bug affecting Unity 6.0.0f4 through 6.0.0f6.
    /// </summary>
    [InitializeOnLoad]
    public static class UiGraphicSelectionPlayModeGuard
    {
        static UiGraphicSelectionPlayModeGuard()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.focusChanged -= OnEditorFocusChanged;
            EditorApplication.focusChanged += OnEditorFocusChanged;
        }

        private static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.ExitingEditMode)
            {
                return;
            }

            TryDeselectGraphicSelection();
        }

        private static void OnEditorFocusChanged(bool hasFocus)
        {
            if (hasFocus)
            {
                return;
            }

            TryDeselectGraphicSelection();
        }

        private static void TryDeselectGraphicSelection()
        {
            GameObject selectedGameObject = Selection.activeGameObject;
            if (selectedGameObject == null)
            {
                return;
            }

            if (selectedGameObject.GetComponent<Graphic>() == null)
            {
                return;
            }

            Selection.objects = Array.Empty<UnityEngine.Object>();
        }
    }
}