using UnityEngine;

namespace MuscleAR
{
    /// <summary>One clock-driven state machine; no coroutines or delayed callbacks.</summary>
    public sealed class ConnectorVisibility
    {
        public enum State { Idle, Manipulating, Settling, Action }
        public State Current { get; private set; }
        bool gesture;
        float restoreAt = float.NegativeInfinity;
        public void SetGesture(bool active, float now, float settle)
        {
            if (gesture == active) return;
            gesture = active; restoreAt = now + settle;
        }
        public void Step(float now, bool moving, bool action, float settle)
        {
            if (gesture || moving) restoreAt = now + settle;
            Current = gesture || moving ? State.Manipulating : now < restoreAt ? State.Settling :
                action ? State.Action : State.Idle;
        }
        public bool Shows(MuscleId id, MuscleId? selected)
        { return Current == State.Idle || (Current == State.Action && selected == id); }
        public static float Fade(float alpha, bool visible, float dt, float fadeOut, float fadeIn)
        { return Mathf.MoveTowards(alpha, visible ? 1f : 0f, dt / Mathf.Max(0.001f, visible ? fadeIn : fadeOut)); }
    }
}
