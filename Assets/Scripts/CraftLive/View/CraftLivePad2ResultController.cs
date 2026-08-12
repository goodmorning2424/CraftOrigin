using UnityEngine;
using UnityEngine.Events;

namespace CraftOrigin.CraftLive
{
    public sealed class CraftLivePad2ResultController :
        MonoBehaviour
    {
        [SerializeField] private CraftLiveSession session;
        [SerializeField] private CraftLivePad2Bindings bindings;
        [SerializeField] private bool createFallbackVisuals = true;
        [SerializeField] private UnityEvent<bool> onResultVisible;
        [SerializeField] private UnityEvent<string> onWeaponNameChanged;
        [SerializeField] private UnityEvent<string> onRankChanged;
        [SerializeField] private UnityEvent<float> onAttackChanged;
        [SerializeField] private UnityEvent<float> onDefenseChanged;
        [SerializeField] private UnityEvent<float> onEvasionChanged;
        [SerializeField] private UnityEvent<string> onAttributeChanged;
        [SerializeField] private UnityEvent<string> onSkillChanged;
        [SerializeField] private UnityEvent<int> onHistoryCountChanged;
        [SerializeField] private UnityEvent<string> onWeaponCodeChanged;

        private GameObject generatedPanel;
        private int displayedResultSerial = -1;
        private int displayedHistoryCount = -1;
        private CraftLiveSessionPhase displayedPhase =
            (CraftLiveSessionPhase)(-1);

        private void Awake()
        {
            ResolveReferences();
        }

        private void OnEnable()
        {
            ResolveReferences();
            if (session != null)
            {
                session.StateChanged -= Refresh;
                session.StateChanged += Refresh;
                Refresh(session.State);
            }
        }

        private void OnDisable()
        {
            if (session != null)
            {
                session.StateChanged -= Refresh;
            }
        }

        public void Configure(CraftLivePad2Bindings targetBindings)
        {
            bindings = targetBindings;
            ResolveReferences();
        }

        public void BeginNextWeapon()
        {
            session?.BeginNextWeapon();
        }

        public void SelectFinalWeapon(int resultSerial)
        {
            session?.SelectFinalWeapon(resultSerial);
        }

        private void ResolveReferences()
        {
            if (session == null)
            {
                session = FindAnyObjectByType<CraftLiveSession>();
            }

            if (bindings == null)
            {
                bindings = GetComponent<CraftLivePad2Bindings>();
            }
        }

        private void Refresh(CraftLiveRoomState state)
        {
            if (state == null)
            {
                return;
            }

            int historyCount = state.completedWeapons != null
                ? state.completedWeapons.Count
                : 0;
            bool changed =
                displayedPhase != state.sessionPhase ||
                displayedResultSerial !=
                    state.result.resultSerial ||
                displayedHistoryCount != historyCount;
            displayedPhase = state.sessionPhase;
            displayedResultSerial =
                state.result.resultSerial;
            displayedHistoryCount = historyCount;

            bool visible =
                state.craft.status ==
                    CraftLiveCraftStatus.Complete ||
                state.sessionPhase !=
                    CraftLiveSessionPhase.Playing;
            onResultVisible?.Invoke(visible);
            PublishResult(state.result);
            onHistoryCountChanged?.Invoke(historyCount);
            onWeaponCodeChanged?.Invoke(
                state.finalWeaponCode ?? string.Empty);
            if (changed && createFallbackVisuals)
            {
                RebuildFallback(state);
            }
        }

        private void PublishResult(CraftLiveResultState result)
        {
            if (result == null)
            {
                return;
            }

            onWeaponNameChanged?.Invoke(result.weaponName);
            onRankChanged?.Invoke(result.rank);
            onAttackChanged?.Invoke(result.stats.attackRate);
            onDefenseChanged?.Invoke(result.stats.defenseRate);
            onEvasionChanged?.Invoke(result.stats.evasionRate);
            onAttributeChanged?.Invoke(result.attributeName);
            onSkillChanged?.Invoke(result.skillName);
        }

        private void RebuildFallback(CraftLiveRoomState state)
        {
            DestroySafely(generatedPanel);
            generatedPanel = null;
            if (bindings == null ||
                bindings.ResultHologramRoot == null)
            {
                return;
            }

            if (state.sessionPhase ==
                CraftLiveSessionPhase.FinalSelection)
            {
                BuildFinalSelection(state);
                return;
            }

            if (state.sessionPhase ==
                CraftLiveSessionPhase.Finished)
            {
                BuildCodePanel(state);
                return;
            }

            if (state.craft.status ==
                CraftLiveCraftStatus.Complete)
            {
                BuildResultPanel(state);
            }
        }

        private void BuildResultPanel(CraftLiveRoomState state)
        {
            generatedPanel = CreatePanel("Generated_ResultPanel");
            CraftLiveResultState result = state.result;
            CreateText(
                generatedPanel.transform,
                "CompletionKicker",
                "—  MASTER FORGE  —",
                new Vector3(0f, 3.05f, -0.72f),
                0.027f,
                CraftLiveForgeUITheme.Brass);
            CreateText(
                generatedPanel.transform,
                "CompletionTitle",
                "鍛造完了",
                new Vector3(0f, 2.53f, -0.72f),
                0.069f);
            CreateText(
                generatedPanel.transform,
                "WeaponName",
                EmptyFallback(result.weaponName),
                new Vector3(-0.25f, 1.65f, -0.72f),
                0.058f);

            CreateRankBadge(
                generatedPanel.transform,
                result.rank,
                new Vector3(2.72f, 1.67f, -0.52f));
            CreateStatPlate(
                generatedPanel.transform,
                "AttackStat",
                "攻撃",
                result.stats.attackRate,
                new Vector3(-2.08f, 0.35f, -0.5f),
                CraftLiveForgeUITheme.Ember);
            CreateStatPlate(
                generatedPanel.transform,
                "DefenseStat",
                "防御",
                result.stats.defenseRate,
                new Vector3(0f, 0.35f, -0.5f),
                new Color(0.28f, 0.48f, 0.62f));
            CreateStatPlate(
                generatedPanel.transform,
                "EvasionStat",
                "回避",
                result.stats.evasionRate,
                new Vector3(2.08f, 0.35f, -0.5f),
                new Color(0.38f, 0.57f, 0.34f));

            GameObject traits = CreateInsetPlate(
                generatedPanel.transform,
                "ForgedTraits",
                new Vector3(0f, -1.05f, -0.47f),
                new Vector3(6.15f, 1.05f, 0.15f),
                CraftLiveForgeUITheme.Iron,
                CraftLiveForgeUITheme.Brass);
            CreateText(
                traits.transform,
                "TraitText",
                $"属性  {EmptyFallback(result.attributeName)}     ◆     " +
                $"技能  {EmptyFallback(result.skillName)}",
                new Vector3(0f, 0f, -0.14f),
                0.035f);
            GameObject next = CreateButton(
                generatedPanel.transform,
                "NextWeapon",
                "次の武器を作る",
                new Vector3(0f, -2.5f, -0.68f),
                CraftLiveForgeUITheme.Ember,
                new Vector3(3.55f, 0.78f, 0.24f));
            next.GetComponent<CraftLiveWorldButton>()
                .AddListener(BeginNextWeapon);
        }

        private void BuildFinalSelection(CraftLiveRoomState state)
        {
            generatedPanel = CreatePanel(
                "Generated_FinalSelection");
            CreateText(
                generatedPanel.transform,
                "Title",
                "完成武器を1つ選ぶ",
                new Vector3(0f, 3.25f, -0.7f),
                0.06f);
            int count = state.completedWeapons != null
                ? state.completedWeapons.Count
                : 0;
            if (count == 0)
            {
                CreateText(
                    generatedPanel.transform,
                    "Empty",
                    "完成した武器がありません",
                    Vector3.zero,
                    0.055f);
                return;
            }

            int visibleCount = Mathf.Min(12, count);
            int start = count - visibleCount;
            for (int i = 0; i < visibleCount; i++)
            {
                CraftLiveResultState result =
                    state.completedWeapons[start + i];
                int serial = result.resultSerial;
                int column = i % 2;
                int row = i / 2;
                GameObject button = CreateButton(
                    generatedPanel.transform,
                    $"Result_{serial}",
                    $"{result.weaponName}\n" +
                    $"{result.stats.attackRate:0}/" +
                    $"{result.stats.defenseRate:0}/" +
                    $"{result.stats.evasionRate:0}",
                    new Vector3(
                        column == 0 ? -1.75f : 1.75f,
                        2.35f - row * 0.9f,
                        -0.7f),
                    new Color(0.14f, 0.48f, 0.62f),
                    new Vector3(3.1f, 0.72f, 0.24f));
                button.GetComponent<CraftLiveWorldButton>()
                    .AddListener(
                        () => SelectFinalWeapon(serial));
            }
        }

        private void BuildCodePanel(CraftLiveRoomState state)
        {
            generatedPanel = CreatePanel(
                "Generated_WeaponCode");
            CreateText(
                generatedPanel.transform,
                "Title",
                "完成武器コード",
                new Vector3(0f, 1.6f, -0.7f),
                0.06f);
            CreateText(
                generatedPanel.transform,
                "Weapon",
                state.result.weaponName,
                new Vector3(0f, 0.5f, -0.7f),
                0.055f);
            CreateText(
                generatedPanel.transform,
                "Code",
                state.finalWeaponCode,
                new Vector3(0f, -0.8f, -0.7f),
                0.085f);
        }

        private GameObject CreatePanel(string name)
        {
            GameObject panel = new GameObject(name);
            panel.name = name;
            panel.transform.SetParent(
                bindings.ResultHologramRoot,
                false);
            panel.transform.localScale = Vector3.one * 0.72f;
            panel.AddComponent<
                CraftLiveGeneratedRuntimeVisual>();

            CreateDecorativePart(
                panel.transform,
                "CastIronShadow",
                new Vector3(0.08f, -0.1f, 0.08f),
                new Vector3(7.48f, 7.86f, 0.22f),
                CraftLiveForgeUITheme.DeepIron,
                0.01f,
                0.84f,
                0.2f);
            CreateDecorativePart(
                panel.transform,
                "WalnutBacking",
                Vector3.zero,
                new Vector3(7.24f, 7.62f, 0.2f),
                new Color(0.25f, 0.115f, 0.045f),
                0.015f,
                0.18f,
                0.24f);
            CreateDecorativePart(
                panel.transform,
                "ForgedIronFace",
                new Vector3(0f, 0f, -0.13f),
                new Vector3(6.82f, 7.18f, 0.11f),
                CraftLiveForgeUITheme.DeepIron,
                0.025f,
                0.8f,
                0.28f);

            Color warmBrass = Color.Lerp(
                CraftLiveForgeUITheme.Brass,
                CraftLiveForgeUITheme.Iron,
                0.16f);
            CreateDecorativePart(
                panel.transform,
                "TopBrassRail",
                new Vector3(0f, 3.54f, -0.35f),
                new Vector3(6.72f, 0.12f, 0.13f),
                warmBrass,
                0.08f,
                0.88f,
                0.44f);
            CreateDecorativePart(
                panel.transform,
                "BottomBrassRail",
                new Vector3(0f, -3.54f, -0.35f),
                new Vector3(6.72f, 0.12f, 0.13f),
                warmBrass,
                0.05f,
                0.88f,
                0.38f);
            CreateDecorativePart(
                panel.transform,
                "LeftIronRail",
                new Vector3(-3.34f, 0f, -0.33f),
                new Vector3(0.14f, 7.08f, 0.13f),
                CraftLiveForgeUITheme.Iron,
                0.025f,
                0.9f,
                0.3f);
            CreateDecorativePart(
                panel.transform,
                "RightIronRail",
                new Vector3(3.34f, 0f, -0.33f),
                new Vector3(0.14f, 7.08f, 0.13f),
                CraftLiveForgeUITheme.Iron,
                0.025f,
                0.9f,
                0.3f);
            CreateDecorativePart(
                panel.transform,
                "HeaderDivider",
                new Vector3(0f, 2.14f, -0.4f),
                new Vector3(5.9f, 0.045f, 0.08f),
                CraftLiveForgeUITheme.Brass,
                0.08f,
                0.85f,
                0.45f);

            CreatePanelRivet(panel.transform, -3.1f, 3.3f);
            CreatePanelRivet(panel.transform, 3.1f, 3.3f);
            CreatePanelRivet(panel.transform, -3.1f, -3.3f);
            CreatePanelRivet(panel.transform, 3.1f, -3.3f);
            return panel;
        }

        private static GameObject CreateButton(
            Transform parent,
            string name,
            string label,
            Vector3 position,
            Color color,
            Vector3? scale = null)
        {
            GameObject button = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            button.name = name;
            button.transform.SetParent(parent, false);
            button.transform.localPosition = position;
            button.transform.localScale =
                scale ?? new Vector3(3f, 0.75f, 0.24f);
            Renderer renderer = button.GetComponent<Renderer>();
            CraftLiveForgeUITheme.GetButtonPalette(
                color,
                out Color normal,
                out Color hover,
                out Color pressed,
                out _,
                out _);
            ApplyColor(renderer, normal);
            CraftLiveWorldButton worldButton =
                button.AddComponent<CraftLiveWorldButton>();
            worldButton.Configure(
                button.transform,
                new[] { renderer },
                normal,
                hover,
                pressed);
            CraftLiveForgeUITheme.BuildButtonFrame(
                button.transform,
                color);
            CreateText(
                button.transform,
                "Label",
                label,
                new Vector3(0f, 0f, -0.62f),
                0.042f);
            return button;
        }

        private static TextMesh CreateText(
            Transform parent,
            string name,
            string value,
            Vector3 position,
            float size,
            Color? color = null)
        {
            GameObject textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = position;
            TextMesh text = textObject.AddComponent<TextMesh>();
            text.text = value ?? string.Empty;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            CraftLiveForgeUITheme.StyleText(
                text,
                size,
                color ?? CraftLiveForgeUITheme.ParchmentText);
            return text;
        }

        private static void CreateStatPlate(
            Transform parent,
            string name,
            string label,
            float value,
            Vector3 position,
            Color accent)
        {
            GameObject plate = CreateInsetPlate(
                parent,
                name,
                position,
                new Vector3(1.78f, 1.04f, 0.15f),
                CraftLiveForgeUITheme.Iron,
                accent);
            CreateText(
                plate.transform,
                "Label",
                label,
                new Vector3(0f, 0.2f, -0.14f),
                0.026f,
                CraftLiveForgeUITheme.MutedText);
            CreateText(
                plate.transform,
                "Value",
                value.ToString("0.#"),
                new Vector3(0f, -0.18f, -0.145f),
                0.058f,
                accent);
        }

        private static void CreateRankBadge(
            Transform parent,
            string rank,
            Vector3 position)
        {
            GameObject badge = GameObject.CreatePrimitive(
                PrimitiveType.Cylinder);
            badge.name = "RankBadge";
            badge.transform.SetParent(parent, false);
            badge.transform.localPosition = position;
            badge.transform.localRotation =
                Quaternion.Euler(90f, 0f, 0f);
            badge.transform.localScale =
                new Vector3(0.52f, 0.09f, 0.52f);
            DestroySafely(badge.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                badge.GetComponent<Renderer>(),
                CraftLiveForgeUITheme.Brass,
                0.12f,
                0.92f,
                0.5f);
            CreateText(
                parent,
                "Rank",
                $"RANK\n{EmptyFallback(rank)}",
                new Vector3(
                    position.x,
                    position.y,
                    position.z - 0.17f),
                0.037f,
                CraftLiveForgeUITheme.DeepIron);
        }

        private static GameObject CreateInsetPlate(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color surface,
            Color accent)
        {
            GameObject plate = new GameObject(name);
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = position;
            CreateDecorativePart(
                plate.transform,
                "InsetSurface",
                Vector3.zero,
                scale,
                surface,
                0.025f,
                0.82f,
                0.3f);
            CreateDecorativePart(
                plate.transform,
                "AccentEdge",
                new Vector3(0f, scale.y * 0.43f, -scale.z * 0.58f),
                new Vector3(scale.x * 0.86f, scale.y * 0.055f, 0.06f),
                accent,
                0.08f,
                0.86f,
                0.44f);
            return plate;
        }

        private static GameObject CreateDecorativePart(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color,
            float emission,
            float metallic,
            float smoothness)
        {
            GameObject part = GameObject.CreatePrimitive(
                PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            DestroySafely(part.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                part.GetComponent<Renderer>(),
                color,
                emission,
                metallic,
                smoothness);
            return part;
        }

        private static void CreatePanelRivet(
            Transform parent,
            float x,
            float y)
        {
            GameObject rivet = GameObject.CreatePrimitive(
                PrimitiveType.Sphere);
            rivet.name = "HandHammeredRivet";
            rivet.transform.SetParent(parent, false);
            rivet.transform.localPosition =
                new Vector3(x, y, -0.47f);
            rivet.transform.localScale =
                new Vector3(0.16f, 0.16f, 0.07f);
            DestroySafely(rivet.GetComponent<Collider>());
            CraftLiveForgeUITheme.ApplyForgeSurface(
                rivet.GetComponent<Renderer>(),
                CraftLiveForgeUITheme.Brass,
                0.06f,
                0.92f,
                0.46f);
        }

        private static string EmptyFallback(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? "なし"
                : value;
        }

        private static void ApplyColor(
            Renderer renderer,
            Color color)
        {
            CraftLiveForgeUITheme.ApplyForgeSurface(renderer, color);
        }

        private static void DestroySafely(Object target)
        {
            if (target == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
