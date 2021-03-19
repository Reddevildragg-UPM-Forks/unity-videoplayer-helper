#region

using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#endregion

namespace Unity.VideoHelper
{
    [Serializable]
    public class FloatEvent : UnityEvent<float>
    {
    }

    /// <summary>
    ///     A slider-like component specialized in media playback.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class Timeline : Selectable, IDragHandler, ICanvasElement, IInitializePotentialDragHandler
    {
#region Fields

        Image positionImage, previewImage;
        Text tooltipText;
        RectTransform positionContainerRect, handleContainerRect, tooltipContainerRect;
        Vector2 handleOffset;
        Camera cam;
        DrivenRectTransformTracker tracker;
        float previewPosition;
        readonly float stepSize = 0.05f;
        bool isInControl;
        ITimelineProvider provider;

        [SerializeField]
        RectTransform positionRect, previewRect, handleRect, tooltipRect;

        RectTransform previewHolder;
        
        [SerializeField]
        [Range(0, 1)]
        float position;

        [SerializeField]
        FloatEvent onSeeked = new FloatEvent();

#endregion

#region Properties

        public RectTransform PositionRect
        {
            get => positionRect;
            set => positionRect = value;
        }

        public RectTransform PreviewRect
        {
            get => previewRect;
            set => previewRect = value;
        }

        public RectTransform HandleRect
        {
            get => handleRect;
            set => handleRect = value;
        }

        public RectTransform TooltipRect
        {
            get => tooltipRect;
            set => tooltipRect = value;
        }

        public float Position
        {
            get => position;
            set => SetPosition(value);
        }

        public UnityEvent<float> OnSeeked => onSeeked;

#endregion

#region Unity methods

        protected override void OnEnable()
        {
            base.OnEnable();

            UpdateReferences();
            SetPosition(position, false);
            UpdateVisuals();

            previewHolder = previewRect.parent.GetComponent<RectTransform>();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            tracker.Clear();
        }

        protected override void OnRectTransformDimensionsChange()
        {
            base.OnRectTransformDimensionsChange();

            if (IsActive())
            {
                UpdateVisuals();
            }
        }

        void Update()
        {
            if (!isInControl)
            {
                return;
            }

            float newPreviewPosition = GetPreviewPoint();
            if (newPreviewPosition == previewPosition)
            {
                return;
            }

            previewPosition = newPreviewPosition;

            UpdateFillableVisuals(previewRect, previewImage, previewPosition);
            UpdateAnchorBasedVisuals(tooltipRect, previewPosition);

            tooltipText.text = provider.GetFormattedPosition(previewPosition);
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (IsActive())
            {
                UpdateReferences();
                SetPosition(position, false);
                UpdateVisuals();
            }

            PrefabType prefabType = PrefabUtility.GetPrefabType(this);
            if (prefabType != PrefabType.Prefab && !Application.isPlaying)
            {
                CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
            }
        }
#endif

#endregion

#region Methods

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }

            Func<bool> isAutomatic = () => navigation.mode == Navigation.Mode.Automatic;

            switch (eventData.moveDir)
            {
                case MoveDirection.Left:
                    if (isAutomatic())
                    {
                        Move(position - stepSize);
                    }
                    else
                    {
                        base.OnMove(eventData);
                    }

                    break;
                case MoveDirection.Right:
                    if (isAutomatic())
                    {
                        Move(position + stepSize);
                    }
                    else
                    {
                        base.OnMove(eventData);
                    }

                    break;
                case MoveDirection.Down:
                case MoveDirection.Up:
                    base.OnMove(eventData);
                    break;
            }
        }

        void Move(float value)
        {
            SetPosition(value);
            onSeeked.Invoke(value);
        }

        void UpdateReferences()
        {
            if (positionRect)
            {
                positionImage = positionRect.GetComponent<Image>();
                positionContainerRect = positionRect.parent.GetComponent<RectTransform>();
            }
            else
            {
                positionRect = null;
                positionImage = null;
                positionContainerRect = null;
            }

            if (previewRect)
            {
                previewImage = previewRect.GetComponent<Image>();
            }
            else
            {
                previewRect = null;
                previewImage = null;
            }

            if (handleRect)
            {
                handleContainerRect = handleRect.parent.GetComponent<RectTransform>();
            }
            else
            {
                handleRect = null;
                handleContainerRect = null;
            }

            if (tooltipRect)
            {
                tooltipContainerRect = tooltipRect.parent.GetComponent<RectTransform>();
                tooltipText = tooltipRect.GetComponentInChildren<Text>();
            }
            else
            {
                tooltipRect = null;
                tooltipContainerRect = null;
            }

            cam = Camera.main;
            provider = GetComponentInParent<VideoPresenter>();
        }

        void UpdateVisuals()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UpdateReferences();
            }
#endif

            tracker.Clear();

            if (positionContainerRect)
            {
                UpdateFillableVisuals(positionRect, positionImage, position);
            }

            if (!isInControl)
            {
                UpdateFillableVisuals(previewRect, previewImage, position);
            }

            if (handleContainerRect)
            {
                UpdateAnchorBasedVisuals(handleRect, position);
            }
        }

        protected void UpdateAnchorBasedVisuals(RectTransform rect, float position)
        {
            if (rect == null)
            {
                return;
            }

            tracker.Add(this, rect, DrivenTransformProperties.Anchors);

            Vector2 anchorMin = Vector2.zero;
            Vector2 anchorMax = Vector2.one;

            anchorMin[0] = anchorMax[0] = position;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
        }

        protected void UpdateFillableVisuals(RectTransform rect, Image image, float value)
        {
            if (rect == null)
            {
                return;
            }

            tracker.Add(this, rect, DrivenTransformProperties.Anchors);

            Vector2 anchorMax = Vector2.one;

            if (image != null && image.type == Image.Type.Filled)
            {
                image.fillAmount = value;
            }
            else
            {
                anchorMax[0] = value;
            }

            rect.anchorMin = Vector2.zero;
            rect.anchorMax = anchorMax;
        }

        bool CanDrag()
        {
            return IsActive() && IsInteractable();
        }

        void UpdateDrag(PointerEventData eventData, Camera cam)
        {
            RectTransform clickRect = handleContainerRect ?? positionContainerRect;
            if (clickRect != null && clickRect.rect.size[0] > 0)
            {
                Vector2 localCursor;
                if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(clickRect, eventData.position, cam, out localCursor))
                {
                    return;
                }

                localCursor -= clickRect.rect.position;

                Position = Mathf.Clamp01((localCursor - handleOffset)[0] / clickRect.rect.size[0]);
                onSeeked.Invoke(Position);
            }
        }

        float GetPreviewPoint()
        {
            Vector2 x = (Vector2) previewHolder.InverseTransformPoint(Input.mousePosition) - previewHolder.rect.position;
            return Mathf.Clamp01(x[0] / previewHolder.rect.size.x);
        }

        void SetPosition(float newPosition, bool sendCallback = true)
        {
            newPosition = Mathf.Clamp01(newPosition);

            if (position == newPosition)
            {
                return;
            }

            position = newPosition;

            UpdateVisuals();
        }

#endregion

#region IPointerEnter, IPointerExit, IPointerDown, IDragHandler members

        public override void OnPointerEnter(PointerEventData eventData)
        {
            isInControl = true;

            if (handleRect != null)
            {
                SetActive(handleRect.gameObject, true);
            }

            if (tooltipRect != null)
            {
                tooltipRect.gameObject.SetActive(true);
            }
        }

        void SetActive(GameObject gameObject, bool value)
        {
            if (gameObject == null)
            {
                return;
            }

            if (value)
            {
                IActivate activate = gameObject.GetComponent<IActivate>();
                if (activate != null)
                {
                    activate.Activate();
                    return;
                }
            }
            else
            {
                IDeactivate deactivate = gameObject.GetComponent<IDeactivate>();
                if (deactivate != null)
                {
                    deactivate.Deactivate();
                    return;
                }
            }

            gameObject.SetActive(value);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            isInControl = false;

            if (handleRect != null)
            {
                SetActive(handleRect.gameObject, false);
            }

            if (tooltipRect != null)
            {
                tooltipRect.gameObject.SetActive(false);
            }

            UpdateFillableVisuals(previewRect, previewImage, 0);
        }

        public override void OnPointerDown(PointerEventData eventData)
        {
            if (!CanDrag())
            {
                return;
            }

            base.OnPointerDown(eventData);

            handleOffset = Vector2.zero;
            if (handleContainerRect != null && RectTransformUtility.RectangleContainsScreenPoint(handleRect, eventData.position, eventData.enterEventCamera))
            {
                Vector2 localMousePos;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(handleRect, eventData.position, eventData.pressEventCamera, out localMousePos))
                {
                    handleOffset = localMousePos;
                }

                handleOffset.y = -handleOffset.y;
            }
            else
            {
                UpdateDrag(eventData, eventData.pressEventCamera);
            }
        }

        public virtual void OnDrag(PointerEventData eventData)
        {
            if (!CanDrag())
            {
                return;
            }

            isInControl = true;
            UpdateDrag(eventData, eventData.pressEventCamera);
        }

#endregion

#region ICanvasElement members

        public void Rebuild(CanvasUpdate executing)
        {
        }

        public void LayoutComplete()
        {
        }

        public void GraphicUpdateComplete()
        {
        }

#endregion

#region IInitializePotentialDragHandler members

        public void OnInitializePotentialDrag(PointerEventData eventData)
        {
            eventData.useDragThreshold = false;
        }

#endregion
    }
}