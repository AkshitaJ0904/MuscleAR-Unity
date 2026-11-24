using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MuscleAR
{
    // ===================================================================================================
    // 09 SELECT A BODY PART  -  DISCOVER
    // ===================================================================================================
    // ===================================================================================================
    // 09 SELECT A BODY PART  -  DISCOVER (Six Target Muscles)
    // ===================================================================================================
    public class SelectPartScreen : ScreenBase
    {
        Text headline, hint;
        MuscleId? picked; float pickT;
        Button exploreBtn;
        readonly List<Image> chipBgs = new List<Image>();
        readonly List<Image> chipBorders = new List<Image>();
        readonly List<Text> chipTexts = new List<Text>();

        public SelectPartScreen(AppFlow f) : base(f, 9, "SELECT A BODY PART", "DISCOVER") { }

        public override void Build()
        {
            TopShade(260, 0.45f);
            BottomShade(340, 0.70f);
            TopRow("DISCOVER", false, true);
            headline = Headline("Target Muscles", 26, 0);
            hint = Subhead("Tap any of the 6 muscles on the 3D model or select below.", 0, 13);

            // Container for 6 target muscle chips (2 rows x 3 columns)
            var chipContainer = UIKit.NewRect("TargetChips", root);
            BC(chipContainer, Bottom + 84, 350, 76);

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int idx = r * 3 + c;
                    if (idx >= MuscleDB.TargetMuscles.Length) break;
                    MuscleId mId = MuscleDB.TargetMuscles[idx];
                    string shortName = Short(MuscleDB.Get(mId).name);
                    if (shortName == "Rectus Abdominis") shortName = "Rectus\nAbdominis";
                    if (shortName == "Quadriceps Femoris") shortName = "Quadriceps\nFemoris";
                    if (shortName == "Pectoralis Major") shortName = "Pectoralis\nMajor";

                    float x = -118f + c * 118f;
                    float y = r == 0 ? 19f : -19f;

                    var chipImg = UIKit.Img(chipContainer, UIKit.Round, new Color(0.08f, 0.12f, 0.22f, 0.95f), "Chip_" + mId, true, true);
                    UIKit.At(chipImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(112, 34));
                    var border = UIKit.Img(chipImg.transform, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
                    UIKit.Fill(border.rectTransform);

                    var lbl = UIKit.Label(chipImg.transform, shortName, 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Txt");
                    lbl.lineSpacing = 0.95f;
                    UIKit.Fill(lbl.rectTransform);

                    var btn = chipImg.gameObject.AddComponent<Button>();
                    btn.targetGraphic = chipImg;
                    btn.transition = Selectable.Transition.None;
                    chipImg.gameObject.AddComponent<PressScale>();
                    btn.onClick.AddListener(() => {
                        Select(mId, false, true);
                        f.GoNumber(10);
                    });
                    MuscleAudioController.AttachClickSound(btn);

                    chipBgs.Add(chipImg);
                    chipBorders.Add(border);
                    chipTexts.Add(lbl);
                }
            }

            exploreBtn = BottomButton("Explore Muscle", (Sprite)null, () => {
                var target = f.selected ?? MuscleId.BicepsBrachii;
                f.selected = target;
                f.MarkExplored(target);
                f.GoNumber(10);
            }, 20f);
        }

        public override void Enter()
        {
            base.Enter();
            picked = null; pickT = 0;
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
            }
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(true);
            f.body.SetLayer(BodyLayer.Muscle);

            var active = f.selected ?? MuscleId.BicepsBrachii;
            Select(active, false, false);

            // Clean 3D callouts for the 6 target muscles only
            var b = f.body; var L = f.labels;
            L.ShowTargetMuscles(b);
        }

        public override void OnTap(Vector2 p)
        {
            MuscleId id;
            if (f.model.Pick(p, out id) && MuscleDB.IsTarget(id))
            {
                Select(id, false, true);
                f.GoNumber(10);
            }
        }

        public void Select(MuscleId id, bool autoAdvance = false, bool playSound = true)
        {
            if (playSound && MuscleAudioController.Instance != null)
                MuscleAudioController.Instance.PlayClick();
            picked = id; pickT = 0f;
            f.selected = id; f.MarkExplored(id);
            f.body.SetHint(null); f.body.Select(id);
            var info = MuscleDB.Get(id);
            headline.text = info.name;
            hint.text = info.function + " • " + info.location;

            var txt = exploreBtn.GetComponentInChildren<Text>();
            if (txt != null) txt.text = "Explore " + Short(info.name);

            for (int i = 0; i < MuscleDB.TargetMuscles.Length && i < chipBgs.Count; i++)
            {
                bool on = MuscleDB.TargetMuscles[i] == id;
                chipBgs[i].color = on ? new Color(0.12f, 0.35f, 0.85f, 0.95f) : new Color(0.08f, 0.12f, 0.22f, 0.95f);
                chipBorders[i].color = on ? UIKit.BlueBtn : UIKit.BorderSubtle;
                chipTexts[i].color = on ? Color.white : new Color(0.75f, 0.85f, 0.95f, 0.85f);
            }
        }

        public override void Tick()
        {
            base.Tick();
        }
    }

    // ===================================================================================================
    // 10 MUSCLE INFORMATION SCREEN  -  LEARN (Dedicated Information Screen for Target Muscles)
    // ===================================================================================================
    public class EducationalToastScreen : ScreenBase
    {
        Text headline, cardTitle;
        Text locationVal, originVal, insertionVal, functionVal, descVal;
        RectTransform card;
        Button actionBtn;

        Image bottomShade;
        RectTransform chipContainer;
        GameObject scrollGO;
        Image headerDiv;
        bool isExpanded = true;

        Image listenAgainBg, listenAgainBorder;
        Text listenAgainText;
        Button listenAgainBtn;
        Button collapseBtn;
        Text collapseTxt;

        readonly List<Image> chipBgs = new List<Image>();
        readonly List<Image> chipBorders = new List<Image>();
        readonly List<Text> chipTexts = new List<Text>();

        public bool IsExpanded => isExpanded;

        public EducationalToastScreen(AppFlow f) : base(f, 10, "MUSCLE INFORMATION", "LEARN") { }

        public override void Build()
        {
            TopShade(260, 0.45f);
            bottomShade = BottomShade(440, 0.85f);
            TopRow("INFO", false, true);
            headline = Headline("Muscle Information", 26, 0);

            // 1. Grid of 6 Target Muscle Selector Buttons (Clean, evenly spaced responsive grid)
            chipContainer = UIKit.NewRect("TargetChips", root);
            BC(chipContainer, Bottom + 338, 350, 74);

            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    int idx = r * 3 + c;
                    if (idx >= MuscleDB.TargetMuscles.Length) break;
                    MuscleId mId = MuscleDB.TargetMuscles[idx];
                    string shortName = Short(MuscleDB.Get(mId).name);
                    if (shortName == "Rectus Abdominis") shortName = "Rectus\nAbdominis";
                    if (shortName == "Quadriceps Femoris") shortName = "Quadriceps\nFemoris";
                    if (shortName == "Pectoralis Major") shortName = "Pectoralis\nMajor";

                    float x = -118f + c * 118f;
                    float y = r == 0 ? 19f : -19f;

                    var chipImg = UIKit.Img(chipContainer, UIKit.Round, new Color(0.08f, 0.12f, 0.22f, 0.95f), "Chip_" + mId, true, true);
                    UIKit.At(chipImg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, y), new Vector2(112, 34));
                    var border = UIKit.Img(chipImg.transform, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
                    UIKit.Fill(border.rectTransform);

                    var lbl = UIKit.Label(chipImg.transform, shortName, 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Txt");
                    lbl.lineSpacing = 0.95f;
                    UIKit.Fill(lbl.rectTransform);

                    var btn = chipImg.gameObject.AddComponent<Button>();
                    btn.targetGraphic = chipImg;
                    btn.transition = Selectable.Transition.None;
                    chipImg.gameObject.AddComponent<PressScale>();
                    btn.onClick.AddListener(() => {
                        ShowMuscle(mId, true);
                        ExpandPanel();
                    });
                    MuscleAudioController.AttachClickSound(btn);

                    chipBgs.Add(chipImg);
                    chipBorders.Add(border);
                    chipTexts.Add(lbl);
                }
            }

            // 2. Information Panel Card (Separate from the muscle selector)
            card = UIKit.Card(root, new Color(0.06f, 0.10f, 0.18f, 0.96f), UIKit.BorderSubtle, "InfoCard");
            BC(card, Bottom + 14, 350, 314);

            // Card Header: Muscle Name Heading + Listen Again + Single Toggle Button (▼ / ▲)
            cardTitle = UIKit.Label(card, "MUSCLE NAME", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleLeft, "Name");
            UIKit.At(cardTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(14, -10), new Vector2(170, 24));

            // Listen Again Button
            var listenGO = UIKit.NewRect("ListenAgainBtn", card);
            UIKit.At(listenGO, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-50, -8), new Vector2(112, 28));
            listenAgainBg = UIKit.Img(listenGO, UIKit.Round, new Color(0.12f, 0.20f, 0.35f, 0.95f), "Bg", true, true);
            UIKit.Fill(listenAgainBg.rectTransform);
            listenAgainBorder = UIKit.Img(listenGO, UIKit.Ring, UIKit.BlueLine, "Border", true);
            UIKit.Fill(listenAgainBorder.rectTransform);
            listenAgainText = UIKit.Label(listenGO, "🔊 Listen Again", 10, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Txt");
            UIKit.Fill(listenAgainText.rectTransform);
            listenAgainBtn = listenGO.gameObject.AddComponent<Button>();
            listenAgainBtn.targetGraphic = listenAgainBg;
            listenAgainBtn.transition = Selectable.Transition.None;
            listenGO.gameObject.AddComponent<PressScale>();
            listenAgainBtn.onClick.AddListener(OnListenAgainClicked);
            MuscleAudioController.AttachClickSound(listenAgainBtn);

            // Single Toggle Arrow Button (beside Listen Again)
            var collapseGO = UIKit.NewRect("CollapseBtn", card);
            UIKit.At(collapseGO, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -8), new Vector2(32, 28));
            var collapseBg = UIKit.Img(collapseGO, UIKit.Round, new Color(0.10f, 0.16f, 0.28f, 0.95f), "Bg", true, true);
            UIKit.Fill(collapseBg.rectTransform);
            var collapseBorder = UIKit.Img(collapseGO, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
            UIKit.Fill(collapseBorder.rectTransform);
            collapseTxt = UIKit.Label(collapseGO, "▼", 12, FontStyle.Bold, new Color(0.85f, 0.92f, 1f, 0.95f), TextAnchor.MiddleCenter, "Txt");
            UIKit.Fill(collapseTxt.rectTransform);
            collapseBtn = collapseGO.gameObject.AddComponent<Button>();
            collapseBtn.targetGraphic = collapseBg;
            collapseBtn.transition = Selectable.Transition.None;
            collapseGO.gameObject.AddComponent<PressScale>();
            collapseBtn.onClick.AddListener(TogglePanel);
            MuscleAudioController.AttachClickSound(collapseBtn);

            // Card Header Divider
            headerDiv = UIKit.Img(card, UIKit.White, new Color(0.2f, 0.35f, 0.6f, 0.35f), "HDiv");
            UIKit.At(headerDiv.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(322, 1));

            // Scrollable Content Region (ScrollRect with vertical scrolling)
            scrollGO = new GameObject("DetailsScroll", typeof(RectTransform), typeof(ScrollRect), typeof(RectMask2D));
            scrollGO.transform.SetParent(card, false);
            var sr = scrollGO.GetComponent<ScrollRect>();
            sr.horizontal = false;
            sr.vertical = true;
            sr.movementType = ScrollRect.MovementType.Clamped;
            var srt = scrollGO.GetComponent<RectTransform>();
            UIKit.Fill(srt);
            srt.offsetMin = new Vector2(14, 56);
            srt.offsetMax = new Vector2(-14, -44);

            var contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(scrollGO.transform, false);
            var crt = contentGO.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0, 1);
            crt.anchorMax = new Vector2(1, 1);
            crt.pivot = new Vector2(0.5f, 1);
            crt.sizeDelta = new Vector2(0, 0);

            var vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.spacing = 5f;
            vlg.padding = new RectOffset(2, 2, 4, 4);
            vlg.childControlWidth = true;
            vlg.childControlHeight = true;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            var csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            sr.content = crt;

            // Fields inside scroll view with consistent alignment
            locationVal = CreateScrollRow(contentGO.transform, "Location");
            originVal = CreateScrollRow(contentGO.transform, "Origin");
            insertionVal = CreateScrollRow(contentGO.transform, "Insertion");
            functionVal = CreateScrollRow(contentGO.transform, "Function");

            var descDiv = UIKit.Img(contentGO.transform, UIKit.White, new Color(0.2f, 0.35f, 0.6f, 0.35f), "DescDiv");
            var descDivLE = descDiv.gameObject.AddComponent<LayoutElement>();
            descDivLE.preferredHeight = 1;
            descDivLE.minHeight = 1;

            descVal = UIKit.Label(contentGO.transform, "", 11, FontStyle.Normal, new Color(0.85f, 0.90f, 0.98f, 0.95f), TextAnchor.UpperLeft, "Description");
            descVal.lineSpacing = 1.25f;
            var descCSF = descVal.gameObject.AddComponent<ContentSizeFitter>();
            descCSF.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // Primary Button: See It in Action (Fixed at card bottom, no arrow icon, always accessible)
            actionBtn = UIKit.PrimaryButton(card, "See It in Action", (Sprite)null, () => f.GoNumber(13), 322, 42);
            UIKit.At(actionBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 10), new Vector2(322, 42));
        }

        public void TogglePanel()
        {
            if (isExpanded)
                CollapsePanel();
            else
                ExpandPanel();
        }

        public void CollapsePanel()
        {
            isExpanded = false;
            if (card != null)
            {
                card.sizeDelta = new Vector2(350, 44);
                BC(card, Bottom + 14, 350, 44);
            }
            if (scrollGO != null) scrollGO.SetActive(false);
            if (actionBtn != null) actionBtn.gameObject.SetActive(false);
            if (headerDiv != null) headerDiv.gameObject.SetActive(false);
            if (chipContainer != null)
            {
                chipContainer.gameObject.SetActive(true);
                BC(chipContainer, Bottom + 64, 350, 74);
            }
            if (collapseTxt != null) collapseTxt.text = "▲";
            if (f.labels != null) f.labels.InvalidateLayout();
        }

        public void ExpandPanel()
        {
            isExpanded = true;
            if (card != null)
            {
                card.sizeDelta = new Vector2(350, 314);
                BC(card, Bottom + 14, 350, 314);
            }
            if (scrollGO != null) scrollGO.SetActive(true);
            if (actionBtn != null) actionBtn.gameObject.SetActive(true);
            if (headerDiv != null) headerDiv.gameObject.SetActive(true);
            if (chipContainer != null)
            {
                chipContainer.gameObject.SetActive(true);
                BC(chipContainer, Bottom + 338, 350, 74);
            }
            if (collapseTxt != null) collapseTxt.text = "▼";
            if (f.labels != null) f.labels.InvalidateLayout();
            UpdateSpeakerUI();
        }

        Text CreateScrollRow(Transform parent, string name)
        {
            var txt = UIKit.Label(parent, "", 11, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, name);
            txt.lineSpacing = 1.15f;
            var csf = txt.gameObject.AddComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return txt;
        }

        void OnListenAgainClicked()
        {
            var audio = f.audioCtrl != null ? f.audioCtrl : MuscleAudioController.Instance;
            if (audio == null) return;
            var current = f.selected ?? MuscleId.BicepsBrachii;
            audio.Play(current);
            UpdateSpeakerUI();
        }

        void UpdateSpeakerUI()
        {
            var audio = f.audioCtrl != null ? f.audioCtrl : MuscleAudioController.Instance;
            var current = f.selected ?? MuscleId.BicepsBrachii;
            bool isPlaying = audio != null && audio.IsPlaying(current);
            bool hasClip = audio != null && audio.GetClip(current) != null;

            if (listenAgainBg != null && listenAgainText != null)
            {
                if (!hasClip)
                {
                    if (listenAgainBtn != null) listenAgainBtn.interactable = false;
                    listenAgainBg.color = new Color(0.08f, 0.12f, 0.20f, 0.5f);
                    listenAgainBorder.color = new Color(0.2f, 0.3f, 0.45f, 0.3f);
                    listenAgainText.text = "No Audio";
                    listenAgainText.color = new Color(0.6f, 0.7f, 0.8f, 0.5f);
                }
                else if (isPlaying)
                {
                    if (listenAgainBtn != null) listenAgainBtn.interactable = true;
                    listenAgainBg.color = new Color(0.12f, 0.35f, 0.85f, 0.95f);
                    listenAgainBorder.color = UIKit.BlueBtn;
                    listenAgainText.text = "Playing...";
                    listenAgainText.color = Color.white;
                }
                else
                {
                    if (listenAgainBtn != null) listenAgainBtn.interactable = true;
                    listenAgainBg.color = new Color(0.10f, 0.16f, 0.28f, 0.95f);
                    listenAgainBorder.color = UIKit.BorderSubtle;
                    listenAgainText.text = "🔊 Listen Again";
                    listenAgainText.color = Color.white;
                }
            }
        }

        public void ShowMuscle(MuscleId id, bool playNarration = true)
        {
            var audio = f.audioCtrl != null ? f.audioCtrl : MuscleAudioController.Instance;
            if (audio != null)
            {
                audio.Stop();
            }

            f.selected = id;
            f.MarkExplored(id);
            if (f.body != null)
            {
                f.body.SetHint(null);
                f.body.Select(id);
            }
            var info = MuscleDB.Get(id);

            headline.text = info.name;
            cardTitle.text = "M. " + info.name.ToUpper();

            locationVal.text = "<color=#60A5FA><b>Location:</b></color> " + info.location;
            originVal.text = "<color=#60A5FA><b>Origin:</b></color> " + info.origin;
            insertionVal.text = "<color=#60A5FA><b>Insertion:</b></color> " + info.insertion;
            functionVal.text = "<color=#60A5FA><b>Function:</b></color> " + info.function;
            descVal.text = info.detail;

            // Update callout in 3D AR
            if (f.labels != null)
            {
                var b = f.body;
                if (b != null)
                {
                    f.labels.ShowTargetMuscles(b);
                }
            }

            // Update 6 chip highlights
            for (int i = 0; i < MuscleDB.TargetMuscles.Length && i < chipBgs.Count; i++)
            {
                bool on = MuscleDB.TargetMuscles[i] == id;
                chipBgs[i].color = on ? new Color(0.12f, 0.35f, 0.85f, 0.95f) : new Color(0.08f, 0.12f, 0.22f, 0.95f);
                chipBorders[i].color = on ? UIKit.BlueBtn : UIKit.BorderSubtle;
                chipTexts[i].color = on ? Color.white : new Color(0.75f, 0.85f, 0.95f, 0.85f);
            }

            // Reopen panel when a muscle is selected
            ExpandPanel();

            if (playNarration && audio != null)
            {
                audio.Play(id);
            }

            UpdateSpeakerUI();
        }

        public override void Enter()
        {
            base.Enter();
            var id = f.selected ?? MuscleId.BicepsBrachii;

            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
                Vector3 c = f.MuscleCenter(id, 1);
                if (f.rig != null) f.rig.Frame(c + new Vector3(0.55f, 0.35f, 0.85f), c, 44f, 1.8f);
            }

            SetupModel(ModelController.Mode.Explore, true, false);
            if (f.model != null) f.model.ShowDisc(false);
            if (f.body != null) f.body.SetLayer(BodyLayer.Muscle);

            ExpandPanel();
            ShowMuscle(id, playNarration: true);
        }

        public override void Tick()
        {
            base.Tick();
            UpdateSpeakerUI();
        }

        public override void Exit()
        {
            base.Exit();
            var audio = f.audioCtrl != null ? f.audioCtrl : MuscleAudioController.Instance;
            if (audio != null)
            {
                audio.Stop();
            }
        }

        public override void OnTap(Vector2 p)
        {
            MuscleId id;
            if (f.model.Pick(p, out id) && MuscleDB.IsTarget(id))
            {
                if (isExpanded && f.selected == id)
                {
                    // Tapping the same muscle while open toggles closed
                    if (MuscleAudioController.Instance != null)
                        MuscleAudioController.Instance.PlayClick();
                    CollapsePanel();
                }
                else
                {
                    // Tapping another muscle or while collapsed selects and opens
                    ShowMuscle(id, playNarration: true);
                    ExpandPanel();
                }
            }
        }

        public override void UpdateSafeAreaLayout()
        {
            base.UpdateSafeAreaLayout();
            if (card != null)
            {
                float h = isExpanded ? 314 : 44;
                card.sizeDelta = new Vector2(350, h);
                BC(card, Bottom + 14, 350, h);
            }
            if (chipContainer != null)
            {
                float y = isExpanded ? Bottom + 338 : Bottom + 64;
                BC(chipContainer, y, 350, 74);
            }
        }
    }

    // ===================================================================================================
    // 13 MUSCLE ACTION  -  SEE IT IN ACTION
    // ===================================================================================================
    public class MuscleActionScreen : ScreenBase
    {
        UISlider slider;
        Text readout, cardTitle, cardDesc;
        bool auto = true;
        float phase;
        MuscleId activeMuscle;

        struct ActionProfile
        {
            public string title;
            public string movement;
            public float maxAngle;
            public string desc;
        }

        static ActionProfile GetProfile(MuscleId id)
        {
            switch (id)
            {
                case MuscleId.PectoralisMajor:
                    return new ActionProfile {
                        title = "PECTORALIS MAJOR",
                        movement = "ARM ADDUCTION",
                        maxAngle = 30f,
                        desc = "The pectoralis major contracts powerfully to draw the arm across the chest in horizontal adduction and internal rotation."
                    };
                case MuscleId.Deltoid:
                    return new ActionProfile {
                        title = "DELTOID",
                        movement = "SHOULDER ABDUCTION",
                        maxAngle = 60f,
                        desc = "The lateral and anterior fibers contract to elevate the arm upward and outward away from the midline of the torso."
                    };
                case MuscleId.RectusAbdominis:
                    return new ActionProfile {
                        title = "RECTUS ABDOMINIS",
                        movement = "TRUNK FLEXION",
                        maxAngle = 22f,
                        desc = "Paired abdominal muscle segments shorten along the linea alba, pulling the rib cage toward the pelvis to curl the spine."
                    };
                case MuscleId.QuadricepsFemoris:
                    return new ActionProfile {
                        title = "QUADRICEPS FEMORIS",
                        movement = "KNEE EXTENSION",
                        maxAngle = 75f,
                        desc = "All four quadriceps heads contract in unison across the patella and patellar tendon to extend and lock the knee joint."
                    };
                case MuscleId.Gastrocnemius:
                    return new ActionProfile {
                        title = "GASTROCNEMIUS",
                        movement = "PLANTAR FLEXION",
                        maxAngle = 35f,
                        desc = "Both heads contract to pull upward on the calcaneus via the Achilles tendon, pointing the toes and lifting body weight."
                    };
                case MuscleId.BicepsBrachii:
                default:
                    return new ActionProfile {
                        title = "BICEPS BRACHII",
                        movement = "ELBOW FLEXION",
                        maxAngle = 145f,
                        desc = "The biceps brachii contracts across the anterior elbow joint, flexing the forearm upward toward the shoulder and supinating the hand."
                    };
            }
        }

        public MuscleActionScreen(AppFlow f) : base(f, 13, "SEE IT IN ACTION", "ACTION") { }

        public override void Build()
        {
            TopShade(260, 0.6f);
            BottomShade(340, 0.92f);
            TopRow("ACTION", false, true);
            Headline("See It in Action", 26, 0);

            var card = UIKit.Card(root, new Color(0.04f, 0.07f, 0.13f, 0.95f), UIKit.BlueBtn, "ActionCard");
            BC(card, 130, 342, 165);

            cardTitle = UIKit.Label(card, "MUSCLE NAME", 15, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperLeft, "T");
            UIKit.At(cardTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -12), new Vector2(200, 20));

            readout = UIKit.Label(card, "0° ACTION", 14, FontStyle.Bold, Color.white, TextAnchor.UpperRight, "R");
            UIKit.At(readout.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-18, -12), new Vector2(150, 20));

            cardDesc = UIKit.Label(card, "", 12, FontStyle.Normal, UIKit.Dim, TextAnchor.UpperLeft, "D");
            UIKit.At(cardDesc.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -36), new Vector2(306, 46));

            slider = UISlider.Create(card, 250);
            UIKit.At(slider.GetComponent<RectTransform>(), new Vector2(0, 0), new Vector2(0, 0.5f), new Vector2(18, 30), new Vector2(250, 36));
            slider.OnChanged = v => { auto = false; Apply(v); };

            var play = UIKit.Img(card, UIKit.Circle, UIKit.BlueBtn, "Auto", false, true);
            UIKit.At(play.rectTransform, new Vector2(1, 0), new Vector2(1, 0.5f), new Vector2(-18, 30), new Vector2(34, 34));
            var pb = play.gameObject.AddComponent<Button>(); pb.targetGraphic = play; pb.transition = Selectable.Transition.None;
            var pi = UIKit.Icon(play.transform, UIKit.IRotate, Color.white, 20, "I"); Mid(pi.rectTransform, 0, 0, 20, 20);
            pb.onClick.AddListener(() => {
                phase = Mathf.Acos(1f - 2f * Mathf.Clamp01(f.body.ActionAmount)) / Mathf.PI;
                auto = true;
            });
            MuscleAudioController.AttachClickSound(pb);

            // Two bottom navigation buttons:
            // 1. "Take Quiz" (primary call-to-action button; passes activeMuscle into quiz)
            var quizBtn = UIKit.PrimaryButton(root, "Take Quiz", false, () => {
                f.selected = activeMuscle;
                f.BeginExplorerQuiz();
            }, 342, 46);
            BC(quizBtn.GetComponent<RectTransform>(), Bottom + 68, 342, 46);

            // 2. Return to Muscle Info screen (muscle switching disabled on Action page)
            var backInfoBtn = UIKit.PrimaryButton(root, "← Back to Muscle Info", false, () => f.GoNumber(10), 342, 42);
            BC(backInfoBtn.GetComponent<RectTransform>(), Bottom + 18, 342, 42);
        }

        void Apply(float v)
        {
            var prof = GetProfile(activeMuscle);
            float ang = v * prof.maxAngle;
            readout.text = Mathf.RoundToInt(ang) + "° " + prof.movement;
            f.body.ActionAmount = v;
            f.body.DemonstrateAction(activeMuscle, v);
        }

        public override void Enter()
        {
            base.Enter();
            auto = true; phase = 0f;
            activeMuscle = f.selected ?? MuscleId.BicepsBrachii;
            f.selected = activeMuscle;
            Debug.Log("[MuscleAR-Diag] ActionScreen.Enter " + activeMuscle + " | root.lossyScale=" + f.model.root.lossyScale + " | body.lossyScale=" + f.body.transform.lossyScale);
            var prof = GetProfile(activeMuscle);

            cardTitle.text = prof.title;
            cardDesc.text = prof.desc;

            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
                Vector3 c = f.MuscleCenter(activeMuscle, 1);
                f.rig.Frame(c + new Vector3(0.65f, 0.40f, 0.65f), c, 40f, 1.8f);
            }

            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle); f.body.Select(activeMuscle);

            var b = f.body;
            f.labels.ShowTargetMuscles(b);

            slider.Set(0f, false);
            Apply(0f);
        }

        public override void Exit()
        {
            f.body.ResetAction();
        }

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
    // 14 EXPLORE ANOTHER MUSCLE  -  DISCOVER (Target Muscles Only)
    // ===================================================================================================
    public class ExploreAnotherScreen : ScreenBase
    {
        Text headline, cardTitle; Text[] rows = new Text[2]; RectTransform finger; MuscleId current;
        public ExploreAnotherScreen(AppFlow f) : base(f, 14, "EXPLORE ANOTHER MUSCLE", "DISCOVER") { }

        public override void Build()
        {
            TopShade(260, 0.6f);
            BottomShade(380, 0.92f);
            TopRow("DISCOVER", false, true);
            headline = Headline("Explore Next", 26, 0);

            var fg = UIKit.Icon(root, UIKit.IFinger, new Color(1f, 0.8f, 0.3f, 1f), 54, "Finger");
            finger = fg.rectTransform;
            finger.anchorMin = finger.anchorMax = new Vector2(0, 0); finger.pivot = new Vector2(0.5f, 1f);
            finger.sizeDelta = new Vector2(44, 44);

            var card = UIKit.Card(root, new Color(0.04f, 0.07f, 0.13f, 0.95f), UIKit.BlueBtn, "InfoCard");
            BC(card, 40, 290, 190);
            cardTitle = UIKit.Label(card, "MUSCLE", 17, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperLeft, "Name");
            UIKit.At(cardTitle.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -14), new Vector2(250, 24));
            for (int i = 0; i < 2; i++)
            {
                rows[i] = UIKit.Label(card, "", 14, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, "Row" + i);
                UIKit.At(rows[i].rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(18, -46 - i * 26), new Vector2(250, 22));
            }
            var b = UIKit.PrimaryButton(card, "Explore Details", false, () => { f.selected = current; f.MarkExplored(current); f.GoNumber(10); }, 250, 46);
            UIKit.At(b.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 16), new Vector2(250, 50));
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
            var b = f.body;
            f.labels.ShowTargetMuscles(b);
            Vector3 c = f.MuscleCenter(id, 1);
            float size = Mathf.Max(0.25f, f.body.BoundsOf(id).size.magnitude * 0.5f);
            f.rig.Frame(c + new Vector3(0.55f, 0.55f, 0.35f) * (0.8f + size), c, 40f, 2.2f);
        }

        public override void Enter()
        {
            base.Enter();
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
            }
            SetupModel(ModelController.Mode.Explore, true, false); f.model.ShowDisc(false);
            f.body.SetLayer(BodyLayer.Muscle);

            // Pick the next unexplored target muscle, or cycle
            MuscleId nextM = MuscleId.Deltoid;
            foreach (var m in MuscleDB.TargetMuscles)
            {
                if (!f.explored.Contains(m) && m != f.selected) { nextM = m; break; }
            }
            Show(nextM);
        }

        public override void OnTap(Vector2 p)
        {
            MuscleId id;
            if (f.model.Pick(p, out id) && id != current)
            {
                if (MuscleAudioController.Instance != null)
                    MuscleAudioController.Instance.PlayClick();
                f.MarkExplored(id);
                Show(id);
            }
        }

        public override void Tick()
        {
            base.Tick();
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
            TopRow("PROGRESS", false, true);
            var h = Headline("KEEP EXPLORING", 26, 0);

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
            qb.onClick.AddListener(() => f.BeginExplorerQuiz());
            MuscleAudioController.AttachClickSound(qb);

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
            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
                f.rig.Orbit(new Vector3(0, 0.9f, -0.1f), 2.4f, 1.0f, 2.5f, 10f, 44f);
            }
            var b = f.body;
            f.labels.ShowTargetMuscles(b);
        }
    }

    // ===================================================================================================
    // 16 QUIZ: Muscle-Specific Data-Driven Anatomy Quiz Flow
    // ===================================================================================================
    public class QuizMuscleSelectionScreen : ScreenBase
    {
        public QuizMuscleSelectionScreen(AppFlow f) : base(f, 17, "CHOOSE YOUR MUSCLE", "QUIZ") { }
        public override void Build()
        {
            Veil(0.98f); TopRow("QUIZ", false, true);
            Headline("Choose Your Muscle", 25, 0);
            Subhead("Choose a muscle for a 5-question quiz.", 0, 14);
            var viewport = UIKit.NewRect("MuscleChoices", root);
            viewport.anchorMin = Vector2.zero; viewport.anchorMax = Vector2.one;
            viewport.offsetMin = new Vector2(22, Bottom + 24);
            viewport.offsetMax = new Vector2(-22, -(Top + 185));
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.viewport = viewport; scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            var content = UIKit.NewRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1); scroll.content = content;
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 12; layout.childControlWidth = true; layout.childControlHeight = true;
            layout.childForceExpandWidth = true; layout.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            foreach (var id in new[] { MuscleId.BicepsBrachii, MuscleId.Deltoid, MuscleId.PectoralisMajor,
                MuscleId.RectusAbdominis, MuscleId.QuadricepsFemoris, MuscleId.Gastrocnemius })
            {
                MuscleId choice = id;
                var button = UIKit.PrimaryButton(content, MuscleDB.Get(id).name, false, () => ChooseMuscle(choice), 346, 58);
                button.gameObject.name = "Choose_" + id;
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 58;
            }
        }
        public void ChooseMuscle(MuscleId id)
        {
            if (!MuscleDB.IsTarget(id)) return;
            f.quizFromMenu = true; f.selected = id; f.GoNumber(16);
        }
        public override void Enter()
        {
            base.Enter(); f.quizFromMenu = true;
            if (f.model != null) { f.model.mode = ModelController.Mode.None; f.model.Show(false, false); }
        }
    }

    public class QuizScreen : ScreenBase
    {
        public class QuizQuestion
        {
            public string question;
            public string[] options;
            public int correctIndex;
            public string explanation;

            public QuizQuestion(string q, string[] opts, int correct, string exp)
            {
                question = q;
                options = opts;
                correctIndex = correct;
                explanation = exp;
            }
        }

        static readonly Dictionary<MuscleId, List<QuizQuestion>> QuizDB = new Dictionary<MuscleId, List<QuizQuestion>>();

        static void A(MuscleId id, string q, string[] opts, int correct, string exp)
        {
            if (!QuizDB.ContainsKey(id)) QuizDB[id] = new List<QuizQuestion>();
            QuizDB[id].Add(new QuizQuestion(q, opts, correct, exp));
        }

        static void InitQuestions()
        {
            QuizDB.Clear();

            // ---------------- 1. PECTORALIS MAJOR (5 Questions) ----------------
            A(MuscleId.PectoralisMajor,
              "Where is the Pectoralis Major muscle located in the human body?",
              new[] {
                  "Anterior thoracic wall (upper chest)",
                  "Posterior thoracic wall (upper back)",
                  "Lateral aspect of the neck",
                  "Posterior compartment of the arm"
              }, 0,
              "The pectoralis major is a thick, fan-shaped muscle covering the anterior chest wall.");

            A(MuscleId.PectoralisMajor,
              "What anatomical structures form the origin of the Pectoralis Major?",
              new[] {
                  "Lateral third of clavicle, acromion, and scapular spine",
                  "Medial clavicle, sternum, and costal cartilages of ribs 1-6",
                  "Coracoid process and supraglenoid tubercle of scapula",
                  "Pubic crest and pubic symphysis"
              }, 1,
              "It originates via two heads: clavicular head (medial clavicle) and sternocostal head (sternum & upper 6 ribs).");

            A(MuscleId.PectoralisMajor,
              "Where does the distal tendon of the Pectoralis Major insert?",
              new[] {
                  "Deltoid tuberosity on the mid-shaft humerus",
                  "Radial tuberosity of the proximal radius",
                  "Lateral lip of the bicipital groove of the humerus",
                  "Olecranon process of the proximal ulna"
              }, 2,
              "Its tendon inserts into the lateral lip of the intertubercular sulcus (bicipital groove) of the humerus.");

            A(MuscleId.PectoralisMajor,
              "What are the primary actions of the Pectoralis Major at the shoulder?",
              new[] {
                  "Arm abduction from 15° to 90° and external rotation",
                  "Forearm supination and elbow extension",
                  "Scapular elevation and upward rotation",
                  "Arm adduction, medial internal rotation, and horizontal flexion"
              }, 3,
              "Pectoralis major draws the arm across the chest (horizontal adduction) and rotates it inward.");

            A(MuscleId.PectoralisMajor,
              "Which common exercise predominantly engages the Pectoralis Major?",
              new[] {
                  "Dumbbell lateral side raises",
                  "Barbell bench press and push-ups",
                  "Standing calf heel raises",
                  "Seated hamstring leg curls"
              }, 1,
              "Horizontal pressing movements like bench presses and push-ups strongly recruit the pectoralis major.");

            // ---------------- 2. DELTOID (5 Questions) ----------------
            A(MuscleId.Deltoid,
              "Where is the Deltoid muscle located in the human body?",
              new[] {
                  "Capping the shoulder joint (glenohumeral joint)",
                  "Along the anterior abdominal core midline",
                  "Superficial posterior calf compartment",
                  "Anterior compartment of the thigh"
              }, 0,
              "The deltoid forms the rounded muscular contour covering the glenohumeral (shoulder) joint.");

            A(MuscleId.Deltoid,
              "What anatomical landmarks give origin to the Deltoid muscle?",
              new[] {
                  "Pubic crest and pubic symphysis",
                  "Lateral clavicle, acromion process, and spine of scapula",
                  "Medial and lateral condyles of the femur",
                  "Costal cartilages of ribs 1 through 6"
              }, 1,
              "The deltoid arises from 3 areas: lateral clavicle (anterior), acromion (lateral), and scapular spine (posterior).");

            A(MuscleId.Deltoid,
              "Where does the Deltoid muscle insert on the arm skeleton?",
              new[] {
                  "Radial tuberosity of the radius",
                  "Greater trochanter of the femur",
                  "Deltoid tuberosity of the lateral humerus",
                  "Coracoid process of the scapula"
              }, 2,
              "All three deltoid heads converge into the deltoid tuberosity on the lateral shaft of the humerus.");

            A(MuscleId.Deltoid,
              "What is the primary action of the lateral (middle) fibers of the Deltoid?",
              new[] {
                  "Elbow extension and forearm pronation",
                  "Plantar flexion of the ankle joint",
                  "Trunk lateral flexion and rotation",
                  "Shoulder abduction between 15° and 90°"
              }, 3,
              "The lateral deltoid fibers are the prime mover for abducting the arm sideways from 15° to 90°.");

            A(MuscleId.Deltoid,
              "Which exercise specifically isolates the lateral head of the Deltoid?",
              new[] {
                  "Barbell back squats",
                  "Dumbbell lateral raises",
                  "Incline bicep dumbbell curls",
                  "Standing calf raises"
              }, 1,
              "Dumbbell lateral raises elevate the humerus in the frontal plane, isolating the lateral deltoid fibers.");

            // ---------------- 3. RECTUS ABDOMINIS (5 Questions) ----------------
            A(MuscleId.RectusAbdominis,
              "Where is the Rectus Abdominis muscle located in the human body?",
              new[] {
                  "Anterior abdominal wall along the core midline",
                  "Posterior surface of the thigh",
                  "Superficial lateral shoulder",
                  "Deep posterior compartment of the calf"
              }, 0,
              "The rectus abdominis is a paired vertical strap muscle running down the front of the abdomen.");

            A(MuscleId.RectusAbdominis,
              "From which pelvic bones does the Rectus Abdominis originate inferiorly?",
              new[] {
                  "Anterior superior iliac spine (ASIS)",
                  "Pubic crest and pubic symphysis",
                  "Ischial tuberosity of the pelvis",
                  "Lateral third of the clavicle"
              }, 1,
              "It arises from the pubic crest and pubic symphysis at the inferior border of the pelvis.");

            A(MuscleId.RectusAbdominis,
              "Where does the Rectus Abdominis insert superiorly?",
              new[] {
                  "Deltoid tuberosity of the humerus",
                  "Radial tuberosity and bicipital fascia",
                  "Xiphoid process and costal cartilages of ribs 5-7",
                  "Calcaneus via the Achilles tendon"
              }, 2,
              "It inserts into the xiphoid process of the sternum and the costal cartilages of the 5th through 7th ribs.");

            A(MuscleId.RectusAbdominis,
              "What is the primary biomechanical action of the Rectus Abdominis?",
              new[] {
                  "Hip extension and external rotation",
                  "Shoulder adduction and internal rotation",
                  "Knee extension and ankle dorsiflexion",
                  "Trunk flexion (bending forward) and intra-abdominal compression"
              }, 3,
              "It flexes the lumbar vertebral column, drawing the thorax toward the pelvis, and stabilizes pelvic tilt.");

            A(MuscleId.RectusAbdominis,
              "Which physical exercise directly engages the Rectus Abdominis?",
              new[] {
                  "Overhead dumbbell shoulder press",
                  "Abdominal crunches and hanging leg raises",
                  "Cable triceps pushdowns",
                  "Seated calf heel extensions"
              }, 1,
              "Crunches and hanging leg raises actively flex the lumbar spine, directly contracting the rectus abdominis.");

            // ---------------- 4. BICEPS BRACHII (5 Questions) ----------------
            A(MuscleId.BicepsBrachii,
              "Where is the Biceps Brachii located in the human body?",
              new[] {
                  "Anterior compartment of the arm (front of upper arm)",
                  "Posterior compartment of the thigh",
                  "Lateral surface of the forearm",
                  "Superficial posterior calf compartment"
              }, 0,
              "The biceps brachii is located on the front of the upper arm between the shoulder and elbow.");

            A(MuscleId.BicepsBrachii,
              "Where do the short and long heads of the Biceps Brachii originate?",
              new[] {
                  "Medial and lateral epicondyles of the humerus",
                  "Short head: coracoid process; Long head: supraglenoid tubercle (scapula)",
                  "Clavicle, sternum, and upper 6 ribs",
                  "Pubic crest and pubic symphysis"
              }, 1,
              "Both heads originate from the scapula: short head at coracoid process, long head at supraglenoid tubercle.");

            A(MuscleId.BicepsBrachii,
              "Where does the distal tendon of the Biceps Brachii insert?",
              new[] {
                  "Deltoid tuberosity on lateral humerus",
                  "Patella and tibial tuberosity",
                  "Radial tuberosity and bicipital aponeurosis",
                  "Olecranon process of the ulna"
              }, 2,
              "It inserts into the radial tuberosity of the radius and fans into the deep bicipital aponeurosis.");

            A(MuscleId.BicepsBrachii,
              "What are the two primary actions produced by the Biceps Brachii?",
              new[] {
                  "Elbow extension and forearm pronation",
                  "Shoulder adduction and trunk lateral flexion",
                  "Ankle plantar flexion and knee extension",
                  "Forearm supination (turning palm up) and elbow flexion"
              }, 3,
              "The biceps brachii is the prime supinator of the flexed forearm and a powerful elbow flexor.");

            A(MuscleId.BicepsBrachii,
              "Which exercise directly targets the Biceps Brachii?",
              new[] {
                  "Triceps rope pushdowns",
                  "Dumbbell curls and underhand chin-ups",
                  "Seated leg extensions",
                  "Standing calf raises"
              }, 1,
              "Dumbbell curls and underhand chin-ups demand high elbow flexion torque, maximally loading the biceps brachii.");

            // ---------------- 5. GASTROCNEMIUS (5 Questions) ----------------
            A(MuscleId.Gastrocnemius,
              "Where is the Gastrocnemius located in the human body?",
              new[] {
                  "Superficial posterior compartment of the leg (calf)",
                  "Anterior compartment of the thigh",
                  "Anterior chest wall across the ribcage",
                  "Superior aspect of the shoulder"
              }, 0,
              "The gastrocnemius is the large, superficial, two-bellied muscle forming the calf of the lower leg.");

            A(MuscleId.Gastrocnemius,
              "From which bony landmark does the Gastrocnemius originate?",
              new[] {
                  "Anterior inferior iliac spine of the pelvis",
                  "Medial and lateral condyles of the femur (above knee)",
                  "Coracoid process of the scapula",
                  "Pubic crest and symphysis"
              }, 1,
              "Its medial and lateral heads originate on the distal femoral condyles, making it a two-joint biarticular muscle.");

            A(MuscleId.Gastrocnemius,
              "Through which tendon does the Gastrocnemius insert into the heel?",
              new[] {
                  "Patellar ligament into the tibial tuberosity",
                  "Bicipital aponeurosis into forearm fascia",
                  "Achilles (calcaneal) tendon into the posterior calcaneus",
                  "Rotator cuff tendon into greater tubercle"
              }, 2,
              "It merges with the soleus into the thick Achilles tendon to insert onto the posterior calcaneus (heel bone).");

            A(MuscleId.Gastrocnemius,
              "What is the primary action of the Gastrocnemius muscle?",
              new[] {
                  "Dorsiflexion of the ankle (lifting toes upward)",
                  "Knee extension and hip abduction",
                  "Forearm pronation and wrist extension",
                  "Plantar flexion of the foot at the ankle (pointing toes down)"
              }, 3,
              "The gastrocnemius is the prime plantar flexor of the foot at the ankle, providing push-off power.");

            A(MuscleId.Gastrocnemius,
              "During which movement or exercise is the Gastrocnemius most heavily recruited?",
              new[] {
                  "Seated quadriceps leg extensions",
                  "Standing calf raises, sprinting, and jumping",
                  "Barbell flat bench press",
                  "Abdominal floor crunches"
              }, 1,
              "Pushing off the ball of the foot with a straight knee (standing calf raises, sprinting, jumping) heavily taxes the calf.");

            // ---------------- 6. QUADRICEPS FEMORIS (5 Questions) ----------------
            A(MuscleId.QuadricepsFemoris,
              "Where is the Quadriceps Femoris muscle group located?",
              new[] {
                  "Anterior compartment of the thigh (front of leg)",
                  "Posterior compartment of the leg (calf)",
                  "Anterior abdominal core midline",
                  "Lateral shoulder region"
              }, 0,
              "The quadriceps femoris covers the anterior compartment of the thigh.");

            A(MuscleId.QuadricepsFemoris,
              "What are the four constituent heads originating the Quadriceps Femoris?",
              new[] {
                  "Biceps femoris, semitendinosus, semimembranosus, and gracilis",
                  "Rectus femoris (pelvis) & Vastus lateralis, medialis, intermedius (femur)",
                  "Anterior, lateral, posterior, and medial deltoid heads",
                  "Clavicular, sternal, costal, and abdominal heads"
              }, 1,
              "It consists of 4 heads: rectus femoris (from AIIS of pelvis) and 3 vastus heads (from femur shaft).");

            A(MuscleId.QuadricepsFemoris,
              "Where does the Quadriceps tendon / patellar ligament insert?",
              new[] {
                  "Deltoid tuberosity on lateral humerus shaft",
                  "Calcaneus via the Achilles tendon",
                  "Tibial tuberosity of the tibia (via patella & patellar ligament)",
                  "Radial tuberosity of the proximal radius"
              }, 2,
              "The tendon encloses the patella and continues as the patellar ligament to insert onto the tibial tuberosity.");

            A(MuscleId.QuadricepsFemoris,
              "What is the primary mechanical action of the Quadriceps Femoris?",
              new[] {
                  "Knee flexion and ankle dorsiflexion",
                  "Shoulder horizontal adduction",
                  "Trunk lateral flexion and rotation",
                  "Knee extension (straightening the leg at the knee joint)"
              }, 3,
              "The quadriceps is the primary extensor of the knee joint, essential for standing, walking, and running.");

            A(MuscleId.QuadricepsFemoris,
              "Which fundamental exercise strongly recruits the Quadriceps Femoris?",
              new[] {
                  "Standing dumbbell bicep curls",
                  "Barbell back squats and leg presses",
                  "Dumbbell lateral side raises",
                  "Cable triceps pushdowns"
              }, 1,
              "Squats and leg presses require high knee extension force, strongly working the entire quadriceps.");
        }

        static void ValidateDatabase()
        {
            foreach (var id in MuscleDB.TargetMuscles)
            {
                if (!QuizDB.ContainsKey(id) || QuizDB[id].Count < 5)
                {
                    Debug.LogError("[QuizValidation] ERROR: Muscle " + id + " has fewer than 5 questions!");
                }
                else
                {
                    for (int i = 0; i < QuizDB[id].Count; i++)
                    {
                        var q = QuizDB[id][i];
                        if (q.options == null || q.options.Length != 4)
                            Debug.LogError("[QuizValidation] ERROR: Muscle " + id + " Q" + i + " does not have exactly 4 options!");
                        if (q.correctIndex < 0 || q.correctIndex >= 4)
                            Debug.LogError("[QuizValidation] ERROR: Muscle " + id + " Q" + i + " invalid correct index " + q.correctIndex);
                    }
                }
            }
            Debug.Log("[QuizValidation] Verified 6 target muscles with " + QuizDB.Count + " quiz categories and 30+ questions.");
        }

        static QuizScreen()
        {
            InitQuestions();
            ValidateDatabase();
        }

        MuscleId activeMuscle;
        List<QuizQuestion> currentQuestions = new List<QuizQuestion>();
        int currentQIndex;
        readonly List<int> userChoices = new List<int>();
        int userScore;
        bool answered;

        // UI references
        Text headline;
        RectTransform qPanel;
        Text badge, progressText, qText, feedbackText;
        Image[] optBgs = new Image[4];
        Image[] optBorders = new Image[4];
        Text[] optLabels = new Text[4];
        Button[] optBtns = new Button[4];
        Button nextBtn;
        Text nextBtnText;

        RectTransform resultsPanel;
        Text resultsMuscle, scoreText, scorePct;
        Button retakeQuizBtn, backToMuscleBtn;
        Button chooseAnotherBtn, backMenuBtn;
        RectTransform leaveConfirmation;

        public QuizScreen(AppFlow f) : base(f, 16, "QUIZ", "QUIZ") { }

        public override void Build()
        {
            TopShade(260, 0.6f);
            BottomShade(520, 0.92f);
            TopRow("QUIZ", false, true);
            headline = Headline("Muscle Quiz", 26, 0);

            // ================= 1. QUESTION PANEL =================
            qPanel = UIKit.NewRect("QuestionPanel", root);
            BC(qPanel, Bottom + 12, 346, 480);
            var qCard = UIKit.Card(qPanel, new Color(0.04f, 0.07f, 0.13f, 0.95f), UIKit.BlueBtn, "QCard");
            UIKit.Fill(qCard);

            badge = UIKit.Label(qCard, "MUSCLE", 12, FontStyle.Bold, UIKit.BlueText, TextAnchor.MiddleLeft, "Badge");
            UIKit.At(badge.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -14), new Vector2(200, 20));

            progressText = UIKit.Label(qCard, "Question 1 of 5", 12, FontStyle.Bold, Color.white, TextAnchor.MiddleRight, "Prog");
            UIKit.At(progressText.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-16, -14), new Vector2(120, 20));

            qText = UIKit.Label(qCard, "", 13, FontStyle.Bold, Color.white, TextAnchor.UpperLeft, "QText");
            qText.lineSpacing = 1.15f;
            UIKit.At(qText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -38), new Vector2(314, 50));

            for (int i = 0; i < 4; i++)
            {
                int idx = i;
                optBgs[i] = UIKit.Img(qCard, UIKit.Round, new Color(0.08f, 0.12f, 0.20f, 0.95f), "Opt" + i, true, true);
                UIKit.At(optBgs[i].rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -92 - i * 48), new Vector2(314, 44));

                optBorders[i] = UIKit.Img(optBgs[i].transform, UIKit.Ring, new Color(0.25f, 0.4f, 0.65f, 0.7f), "Border", true);
                UIKit.Fill(optBorders[i].rectTransform);

                optLabels[i] = UIKit.Label(optBgs[i].transform, "", 12, FontStyle.Normal, Color.white, TextAnchor.MiddleLeft, "Txt");
                optLabels[i].lineSpacing = 1.05f;
                UIKit.Fill(optLabels[i].rectTransform);
                optLabels[i].rectTransform.offsetMin = new Vector2(16, 2);
                optLabels[i].rectTransform.offsetMax = new Vector2(-16, -2);

                optBtns[i] = optBgs[i].gameObject.AddComponent<Button>();
                optBtns[i].targetGraphic = optBgs[i];
                optBtns[i].transition = Selectable.Transition.None;
                optBgs[i].gameObject.AddComponent<PressScale>();
                optBtns[i].onClick.AddListener(() => OnOptionSelected(idx));
                MuscleAudioController.AttachClickSound(optBtns[i]);
            }

            feedbackText = UIKit.Label(qCard, "", 11, FontStyle.Normal, Color.white, TextAnchor.UpperLeft, "Feedback");
            feedbackText.lineSpacing = 1.15f;
            UIKit.At(feedbackText.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(16, -286), new Vector2(314, 70));

            nextBtn = UIKit.PrimaryButton(qCard, "Next Question", false, () => OnNextClicked(), 314, 44);
            UIKit.At(nextBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 36), new Vector2(314, 44));
            nextBtnText = nextBtn.GetComponentInChildren<Text>();

            // ================= 2. RESULTS PANEL (Clean, Centered, Focused) =================
            resultsPanel = UIKit.NewRect("ResultsPanel", root);
            Mid(resultsPanel, 0, -10, 336, 280);
            var rCard = UIKit.Card(resultsPanel, new Color(0.04f, 0.07f, 0.13f, 0.96f), UIKit.BorderAccent, "ResultsCard");
            UIKit.Fill(rCard);

            var rRing = UIKit.Img(rCard.transform, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
            UIKit.Fill(rRing.rectTransform);

            // 1. Quiz Title
            resultsMuscle = UIKit.Label(rCard, "QUIZ RESULTS", 13, FontStyle.Bold, UIKit.BlueText, TextAnchor.UpperCenter, "RMuscle");
            UIKit.At(resultsMuscle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(310, 20));

            var div = UIKit.Img(rCard, UIKit.White, new Color(0.2f, 0.35f, 0.6f, 0.35f), "Div");
            UIKit.At(div.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(290, 1));

            // 2. Score & Percentage (Main Focus)
            scoreText = UIKit.Label(rCard, "0/5 Correct", 30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Score");
            UIKit.At(scoreText.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -78), new Vector2(300, 36));

            scorePct = UIKit.Label(rCard, "0%", 22, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "ScorePct");
            UIKit.At(scorePct.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -114), new Vector2(200, 28));

            // 3. Prominent Retake Quiz Button
            retakeQuizBtn = UIKit.PrimaryButton(rCard, "Retake Quiz", false, () => RestartQuiz(), 290, 44);
            UIKit.At(retakeQuizBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 74), new Vector2(290, 44));

            // 4. Back to Muscle Button
            var backMuscleImg = UIKit.Img(rCard, UIKit.Round, new Color(0.10f, 0.16f, 0.28f, 0.95f), "BackToMuscleBtn", true, true);
            UIKit.At(backMuscleImg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(290, 44));
            var backMuscleBorder = UIKit.Img(backMuscleImg.transform, UIKit.Ring, UIKit.BorderSubtle, "Border", true);
            UIKit.Fill(backMuscleBorder.rectTransform);
            var backMuscleLbl = UIKit.Label(backMuscleImg.transform, "← Back to Muscle", 14, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter, "Txt");
            UIKit.Fill(backMuscleLbl.rectTransform);
            backToMuscleBtn = backMuscleImg.gameObject.AddComponent<Button>();
            backToMuscleBtn.targetGraphic = backMuscleImg;
            backToMuscleBtn.transition = Selectable.Transition.None;
            backMuscleImg.gameObject.AddComponent<PressScale>();
            backToMuscleBtn.onClick.AddListener(() => {
                f.selected = activeMuscle;
                f.GoNumber(10);
            });
            MuscleAudioController.AttachClickSound(backToMuscleBtn);
            chooseAnotherBtn = UIKit.PrimaryButton(rCard, "Choose Another Muscle", false, () => f.GoNumber(17), 290, 38);
            UIKit.At(chooseAnotherBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 58), new Vector2(290, 38));
            backMenuBtn = UIKit.PrimaryButton(rCard, "Back to Menu", false, () => f.ReturnToMainMenu("Quiz results"), 290, 38);
            UIKit.At(backMenuBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(290, 38));
            chooseAnotherBtn.gameObject.SetActive(false); backMenuBtn.gameObject.SetActive(false);

            leaveConfirmation = UIKit.NewRect("LeaveAttemptConfirmation", root);
            UIKit.Fill(leaveConfirmation);
            var shade = UIKit.Img(leaveConfirmation, UIKit.White, new Color(0, 0, 0, 0.88f), "Shade", false, true);
            UIKit.Fill(shade.rectTransform);
            var leaveCard = UIKit.Card(leaveConfirmation, UIKit.CardFill, UIKit.BorderAccent, "LeaveCard");
            Mid(leaveCard, 0, 0, 340, 220);
            var warning = UIKit.Label(leaveCard, "Leave this attempt?\nYour current answers will be discarded.", 17, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);
            TC(warning.rectTransform, 18, 310, 72);
            var stay = UIKit.PrimaryButton(leaveCard, "Continue Quiz", false, () => leaveConfirmation.gameObject.SetActive(false), 300, 42);
            BC(stay.GetComponent<RectTransform>(), 68, 300, 42);
            var leave = UIKit.PrimaryButton(leaveCard, "Choose Muscle", false, () => { leaveConfirmation.gameObject.SetActive(false); f.GoNumber(17); }, 300, 42);
            BC(leave.GetComponent<RectTransform>(), 16, 300, 42);
            leaveConfirmation.gameObject.SetActive(false);
        }

        public void RequestBack()
        {
            if (!f.quizFromMenu) { f.selected = activeMuscle; f.GoNumber(10); return; }
            if (leaveConfirmation.gameObject.activeSelf) { leaveConfirmation.gameObject.SetActive(false); return; }
            if (!resultsPanel.gameObject.activeSelf && userChoices.Exists(choice => choice >= 0))
                leaveConfirmation.gameObject.SetActive(true);
            else f.GoNumber(17);
        }

        public override void Enter()
        {
            base.Enter();
            if (!f.selected.HasValue || !MuscleDB.IsTarget(f.selected.Value)) { f.BeginDirectQuiz(); return; }
            activeMuscle = f.selected.Value;
            f.selected = activeMuscle;

            if (f.quizFromMenu)
            {
                RestartQuiz(); return;
            }

            if (f.viewingMode == AppFlow.ViewingMode.CameraAR)
            {
                if (f.lab != null) { f.lab.SetVisible(false); f.lab.gameObject.SetActive(false); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = false; f.rig.main.tag = "Untagged"; }
                if (f.ar != null) f.ar.SetARActive(true);
                f.EnsureModelAnchoredInAR();
            }
            else
            {
                if (f.lab != null) { f.lab.gameObject.SetActive(true); f.lab.SetVisible(true); f.lab.SetLook(f.currentEnvironmentLook); }
                if (f.rig != null && f.rig.main != null) { f.rig.main.enabled = true; f.rig.main.tag = "MainCamera"; }
            }

            f.body.Select(activeMuscle);
            RestartQuiz();
        }

        void RestartQuiz()
        {
            if (!QuizDB.TryGetValue(activeMuscle, out var list) || list.Count != 5)
                throw new System.InvalidOperationException("Expected five questions for " + activeMuscle);
            currentQuestions = list;
            leaveConfirmation.gameObject.SetActive(false);
            chooseAnotherBtn.gameObject.SetActive(f.quizFromMenu);
            backMenuBtn.gameObject.SetActive(f.quizFromMenu);
            backToMuscleBtn.gameObject.SetActive(!f.quizFromMenu);
            resultsPanel.sizeDelta = new Vector2(336, f.quizFromMenu ? 330 : 280);
            UIKit.At(retakeQuizBtn.GetComponent<RectTransform>(), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, f.quizFromMenu ? 104 : 74), new Vector2(290, 44));
            currentQIndex = 0;
            userChoices.Clear();
            for (int i = 0; i < currentQuestions.Count; i++) userChoices.Add(-1);
            userScore = 0;
            answered = false;

            qPanel.gameObject.SetActive(true);
            resultsPanel.gameObject.SetActive(false);
            headline.text = MuscleDB.Get(activeMuscle).name + " Quiz";

            ShowQuestion(0);
        }

        void ShowQuestion(int idx)
        {
            currentQIndex = idx;
            answered = false;
            var q = currentQuestions[idx];
            var info = MuscleDB.Get(activeMuscle);

            badge.text = info.name.ToUpper();
            progressText.text = "Question " + (idx + 1) + " of " + currentQuestions.Count;
            qText.text = q.question;

            for (int i = 0; i < 4; i++)
            {
                optBgs[i].color = new Color(0.08f, 0.12f, 0.20f, 0.95f);
                optBorders[i].color = new Color(0.25f, 0.4f, 0.65f, 0.7f);
                optLabels[i].color = Color.white;
                optLabels[i].text = string.Format("<b>{0}.</b>  {1}", (char)('A' + i), q.options[i]);
            }

            feedbackText.text = "";
            nextBtn.gameObject.SetActive(false);
        }

        void OnOptionSelected(int chosenIdx)
        {
            if (answered) return;
            answered = true;
            userChoices[currentQIndex] = chosenIdx;

            var q = currentQuestions[currentQIndex];
            bool isCorrect = (chosenIdx == q.correctIndex);
            if (isCorrect) userScore++;

            // Play correct.mp3 or wrong.wav feedback cleanly after button click
            if (MuscleAudioController.Instance != null)
            {
                MuscleAudioController.Instance.PlayAnswerFeedback(isCorrect);
            }

            // Highlight chosen & correct answers
            for (int i = 0; i < 4; i++)
            {
                if (i == q.correctIndex)
                {
                    optBgs[i].color = new Color(0.12f, 0.55f, 0.28f, 0.95f); // Green
                    optBorders[i].color = new Color(0.4f, 0.9f, 0.5f, 1f);
                    optLabels[i].color = Color.white;
                }
                else if (i == chosenIdx)
                {
                    optBgs[i].color = new Color(0.65f, 0.18f, 0.18f, 0.95f); // Red
                    optBorders[i].color = new Color(0.9f, 0.4f, 0.4f, 1f);
                    optLabels[i].color = Color.white;
                }
                else
                {
                    optBgs[i].color = new Color(0.06f, 0.09f, 0.15f, 0.8f);
                    optBorders[i].color = new Color(0.2f, 0.3f, 0.45f, 0.4f);
                    optLabels[i].color = new Color(1, 1, 1, 0.55f);
                }
            }

            if (isCorrect)
            {
                feedbackText.text = "<color=#4ADE80><b>✓ Correct!</b></color> " + q.explanation;
            }
            else
            {
                feedbackText.text = "<color=#F87171><b>✗ Incorrect.</b></color> " + q.explanation;
            }

            nextBtn.gameObject.SetActive(true);
            nextBtnText.text = (currentQIndex == currentQuestions.Count - 1) ? "See Results" : "Next Question";
        }

        void OnNextClicked()
        {
            if (currentQIndex < currentQuestions.Count - 1)
            {
                ShowQuestion(currentQIndex + 1);
            }
            else
            {
                ShowResults();
            }
        }

        void ShowResults()
        {
            qPanel.gameObject.SetActive(false);
            resultsPanel.gameObject.SetActive(true);

            int total = currentQuestions.Count;
            int correctCount = 0;
            for (int i = 0; i < total; i++)
            {
                if (userChoices[i] == currentQuestions[i].correctIndex) correctCount++;
            }
            userScore = correctCount;
            int pct = Mathf.RoundToInt(((float)correctCount / total) * 100f);

            var info = MuscleDB.Get(activeMuscle);
            resultsMuscle.text = info.name.ToUpper() + " QUIZ RESULTS";
            scoreText.text = correctCount + "/" + total + " Correct";
            scorePct.text = pct + "%";

            if (pct == 100)
            {
                scorePct.color = new Color(0.29f, 0.87f, 0.50f, 1f); // Vibrant green
            }
            else if (pct >= 80)
            {
                scorePct.color = new Color(0.22f, 0.74f, 0.97f, 1f); // Sky blue
            }
            else if (pct >= 60)
            {
                scorePct.color = new Color(0.98f, 0.75f, 0.14f, 1f); // Amber
            }
            else
            {
                scorePct.color = new Color(0.97f, 0.44f, 0.44f, 1f); // Coral red
            }
        }
    }
}
