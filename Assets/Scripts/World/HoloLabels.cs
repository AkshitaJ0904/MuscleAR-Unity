using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>Fixed clickable tooltips with gesture-aware anatomical connectors.</summary>
    public class HoloLabels : MonoBehaviour
    {
        class MuscleTooltip
        {
            [NonSerialized] public MuscleId id;
            [NonSerialized] public Image background, border, accent;
            [NonSerialized] public Text text;
            [NonSerialized] public Func<Vector3> anchor;
            [NonSerialized] public MuscleConnector connector;
            [NonSerialized] public CanvasGroup connectorGroup;
            [NonSerialized] public Image marker;
            [NonSerialized] public Vector2 exit, elbow;
        }
        readonly List<MuscleTooltip> items = new List<MuscleTooltip>();
        public Camera cam;
        public float textScale = 1f;
        public Action<MuscleId> OnMuscleClicked;
        RectTransform overlay, panels, connectors;
        RectTransform screenUI, persistentUI;
        readonly List<Rect> occupied = new List<Rect>();
        readonly List<Rect> connectorObstacles = new List<Rect>();
        readonly Vector3[] corners = new Vector3[4];
        readonly ConnectorVisibility visibility = new ConnectorVisibility();
        Transform modelRoot;
        Vector3 lastModelPosition, lastModelScale;
        Quaternion lastModelRotation;
        bool poseInitialized, actionMode;
        Rect labellingArea;
        float density;
        [Header("Connector transitions (seconds)")]
        [Min(0.01f)] public float fadeOutDuration = 0.12f;
        [Min(0f)] public float settlingDelay = 0.2f;
        [Min(0.01f)] public float fadeInDuration = 0.2f;
        public Rect LabellingArea => labellingArea;
        public IReadOnlyList<Rect> OccupiedRegions => occupied;
        public ConnectorVisibility.State ConnectorState => visibility.Current;
        public void TrackModel(Transform root) { modelRoot = root; poseInitialized = false; }
        public void SetGestureActive(bool active) { visibility.SetGesture(active, Time.unscaledTime, settlingDelay); }
        public void SetContext(RectTransform screen, RectTransform persistent, bool action)
        { screenUI = screen; persistentUI = persistent; actionMode = action; InvalidateLayout(); }
        public void InvalidateLayout() { layoutDirty = true; }
        HumanBody labelledBody;
        bool layoutDirty = true, selectionInitialized;
        MuscleId? selectedMuscle;
        Rect lastSafe, lastViewport;
        Vector2Int lastResolution;

        public static HoloLabels Create(Transform parent, Camera cam)
        {
            var go = new GameObject("HoloLabels"); go.transform.SetParent(parent, false);
            var labels = go.AddComponent<HoloLabels>(); labels.cam = cam; labels.Initialize(); return labels;
        }
        void Initialize()
        {
            var go = new GameObject("MuscleTooltips", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            go.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            overlay = go.GetComponent<RectTransform>();
            connectors = UIKit.NewRect("Connectors", overlay); UIKit.Fill(connectors);
            panels = UIKit.NewRect("Tooltips", overlay); UIKit.Fill(panels);
        }
        public void Clear()
        {
            foreach (var item in items)
            {
                item.background.gameObject.SetActive(false);
                Destroy(item.background.gameObject);
                item.connectorGroup.gameObject.SetActive(false); Destroy(item.connectorGroup.gameObject);
            }
            items.Clear(); labelledBody = null; layoutDirty = true; selectionInitialized = false;
        }
        public void ShowTargetMuscles(HumanBody body)
        {
            if (body == null || (labelledBody == body && items.Count == 6)) return;
            Clear(); labelledBody = body;
            if (overlay == null) Initialize();
            // Fixed muscle-to-tooltip assignments: no sorting, occlusion hiding or side switching.
            var ids = new[] { MuscleId.Deltoid, MuscleId.PectoralisMajor, MuscleId.BicepsBrachii,
                MuscleId.RectusAbdominis, MuscleId.QuadricepsFemoris, MuscleId.Gastrocnemius };
            for (int i=0;i<ids.Length;i++)
            {
                CreateTooltip(ids[i]);
                items[i].anchor = body.CreateLabelAnchor(ids[i], i<3 ? 1 : -1);
            }
            SetSelectedMuscle(AppFlow.I != null ? AppFlow.I.selected : null);
        }
        void CreateTooltip(MuscleId id)
        {
            var background = UIKit.Img(panels, UIKit.Round, Color.white, "Tooltip_" + id, true, true);
            var border = UIKit.Img(background.transform, UIKit.Ring, Color.white, "Border", true); UIKit.Fill(border.rectTransform);
            var accent = UIKit.Img(background.transform, UIKit.Round, UIKit.BlueText, "SelectedAccent", true);
            accent.rectTransform.anchorMin = new Vector2(0f, 0.22f); accent.rectTransform.anchorMax = new Vector2(0f, 0.78f);
            accent.rectTransform.pivot = new Vector2(0f, 0.5f);
            var text = UIKit.Label(background.transform, MuscleDB.Get(id).name, 12, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Name");
            UIKit.Fill(text.rectTransform);
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f); outline.effectDistance = new Vector2(0.6f, -0.6f);
            var button = background.gameObject.AddComponent<MuscleLabelButton>();
            button.targetGraphic = background; button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => OnMuscleClicked?.Invoke(id));
            MuscleAudioController.AttachClickSound(button);
            var linkRect = UIKit.NewRect("Connector_" + id, connectors); UIKit.Fill(linkRect); linkRect.pivot = Vector2.zero;
            var link = linkRect.gameObject.AddComponent<MuscleConnector>();
            link.color = new Color(0.35f,0.85f,1f,0.9f); link.raycastTarget = false;
            var group = linkRect.gameObject.AddComponent<CanvasGroup>(); group.alpha = 0f;
            group.blocksRaycasts = false; group.interactable = false;
            var marker = UIKit.Img(linkRect,UIKit.Circle,new Color(0.45f,0.95f,1f),"Anchor");
            marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = Vector2.zero;
            marker.rectTransform.pivot = new Vector2(0.5f,0.5f);
            items.Add(new MuscleTooltip { id = id, background = background, border = border, accent = accent, text = text,
                connector = link, connectorGroup = group, marker = marker });
        }
        public void SetSelectedMuscle(MuscleId? id)
        {
            if (selectionInitialized && selectedMuscle == id) return;
            selectionInitialized = true; selectedMuscle = id;
            foreach (var item in items)
            {
                bool selected = id == item.id;
                item.background.color = selected ? new Color(0.06f, 0.22f, 0.40f, 0.94f) : new Color(0.035f, 0.065f, 0.105f, 0.86f);
                item.border.color = selected ? new Color(0.35f, 0.75f, 1f, 0.95f) : new Color(0.50f, 0.68f, 0.85f, 0.30f);
                item.accent.enabled = selected;
                item.text.color = Color.white;
            }
        }
        Rect ScreenBounds(RectTransform rect)
        {
            var canvas = rect.GetComponentInParent<Canvas>();
            Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            rect.GetWorldCorners(corners); Vector2 min = Vector2.positiveInfinity, max = Vector2.negativeInfinity;
            foreach (Vector3 corner in corners)
            { Vector2 p = RectTransformUtility.WorldToScreenPoint(camera,corner); min = Vector2.Min(min,p); max = Vector2.Max(max,p); }
            return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
        }
        static bool Visible(Graphic graphic)
        {
            if (!graphic.isActiveAndEnabled || graphic.color.a < 0.01f) return false;
            foreach (var group in graphic.GetComponentsInParent<CanvasGroup>()) if (group.alpha < 0.01f) return false;
            return true;
        }
        void CollectOccupied(RectTransform root)
        {
            if (root == null || !root.gameObject.activeInHierarchy) return;
            foreach (var selectable in root.GetComponentsInChildren<Selectable>())
            {
                if (!selectable.isActiveAndEnabled) continue;
                var rect = selectable.targetGraphic != null ? selectable.targetGraphic.rectTransform : selectable.transform as RectTransform;
                if (rect != null) occupied.Add(ScreenBounds(rect));
            }
            foreach (var graphic in root.GetComponentsInChildren<Graphic>())
            {
                if (!Visible(graphic)) continue;
                bool panel = graphic is Image && (graphic.name == "Card" || graphic.name.EndsWith("Card"));
                bool text = graphic is Text label && !string.IsNullOrEmpty(label.text) && graphic.GetComponentInParent<ScrollRect>() == null;
                if (panel || text) occupied.Add(ScreenBounds(graphic.rectTransform));
            }
        }
        public static Rect AvailableArea(Rect safe, IReadOnlyList<Rect> blocked, float padding)
        {
            Rect inner = Rect.MinMaxRect(safe.xMin+padding,safe.yMin+padding,safe.xMax-padding,safe.yMax-padding);
            var bands = new List<Vector2> { new Vector2(inner.yMin,inner.yMax) };
            foreach (Rect box in blocked)
            {
                if (box.xMax < inner.xMin || box.xMin > inner.xMax) continue;
                float low = box.yMin-padding, high = box.yMax+padding;
                for (int i=bands.Count-1;i>=0;i--)
                {
                    Vector2 band = bands[i]; if (high <= band.x || low >= band.y) continue;
                    bands.RemoveAt(i);
                    if (low > band.x) bands.Add(new Vector2(band.x,Mathf.Min(low,band.y)));
                    if (high < band.y) bands.Add(new Vector2(Mathf.Max(high,band.x),band.y));
                }
            }
            Vector2 best = new Vector2(inner.center.y,inner.center.y);
            foreach (var band in bands) if (band.y-band.x > best.y-best.x) best = band;
            return Rect.MinMaxRect(inner.xMin,best.x,inner.xMax,best.y);
        }
        public static void ConnectorPoints(Rect box, int slot, float density, out Vector2 exit, out Vector2 elbow)
        {
            float direction = slot<3 ? 1f : -1f;
            exit = new Vector2(slot<3 ? box.xMax : box.xMin,box.center.y);
            elbow = exit + Vector2.right * direction * (12f+slot%3*4f)*density;
        }
        void Layout(Rect safe)
        {
            Canvas.ForceUpdateCanvases();
            density = Mathf.Max(0.5f, Mathf.Min(safe.width / 390f, safe.height / 700f));
            occupied.Clear(); CollectOccupied(screenUI); CollectOccupied(persistentUI);
            labellingArea = AvailableArea(safe, occupied, 7f * density);
            density = Mathf.Max(density*0.8f, Mathf.Min(density,labellingArea.height/152f));
            Rect[] slots = CalculateSlots(labellingArea, density);
            connectorObstacles.Clear(); connectorObstacles.AddRange(occupied); connectorObstacles.AddRange(slots);
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i]; var rect = item.background.rectTransform;
                rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = slots[i].center; rect.sizeDelta = slots[i].size;
                item.background.pixelsPerUnitMultiplier = item.border.pixelsPerUnitMultiplier = 1f / density;
                item.text.fontSize = Mathf.RoundToInt(12f * density * Mathf.Clamp(textScale, 0.95f, 1.1f));
                item.text.rectTransform.offsetMin = new Vector2(8f, 6f) * density;
                item.text.rectTransform.offsetMax = new Vector2(-8f, -6f) * density;
                // Keep single-word muscle names intact instead of orphaning the final letter.
                float preferred = item.text.preferredWidth;
                if (!item.text.text.Contains(" ") && preferred > item.text.rectTransform.rect.width)
                    item.text.fontSize = Mathf.FloorToInt(item.text.fontSize * item.text.rectTransform.rect.width / preferred);
                item.accent.rectTransform.anchoredPosition = new Vector2(4f * density, 0f);
                item.accent.rectTransform.sizeDelta = new Vector2(3f * density, 0f);
                ConnectorPoints(slots[i], i, density, out item.exit, out item.elbow);
                item.marker.rectTransform.sizeDelta = Vector2.one * 5f * density;
            }
            layoutDirty = false;
            RefreshConnectorGeometry(true);
        }
        public static Rect[] CalculateSlots(Rect area, float density)
        {
            float inset = Mathf.Max(1f, density);
            area = Rect.MinMaxRect(area.xMin + inset, area.yMin + inset, area.xMax - inset, area.yMax - inset);
            var slots = new Rect[6];
            float width = Mathf.Min(124f * density, area.width * 0.31f);
            float rowHeight = Mathf.Max(1f, Mathf.Min(44f * density, area.height / 3f - 8f * density));
            for (int i = 0; i < 6; i++)
            {
                Vector2 center = new Vector2(i < 3 ? area.xMin + width * 0.5f : area.xMax - width * 0.5f,
                    area.yMax - area.height * (i % 3 + 0.5f) / 3f);
                slots[i] = new Rect(center - new Vector2(width, rowHeight) * 0.5f, new Vector2(width, rowHeight));
            }
            return slots;
        }
        bool ModelMoving()
        {
            if (modelRoot == null) return false;
            bool moving = poseInitialized && ((modelRoot.localPosition-lastModelPosition).sqrMagnitude > 0.0000000001f ||
                Quaternion.Angle(modelRoot.localRotation,lastModelRotation) > 0.001f || (modelRoot.localScale-lastModelScale).sqrMagnitude > 0.0000000001f);
            lastModelPosition = modelRoot.localPosition; lastModelRotation = modelRoot.localRotation; lastModelScale = modelRoot.localScale;
            poseInitialized = true; return moving;
        }
        void RefreshConnectorGeometry(bool force)
        {
            if (cam == null) return;
            foreach (var item in items)
            {
                if (!force && item.connectorGroup.alpha <= 0f) continue;
                Vector3 projected = cam.WorldToScreenPoint(item.anchor());
                Vector2 point = projected;
                if (projected.z <= cam.nearClipPlane) point = labellingArea.center - (point-labellingArea.center);
                item.connector.SetGeometry(item.exit,item.elbow,point,labellingArea,connectorObstacles,1.5f*density,force);
                bool markerVisible = projected.z > cam.nearClipPlane && labellingArea.Contains(point);
                foreach (Rect box in connectorObstacles) if (box.Contains(point)) { markerVisible = false; break; }
                item.marker.color = new Color(0.45f,0.95f,1f,markerVisible ? 1f : 0f);
                item.marker.rectTransform.anchoredPosition = point;
            }
        }
        public void TickConnectors(float now, float dt, bool moving)
        {
            visibility.Step(now,moving,actionMode,settlingDelay);
            foreach (var item in items)
                item.connectorGroup.alpha = ConnectorVisibility.Fade(item.connectorGroup.alpha,
                    visibility.Shows(item.id,selectedMuscle),dt,fadeOutDuration,fadeInDuration);
            // Geometry is refreshed on the first nonzero fade-in frame before the Canvas renders.
            RefreshConnectorGeometry(false);
        }
        void LateUpdate()
        {
            if (overlay == null || items.Count == 0) return;
            Rect safe = Screen.safeArea;
            Rect viewport = cam != null ? cam.pixelRect : new Rect(0, 0, Screen.width, Screen.height);
            safe = Rect.MinMaxRect(Mathf.Max(safe.xMin, viewport.xMin), Mathf.Max(safe.yMin, viewport.yMin),
                Mathf.Min(safe.xMax, viewport.xMax), Mathf.Min(safe.yMax, viewport.yMax));
            var resolution = new Vector2Int(Screen.width, Screen.height);
            if (layoutDirty || resolution != lastResolution || safe != lastSafe || viewport != lastViewport)
            {
                Layout(safe); lastResolution = resolution; lastSafe = safe; lastViewport = viewport;
            }
            // Selection colour changes only; model motion never drives layout or visibility.
            if (AppFlow.I != null) SetSelectedMuscle(AppFlow.I.selected);
            TickConnectors(Time.unscaledTime,Time.unscaledDeltaTime,ModelMoving());
        }
    }
}
