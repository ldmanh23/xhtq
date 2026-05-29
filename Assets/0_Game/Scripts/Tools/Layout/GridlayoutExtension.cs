using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;


namespace UnityEngine.UI.Extensions
{
    [AddComponentMenu("Layout/Extensions/GridlayoutExtension")]
    public class GridlayoutExtension : LayoutGroup
    {
        /// <summary>
        /// Use for inventory
        /// set active false when child outside mask
        /// auto resize content
        /// fix child fit follow inventory
        /// </summary>

        private RectTransform contentRect;
        private RectTransform m_ContentRect
        {
            get{
                if (contentRect == null) contentRect = GetComponent<RectTransform>();
                return contentRect;
            }
        }
        
        private RectTransform parentRect;
        private RectTransform m_ParentRect
        {
            get{
                if (parentRect == null) parentRect = m_ContentRect.parent.GetComponent<RectTransform>();
                return parentRect;
            }
        }

        [Header("----Property----")]
        [SerializeField] protected ScrollRect m_ScrollRect;
        [SerializeField] protected Vector2 m_CellSize;
        [SerializeField] protected Vector2 m_Spacing;
        [SerializeField] protected float m_TailSide;

        protected override void Start()
        {
            base.Start();
            m_ScrollRect.onValueChanged.AddListener(OnScroll);
        }

        protected override void OnEnable() 
        { base.OnEnable(); Calculate(); 
        }
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
            //for lock
            //m_Rect.hideFlags = HideFlags.NotEditable;
            m_ContentRect.anchorMin = Vector2.zero;
            m_ContentRect.anchorMax = Vector2.one;
            m_ContentRect.pivot = Vector2.up;

            int childCount = m_ContentRect.childCount;

            if (childCount == 0) return;

            Vector2 size = m_ParentRect.rect.size;

            Vector2 realSpacingX = Vector2.right * (m_CellSize.x + m_Spacing.x);
            Vector2 realSpacingY = Vector2.down * (m_CellSize.y + m_Spacing.y);

            int maxChildInColunm = (int)Math.Floor((size.x - m_Padding.left - m_Padding.right + m_Spacing.x) / realSpacingX.x);

            maxChildInColunm = maxChildInColunm < 1 ? 1 : maxChildInColunm;

            float realPaddingX = (size.x - (maxChildInColunm * realSpacingX.x - m_Spacing.x)) / 2;

            Vector2 firstPoint = Vector2.right * (realPaddingX + m_CellSize.x / 2) + Vector2.down * (m_Padding.top + m_CellSize.y / 2);

            //Debug.Log(size);

            for (int i = 0; i < childCount; i++)
            {
                int x = i % maxChildInColunm;
                int y = i / maxChildInColunm;

                RectTransform rect = (RectTransform)m_ContentRect.GetChild(i);
                rect.anchorMin = Vector2.up;
                rect.anchorMax = Vector2.up;
                rect.pivot = Vector2.one * 0.5f;
                rect.sizeDelta = m_CellSize;
                rect.anchoredPosition = firstPoint + x * realSpacingX + y * realSpacingY;
                rect.hideFlags = HideFlags.NotEditable;
            }

            //resize for content
            float newRectSizeY = (childCount - 1) / maxChildInColunm * Mathf.Abs(realSpacingY.y) + m_CellSize.y + m_Padding.top + m_Padding.bottom;
            m_ContentRect.sizeDelta = Vector2.down * (m_ParentRect.rect.size.y - newRectSizeY - m_TailSide);
        }

        //for enable gameobject outside
        private Vector2 last;
        private void OnScroll(Vector2 scroll)
        {
            if ((last - scroll).sqrMagnitude > 0.000001f)
            {
                int childCount = m_ContentRect.childCount;

                for (int i = 0; i < childCount; i++)
                {
                    m_ContentRect.GetChild(i).gameObject.SetActive(IsIn((RectTransform)m_ContentRect.GetChild(i)));
                }
            }

            last = scroll;
        }

        public bool IsIn(RectTransform rect)
        {
            float y = rect.anchoredPosition.y + m_ContentRect.anchoredPosition.y;
            float yMax = (m_CellSize.y + m_Spacing.y) / 2;
            float yMin = -(m_ParentRect.rect.size.y + yMax);

            return (y < yMax && y > yMin);
        }
    }


}
