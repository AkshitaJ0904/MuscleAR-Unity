using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    // ===================================================================================================
    // 09 SELECT A BODY PART  -  DISCOVER
    // ===================================================================================================
    public class SelectPartScreen : ScreenBase
    {
        Text headline, hint; RectTransform reticle, reticleGlow;
        MuscleId? picked; float pickT;
        public SelectPartScreen(AppFlow f) : base(f, 9, "SELECT A BODY PART", "DISCOVER") { }

        public static void LateralPose(AppFlow f, float dist = 1.42f)
        {
            f.rig.Frame(new Vector3(dist, 1.12f, -0.22f), new Vector3(0.05f, 0.93f, -0.36f), 46f, 1.8f);
        }

        public override void Build()
        {
            TopShade(300, 0.6f);
            BottomShade(360, 0.9f);
            TopRow("DISCOVER");
            headline = Headline("Select a body part", 33, 6);

            var glow = UIKit.Img(root, UIKit.Circle, new Color(0.2f, 0.5f, 1f, 0.16f), "ReticleGlow");
            BC(glow.rectTransform, 118, 146, 146); reticleGlow = glow.rectTransform;
            var fill = UIKit.Img(root, UIKit.Circle, new Color(0.05f, 0.1f, 0.2f, 0.75f), "ReticleFill");
            BC(fill.rectTransform, 128, 124, 124);
            var ring = UIKit.Img(root, UIKit.CircleRing, new Color(0.38f, 0.66f, 1f, 1f), "Reticle");
            BC(ring.rectTransform, 128, 124, 124); reticle = ring.rectTransform;
            hint = UIKit.Label(root, "Tap a muscle to reveal its identity.", 15, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Hint");
            BC(hint.rectTransform, 78, 342, 24);
            var b = UIKit.NextArrow(root, () => { if (!picked.HasValue) Select(MuscleId.BicepsBrachii); }, 50);
            BR(b.GetComponent<RectTransform>(), 24, 150, 50, 50);
        }

        public override void Enter()
        {
            base.Enter();
            picked = null; pickT = 0;
            headline.text = "Select a body part";
            hint.text = "Tap a muscle to reveal its identity.";
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle);
            f.body.SetHint(MuscleId.BicepsBrachii);
            LateralPose(f);
            var b = f.body; var L = f.labels;
            L.Add(() => b.MuscleCenter(MuscleId.PectoralisMajor, 1), new Vector2(-0.30f, 0.55f), "LEFT PECTORALIS MAJOR");
            L.Add(() => b.AnchorPos("clavicle_L"), new Vector2(0.5f, 0.55f), "CLAVICLE");
            L.Add(() => b.MuscleCenter(MuscleId.Deltoid, 1), new Vector2(-0.55f, -0.22f), "DELTOID");
            L.Add(() => b.MuscleCenter(MuscleId.Brachialis, 1), new Vector2(-0.35f, -0.52f), "BRACHIALIS");
            L.Add(() => b.MuscleCenter(MuscleId.TricepsBrachii, 1), new Vector2(0.1f, -0.62f), "TRICEPS BRACHII");
            L.Add(() => b.MuscleCenter(MuscleId.BicepsBrachii, 1), new Vector2(0.62f, -0.28f), "BICEPS BRACHII", "(Long Head / Short Head)\nORIGIN: Scapula  INSERTION: Radius");
        }

        public override void OnTap(Vector2 p)
        {
            if (picked.HasValue) return;
            MuscleId id;
            if (f.model.Pick(p, out id)) Select(id);
        }

        public void Select(MuscleId id)
        {
            picked = id; pickT = 0f;
            f.selected = id; f.MarkExplored(id);
            f.body.SetHint(null); f.body.Select(id);
            var info = MuscleDB.Get(id);
            headline.text = info.name;
            hint.text = "Tap → " + Short(info.name) + " selected.";
        }

        public override void Tick()
        {
            base.Tick();
            float s = 1f + 0.05f * Mathf.Sin(T * 3f);
            reticleGlow.localScale = new Vector3(s, s, 1);
            if (picked.HasValue)
            {
                pickT += Time.deltaTime;
                reticle.localScale = Vector3.one * (1f + 0.12f * Mathf.Sin(Mathf.Min(pickT, 0.5f) * Mathf.PI * 2f));
                if (pickT > 1.15f) f.Next();
            }
        }
    }

    // ===================================================================================================
    // 10 EDUCATIONAL TOAST  -  LEARN
    // ===================================================================================================
    public class EducationalToastScreen : ScreenBase
    {
        Text headline, cardTitle; Text[] rows = new Text[4]; RectTransform card;
        public EducationalToastScreen(AppFlow f) : base(f, 10, "EDUCATIONAL TOAST", "LEARN") { }

        public override void Build()
        {
            TopShade(300, 0.6f);
            BottomShade(420, 0.88f);
            TopRow("LEARN");
            headline = Headline("Biceps selected", 33, 6);

            card = UIKit.Card(root, new Color(0.04f, 0.07f, 0.13f, 0.94f), UIKit.BlueBtn, "InfoCard");
            BC(card, 70, 276, 252);
            cardTitle = UIKit.Label(card, "BICEPS BRACHII", 17, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperLeft, "Name");
            UIKit.At(cardTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -16), new Vector2(240, 24));
            for (int i = 0; i < 4; i++)
            {
                rows[i] = UIKit.Label(card, "", 15, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, "Row" + i);
                UIKit.At(rows[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -48 - i * 28), new Vector2(240, 22));
            }
            var b = UIKit.PrimaryButton(card, "Explore Details", true, () => f.Next(), 240, 52);
            UIKit.At(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(240, 52));
        }

        public override void Enter()
        {
            base.Enter();
            var id = f.selected ?? MuscleId.BicepsBrachii; f.selected = id;
            var info = MuscleDB.Get(id);
            headline.text = Short(info.name) + " selected";
            cardTitle.text = info.name.ToUpper();
            rows[0].text = "Location / " + info.location;
            rows[1].text = "Origin / " + info.origin;
            rows[2].text = "Insertion / " + info.insertion;
            rows[3].text = "Function / " + info.function;
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(true);
            f.body.SetLayer(BodyLayer.Muscle);
            f.body.Select(id);
            f.rig.Frame(new Vector3(0.0f, 1.95f, 1.85f), new Vector3(0, 0.82f, -0.25f), 48f, 1.8f);
        }
    }

    // ===================================================================================================
    // 11 EXPLORE LAYERS  -  EXPLORE
    // ===================================================================================================
    public class ExploreLayersScreen : ScreenBase
    {
        class Chip { public Image bg, ring; public Text label; public GameObject check; public BodyLayer layer; }
        readonly List<Chip> chips = new List<Chip>();

        public ExploreLayersScreen(AppFlow f) : base(f, 11, "EXPLORE LAYERS", "EXPLORE") { }

        public override void Build()
        {
            TopShade(300, 0.6f);
            BottomShade(260, 0.9f);
            TopRow("EXPLORE");
            Headline("Reveal deeper\nstructures", 33, 6);
            var meta = UIKit.Label(root, "ANATOMY 3.4  |  SUBJECT A  |  09:42 PM", 8, FontStyle.Bold, new Color(1, 1, 1, 0.45f), TextAnchor.MiddleCenter, "Meta");
            TC(meta.rectTransform, Top + 216, 300, 12);

            var row = UIKit.NewRect("Chips", root);
            BC(row, 34, 360, 38);
            var hl = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10; hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = hl.childControlHeight = true; hl.childForceExpandWidth = hl.childForceExpandHeight = false;
            string[] names = { "Skin", "Muscle", "Tendon", "Bone" };
            BodyLayer[] layers = { BodyLayer.Skin, BodyLayer.Muscle, BodyLayer.Tendon, BodyLayer.Bone };
            for (int i = 0; i < 4; i++)
            {
                var c = new Chip { layer = layers[i] };
                c.bg = UIKit.Img(row, UIKit.Round, Color.black, "Chip" + names[i], true, true);
                var h = c.bg.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(16, 16, 9, 9); h.spacing = 6; h.childAlignment = TextAnchor.MiddleCenter;
                h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
                c.label = UIKit.Label(c.bg.transform, names[i], 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "L");
                c.label.horizontalOverflow = HorizontalWrapMode.Overflow;
                var ck = UIKit.Icon(c.bg.transform, UIKit.ICheck, UIKit.BlueText, 14, "Check");
                var le = ck.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 14; le.preferredHeight = 14;
                c.check = ck.gameObject;
                c.ring = UIKit.Img(c.bg.transform, UIKit.Ring, Color.white, "Border", true); UIKit.Fill(c.ring.rectTransform);
                var ig = c.ring.gameObject.AddComponent<LayoutElement>(); ig.ignoreLayout = true;
                var b = c.bg.gameObject.AddComponent<Button>(); b.targetGraphic = c.bg; b.transition = UnityEngine.UI.Selectable.Transition.None;
                c.bg.gameObject.AddComponent<PressScale>();
                var cc = c; b.onClick.AddListener(() => Choose(cc.layer));
                chips.Add(c);
            }
            Fab(92);
            Choose(BodyLayer.Muscle);
        }

        void Choose(BodyLayer l)
        {
            f.body.SetLayer(l);
            foreach (var c in chips)
            {
                bool on = c.layer == l;
                c.bg.color = on ? new Color(0.1f, 0.24f, 0.5f, 0.95f) : new Color(0.06f, 0.09f, 0.16f, 0.9f);
                c.ring.color = on ? UIKit.BlueBtn : new Color(0.3f, 0.4f, 0.55f, 0.6f);
                c.label.color = on ? UIKit.BlueText : Color.white;
                c.check.SetActive(on);
            }
        }

        public override void Enter()
        {
            base.Enter();
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            Choose(BodyLayer.Muscle);
            f.rig.Frame(new Vector3(1.0f, 1.12f, -0.12f), new Vector3(0.12f, 0.93f, -0.4f), 44f, 1.8f);
            var b = f.body; var L = f.labels;
            L.Add(() => b.MuscleCenter(MuscleId.BicepsBrachii, 1), new Vector2(-0.18f, 0.55f), "BICEPS");
            L.Add(() => b.MuscleCenter(MuscleId.Deltoid, 1), new Vector2(-0.6f, 0.28f), "DELTOID");
            L.Add(() => b.AnchorPos("scapula_L"), new Vector2(0.45f, 0.62f), "SCAPULA");
            L.Add(() => b.MuscleCenter(MuscleId.Brachialis, 1), new Vector2(0.62f, 0.3f), "BRACHIAL");
            L.Add(() => b.AnchorPos("humerus_L"), new Vector2(-0.5f, -0.45f), "HUMERUS");
            L.Add(() => b.MuscleCenter(MuscleId.TricepsBrachii, 1), new Vector2(0.28f, -0.55f), "TRICEPS");
        }

        public override void Exit() { f.body.SetLayer(BodyLayer.Muscle); }
    }

    // ===================================================================================================
    // 12 MUSCLE DETAILS  -  ANATOMY  (split view: full body above, close-up below)
    // ===================================================================================================
    public class MuscleDetailsScreen : ScreenBase
    {
        Text headline; Text[] chips = new Text[4]; Text cardTitle, cardBody;
        public MuscleDetailsScreen(AppFlow f) : base(f, 12, "MUSCLE DETAILS", "ANATOMY") { }

        public override void Build()
        {
            TopShade(240, 0.6f);
            BottomShade(300, 0.55f);
            var split = UIKit.Img(root, UIKit.White, new Color(0.26f, 0.53f, 0.96f, 0.55f), "SplitLine");
            UIKit.At(split.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(0, 2));
            split.rectTransform.anchorMax = new Vector2(1, 0.5f); split.rectTransform.sizeDelta = new Vector2(0, 2);
            TopRow("ANATOMY");
            headline = Headline("Biceps Brachii", 33, 6);

            var col = UIKit.NewRect("Chips", root);
            UIKit.At(col, new Vector2(0, 0), new Vector2(0, 0), new Vector2(112, 150), new Vector2(250, 190));
            var v = col.gameObject.AddComponent<VerticalLayoutGroup>();
            v.spacing = 8; v.childAlignment = TextAnchor.LowerLeft;
            v.childControlWidth = v.childControlHeight = true; v.childForceExpandWidth = v.childForceExpandHeight = false;
            for (int i = 0; i < 4; i++)
            {
                var chip = UIKit.Img(col, UIKit.Round, new Color(0.1f, 0.24f, 0.5f, 0.88f), "Chip" + i, true);
                var ring = UIKit.Img(chip.transform, UIKit.Ring, UIKit.BlueBtn, "Border", true); UIKit.Fill(ring.rectTransform);
                var ig = ring.gameObject.AddComponent<LayoutElement>(); ig.ignoreLayout = true;
                var h = chip.gameObject.AddComponent<HorizontalLayoutGroup>();
                h.padding = new RectOffset(16, 16, 9, 9); h.childAlignment = TextAnchor.MiddleLeft;
                h.childControlWidth = h.childControlHeight = true; h.childForceExpandWidth = h.childForceExpandHeight = false;
                chips[i] = UIKit.Label(chip.transform, "", 14, FontStyle.Bold, UIKit.BlueText, TextAnchor.MiddleLeft, "L");
                chips[i].horizontalOverflow = HorizontalWrapMode.Overflow;
                var fit = chip.gameObject.AddComponent<ContentSizeFitter>();
                fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize; fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            }
            var card = UIKit.Card(root, new Color(0.05f, 0.08f, 0.14f, 0.9f), new Color(0.3f, 0.4f, 0.6f, 0.5f), "InfoCard");
            BC(card, 34, 270, 96);
            cardTitle = UIKit.Label(card, "Biceps Brachii", 18, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "T");
            UIKit.At(cardTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -14), new Vector2(240, 24));
            cardBody = UIKit.Label(card, "", 14, FontStyle.Normal, UIKit.Dim, TextAnchor.UpperLeft, "B");
            UIKit.At(cardBody.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -42), new Vector2(236, 48));
            Fab(144);
        }

        public override void Enter()
        {
            base.Enter();
            var id = f.selected ?? MuscleId.BicepsBrachii; f.selected = id; f.MarkExplored(id);
            var info = MuscleDB.Get(id);
            headline.text = info.name;
            chips[0].text = "ORIGIN / " + info.origin;
            chips[1].text = "INSERTION / " + info.insertion;
            chips[2].text = "ACTION / " + info.function;
            chips[3].text = id == MuscleId.BicepsBrachii ? "SUPINATION" : info.latin.ToUpper();
            cardTitle.text = info.name;
            cardBody.text = info.detail.Replace("Primary actions: ", "Primary actions: ");
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle); f.body.Select(id);
            // top half: whole body from the side;  bottom half: close-up of the selected muscle
            f.rig.Frame(new Vector3(3.1f, 1.25f, 0.0f), new Vector3(0, 0.95f, 0.0f), 40f, 1.8f);
            Vector3 c = f.MuscleCenter(id, 1);
            f.rig.SetSplit(true, c + new Vector3(0.62f, 0.3f, 0.12f), c, 36f);
        }

        public override void Exit() { f.rig.SetSplit(false); }
    }

    // ===================================================================================================
    // 13 MUSCLE ACTION  -  ACTION  (not in the shared screenshots; inferred from the "145° FLEXION" callout in 15)
    // ===================================================================================================
    public class MuscleActionScreen : ScreenBase
    {
        UISlider slider; Text readout; bool auto = true; float phase;
        public MuscleActionScreen(AppFlow f) : base(f, 13, "MUSCLE IN ACTION", "ACTION") { }

        public override void Build()
        {
            TopShade(300, 0.6f);
            BottomShade(340, 0.9f);
            TopRow("ACTION");
            Headline("See it in action", 33, 6);

            var card = UIKit.Card(root, new Color(0.05f, 0.08f, 0.14f, 0.9f), UIKit.BlueBtn, "ActionCard");
            BC(card, 34, 342, 150);
            var t = UIKit.Label(card, "BICEPS BRACHII", 15, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperLeft, "T");
            UIKit.At(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(200, 20));
            readout = UIKit.Label(card, "0° FLEXION", 15, FontStyle.Bold, Color.white, TextAnchor.UpperRight, "R");
            UIKit.At(readout.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-20, -16), new Vector2(150, 20));
            var d = UIKit.Label(card, "The biceps contracts to flex the elbow and supinate the forearm. Drag the slider to control the movement.", 13, FontStyle.Normal, UIKit.Dim, TextAnchor.UpperLeft, "D");
            UIKit.At(d.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -42), new Vector2(302, 40));
            slider = UISlider.Create(card, 250);
            UIKit.At(slider.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(20, 32), new Vector2(250, 36));
            slider.OnChanged = v => { auto = false; Apply(v); };
            var play = UIKit.Img(card, UIKit.Circle, UIKit.BlueBtn, "Auto", false, true);
            UIKit.At(play.rectTransform, new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-18, 32), new Vector2(34, 34));
            var pb = play.gameObject.AddComponent<Button>(); pb.targetGraphic = play; pb.transition = UnityEngine.UI.Selectable.Transition.None;
            var pi = UIKit.Icon(play.transform, UIKit.IRotate, Color.white, 20, "I"); Mid(pi.rectTransform, 0, 0, 20, 20);
            pb.onClick.AddListener(() => auto = true);
            Fab(196);
        }

        void Apply(float v)
        {
            float ang = v * 145f;
            f.body.Flexion = ang;
            readout.text = Mathf.RoundToInt(ang) + "° FLEXION";
        }

        public override void Enter()
        {
            base.Enter();
            auto = true; phase = 0f;
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle); f.body.Select(MuscleId.BicepsBrachii);
            Vector3 c = f.MuscleCenter(MuscleId.BicepsBrachii, 1);
            f.rig.Frame(c + new Vector3(0.95f, 0.42f, 0.18f), c + new Vector3(0, 0.12f, 0.1f), 38f, 1.8f);
            var b = f.body;
            f.labels.Add(() => b.MuscleCenter(MuscleId.BicepsBrachii, 1), new Vector2(0.55f, 0.5f), "BICEPS BRACHII", "CONTRACTING");
        }

        public override void Exit() { f.body.Flexion = 0f; }

        public override void Tick()
        {
            base.Tick();
            if (auto)
            {
                phase += Time.deltaTime * 0.5f;
                float v = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f);
                slider.Set(v, false);
                Apply(v);
            }
        }
    }

    // ===================================================================================================
    // 14 EXPLORE ANOTHER MUSCLE  -  DISCOVER
    // ===================================================================================================
    public class ExploreAnotherScreen : ScreenBase
    {
        Text headline, cardTitle; Text[] rows = new Text[2]; RectTransform finger; MuscleId current;
        public ExploreAnotherScreen(AppFlow f) : base(f, 14, "EXPLORE ANOTHER MUSCLE", "DISCOVER") { }

        public override void Build()
        {
            TopShade(300, 0.6f);
            BottomShade(380, 0.9f);
            TopRow("DISCOVER");
            headline = Headline("Deltoid", 33, 6);

            var fg = UIKit.Icon(root, UIKit.IFinger, new Color(1f, 0.8f, 0.3f, 1f), 54, "Finger");
            finger = fg.rectTransform;
            finger.anchorMin = finger.anchorMax = new Vector2(0, 0); finger.pivot = new Vector2(0.5f, 1f);
            finger.sizeDelta = new Vector2(44, 44);

            var card = UIKit.Card(root, new Color(0.04f, 0.07f, 0.13f, 0.94f), UIKit.BlueBtn, "InfoCard");
            BC(card, 40, 270, 190);
            cardTitle = UIKit.Label(card, "DELTOID", 17, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperLeft, "Name");
            UIKit.At(cardTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(240, 24));
            for (int i = 0; i < 2; i++)
            {
                rows[i] = UIKit.Label(card, "", 15, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, "Row" + i);
                UIKit.At(rows[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -46 - i * 26), new Vector2(240, 22));
            }
            var b = UIKit.PrimaryButton(card, "Explore", true, () => { f.MarkExplored(current); f.Next(); }, 240, 52);
            UIKit.At(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(240, 52));
        }

        void Show(MuscleId id)
        {
            current = id; f.selected = id;
            var info = MuscleDB.Get(id);
            headline.text = info.name;
            cardTitle.text = info.name.ToUpper();
            rows[0].text = "Location / " + info.location;
            rows[1].text = "Function / " + info.function;
            f.body.Select(id);
            f.labels.Clear();
            var b = f.body;
            f.labels.Add(() => b.MuscleCenter(id, 1), new Vector2(-0.45f, 0.5f), info.name.ToUpper() + " (" + info.latin.Replace("M. ", "M. ").ToUpper() + ")",
                         "ORIGIN: " + info.origin + "\nINSERTION: " + info.insertion);
            Vector3 c = f.MuscleCenter(id, 1);
            float size = Mathf.Max(0.25f, f.body.BoundsOf(id).size.magnitude * 0.5f);
            f.rig.Frame(c + new Vector3(0.55f, 0.55f, 0.35f) * (0.8f + size), c, 40f, 2.2f);
        }

        public override void Enter()
        {
            base.Enter();
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle);
            Show(MuscleId.Deltoid);
        }

        public override void OnTap(Vector2 p)
        {
            MuscleId id;
            if (f.model.Pick(p, out id) && id != current) { f.MarkExplored(id); Show(id); }
        }

        public override void Tick()
        {
            base.Tick();
            // tap hint hovers over the highlighted muscle
            Vector3 vp = f.rig.main.WorldToViewportPoint(f.MuscleCenter(current, 1));
            if (vp.z > 0)
            {
                Vector2 pp = ToPhone(vp, f);
                finger.anchoredPosition = new Vector2(pp.x, pp.y - 12f - 5f * Mathf.Abs(Mathf.Sin(T * 3f)));
                finger.gameObject.SetActive(T < 6f);
            }
        }
    }

    // ===================================================================================================
    // 15 LEARNING COMPLETE  -  PROGRESS
    // ===================================================================================================
    public class LearningCompleteScreen : ScreenBase
    {
        Text muscles, structures, progress;
        public LearningCompleteScreen(AppFlow f) : base(f, 15, "LEARNING COMPLETE", "PROGRESS") { }

        public override void Build()
        {
            Veil(0.38f);
            TopShade(300, 0.7f);
            TopRow("PROGRESS");
            var h = Headline("KEEP EXPLORING", 33, 6);

            // HUD frame
            float y0 = Top + 150, hgt = 330;
            Corner(true, true, y0, hgt); Corner(false, true, y0, hgt); Corner(true, false, y0, hgt); Corner(false, false, y0, hgt);
            var a = UIKit.Label(root, "ANATOMY // FULL BODY // MALE\nSTATUS // HORIZONTAL // 100% ACCURATE", 8, FontStyle.Normal, new Color(1, 1, 1, 0.55f), TextAnchor.UpperLeft, "HudA");
            TL(a.rectTransform, 24, y0 + 12, 200, 26);
            var b = UIKit.Label(root, "LIVE FEED\nAR ACTIVE", 8, FontStyle.Normal, new Color(1, 1, 1, 0.55f), TextAnchor.UpperRight, "HudB");
            TR(b.rectTransform, 40, y0 + 12, 120, 26);
            for (int i = 0; i < 2; i++)
            {
                var bat = UIKit.Img(root, UIKit.Ring, i == 0 ? new Color(0.4f, 0.9f, 0.5f, 0.9f) : new Color(0.6f, 0.75f, 1f, 0.8f), "Bat" + i, true);
                TR(bat.rectTransform, 24, y0 + 14 + i * 10.5f, 14, 7);
            }

            var card = UIKit.Card(root, new Color(0.04f, 0.07f, 0.13f, 0.95f), new Color(0.3f, 0.45f, 0.7f, 0.7f), "Card");
            BC(card, 34, 334, 440);
            var t = UIKit.Label(card, "KEEP EXPLORING", 24, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "T");
            UIKit.At(t.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -22), new Vector2(290, 32));
            string[] labs = { "Muscles explored", "Structures discovered", "Learning progress" };
            Text[] vals = new Text[3];
            for (int i = 0; i < 3; i++)
            {
                var l = UIKit.Label(card, labs[i], 15, FontStyle.Normal, UIKit.Dim, TextAnchor.MiddleLeft, "L" + i);
                UIKit.At(l.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(22, -78 - i * 34), new Vector2(220, 24));
                vals[i] = UIKit.Label(card, "", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleRight, "V" + i);
                UIKit.At(vals[i].rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-22, -78 - i * 34), new Vector2(100, 24));
            }
            muscles = vals[0]; structures = vals[1]; progress = vals[2];
            var more = UIKit.PrimaryButton(card, "Explore More", true, () => f.Next(), 290, 54);
            UIKit.At(more.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 168), new Vector2(290, 54));

            var quiz = UIKit.Img(card, UIKit.Round, new Color(0.07f, 0.1f, 0.18f, 0.95f), "QuizBtn", true, true);
            UIKit.At(quiz.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(290, 54));
            var qr = UIKit.Img(quiz.transform, UIKit.Ring, new Color(0.3f, 0.4f, 0.55f, 0.8f), "B", true); UIKit.Fill(qr.rectTransform);
            var ql = UIKit.Label(quiz.transform, "Take Quiz", 19, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "L"); UIKit.Fill(ql.rectTransform);
            var qb = quiz.gameObject.AddComponent<Button>(); qb.targetGraphic = quiz; qb.transition = UnityEngine.UI.Selectable.Transition.None;
            quiz.gameObject.AddComponent<PressScale>();
            qb.onClick.AddListener(() => f.GoNumber(16));

            var logo = UIKit.Label(card, "Muscle<color=#60A5FA>AR</color>", 22, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "Logo");
            UIKit.At(logo.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 42), new Vector2(200, 28));
            var tag = UIKit.Label(card, "Explore. Learn. Understand.", 12, FontStyle.Normal, UIKit.Dim, TextAnchor.UpperLeft, "Tag");
            UIKit.At(tag.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(22, 22), new Vector2(250, 18));
        }

        void Corner(bool left, bool top, float y0, float h)
        {
            Color c = new Color(1, 1, 1, 0.5f);
            float x = left ? 14 : 14; float len = 24;
            var hl = UIKit.Img(root, UIKit.White, c, "CH");
            var vl = UIKit.Img(root, UIKit.White, c, "CV");
            Vector2 anchor = new Vector2(left ? 0 : 1, 1); Vector2 pivot = new Vector2(left ? 0 : 1, top ? 1 : 0);
            float yy = -(top ? y0 : y0 + h);
            UIKit.At(hl.rectTransform, anchor, pivot, new Vector2(left ? 14 : -14, yy), new Vector2(len, 1.5f));
            UIKit.At(vl.rectTransform, anchor, pivot, new Vector2(left ? 14 : -14, yy), new Vector2(1.5f, len));
        }

        public override void Enter()
        {
            base.Enter();
            int n = f.explored.Count;
            muscles.text = (8 + n).ToString();
            structures.text = (12 + n * 2).ToString();
            progress.text = Mathf.Min(100, 70 + n * 4) + "%";
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.None, true, false); f.model.ShowDisc(true);
            f.body.SetLayer(BodyLayer.Muscle);
            f.rig.Orbit(new Vector3(0, 0.9f, -0.1f), 2.4f, 1.0f, 2.5f, 10f, 44f);
            var b = f.body;
            f.labels.Add(() => b.MuscleCenter(MuscleId.BicepsBrachii, 1), new Vector2(0.7f, 0.35f), "BICEPS BRACHII", "145° FLEXION");
        }
    }

    // ===================================================================================================
    // 16 QUIZ (extra: the "Take Quiz" / Quiz-mode destination)
    // ===================================================================================================
    public class QuizScreen : ScreenBase
    {
        class Q { public MuscleId id; public string[] options; public int correct; }
        readonly List<Q> qs = new List<Q>();
        int qi, score; bool answered;
        Text qCount, qTitle, feedback; Image[] optBg = new Image[4]; Text[] optTx = new Text[4]; Button next; Text nextLabel;
        RectTransform card;

        static readonly MuscleId[] pool = {
            MuscleId.BicepsBrachii, MuscleId.Deltoid, MuscleId.PectoralisMajor, MuscleId.RectusAbdominis, MuscleId.RectusFemoris,
            MuscleId.TibialisAnterior, MuscleId.Trapezius, MuscleId.Sartorius, MuscleId.Gastrocnemius, MuscleId.SerratusAnterior
        };

        public QuizScreen(AppFlow f) : base(f, 16, "QUIZ", "QUIZ") { }

        public override void Build()
        {
            TopShade(260, 0.6f);
            BottomShade(520, 0.92f);
            TopRow("QUIZ");
            Headline("Test your\nknowledge", 33, 6);
            card = UIKit.Card(root, new Color(0.04f, 0.07f, 0.13f, 0.95f), UIKit.BlueBtn, "QCard");
            BC(card, 30, 342, 380);
            qCount = UIKit.Label(card, "QUESTION 1 / 5", 12, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperLeft, "N");
            UIKit.At(qCount.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -16), new Vector2(250, 18));
            qTitle = UIKit.Label(card, "Which muscle is highlighted?", 20, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "Q");
            UIKit.At(qTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -40), new Vector2(302, 56));
            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                optBg[i] = UIKit.Img(card, UIKit.Round, new Color(0.08f, 0.12f, 0.2f, 0.95f), "Opt" + i, true, true);
                UIKit.At(optBg[i].rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -86 - i * 54), new Vector2(302, 46));
                var rg = UIKit.Img(optBg[i].transform, UIKit.Ring, new Color(0.3f, 0.4f, 0.55f, 0.7f), "B", true); UIKit.Fill(rg.rectTransform);
                optTx[i] = UIKit.Label(optBg[i].transform, "", 16, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "T");
                UIKit.Fill(optTx[i].rectTransform); optTx[i].rectTransform.offsetMin = new Vector2(16, 0);
                var b = optBg[i].gameObject.AddComponent<Button>(); b.targetGraphic = optBg[i]; b.transition = UnityEngine.UI.Selectable.Transition.None;
                optBg[i].gameObject.AddComponent<PressScale>();
                b.onClick.AddListener(() => Answer(idx));
            }
            feedback = UIKit.Label(card, "", 14, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "F");
            UIKit.At(feedback.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 18), new Vector2(150, 40));
            next = UIKit.PrimaryButton(card, "Next", true, () => NextQ(), 140, 44);
            UIKit.At(next.GetComponent<RectTransform>(), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-20, 18), new Vector2(140, 44));
            nextLabel = next.GetComponentInChildren<Text>();
        }

        public override void Enter()
        {
            base.Enter();
            f.lab.SetLook(LabEnvironment.Look.Lab);
            SetupModel(ModelController.Mode.None, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle);
            qs.Clear(); qi = 0; score = 0;
            var ids = new List<MuscleId>(pool);
            for (int i = 0; i < ids.Count; i++) { int j = Random.Range(i, ids.Count); var t = ids[i]; ids[i] = ids[j]; ids[j] = t; }
            for (int k = 0; k < 5; k++)
            {
                var q = new Q { id = ids[k] };
                var opts = new List<string> { MuscleDB.Get(ids[k]).name };
                var others = new List<MuscleId>(pool); others.Remove(ids[k]);
                while (opts.Count < 4) { var o = others[Random.Range(0, others.Count)]; others.Remove(o); opts.Add(MuscleDB.Get(o).name); }
                for (int i = 0; i < 4; i++) { int j = Random.Range(i, 4); var t = opts[i]; opts[i] = opts[j]; opts[j] = t; }
                q.options = opts.ToArray(); q.correct = opts.IndexOf(MuscleDB.Get(ids[k]).name);
                qs.Add(q);
            }
            ShowQ();
        }

        void ShowQ()
        {
            answered = false;
            var q = qs[qi];
            qCount.text = "QUESTION " + (qi + 1) + " / " + qs.Count;
            qTitle.text = "Which muscle is highlighted?";
            for (int i = 0; i < 4; i++)
            {
                optBg[i].gameObject.SetActive(true);
                optTx[i].text = q.options[i]; optBg[i].color = new Color(0.08f, 0.12f, 0.2f, 0.95f);
            }
            feedback.text = ""; next.gameObject.SetActive(false);
            f.body.Select(q.id);
            Vector3 c = f.MuscleCenter(q.id, 1);
            float size = Mathf.Max(0.3f, f.body.BoundsOf(q.id).size.magnitude);
            f.rig.Frame(c + new Vector3(0.6f, 0.9f, 0.9f) * (0.55f + size * 0.9f), c, 42f, 2.2f);
        }

        void Answer(int i)
        {
            if (answered) return;
            answered = true;
            var q = qs[qi];
            bool ok = i == q.correct;
            if (ok) score++;
            optBg[i].color = ok ? new Color(0.1f, 0.45f, 0.25f, 0.95f) : new Color(0.55f, 0.15f, 0.15f, 0.95f);
            optBg[q.correct].color = new Color(0.1f, 0.45f, 0.25f, 0.95f);
            feedback.text = ok ? "Correct!" : "It's the " + MuscleDB.Get(q.id).name;
            f.MarkExplored(q.id);
            next.gameObject.SetActive(true);
            nextLabel.text = qi == qs.Count - 1 ? "Finish" : "Next";
        }

        void NextQ()
        {
            if (qi < qs.Count - 1) { qi++; ShowQ(); return; }
            // results
            qCount.text = "RESULT";
            qTitle.text = "You scored " + score + " / " + qs.Count + (score >= 4 ? "  —  excellent!" : "");
            for (int i = 0; i < 4; i++) optBg[i].gameObject.SetActive(false);
            feedback.text = ""; f.body.Select(null);
            nextLabel.text = "Done";
            next.onClick.RemoveAllListeners();
            next.onClick.AddListener(() => { next.onClick.RemoveAllListeners(); next.onClick.AddListener(() => NextQ()); f.GoNumber(15); });
        }
    }
}
