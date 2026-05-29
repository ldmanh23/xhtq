

using static UnityEngine.UI.GridLayoutGroup;

namespace UnityEngine.UI.Extensions
{
    public class LayoutScale : LayoutGroup
    {

        /// <summary>
        /// Auto scale child
        /// cap theo chieu ngang hoac chieu doc
        /// sorting direct ngang -> scale theo chieu doc
        /// sorting direct doc -> scale theo chieu ngang

        /// child size se lay theo size goc cua thang con
        /// neu thay doi size se tu scale child size len theo
        /// auto fit content size
        /// </summary>

        public enum Layout { Horizontal, Vertical }

        [SerializeField] private Layout m_SortingDirect;
        private Vector2 m_MemberSize = new Vector2(100, 100);
        [SerializeField] private Vector2 m_Spacing = Vector2.one;
        [SerializeField] private int m_AmountMember = 1;
        private bool m_OnlyLayoutVisible = true;

        private RectTransform parentRect;
        private RectTransform m_ParentRect
        {
            get
            {
                if (parentRect == null) parentRect = rectTransform.parent.GetComponent<RectTransform>();
                return parentRect;
            }
        }

        private Vector3 Padding => this.padding.left * Vector3.right + this.padding.top * Vector3.up;
        private Vector3 RealMemberSize(float scale) => m_MemberSize * scale;

        private Vector3 Distance(float scale) 
        {
            Vector3 distance = m_MemberSize * scale + m_Spacing;
            distance.y = -distance.y;
            return distance; 
        }

        private Vector3 Spacing => m_Spacing;

        protected override void OnEnable() { base.OnEnable(); Calculate(); }
        public override void SetLayoutHorizontal()
        {
        }
        public override void SetLayoutVertical()
        {
        }
        public override void CalculateLayoutInputVertical()
        {
            Calculate();
        }
        public override void CalculateLayoutInputHorizontal()
        {
            Calculate();
        }
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();
            Calculate();
        }
#endif

        private void Calculate()
        {
            m_Tracker.Clear();
            if (transform.childCount == 0)
                return;

            //set anchor 
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.pivot = Vector2.up;

            RectTransform rectChild = (RectTransform)transform.GetChild(0);
            m_MemberSize = rectChild.sizeDelta;

            int ChildrenToFormat = 0;
            if (m_OnlyLayoutVisible)
            {
                for (int i = 0; i < transform.childCount; i++)
                {
                    RectTransform child = (RectTransform)transform.GetChild(i);
                    if ((child != null) && child.gameObject.activeSelf)
                    {
                        ++ChildrenToFormat;
                    }
                }
            }
            else
            {
                ChildrenToFormat = transform.childCount;
            }

            float empty = 1;
            float scale = 1;


            Vector3 startPosition = Vector3.zero;

            if (m_SortingDirect == Layout.Vertical)
            {
                empty = (rectTransform.rect.width - padding.left * 2 - m_Spacing.x * (m_AmountMember - 1));
                scale = empty / (m_AmountMember * m_MemberSize.x);
            }
            else if (m_SortingDirect == Layout.Horizontal)
            {
                empty = (rectTransform.rect.height - padding.top * 2 - m_Spacing.y * (m_AmountMember - 1));
                scale = empty / (m_AmountMember * m_MemberSize.y);
            }

            Vector3 childSize = RealMemberSize(scale);
            startPosition = Vector3.right * Padding.x + Vector3.down * Padding.y  + (-Vector3.right * rectTransform.rect.width + Vector3.up * rectTransform.rect.height) * 0.5f + Vector3.right * childSize.x * 0.5f + Vector3.down * childSize.y * 0.5f;

            FillContentSize(childSize, ChildrenToFormat);

            int indexChild = 0;

            for (int i = 0; i < transform.childCount; i++)
            {
                RectTransform child = (RectTransform)transform.GetChild(i);
                if ((child != null) && (!m_OnlyLayoutVisible || child.gameObject.activeSelf))
                {
                    //Adding the elements to the tracker stops the user from modifying their positions via the editor.
                    m_Tracker.Add(this, child,
                    DrivenTransformProperties.Anchors |
                    DrivenTransformProperties.AnchoredPosition |
                    DrivenTransformProperties.Pivot);
                    child.anchorMin = child.anchorMax = child.pivot = Vector2.one * 0.5f;

                    child.sizeDelta = m_MemberSize;

                    int x = indexChild / m_AmountMember;
                    int y = indexChild % m_AmountMember;

                    Vector3 vPos = Vector3.zero; 

                    if (m_SortingDirect == Layout.Horizontal)
                    {
                        vPos = startPosition + Vector3.right * Distance(scale).x * x + Vector3.up * Distance(scale).y * y;
                    }
                    else if (m_SortingDirect == Layout.Vertical)
                    {
                        vPos = startPosition + Vector3.right * Distance(scale).x * y + Vector3.up * Distance(scale).y * x;
                    }

                    child.anchoredPosition = vPos;

                    child.localScale = scale * Vector3.one;


                    indexChild++;
                }
            }


        }

        private void FillContentSize(Vector3 childSize,int childrenToFormat)
        {
            int lineAmount = (childrenToFormat + m_AmountMember - 1) / m_AmountMember ;

            if (m_SortingDirect == Layout.Vertical)
            {
                float newSize = childSize.y * lineAmount + Spacing.y * (lineAmount - 1) + Padding.y * 2;
                rectTransform.sizeDelta = Vector2.down * (m_ParentRect.rect.size.y - newSize);
            }
            else if (m_SortingDirect == Layout.Horizontal)
            {
                float newSize = childSize.x * lineAmount + Spacing.x * (lineAmount - 1) + Padding.x * 2;
                rectTransform.sizeDelta = Vector2.left * (m_ParentRect.rect.size.x - newSize);
            }

        }

    }
}