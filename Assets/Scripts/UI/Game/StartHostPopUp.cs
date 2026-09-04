using System.Collections.Generic;
using Unity.MP_FPS.Match;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.MP_FPS.Client
{
    [RequireComponent(typeof(UIDocument))]
    public class StartHostPopUp : MonoBehaviour
    {
        static class UIElementNames
        {
            public const string PortInputField = "PortField";
            public const string MatchFormatField = "MatchFormatField";
            public const string StartButton = "StartButton";
            public const string CancelButton = "CancelButton";
        }

        // The formats in the order they are offered, so that the dropdown's index is the
        // choice itself and no name has to be parsed back into an enum.
        static readonly MatchFormat[] k_Formats =
        {
            MatchFormat.OneRound,
            MatchFormat.FT3,
            MatchFormat.FT5
        };

        VisualElement m_StartHostPopUp;
        DropdownField m_MatchFormatField;
        Button m_StartButton;
        Button m_CancelButton;

        void OnEnable()
        {
            m_StartHostPopUp = GetComponent<UIDocument>().rootVisualElement;

            m_StartHostPopUp.SetBinding("style.display", new DataBinding
            {
                dataSource = GameSettings.Instance,
                dataSourcePath = new PropertyPath(GameSettings.StartHostStylePropertyName),
                bindingMode = BindingMode.ToTarget,
            });

            var portInputField = m_StartHostPopUp.Q<TextField>(UIElementNames.PortInputField);
            portInputField.SetBinding("value", new DataBinding
            {
                dataSource = ConnectionSettings.Instance,
                dataSourcePath = new PropertyPath(nameof(ConnectionSettings.Port)),
                bindingMode = BindingMode.TwoWay,
            });

            SetUpMatchFormatField();

            m_StartButton = m_StartHostPopUp.Q<Button>(UIElementNames.StartButton);
            m_StartButton.clicked += OnStartPressed;
            m_StartButton.SetBinding("enabledSelf", new DataBinding
            {
                dataSource = ConnectionSettings.Instance,
                dataSourcePath = new PropertyPath(nameof(ConnectionSettings.IsNetworkEndpointValid)),
                bindingMode = BindingMode.ToTarget,
            });

            m_CancelButton = m_StartHostPopUp.Q<Button>(UIElementNames.CancelButton);
            m_CancelButton.clicked += OnCancelPressed;
        }

        /// <summary>
        /// Fills the format dropdown and records the host's choice as they make it.
        ///
        /// Recorded on change rather than when Start is pressed, so that cancelling and
        /// coming back shows the format they had picked instead of resetting to the top of
        /// the list.
        /// </summary>
        void SetUpMatchFormatField()
        {
            m_MatchFormatField = m_StartHostPopUp.Q<DropdownField>(UIElementNames.MatchFormatField);

            // A popup laid out before this field existed is not worth an exception: the
            // host simply gets whatever format the MatchManager prefab is set to.
            if (m_MatchFormatField == null)
            {
                return;
            }

            m_MatchFormatField.choices = new List<string>
            {
                "One Round",
                "First to 3",
                "First to 5"
            };

            // Starts on whatever was chosen last, which is the format the match would run
            // with if Start were pressed right now.
            m_MatchFormatField.index = System.Array.IndexOf(k_Formats, MatchSettings.SelectedFormat);
            if (m_MatchFormatField.index < 0)
            {
                m_MatchFormatField.index = 0;
            }

            m_MatchFormatField.RegisterValueChangedCallback(OnMatchFormatChanged);
        }

        void OnMatchFormatChanged(ChangeEvent<string> _)
        {
            int index = m_MatchFormatField.index;

            if (index >= 0 && index < k_Formats.Length)
            {
                MatchSettings.Select(k_Formats[index]);
            }
        }

        void OnDisable()
        {
            m_MatchFormatField?.UnregisterValueChangedCallback(OnMatchFormatChanged);
            m_StartButton.clicked -= OnStartPressed;
            m_CancelButton.clicked -= OnCancelPressed;
        }

        static void OnStartPressed() => GameSettings.Instance.CancellableUserInputPopUp.SetResult();

        static void OnCancelPressed()
        {
            GameSettings.Instance.CancellableUserInputPopUp.SetCanceled();
            ConnectionSettings.Instance.IPAddress = ConnectionSettings.DefaultServerAddress;
            ConnectionSettings.Instance.Port = ConnectionSettings.DefaultServerPort.ToString();
        }
    }
}
