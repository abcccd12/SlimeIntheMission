using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Utilities.Editor.PlayMode
{
    // 플레이모드들어가면 ui셀렉션때문에 에러남. 유니티버그인듯
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