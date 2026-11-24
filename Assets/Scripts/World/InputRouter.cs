using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MuscleAR
{
    /// <summary>
    /// Unifies touch (drag / pinch / twist / tap) and mouse (drag / scroll / right-drag / click) input.
    /// Pointers that start over UI are ignored so buttons and sliders keep working.
    /// </summary>
    public class InputRouter : MonoBehaviour
    {
        public Action<Vector2> OnDrag;          // delta pixels
        public Action<Vector2> OnPan;           // two-finger / right-mouse delta pixels
        public Func<Vector2, bool> CanManipulate;
        public Action<float> OnPinch;           // multiplicative zoom factor for this frame
        public Action<float> OnTwist;           // degrees
        public Action<Vector2> OnTap;           // screen position
        public Action<Vector2> OnDown;          // screen position when pointer goes down (not over UI)
        public Action<Vector2> OnMovePosition;  // absolute screen position while moving (not over UI)
        public Action<Vector2> OnUp;            // screen position when pointer is released
        public Action OnTwoFingerBegan;         // fired when two-finger gesture begins

        public bool SuppressTap { get; set; }   // suppresses OnTap when dragging or repositioning
        public bool IsManipulating { get; private set; }
        public Action<bool> OnManipulationChanged;
        bool scrollActivity;

        bool tracking, blocked; Vector2 downPos, lastPos; float downTime, moved;
        float lastPinchDist; Vector2 lastTwoFingerCenter; bool twoFinger, twoFingerBlocked;
        bool rightMouseTracking;
        int firstFinger = -1, secondFinger = -1;
        static readonly List<RaycastResult> uiHits = new List<RaycastResult>();

        static bool OverUI(int fingerId, Vector2 position)
        {
            var events = EventSystem.current;
            if (events == null) return false;
            var pointer = new PointerEventData(events) { pointerId = fingerId, position = position };
            uiHits.Clear();
            events.RaycastAll(pointer, uiHits);
            foreach (var hit in uiHits)
            {
                // Decorative panels and empty background graphics do not capture gestures.
                if (ExecuteEvents.GetEventHandler<IPointerClickHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IPointerDownHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IDragHandler>(hit.gameObject) != null ||
                    ExecuteEvents.GetEventHandler<IScrollHandler>(hit.gameObject) != null) return true;
            }
            return false;
        }

        void Update()
        {
            scrollActivity = false;
            ReadInput();
            bool keys = Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.Equals) ||
                Input.GetKey(KeyCode.KeypadPlus) || Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus);
            bool active = (twoFinger && !twoFingerBlocked) || (tracking && !blocked && SuppressTap) ||
                (rightMouseTracking && Input.GetMouseButton(1)) || scrollActivity || keys;
            SetManipulating(active);
        }
        void SetManipulating(bool active)
        {
            if (IsManipulating == active) return;
            IsManipulating = active; OnManipulationChanged?.Invoke(active);
        }
        void OnDisable() { SetManipulating(false); }
        void ReadInput()
        {
            if (Input.touchCount >= 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                Vector2 d = b.position - a.position;
                Vector2 center = (a.position + b.position) * 0.5f;
                float dist = d.magnitude;
                if (!twoFinger || firstFinger != a.fingerId || secondFinger != b.fingerId)
                {
                    twoFinger = true;
                    firstFinger = a.fingerId; secondFinger = b.fingerId;
                    twoFingerBlocked = blocked || OverUI(a.fingerId, a.position) || OverUI(b.fingerId, b.position);
                    lastPinchDist = dist;
                    lastTwoFingerCenter = center;
                    if (!twoFingerBlocked) OnTwoFingerBegan?.Invoke();
                }
                else if (!twoFingerBlocked && a.phase != TouchPhase.Ended && b.phase != TouchPhase.Ended &&
                    a.phase != TouchPhase.Canceled && b.phase != TouchPhase.Canceled)
                {
                    Vector2 panDelta = center - lastTwoFingerCenter;
                    if (panDelta.sqrMagnitude > 0.01f) OnPan?.Invoke(panDelta);
                    if (lastPinchDist > 1f && OnPinch != null)
                        OnPinch(Mathf.Clamp(dist / lastPinchDist, 0.94f, 1.06f));
                }
                lastPinchDist = dist; lastTwoFingerCenter = center;
                tracking = false;
                SuppressTap = true;
                return;
            }
            bool wasTwoFinger = twoFinger;
            twoFinger = false; twoFingerBlocked = false;

            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                if (wasTwoFinger)
                {
                    // Rebase the remaining finger so lifting a finger cannot jump or select a muscle.
                    blocked = blocked || OverUI(t.fingerId, t.position);
                    tracking = !blocked;
                    downPos = lastPos = t.position; moved = 0f; downTime = Time.time;
                    SuppressTap = true;
                    return;
                }
                switch (t.phase)
                {
                    case TouchPhase.Began: Down(t.position, OverUI(t.fingerId, t.position)); break;
                    case TouchPhase.Moved: case TouchPhase.Stationary: Move(t.position); break;
                    case TouchPhase.Ended: Up(t.position); break;
                    case TouchPhase.Canceled: SuppressTap = true; Up(t.position); break;
                }
                return;
            }

            // mouse
            if (Input.GetMouseButtonDown(0)) Down(Input.mousePosition, OverUI(-1, Input.mousePosition));
            else if (Input.GetMouseButton(0)) Move(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) Up(Input.mousePosition);

            if (Input.GetMouseButtonDown(1)) rightMouseTracking = !OverUI(-1, Input.mousePosition);
            if (Input.GetMouseButtonUp(1)) rightMouseTracking = false;
            if (Input.GetMouseButton(1) && rightMouseTracking && OnPan != null)
                OnPan(new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 12f);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f && !OverUI(-1, Input.mousePosition) && OnPinch != null)
            { scrollActivity = true; OnPinch(1f + scroll * 0.06f); }
            if (OnTwist != null)
            {
                if (Input.GetKey(KeyCode.Q)) OnTwist(60f * Time.deltaTime);
                if (Input.GetKey(KeyCode.E)) OnTwist(-60f * Time.deltaTime);
            }
            if (OnPinch != null)
            {
                if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) OnPinch(1f + 0.8f * Time.deltaTime);
                if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) OnPinch(1f - 0.8f * Time.deltaTime);
            }
        }

        void Down(Vector2 p, bool overUI)
        {
            blocked = overUI; tracking = !overUI; SuppressTap = false;
            downPos = lastPos = p; downTime = Time.time; moved = 0f;
            if (!blocked && OnDown != null) OnDown(p);
        }

        void Move(Vector2 p)
        {
            if (!tracking || blocked) return;
            Vector2 d = p - lastPos; lastPos = p;
            moved += d.magnitude;
            if (OnMovePosition != null) OnMovePosition(p);
            if (moved > 10f)
            {
                SuppressTap = true;
                OnDrag?.Invoke(d);
            }
        }

        void Up(Vector2 p)
        {
            if (!blocked && OnUp != null) OnUp(p);
            if (tracking && !blocked && !SuppressTap && moved < 14f && Time.time - downTime < 0.45f && OnTap != null)
                OnTap(p);
            tracking = false; blocked = false; SuppressTap = false;
        }
    }
}
