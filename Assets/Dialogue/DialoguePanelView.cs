using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Dialogue
{
    /// <summary>
    /// Dialogue UI + controller + input for Tier 1.
    /// Attach to a panel in your Canvas and wire the fields.
    /// </summary>
    public sealed class DialoguePanelView : MonoBehaviour
    {
        [Header("Root")]
        [SerializeField] private GameObject _root;

        [Header("Labels (TMP)")]
        [SerializeField] private TextMeshProUGUI _speakerLabel;
        [SerializeField] private TextMeshProUGUI _bodyLabel;

        [Header("Choices")]
        [SerializeField] private Transform _choicesContainer;
        [SerializeField] private Button _choiceButtonPrefab;

        [Header("Continue Button (optional)")]
        [SerializeField] private Button _continueButton;

        private readonly List<Button> _choiceButtons = new List<Button>();

        private DialogueSession _session;
        private bool _hasChoices;
        private int _choiceCount;

        public bool HasActiveSession => _session != null && _session.IsActive;

        public event Action SessionStarted;
        public event Action SessionEnded;

        void Awake()
        {
            Debug.Log("[DialoguePanelView] Awake", this);

            if (_root == null)
            {
                // Sensible default: root is this GameObject if nothing is wired
                _root = gameObject;
            }
        }

        void OnEnable()
        {
            Debug.Log("[DialoguePanelView] OnEnable", this);
        }

        void OnDisable()
        {
            Debug.Log("[DialoguePanelView] OnDisable", this);
            TearDownSession();
        }

        // --------------------------------------------------
        // PUBLIC API (called by DialogueState)
        // --------------------------------------------------

        public void StartConversation(DialogueGraphAsset asset)
        {
            if (asset == null)
            {
                Debug.LogWarning("[DialoguePanelView] StartConversation called with null asset.", this);
                return;
            }

            Debug.Log($"[DialoguePanelView] StartConversation with graph '{asset.name}'", this);

            TearDownSession();

            DialogueGraph graph;
            try
            {
                graph = asset.BuildGraph();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialoguePanelView] BuildGraph failed: {ex.Message}\n{ex}", this);
                return;
            }

            _session = new DialogueSession(graph);
            _session.NodeEntered += OnNodeEntered;
            _session.SessionEnded += OnSessionEndedInternal;

            try
            {
                _session.Start();
                Debug.Log("[DialoguePanelView] Session started.", this);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DialoguePanelView] Session.Start failed: {ex.Message}\n{ex}", this);
                TearDownSession();
                return;
            }

            SessionStarted?.Invoke();
        }

        public void Advance()
        {
            if (!HasActiveSession)
                return;

            _session.Advance();
        }

        public void ChooseChoice(int index)
        {
            if (!HasActiveSession)
                return;

            _session.ChooseChoice(index);
        }

        public void Cancel()
        {
            if (!HasActiveSession)
                return;

            _session.Cancel();
        }

        // --------------------------------------------------
        // INPUT (kept here for minimal boilerplate)
        // --------------------------------------------------

        void Update()
        {
            if (!HasActiveSession)
                return;

            if (_root == null || !_root.activeInHierarchy)
                return;

            // Cancel / close
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Cancel();
                return;
            }

            if (_hasChoices)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1) && _choiceCount >= 1)
                    ChooseChoice(0);
                else if (Input.GetKeyDown(KeyCode.Alpha2) && _choiceCount >= 2)
                    ChooseChoice(1);
                else if (Input.GetKeyDown(KeyCode.Alpha3) && _choiceCount >= 3)
                    ChooseChoice(2);
                else if (Input.GetKeyDown(KeyCode.Alpha4) && _choiceCount >= 4)
                    ChooseChoice(3);

                return;
            }

            // No choices → advance on confirm keys
            if (Input.GetKeyDown(KeyCode.Space) ||
                Input.GetKeyDown(KeyCode.Return) ||
                Input.GetMouseButtonDown(0))
            {
                Advance();
            }
        }

        public void OnAdvancePressed()
        {
            Advance();
        }

        // --------------------------------------------------
        // SESSION EVENT HANDLERS
        // --------------------------------------------------

        void OnNodeEntered(DialogueNode node)
        {
            if (node == null)
            {
                Debug.LogWarning("[DialoguePanelView] OnNodeEntered got null node.", this);
                Hide();
                return;
            }

            Debug.Log($"[DialoguePanelView] OnNodeEntered node '{node.Id}'", this);

            Show();

            if (_speakerLabel != null)
                _speakerLabel.text = string.IsNullOrEmpty(node.SpeakerId) ? string.Empty : node.SpeakerId;

            if (_bodyLabel != null)
                _bodyLabel.text = node.Text ?? string.Empty;

            BuildChoices(node);
        }

        void OnSessionEndedInternal()
        {
            Debug.Log("[DialoguePanelView] Session ended.", this);
            Hide();
            SessionEnded?.Invoke();
        }

        // --------------------------------------------------
        // UI HELPERS
        // --------------------------------------------------

        void Show()
        {
            if (_root != null)
            {
                Debug.Log("[DialoguePanelView] Show() root.SetActive(true)", this);
                _root.SetActive(true);
            }
        }

        void Hide()
        {
            if (_root != null)
            {
                Debug.Log("[DialoguePanelView] Hide() root.SetActive(false)", this);
                _root.SetActive(false);
            }

            ClearChoices();
            _hasChoices = false;
            _choiceCount = 0;
        }

        void BuildChoices(DialogueNode node)
        {
            ClearChoices();

            if (!node.HasChoices)
            {
                _hasChoices = false;
                _choiceCount = 0;

                if (_continueButton != null)
                    _continueButton.gameObject.SetActive(true);

                return;
            }

            if (_continueButton != null)
                _continueButton.gameObject.SetActive(false);

            _hasChoices = true;
            _choiceCount = node.Choices.Count;

            for (int i = 0; i < node.Choices.Count; i++)
            {
                int index = i;
                var choice = node.Choices[i];

                var button = Instantiate(_choiceButtonPrefab, _choicesContainer);
                _choiceButtons.Add(button);

                var label = button.GetComponentInChildren<TextMeshProUGUI>();
                if (label != null)
                    label.text = choice.Text;

                button.onClick.AddListener(() => ChooseChoice(index));
            }
        }

        void ClearChoices()
        {
            foreach (var button in _choiceButtons)
            {
                if (button != null)
                    Destroy(button.gameObject);
            }

            _choiceButtons.Clear();
        }

        void TearDownSession()
        {
            if (_session == null)
                return;

            _session.NodeEntered -= OnNodeEntered;
            _session.SessionEnded -= OnSessionEndedInternal;
            _session = null;
        }
    }
}
