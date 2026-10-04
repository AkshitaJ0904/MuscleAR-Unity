using System;
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
        public Action<Vector2> OnDrag;      // pixels
        public Action<float> OnPinch;       // multiplicative zoom factor for this frame
        public Action<float> OnTwist;       // degrees
        public Action<Vector2> OnTap;       // screen position

        bool tracking, blocked; Vector2 downPos, lastPos; float downTime, moved;
        float lastPinchDist, lastTwistAngle; bool twoFinger;

        static bool OverUI(int fingerId)
        {
            return EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(fingerId);
        }

        void Update()
        {
            if (Input.touchCount >= 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                Vector2 d = b.position - a.position;
                float dist = d.magnitude, ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
                if (!twoFinger) { twoFinger = true; lastPinchDist = dist; lastTwistAngle = ang; }
                else
                {
                    if (lastPinchDist > 1f && OnPinch != null) OnPinch(dist / lastPinchDist);
                    if (OnTwist != null) OnTwist(Mathf.DeltaAngle(lastTwistAngle, ang));
                    lastPinchDist = dist; lastTwistAngle = ang;
                }
                tracking = false;
                return;
            }
            twoFinger = false;

            if (Input.touchCount == 1)
            {
                Touch t = Input.GetTouch(0);
                switch (t.phase)
                {
                    case TouchPhase.Began: Down(t.position, OverUI(t.fingerId)); break;
                    case TouchPhase.Moved: case TouchPhase.Stationary: Move(t.position); break;
                    case TouchPhase.Ended: case TouchPhase.Canceled: Up(t.position); break;
                }
                return;
            }

            // mouse
            if (Input.GetMouseButtonDown(0)) Down(Input.mousePosition, OverUI(-1));
            else if (Input.GetMouseButton(0)) Move(Input.mousePosition);
            else if (Input.GetMouseButtonUp(0)) Up(Input.mousePosition);

            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.001f && !OverUI(-1) && OnPinch != null) OnPinch(1f + scroll * 0.06f);
            if (Input.GetMouseButton(1) && !OverUI(-1) && OnTwist != null)
                OnTwist(-Input.GetAxis("Mouse X") * 6f);
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
            blocked = overUI; tracking = !overUI;
            downPos = lastPos = p; downTime = Time.time; moved = 0f;
        }

        void Move(Vector2 p)
        {
            if (!tracking || blocked) return;
            Vector2 d = p - lastPos; lastPos = p;
            moved += d.magnitude;
            if (moved > 10f && OnDrag != null) OnDrag(d);
        }

        void Up(Vector2 p)
        {
            if (tracking && !blocked && moved < 14f && Time.time - downTime < 0.45f && OnTap != null) OnTap(p);
            tracking = false; blocked = false;
        }
    }
}
