using System;
using System.Reflection;
using Unity.VisualScripting;
using UnityEditor;
using UnityEngine;

namespace Editor
{
    public class PlayModeGameSizeEditor : EditorWindow
    {
        [MenuItem("Tools/编辑Game窗口运行时大小")]
        public static void Open()
        {
            GetWindow<PlayModeGameSizeEditor>();
        }

        private Vector2Int size;
        private bool set;

        private void OnEnable()
        {
            titleContent.text = "编辑Game窗口运行时大小";
            size = new Vector2Int(PlayerPrefs.GetInt("PlayModeGameSizeEditorWidth", 1080),
                PlayerPrefs.GetInt("PlayModeGameSizeEditorHeight", 1920));
            set = PlayerPrefs.GetInt("PlayModeGameSizeEditorSet", 0) != 0;
        }

        private void OnGUI()
        {
            set = GUILayout.Toggle(set, "是否在运行时修改Game窗口大小");
            size = EditorGUILayout.Vector2IntField("大小", size);

            if (GUILayout.Button("确定"))
            {
                PlayerPrefs.SetInt("PlayModeGameSizeEditorSet", set ? 1 : 0);
                PlayerPrefs.SetInt("PlayModeGameSizeEditorWidth", size.x);
                PlayerPrefs.SetInt("PlayModeGameSizeEditorHeight", size.y);
                Close();
            }
        }
    }

    [InitializeOnLoad]
    public static class GameViewTool
    {
        private static Type _typeGameView;
        private static MethodInfo _methodSizeSelectionCallback;
        private static PropertyInfo _propCurrentSizeGroupType;
        private static PropertyInfo _propSelectedSizeIndex;
        private static Type _typeGameViewSizes;
        private static Type _singleTypeGameViewSizes;
        private static object _gameViewSizes;
        private static MethodInfo _getGroup;
        private static MethodInfo _totalFunc;
        private static MethodInfo _getSizeFunc;
        private static PropertyInfo _propWidth;
        private static PropertyInfo _propHeight;
        private static PropertyInfo _sizeTypeEnum;
        private static MethodInfo _sizeSelectionCallback;
        private static Assembly _assembly;
        private static MethodInfo _addCustomSize;
        private static object _customSizeType;
        private static bool _inited;

        [UnityEditor.Callbacks.DidReloadScripts]
        public static void OnScriptLoad()
        {
            if (PlayerPrefs.GetInt("PlayModeGameSizeEditorSet", 0) != 1)
            {
                return;
            }
            EditorApplication.delayCall -= OnLoad;
            EditorApplication.delayCall += OnLoad;
        }

        static GameViewTool()
        {
            if (PlayerPrefs.GetInt("PlayModeGameSizeEditorSet", 0) != 1)
            {
                return;
            }

            OnEditorStartUp();
        }

        private static void OnLoad()
        {
            if (!EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Restore();
            }
        }

        static void OnEditorStartUp()
        {
            EditorApplication.playModeStateChanged -= EditorApplication_playModeStateChanged;
            EditorApplication.playModeStateChanged += EditorApplication_playModeStateChanged;

            if (_inited)
            {
                return;
            }

            _inited = true;

            _assembly = Assembly.GetAssembly(typeof(EditorWindow));
            _typeGameView = _assembly.GetType("UnityEditor.GameView");
            _methodSizeSelectionCallback = _typeGameView.GetMethod("SizeSelectionCallback",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _propCurrentSizeGroupType = _typeGameView.GetProperty("currentSizeGroupType",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Static);
            _propSelectedSizeIndex =
                _typeGameView.GetProperty("selectedSizeIndex", BindingFlags.Instance | BindingFlags.NonPublic);
            _typeGameViewSizes = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSizes");
            _singleTypeGameViewSizes = typeof(ScriptableSingleton<>).MakeGenericType(_typeGameViewSizes);
            var instanceProp = _singleTypeGameViewSizes.GetProperty("instance");
            _gameViewSizes = instanceProp.GetValue(null);
            _getGroup = _typeGameViewSizes.GetMethod("GetGroup");
            var type = _assembly.GetType("UnityEditor.GameViewSizeGroup");
            _totalFunc = type.GetMethod("GetTotalCount");
            _getSizeFunc = type.GetMethod("GetGameViewSize");
            var sizeType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameViewSize");
            _propWidth = sizeType.GetProperty("width");
            _propHeight = sizeType.GetProperty("height");
            _sizeTypeEnum = sizeType.GetProperty("sizeType");
            _sizeSelectionCallback = _typeGameView.GetMethod("SizeSelectionCallback",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            _addCustomSize = type.GetMethod("AddCustomSize");

            type = _assembly.GetType("UnityEditor.GameViewSizeType");
            _customSizeType = Enum.Parse(type, "FixedResolution");
        }

        private static void EditorApplication_playModeStateChanged(PlayModeStateChange obj)
        {
            if (PlayerPrefs.GetInt("PlayModeGameSizeEditorSet", 0) != 1)
            {
                return;
            }

            //ExitingPlayMode
            if (obj == PlayModeStateChange.ExitingPlayMode)
            {
                Restore();
                return;
            }

            //EnteredPlayMode
            if (obj == PlayModeStateChange.EnteredPlayMode)
            {
                OnPlay();
            }
        }

        private static void Restore()
        {
            var win = EditorWindow.GetWindow(_typeGameView);
            if (win == null)
            {
                return;
            }

            var index = PlayerPrefs.GetInt("GameViewToolSizeIndex");
            if (index < 0)
            {
                return;
            }

            _methodSizeSelectionCallback.Invoke(win, new object[] { index, null });
            PlayerPrefs.SetInt("GameViewToolSizeIndex", -1);
        }

        private static void OnPlay()
        {
            var win = EditorWindow.GetWindow(_typeGameView);
            if (win == null)
            {
                return;
            }

            PlayerPrefs.SetInt("GameViewToolSizeIndex", (int)_propSelectedSizeIndex.GetValue(win));
            PlayerPrefs.Save();

            var group = _getGroup.Invoke(_gameViewSizes, new object[] { _propCurrentSizeGroupType.GetValue(win) });
            var total = (int)_totalFunc.Invoke(group, new object[] { });
            var index = -1;

            var targetheight = PlayerPrefs.GetInt("PlayModeGameSizeEditorHeight", 1920);
            var targetwidth = PlayerPrefs.GetInt("PlayModeGameSizeEditorWidth", 1080);

            for (int i = 0; i < total; i++)
            {
                var size = _getSizeFunc.Invoke(group, new object[] { i });
                var width = (int)_propWidth.GetValue(size);
                var height = (int)_propHeight.GetValue(size);
                var sizeSizeType = (int)_sizeTypeEnum.GetValue(size);
                if (sizeSizeType == 1 && width == targetwidth && height == targetheight)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                var param = new object[4];
                param[0] = _customSizeType;
                param[1] = targetwidth;
                param[2] = targetheight;
                param[3] = targetwidth + "x" + targetheight;
                var size = _assembly.CreateInstance("UnityEditor.GameViewSize", true, BindingFlags.Default, null,
                    param, null, null);
                _addCustomSize.Invoke(group, new[] { size });
                index = total;
            }

            _sizeSelectionCallback.Invoke(win, new object[] { index, null });
        }
    }
}