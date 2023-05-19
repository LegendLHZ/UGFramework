// -----------------------------------------------------------------------
// <copyright file="ScrollView.cs" company="AillieoTech">
// Copyright (c) AillieoTech. All rights reserved.
// </copyright>
// -----------------------------------------------------------------------

using UnityEngine.EventSystems;
using DG.Tweening;

namespace UnityEngine.UI
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using UnityEngine;
    using UnityEngine.Serialization;

    [RequireComponent(typeof(RectTransform))]
    [DisallowMultipleComponent]
    public class ScrollView : ScrollRect
    {
        [Serializable]
        class Snap
        {
            public bool Enable;
            public float VelocityThreshold = 0.5f;
            public float Duration = 0.3f;
            public Ease Easing = Ease.OutCubic;
        }

        [Tooltip("默认item尺寸")]
        public Vector2 defaultItemSize;

        public Vector2 leftAndTopBound;
        
        public Vector2 rightAndBottomBound;

        public Vector2 space;

        [Tooltip("item的模板")]
        public RectTransform itemTemplate;

        [Tooltip("可见范围")]
        public RectTransform visibleRectTransform;
        
        [SerializeField] Snap snap = new Snap {
            Enable = false,
            VelocityThreshold = 0.5f,
            Duration = 0.3f,
            Easing = Ease.OutCubic
        };

        private bool _checkSnap = false;

        // 0001
        protected const int flagScrollDirection = 1;

        [SerializeField]
        [FormerlySerializedAs("m_layoutType")]
        protected ItemLayoutType layoutType = ItemLayoutType.Vertical;

        // 只保存4个临界index
        protected int[] criticalItemIndex = new int[4];

        // callbacks for items
        protected Action<int, RectTransform> updateFunc;
        protected Func<int, Vector2> itemSizeFunc;
        protected Func<int> itemCountFunc;
        protected Func<int, RectTransform> itemGetFunc;
        protected Action<RectTransform> itemRecycleFunc;
        protected Action<int, RectTransform> selectFunc;

        private readonly List<ScrollItemWithRect> managedItems = new List<ScrollItemWithRect>();

        private Rect refRect;

        // resource management
        private SimpleObjPool<RectTransform> itemPool = null;

        private int dataCount = 0;

        // status
        private bool initialized = false;
        private int willUpdateData = 0;

        private Vector3[] viewWorldConers = new Vector3[4];
        private Vector3[] rectCorners = new Vector3[2];
        private Vector2 startPoint;
        
        private Tweener _positionTweener;

        public int SelectPage { get; private set; } = -1;

        // for hide and show
        public enum ItemLayoutType
        {
            // 最后一位表示滚动方向
            Vertical = 0b0001,                   // 0001
            Horizontal = 0b0010,                 // 0010
            VerticalThenHorizontal = 0b0100,     // 0100
            HorizontalThenVertical = 0b0101,     // 0101
        }

        protected override void Awake()
        {
            startPoint = new Vector2(leftAndTopBound.x, -leftAndTopBound.y);

            InitPool();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
        }

        protected override void LateUpdate()
        {
            base.LateUpdate();

            CheckSnap();
        }

        private void CheckSnap()
        {
            if (!snap.Enable) return;

            if (!_checkSnap) return;

            if (snap.VelocityThreshold * 1000 < velocity.magnitude) return;
            
            _checkSnap = false;
            var selectPage = GetSelectPage();
            ScrollTo(selectPage, true);
            selectFunc?.Invoke(selectPage, managedItems[selectPage].item);
        }

        private int GetSelectPage()
        {
            var index = -1;
            var d = 0f;
            var center = new Rect(this.refRect.position - this.content.anchoredPosition, this.refRect.size).center;
            for (int i = criticalItemIndex[CriticalItemType.UpToShow]; i <= criticalItemIndex[CriticalItemType.DownToShow]; i++)
            {
                var y = Vector2.Distance(center, managedItems[i].rect.center);
                if (index >= 0 && !(y < d)) continue;
                index = i;
                d = y;
            }
            return index;
        }

        public override void OnBeginDrag(PointerEventData eventData)
        {
            StopPositionAnim();
            base.OnBeginDrag(eventData);
        }

        public override void OnEndDrag(PointerEventData eventData)
        {
            base.OnEndDrag(eventData);

            _checkSnap = true;
        }

        public virtual void SetUpdateFunc(Action<int, RectTransform> func)
        {
            this.updateFunc = func;
        }

        public virtual void SetSelectFunc(Action<int, RectTransform> func)
        {
            selectFunc = func;
        }

        public virtual void SetItemSizeFunc(Func<int, Vector2> func)
        {
            this.itemSizeFunc = func;
        }

        public virtual void SetItemCountFunc(Func<int> func)
        {
            this.itemCountFunc = func;
        }

        public void SetItemGetAndRecycleFunc(Func<int, RectTransform> getFunc, Action<RectTransform> recycleFunc)
        {
            if (getFunc != null && recycleFunc != null)
            {
                this.itemGetFunc = getFunc;
                this.itemRecycleFunc = recycleFunc;
            }
            else
            {
                this.itemGetFunc = null;
                this.itemRecycleFunc = null;
            }
        }

        public void ResetAllDelegates()
        {
            this.SetUpdateFunc(null);
            this.SetItemSizeFunc(null);
            this.SetItemCountFunc(null);
            this.SetItemGetAndRecycleFunc(null, null);
        }

        public void UpdateData(bool immediately = true)
        {
            if (immediately)
            {
                this.willUpdateData |= 3; // 0011
                this.InternalUpdateData();
            }
            else
            {
                if (this.willUpdateData == 0 && this.IsActive())
                {
                    this.StartCoroutine(this.DelayUpdateData());
                }

                this.willUpdateData |= 3;
            }
        }

        public void UpdateDataIncrementally(bool immediately = true)
        {
            if (immediately)
            {
                this.willUpdateData |= 1; // 0001
                this.InternalUpdateData();
            }
            else
            {
                if (this.willUpdateData == 0)
                {
                    this.StartCoroutine(this.DelayUpdateData());
                }

                this.willUpdateData |= 1;
            }
        }

        public void ScrollTo(int index, bool anim = false)
        {
            SelectPage = index;
            this.InternalScrollTo(index, anim);
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (this.willUpdateData != 0)
            {
                this.StartCoroutine(this.DelayUpdateData());
            }
        }

        protected virtual void InternalScrollTo(int index, bool anim)
        {
            StopMovement();

            if (managedItems.Count == 0)
            {
                return;
            }

            if (dataCount < 1)
            {
                return;
            }
            
            index = Mathf.Clamp(index, 0, this.dataCount - 1);
            EnsureItemRect(index);
            Rect r = managedItems[index].rect;
            var dir = (int)layoutType & flagScrollDirection;
            if (dir == 1)
            {
                // vertical
                var value = (-r.center.y - refRect.height / 2) / (this.content.sizeDelta.y - this.refRect.height);
                value = Mathf.Clamp01(value);
                if (!anim) SetNormalizedPosition(1 - value, 1);
                else SetNormalizedPositionAnim(1 - value, 1);
            }
            else
            {
                // horizontal
                var value = (r.center.x - refRect.width / 2) / (this.content.sizeDelta.x - this.refRect.width);
                value = Mathf.Clamp01(value);
                if (!anim) this.SetNormalizedPosition(value, 0);
                else SetNormalizedPositionAnim(value, 0);
            }
        }

        protected override void SetContentAnchoredPosition(Vector2 position)
        {
            base.SetContentAnchoredPosition(position);
            this.UpdateCriticalItems();
        }

        private void StopPositionAnim()
        {
            if (_positionTweener == null) return;
            _positionTweener.Kill();
            _positionTweener = null;
        }
        
        protected void SetNormalizedPositionAnim(float value, int axis)
        {
            StopPositionAnim();
            if (axis == 0)
            {
                _positionTweener = this.DOHorizontalNormalizedPos(value, snap.Duration).SetEase(snap.Easing);
            }
            else
            {
                _positionTweener = this.DOVerticalNormalizedPos(value, snap.Duration).SetEase(snap.Easing);
            }
        }

        protected override void SetNormalizedPosition(float value, int axis)
        {
            base.SetNormalizedPosition(value, axis);
            this.ResetCriticalItems();
        }

        protected void EnsureItemRect(int index)
        {
            if (this.managedItems.Count == 0)
            {
                return;
            }
            if (index < 0)
            {
                return;
            }
            if (!this.managedItems[index].rectDirty)
            {
                // 已经是干净的了
                return;
            }

            ScrollItemWithRect firstItem = this.managedItems[0];
            if (firstItem.rectDirty)
            {
                Vector2 firstSize = this.GetItemSize(0);
                firstItem.rect = CreateWithLeftTopAndSize(startPoint, firstSize);
                firstItem.rectDirty = false;
            }

            // 当前item之前的最近的已更新的rect
            var nearestClean = 0;
            for (var i = index; i >= 0; --i)
            {
                if (!this.managedItems[i].rectDirty)
                {
                    nearestClean = i;
                    break;
                }
            }

            // 需要更新 从 nearestClean 到 index 的尺寸
            Rect nearestCleanRect = this.managedItems[nearestClean].rect;
            Vector2 curPos = GetLeftTop(nearestCleanRect);
            Vector2 size = nearestCleanRect.size;
            this.MovePos(ref curPos, size, true);

            for (var i = nearestClean + 1; i <= index; i++)
            {
                size = this.GetItemSize(i);
                this.managedItems[i].rect = CreateWithLeftTopAndSize(curPos, size);
                this.managedItems[i].rectDirty = false;
                this.MovePos(ref curPos, size, i != index);
            }

            var range = new Vector2(Mathf.Abs(curPos.x), Mathf.Abs(curPos.y));
            switch (this.layoutType)
            {
                case ItemLayoutType.VerticalThenHorizontal:
                    range.x += size.x;
                    range.y = this.refRect.height;
                    break;
                case ItemLayoutType.HorizontalThenVertical:
                    range.x = this.refRect.width;
                    if (curPos.x != 0)
                    {
                        range.y += size.y;
                    }
                    break;
                default:
                    break;
            }

            this.content.sizeDelta = range + rightAndBottomBound;
        }

        protected override void OnDestroy()
        {
            if (this.itemPool != null)
            {
                this.itemPool.Purge();
            }
        }

        protected Rect GetItemLocalRect(int index)
        {
            if (index >= 0 && index < this.dataCount)
            {
                this.EnsureItemRect(index);
                return this.managedItems[index].rect;
            }

            return (Rect)default;
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            var dir = (int)this.layoutType & flagScrollDirection;
            if (dir == 1)
            {
                // vertical
                if (this.horizontalScrollbar != null)
                {
                    this.horizontalScrollbar.gameObject.SetActive(false);
                    this.horizontalScrollbar = null;
                }
            }
            else
            {
                // horizontal
                if (this.verticalScrollbar != null)
                {
                    this.verticalScrollbar.gameObject.SetActive(false);
                    this.verticalScrollbar = null;
                }
            }

            base.OnValidate();
        }
#endif

        private static Vector2 GetLeftTop(Rect rect)
        {
            Vector2 ret = rect.position;
            ret.y += rect.size.y;
            return ret;
        }

        private static Rect CreateWithLeftTopAndSize(Vector2 leftTop, Vector2 size)
        {
            Vector2 leftBottom = leftTop - new Vector2(0, size.y);
            return new Rect(leftBottom, size);
        }

        private IEnumerator DelayUpdateData()
        {
            yield return new WaitForEndOfFrame();
            this.InternalUpdateData();
        }

        private void InternalUpdateData()
        {
            if (!this.IsActive())
            {
                this.willUpdateData |= 3;
                return;
            }

            if (!this.initialized)
            {
                this.InitScrollView();
            }

            var newDataCount = 0;
            var keepOldItems = (this.willUpdateData & 2) == 0;

            if (this.itemCountFunc != null)
            {
                newDataCount = this.itemCountFunc();
            }

            if (newDataCount != this.managedItems.Count)
            {
                if (this.managedItems.Count < newDataCount)
                {
                    // 增加
                    if (!keepOldItems)
                    {
                        foreach (var itemWithRect in this.managedItems)
                        {
                            // 重置所有rect
                            itemWithRect.rectDirty = true;
                        }
                    }

                    while (this.managedItems.Count < newDataCount)
                    {
                        this.managedItems.Add(new ScrollItemWithRect());
                    }
                }
                else
                {
                    // 减少 保留空位 避免GC
                    for (int i = 0, count = this.managedItems.Count; i < count; ++i)
                    {
                        if (i < newDataCount)
                        {
                            // 重置所有rect
                            if (!keepOldItems)
                            {
                                this.managedItems[i].rectDirty = true;
                            }

                            if (i == newDataCount - 1)
                            {
                                this.managedItems[i].rectDirty = true;
                            }
                        }

                        // 超出部分 清理回收item
                        if (i >= newDataCount)
                        {
                            this.managedItems[i].rectDirty = true;
                            if (this.managedItems[i].item != null)
                            {
                                this.RecycleOldItem(this.managedItems[i].item);
                                this.managedItems[i].item = null;
                            }
                        }
                    }
                }
            }
            else
            {
                if (!keepOldItems)
                {
                    for (int i = 0, count = this.managedItems.Count; i < count; ++i)
                    {
                        // 重置所有rect
                        this.managedItems[i].rectDirty = true;
                    }
                }
            }

            this.dataCount = newDataCount;

            this.ResetCriticalItems();

            this.willUpdateData = 0;
        }

        private void ResetCriticalItems()
        {
            bool hasItem, shouldShow;
            int firstIndex = -1, lastIndex = -1;

            for (var i = 0; i < this.dataCount; i++)
            {
                hasItem = this.managedItems[i].item != null;
                shouldShow = this.ShouldItemSeenAtIndex(i);

                if (shouldShow)
                {
                    if (firstIndex == -1)
                    {
                        firstIndex = i;
                    }

                    lastIndex = i;
                }

                if (hasItem && shouldShow)
                {
                    // 应显示且已显示
                    this.SetDataForItemAtIndex(this.managedItems[i].item, i);
                    continue;
                }

                if (hasItem == shouldShow)
                {
                    // 不应显示且未显示
                    continue;
                }

                if (hasItem && !shouldShow)
                {
                    // 不该显示 但是有
                    this.RecycleOldItem(this.managedItems[i].item);
                    this.managedItems[i].item = null;
                    continue;
                }
            }

            if (firstIndex >= 0 && lastIndex >= 0)
            {
                for (var i = firstIndex; i <= lastIndex; i++)
                {
                    if (this.managedItems[i].item != null) continue;
                    // 需要显示 但是没有
                    RectTransform item = this.GetNewItem(i);
                    this.OnGetItemForDataIndex(item, i);
                    this.managedItems[i].item = item;
                }
            }

            this.criticalItemIndex[CriticalItemType.UpToHide] = firstIndex;
            this.criticalItemIndex[CriticalItemType.DownToHide] = lastIndex;
            this.criticalItemIndex[CriticalItemType.UpToShow] = Mathf.Max(firstIndex - 1, 0);
            this.criticalItemIndex[CriticalItemType.DownToShow] = Mathf.Min(lastIndex + 1, this.dataCount - 1);
        }

        private RectTransform GetCriticalItem(int type)
        {
            var index = this.criticalItemIndex[type];
            if (index >= 0 && index < this.dataCount)
            {
                return this.managedItems[index].item;
            }

            return null;
        }

        private void UpdateCriticalItems()
        {
            var dirty = true;

            while (dirty)
            {
                dirty = false;

                for (int i = CriticalItemType.UpToHide; i <= CriticalItemType.DownToShow; i++)
                {
                    if (i <= CriticalItemType.DownToHide)
                    {
                        // 隐藏离开可见区域的item
                        dirty = dirty || this.CheckAndHideItem(i);
                    }
                    else
                    {
                        // 显示进入可见区域的item
                        dirty = dirty || this.CheckAndShowItem(i);
                    }
                }
            }
        }

        private bool CheckAndHideItem(int criticalItemType)
        {
            RectTransform item = this.GetCriticalItem(criticalItemType);
            var criticalIndex = this.criticalItemIndex[criticalItemType];
            if (item != null && !this.ShouldItemSeenAtIndex(criticalIndex))
            {
                this.RecycleOldItem(item);
                this.managedItems[criticalIndex].item = null;

                if (criticalItemType == CriticalItemType.UpToHide)
                {
                    // 最上隐藏了一个
                    this.criticalItemIndex[criticalItemType + 2] = Mathf.Max(criticalIndex, this.criticalItemIndex[criticalItemType + 2]);
                    this.criticalItemIndex[criticalItemType]++;
                }
                else
                {
                    // 最下隐藏了一个
                    this.criticalItemIndex[criticalItemType + 2] = Mathf.Min(criticalIndex, this.criticalItemIndex[criticalItemType + 2]);
                    this.criticalItemIndex[criticalItemType]--;
                }

                this.criticalItemIndex[criticalItemType] = Mathf.Clamp(this.criticalItemIndex[criticalItemType], 0, this.dataCount - 1);

                if (this.criticalItemIndex[CriticalItemType.UpToHide] > this.criticalItemIndex[CriticalItemType.DownToHide])
                {
                    // 偶然的情况 拖拽超出一屏
                    this.ResetCriticalItems();
                    return false;
                }

                return true;
            }

            return false;
        }

        private bool CheckAndShowItem(int criticalItemType)
        {
            RectTransform item = this.GetCriticalItem(criticalItemType);
            var criticalIndex = this.criticalItemIndex[criticalItemType];

            if (item == null && this.ShouldItemSeenAtIndex(criticalIndex))
            {
                RectTransform newItem = this.GetNewItem(criticalIndex);
                this.OnGetItemForDataIndex(newItem, criticalIndex);
                this.managedItems[criticalIndex].item = newItem;

                if (criticalItemType == CriticalItemType.UpToShow)
                {
                    // 最上显示了一个
                    this.criticalItemIndex[criticalItemType - 2] = Mathf.Min(criticalIndex, this.criticalItemIndex[criticalItemType - 2]);
                    this.criticalItemIndex[criticalItemType]--;
                }
                else
                {
                    // 最下显示了一个
                    this.criticalItemIndex[criticalItemType - 2] = Mathf.Max(criticalIndex, this.criticalItemIndex[criticalItemType - 2]);
                    this.criticalItemIndex[criticalItemType]++;
                }

                this.criticalItemIndex[criticalItemType] = Mathf.Clamp(this.criticalItemIndex[criticalItemType], 0, this.dataCount - 1);

                if (this.criticalItemIndex[CriticalItemType.UpToShow] >= this.criticalItemIndex[CriticalItemType.DownToShow])
                {
                    // 偶然的情况 拖拽超出一屏
                    this.ResetCriticalItems();
                    return false;
                }

                return true;
            }

            return false;
        }

        private bool ShouldItemSeenAtIndex(int index)
        {
            if (index < 0 || index >= this.dataCount)
            {
                return false;
            }

            this.EnsureItemRect(index);
            return new Rect(this.refRect.position - this.content.anchoredPosition, this.refRect.size).Overlaps(this.managedItems[index].rect);
        }

        private void InitPool()
        {
            this.itemPool = new SimpleObjPool<RectTransform>(
                (RectTransform item) =>
                {
                    item.gameObject.SetActive(false);
                },
                () =>
                {
                    GameObject itemObj = Instantiate(this.itemTemplate.gameObject);
                    RectTransform item = itemObj.GetComponent<RectTransform>();
                    item.SetParent(content, false);
                    item.anchorMin = Vector2.up;
                    item.anchorMax = Vector2.up;
                    item.pivot = Vector2.zero;

                    itemObj.SetActive(true);
                    return item;
                });
        }

        private void OnGetItemForDataIndex(RectTransform item, int index)
        {
            this.SetDataForItemAtIndex(item, index);
            item.gameObject.SetActive(true);
        }

        private void SetDataForItemAtIndex(RectTransform item, int index)
        {
            if (this.updateFunc != null)
            {
                this.updateFunc(index, item);
            }

            this.SetPosForItemAtIndex(item, index);
        }

        private void SetPosForItemAtIndex(RectTransform item, int index)
        {
            this.EnsureItemRect(index);
            Rect r = this.managedItems[index].rect;
            item.localPosition = r.position;
            item.sizeDelta = r.size;
        }

        private Vector2 GetItemSize(int index)
        {
            if (index >= 0 && index <= this.dataCount)
            {
                if (this.itemSizeFunc != null)
                {
                    return this.itemSizeFunc(index);
                }
            }

            return this.defaultItemSize;
        }

        private RectTransform GetNewItem(int index)
        {
            RectTransform item;
            if (this.itemGetFunc != null)
            {
                item = this.itemGetFunc(index);
            }
            else
            {
                item = this.itemPool.Get();
            }

            return item;
        }

        private void RecycleOldItem(RectTransform item)
        {
            if (this.itemRecycleFunc != null)
            {
                this.itemRecycleFunc(item);
            }
            else
            {
                this.itemPool.Recycle(item);
            }
        }

        private void InitScrollView()
        {
            this.initialized = true;

            // 根据设置来控制原ScrollRect的滚动方向
            var dir = (int)this.layoutType & flagScrollDirection;
            this.vertical = dir == 1;
            this.horizontal = dir == 0;

            this.content.pivot = Vector2.up;
            this.content.anchorMin = Vector2.up;
            this.content.anchorMax = Vector2.up;
            this.content.sizeDelta = Vector2.zero;
            this.content.anchoredPosition = Vector2.zero;

            this.UpdateRefRect();
        }

        // refRect是在Content节点下的 viewport的 rect
        private void UpdateRefRect()
        {
            /*
             *  WorldCorners
             *
             *    1 ------- 2
             *    |         |
             *    |         |
             *    0 ------- 3
             *
             */

            if (!CanvasUpdateRegistry.IsRebuildingLayout())
            {
                Canvas.ForceUpdateCanvases();
            }

            if (visibleRectTransform != null)
            {
                visibleRectTransform.GetWorldCorners(viewWorldConers);
            }
            else
            {
                viewRect.GetWorldCorners(viewWorldConers);
            }
            this.rectCorners[0] = this.content.transform.InverseTransformPoint(this.viewWorldConers[0]);
            this.rectCorners[1] = this.content.transform.InverseTransformPoint(this.viewWorldConers[2]);
            this.refRect = new Rect((Vector2)this.rectCorners[0] - this.content.anchoredPosition, this.rectCorners[1] - this.rectCorners[0]);
        }

        private void MovePos(ref Vector2 pos, Vector2 size, bool addSpace)
        {
            // 注意 所有的rect都是左下角为基准
            switch (this.layoutType)
            {
                case ItemLayoutType.Vertical:
                    // 垂直方向 向下移动
                    pos.y -= size.y;
                    if (addSpace)
                    {
                        pos.y -= space.y;
                    }
                    break;
                case ItemLayoutType.Horizontal:
                    // 水平方向 向右移动
                    pos.x += size.x;
                    if (addSpace)
                    {
                        pos.x += space.x; 
                    }
                    break;
                case ItemLayoutType.VerticalThenHorizontal:
                    pos.y -= size.y;
                    if (addSpace)
                    {
                        pos.y -= space.y;
                    }
                    if (pos.y - size.y < -this.refRect.height)
                    {
                        pos.y = -leftAndTopBound.y;
                        pos.x += size.x;
                        if (addSpace)
                        {
                            pos.x += space.x;
                        }
                    }
                    break;
                case ItemLayoutType.HorizontalThenVertical:
                    pos.x += size.x;
                    if (addSpace)
                    {
                        pos.x += space.x;
                    }
                    if (pos.x + size.x > this.refRect.width)
                    {
                        pos.x = leftAndTopBound.x;
                        pos.y -= size.y;
                        if (addSpace)
                        {
                            pos.y -= space.y;
                        }
                    }
                    break;
            }
        }

        // const int 代替 enum 减少 (int)和(CriticalItemType)转换
        protected static class CriticalItemType
        {
            public static byte UpToHide = 0;
            public static byte DownToHide = 1;
            public static byte UpToShow = 2;
            public static byte DownToShow = 3;
        }

        private class ScrollItemWithRect
        {
            // scroll item 身上的 RectTransform组件
            public RectTransform item;

            // scroll item 在scrollview中的位置
            public Rect rect;

            // rect 是否需要更新
            public bool rectDirty = true;
        }
    }
}
