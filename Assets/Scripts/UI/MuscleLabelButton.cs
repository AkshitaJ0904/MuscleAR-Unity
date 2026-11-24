using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MuscleAR
{
    /// <summary>A real label-sized UI Button, with tap-only activation.</summary>
    public class MuscleLabelButton : Button, IBeginDragHandler, IDragHandler
    {
        Vector2 down;
        float downTime;
        int pointer;
        bool canceled;
        public override void OnPointerDown(PointerEventData data)
        {
            down = data.position; downTime = Time.unscaledTime; pointer = data.pointerId;
            canceled = Input.touchCount > 1;
            base.OnPointerDown(data);
        }
        void Update() { if (Input.touchCount > 1) canceled = true; }
        public void OnBeginDrag(PointerEventData data) { CheckDrag(data); }
        public void OnDrag(PointerEventData data) { CheckDrag(data); }
        void CheckDrag(PointerEventData data)
        {
            canceled |= Vector2.Distance(down, data.position) > 10f || data.pointerId != pointer;
        }
        public override void OnPointerUp(PointerEventData data) { CheckDrag(data); base.OnPointerUp(data); }
        public override void OnPointerClick(PointerEventData data)
        {
            CheckDrag(data);
            if (!canceled && Input.touchCount < 2 && Time.unscaledTime - downTime < 0.45f)
                base.OnPointerClick(data);
        }
    }
}
