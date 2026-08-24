using System;
using System.Collections.Generic;
using PixelRoad.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PixelRoad.UI
{
    /// <summary>
    /// 전체 화면 도감. 상단 카테고리 필터, 카드 그리드, 하단 수집률 바, 카드 상세 보기를 묶어서 관리한다.
    /// 하단 GNB는 덮지 않는다(패널 아래쪽을 GNB 높이만큼 비워 둔다).
    /// </summary>
    public sealed class CodexView : MonoBehaviour
    {
        /// <summary>`전체` 칩이 쓰는 category 값. 빈 문자열이면 필터를 걸지 않는다.</summary>
        private const string AllCategory = "";

        private static readonly Color32 LockedIconTint = new Color32(124, 120, 112, 235);
        private static readonly Color32 LockedNameColor = new Color32(122, 117, 108, 255);
        private static readonly Color32 UnlockedNameColor = new Color32(246, 237, 217, 255);
        private static readonly Color32 SegmentFilled = new Color32(200, 58, 45, 255);
        private static readonly Color32 SegmentEmpty = new Color32(214, 203, 180, 255);

        [Header("Panel")]
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text progressText;

        [Header("Filter")]
        [SerializeField] private RectTransform filterRow;
        [SerializeField] private CodexFilterChipView filterChipPrefab;

        [Header("Grid")]
        [SerializeField] private RectTransform content;
        [SerializeField] private LandmarkCardView cardPrefab;

        [Header("Collection rate")]
        [SerializeField] private TMP_Text rateValueText;
        [SerializeField] private RectTransform rateSegmentRow;

        [Header("Detail")]
        [SerializeField] private CodexDetailView detail;

        private readonly Dictionary<string, CardBinding> cards = new Dictionary<string, CardBinding>();
        private readonly List<CodexFilterChipView> chips = new List<CodexFilterChipView>();
        private readonly List<Image> rateSegments = new List<Image>();
        private string selectedCategory = AllCategory;
        private int lastFilledSegments = -1;

        public GameObject Root => root;
        public TMP_Text ProgressText => progressText;
        public RectTransform FilterRow => filterRow;
        public CodexFilterChipView FilterChipPrefab => filterChipPrefab;
        public RectTransform Content => content;
        public LandmarkCardView CardPrefab => cardPrefab;
        public TMP_Text RateValueText => rateValueText;
        public RectTransform RateSegmentRow => rateSegmentRow;
        public CodexDetailView Detail => detail;
        public bool IsVisible => root != null && root.activeSelf;

        /// <summary>카드 앞면의 `지도에서 보기`를 눌렀을 때. 도감을 닫고 지도를 옮기는 일은 바깥이 맡는다.</summary>
        public event Action<SpotDefinition> DetailMapRequested;

        /// <summary>직렬화 참조가 하나라도 비어 있으면 즉시 예외를 던져 프리팹 설정 실수를 잡는다.</summary>
        public void ValidateReferences()
        {
            Require(root, nameof(root));
            Require(progressText, nameof(progressText));
            Require(filterRow, nameof(filterRow));
            Require(filterChipPrefab, nameof(filterChipPrefab));
            Require(content, nameof(content));
            Require(cardPrefab, nameof(cardPrefab));
            Require(rateValueText, nameof(rateValueText));
            Require(rateSegmentRow, nameof(rateSegmentRow));
            Require(detail, nameof(detail));
            detail.ValidateReferences();
        }

        /// <summary>참조가 비었으면 어느 필드인지 이름을 담아 예외를 던진다.</summary>
        private void Require(UnityEngine.Object reference, string fieldName)
        {
            if (reference == null)
            {
                throw new InvalidOperationException(
                    "CodexPanel is missing the serialized reference '" + fieldName + "'.");
            }
        }

        /// <summary>상세 팝업을 준비하고 수집률 칸을 모아 둔 뒤 패널을 닫힌 상태로 시작한다.</summary>
        public void Initialize()
        {
            detail.Initialize(detail.Hide);
            detail.MapRequested += definition => DetailMapRequested?.Invoke(definition);

            // 수집률 바의 칸은 프리팹에 미리 만들어 둔 것을 그대로 쓴다. 런타임에 늘리지 않는다.
            rateSegments.Clear();
            for (int index = 0; index < rateSegmentRow.childCount; index++)
            {
                Image segment = rateSegmentRow.GetChild(index).GetComponent<Image>();
                if (segment != null)
                {
                    rateSegments.Add(segment);
                }
            }

            root.SetActive(false);
        }

        /// <summary>도감 패널을 여닫는다. 닫을 때는 위에 떠 있던 상세 팝업도 같이 정리한다.</summary>
        public void SetVisible(bool visible)
        {
            if (!visible)
            {
                detail.Hide();
            }

            root.SetActive(visible);
        }

        /// <summary>
        /// 랜드마크 카드를 하나 추가한다. 도감 이미지는 landmarks.json의 thumbnail을 따르며,
        /// 썸네일이 없거나 아직 해금하지 않았을 때 쓸 대체 이미지를 함께 받는다.
        /// </summary>
        public void AddCard(
            SpotRuntimeState state,
            Sprite thumbnail,
            Sprite placeholder,
            Action<SpotRuntimeState> onSelected)
        {
            LandmarkCardView view = Instantiate(cardPrefab, content, false);
            view.gameObject.name = "Codex_" + state.Definition.Id;
            view.Button.onClick.AddListener(() => ShowDetail(state, thumbnail, placeholder, onSelected));
            view.Icon.raycastTarget = false;
            cards[state.Definition.Id] = new CardBinding(
                view,
                SpotCategory.Normalize(state.Definition.Category),
                thumbnail,
                placeholder);
            UpdateCard(state);
        }

        /// <summary>
        /// 해금 여부에 맞춰 카드의 이미지·글자·색·자물쇠를 다시 칠한다.
        /// 잠긴 카드는 이름만 남기고 설명을 ??? 로 가리며, 이미지도 대체 이미지로 덮는다.
        /// </summary>
        public void UpdateCard(SpotRuntimeState state)
        {
            if (!cards.TryGetValue(state.Definition.Id, out CardBinding binding))
            {
                return;
            }

            bool unlocked = state.IsUnlocked;
            LandmarkCardView view = binding.View;
            // 이름은 잠김이어도 보여 준다(어디를 더 찾아가야 하는지 알려 주는 정보). 설명만 감춘다.
            view.NameText.text = state.Definition.DisplayName;
            view.NameText.color = unlocked ? (Color)UnlockedNameColor : (Color)LockedNameColor;
            view.DescriptionText.text = unlocked ? state.Definition.Description : "???";
            view.BadgeText.text = state.Definition.CollectionTitle;

            // 해금 전에는 실제 썸네일을 보여 주지 않는다. 해금하는 순간 진짜 이미지로 바뀐다.
            Sprite image = unlocked ? (binding.Thumbnail ?? binding.Placeholder) : binding.Placeholder;
            if (image != null && view.Icon.sprite != image)
            {
                view.Icon.sprite = image;
            }

            view.Icon.color = unlocked ? Color.white : (Color)LockedIconTint;
            view.LockIcon.gameObject.SetActive(!unlocked);
        }

        /// <summary>해금 개수를 받아 상단 진행 텍스트와 하단 수집률 바를 갱신한다.</summary>
        public void SetProgress(int unlocked, int total)
        {
            progressText.text = string.Format("{0} / {1}", unlocked, total);

            int percent = total > 0 ? Mathf.RoundToInt(unlocked * 100f / total) : 0;
            rateValueText.text = percent + "%";

            // 칸 색은 채워진 개수가 바뀔 때만 건드린다. 해금할 때마다 전부 다시 칠하지 않는다.
            int filled = rateSegments.Count > 0 && total > 0
                ? Mathf.Clamp(Mathf.RoundToInt(rateSegments.Count * unlocked / (float)total), 0, rateSegments.Count)
                : 0;
            if (filled == lastFilledSegments)
            {
                return;
            }

            lastFilledSegments = filled;
            for (int index = 0; index < rateSegments.Count; index++)
            {
                rateSegments[index].color = index < filled ? SegmentFilled : SegmentEmpty;
            }
        }

        /// <summary>
        /// 수록된 랜드마크의 category로 필터 칩을 만든다. 데이터에 없는 칩은 만들지 않는다.
        /// 표기가 영문으로 남아 있는 항목(station 등)은 <see cref="SpotCategory"/>가 같은 칩으로 합쳐 준다.
        /// </summary>
        public void BuildFilters(IList<SpotRuntimeState> spots)
        {
            List<string> categories = new List<string>();
            for (int index = 0; index < spots.Count; index++)
            {
                string category = SpotCategory.Normalize(spots[index].Definition.Category);
                if (!string.IsNullOrEmpty(category) && !categories.Contains(category))
                {
                    categories.Add(category);
                }
            }

            categories.Sort(CompareCategories);
            CreateChip(AllCategory, "전체");
            for (int index = 0; index < categories.Count; index++)
            {
                CreateChip(categories[index], categories[index]);
            }

            ApplyFilter(AllCategory);
        }

        /// <summary>SpotCategory.DisplayOrder 순서를 우선 따르고, 목록에 없는 category는 이름순으로 뒤에 둔다.</summary>
        private static int CompareCategories(string left, string right)
        {
            int leftRank = SpotCategory.IndexOf(left);
            int rightRank = SpotCategory.IndexOf(right);
            if (leftRank < 0)
            {
                leftRank = int.MaxValue;
            }

            if (rightRank < 0)
            {
                rightRank = int.MaxValue;
            }

            return leftRank != rightRank
                ? leftRank.CompareTo(rightRank)
                : string.CompareOrdinal(left, right);
        }

        /// <summary>칩 프리팹을 하나 찍어 필터 줄에 붙이고 누르면 해당 category만 남기도록 연결한다.</summary>
        private void CreateChip(string category, string displayName)
        {
            CodexFilterChipView chip = Instantiate(filterChipPrefab, filterRow, false);
            chip.Initialize(category, displayName);
            chip.Button.onClick.AddListener(() => ApplyFilter(category));
            chips.Add(chip);
        }

        /// <summary>선택한 category만 남기고 나머지 카드를 끈다. 카드는 지우지 않고 활성 상태만 바꾼다.</summary>
        private void ApplyFilter(string category)
        {
            selectedCategory = category ?? AllCategory;
            for (int index = 0; index < chips.Count; index++)
            {
                chips[index].SetSelected(chips[index].Category == selectedCategory);
            }

            bool showAll = string.IsNullOrEmpty(selectedCategory);
            foreach (KeyValuePair<string, CardBinding> pair in cards)
            {
                CardBinding binding = pair.Value;
                bool visible = showAll || binding.Category == selectedCategory;
                if (binding.View.gameObject.activeSelf != visible)
                {
                    binding.View.gameObject.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// 도감을 열지 않고 상세 팝업만 띄운다. 지도 배너의 `카드 보기`가 쓴다.
        /// 상세 팝업은 도감 패널 밖(캔버스 직속)에 있어서 도감이 닫혀 있어도 그대로 보인다.
        /// </summary>
        public void ShowDetail(SpotRuntimeState state, Sprite thumbnail)
        {
            ShowDetail(state, thumbnail, null, null);
        }

        /// <summary>
        /// 선택 콜백을 먼저 알린 뒤 상세 팝업을 띄운다.
        ///
        /// 잠긴 랜드마크도 연다. 어디로 가야 해금되는지 알려면 앞면의 `지도에서 보기`를 눌릴 수 있어야 하기 때문이다.
        /// 내용을 가리는 일은 <see cref="CodexDetailView.Show(SpotDefinition, Sprite, bool)"/>가 맡고,
        /// 여기서는 잠긴 카드에 실제 썸네일 대신 대체 이미지를 넘긴다.
        /// </summary>
        private void ShowDetail(
            SpotRuntimeState state,
            Sprite thumbnail,
            Sprite placeholder,
            Action<SpotRuntimeState> onSelected)
        {
            onSelected?.Invoke(state);

            bool unlocked = state.IsUnlocked;
            Sprite image = unlocked ? (thumbnail ?? placeholder) : placeholder;
            detail.Show(state.Definition, image, unlocked);
        }

        /// <summary>
        /// 카드 뷰와 그 카드의 category·이미지를 묶어 둔다.
        /// 필터링과 해금 반영 때 정의를 다시 뒤지거나 Resources를 다시 읽지 않기 위함이다.
        /// </summary>
        private readonly struct CardBinding
        {
            public readonly LandmarkCardView View;
            public readonly string Category;

            /// <summary>해금 뒤에 보여 줄 실제 썸네일. 파일이 없으면 null.</summary>
            public readonly Sprite Thumbnail;

            /// <summary>썸네일이 없거나 잠겨 있을 때 보여 줄 대체 이미지.</summary>
            public readonly Sprite Placeholder;

            public CardBinding(LandmarkCardView view, string category, Sprite thumbnail, Sprite placeholder)
            {
                View = view;
                Category = category;
                Thumbnail = thumbnail;
                Placeholder = placeholder;
            }
        }
    }
}
