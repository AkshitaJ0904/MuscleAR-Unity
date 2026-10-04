using System.Collections.Generic;

namespace MuscleAR
{
    public enum MuscleId
    {
        BicepsBrachii, Brachialis, TricepsBrachii, Deltoid, PectoralisMajor, RectusAbdominis,
        ExternalOblique, SerratusAnterior, Trapezius, Sternocleidomastoid, Brachioradialis,
        ForearmFlexors, ForearmExtensors, PronatorTeres, RectusFemoris, VastusLateralis,
        VastusMedialis, Sartorius, Adductors, TensorFasciaeLatae, GluteusMedius, GluteusMaximus,
        TibialisAnterior, Gastrocnemius, Soleus, PeroneusLongus, Masseter, Temporalis,
        OrbicularisOculi, OrbicularisOris, Frontalis
    }

    public class MuscleInfo
    {
        public MuscleId id;
        public string name, latin, location, origin, insertion, function, detail;
        public MuscleInfo(MuscleId id, string name, string latin, string location, string origin,
                          string insertion, string function, string detail)
        {
            this.id = id; this.name = name; this.latin = latin; this.location = location;
            this.origin = origin; this.insertion = insertion; this.function = function; this.detail = detail;
        }
    }

    public static class MuscleDB
    {
        static readonly Dictionary<MuscleId, MuscleInfo> db = new Dictionary<MuscleId, MuscleInfo>();

        static void A(MuscleId id, string name, string latin, string loc, string org, string ins, string fn, string detail)
        {
            db[id] = new MuscleInfo(id, name, latin, loc, org, ins, fn, detail);
        }

        static MuscleDB()
        {
            A(MuscleId.BicepsBrachii, "Biceps Brachii", "M. biceps brachii", "Upper arm", "Scapula", "Radius",
              "Elbow flexion", "Primary actions: Elbow flexion; Forearm supination");
            A(MuscleId.Brachialis, "Brachialis", "M. brachialis", "Upper arm", "Humerus", "Ulna",
              "Elbow flexion", "The workhorse of elbow flexion, lying beneath the biceps");
            A(MuscleId.TricepsBrachii, "Triceps Brachii", "M. triceps brachii", "Back of upper arm", "Scapula, Humerus", "Ulna",
              "Elbow extension", "Primary actions: Elbow extension; Shoulder adduction");
            A(MuscleId.Deltoid, "Deltoid", "M. deltoideus", "Shoulder region", "Clavicle, Scapula", "Humerus",
              "Shoulder abduction", "Primary actions: Shoulder abduction, flexion and extension");
            A(MuscleId.PectoralisMajor, "Pectoralis Major", "M. pectoralis major", "Chest", "Clavicle, Sternum", "Humerus",
              "Shoulder adduction", "Primary actions: Adduction and medial rotation of the arm");
            A(MuscleId.RectusAbdominis, "Rectus Abdominis", "M. rectus abdominis", "Front of abdomen", "Pubis", "Ribs 5-7, Xiphoid",
              "Trunk flexion", "Primary actions: Flexes the trunk; Compresses the abdomen");
            A(MuscleId.ExternalOblique, "External Oblique", "M. obliquus externus", "Side of abdomen", "Lower ribs", "Iliac crest, Linea alba",
              "Trunk rotation", "Primary actions: Trunk rotation; Lateral flexion");
            A(MuscleId.SerratusAnterior, "Serratus Anterior", "M. serratus anterior", "Side of chest", "Ribs 1-8", "Scapula",
              "Scapular protraction", "Primary actions: Protracts and stabilises the scapula");
            A(MuscleId.Trapezius, "Trapezius", "M. trapezius", "Neck and upper back", "Skull, Spine", "Clavicle, Scapula",
              "Scapular elevation", "Primary actions: Elevates, retracts and rotates the scapula");
            A(MuscleId.Sternocleidomastoid, "Sternocleidomastoid", "M. sternocleidomastoideus", "Side of neck", "Sternum, Clavicle", "Mastoid process",
              "Neck rotation", "Primary actions: Rotates and flexes the head");
            A(MuscleId.Brachioradialis, "Brachioradialis", "M. brachioradialis", "Lateral forearm", "Humerus", "Radius",
              "Elbow flexion", "Primary actions: Flexes the forearm in mid-pronation");
            A(MuscleId.ForearmFlexors, "Forearm Flexors", "Mm. flexores antebrachii", "Front of forearm", "Medial epicondyle", "Carpals, Fingers",
              "Wrist flexion", "Primary actions: Flex the wrist and fingers");
            A(MuscleId.ForearmExtensors, "Forearm Extensors", "Mm. extensores antebrachii", "Back of forearm", "Lateral epicondyle", "Carpals, Fingers",
              "Wrist extension", "Primary actions: Extend the wrist and fingers");
            A(MuscleId.PronatorTeres, "Pronator Teres", "M. pronator teres", "Upper forearm", "Medial epicondyle", "Radius",
              "Forearm pronation", "Primary actions: Pronates the forearm; Assists elbow flexion");
            A(MuscleId.RectusFemoris, "Rectus Femoris", "M. rectus femoris", "Front of thigh", "Pelvis (AIIS)", "Patella",
              "Knee extension", "Primary actions: Knee extension; Hip flexion");
            A(MuscleId.VastusLateralis, "Vastus Lateralis", "M. vastus lateralis", "Outer thigh", "Femur", "Patella",
              "Knee extension", "Primary actions: Extends the knee");
            A(MuscleId.VastusMedialis, "Vastus Medialis", "M. vastus medialis", "Inner thigh", "Femur", "Patella",
              "Knee extension", "Primary actions: Extends the knee; Stabilises the patella");
            A(MuscleId.Sartorius, "Sartorius", "M. sartorius", "Front of thigh", "Pelvis (ASIS)", "Tibia",
              "Hip flexion", "Longest muscle of the body; flexes hip and knee");
            A(MuscleId.Adductors, "Adductors", "Mm. adductores", "Inner thigh", "Pubis", "Femur",
              "Hip adduction", "Primary actions: Draw the thigh toward the midline");
            A(MuscleId.TensorFasciaeLatae, "Tensor Fasciae Latae", "M. tensor fasciae latae", "Outer hip", "Pelvis (ASIS)", "Iliotibial band",
              "Hip abduction", "Primary actions: Abducts and flexes the hip");
            A(MuscleId.GluteusMedius, "Gluteus Medius", "M. gluteus medius", "Outer hip", "Ilium", "Greater trochanter",
              "Hip abduction", "Primary actions: Abducts the hip; Stabilises the pelvis");
            A(MuscleId.GluteusMaximus, "Gluteus Maximus", "M. gluteus maximus", "Buttock", "Sacrum, Ilium", "Femur, Iliotibial band",
              "Hip extension", "Primary actions: Hip extension; External rotation");
            A(MuscleId.TibialisAnterior, "Tibialis Anterior", "M. tibialis anterior", "Front of shin", "Tibia", "Medial cuneiform",
              "Ankle dorsiflexion", "Primary actions: Dorsiflexion; Foot inversion");
            A(MuscleId.Gastrocnemius, "Gastrocnemius", "M. gastrocnemius", "Calf", "Femoral condyles", "Calcaneus",
              "Plantar flexion", "Primary actions: Plantar flexion; Knee flexion");
            A(MuscleId.Soleus, "Soleus", "M. soleus", "Deep calf", "Tibia, Fibula", "Calcaneus",
              "Plantar flexion", "Primary actions: Plantar flexion with the knee bent");
            A(MuscleId.PeroneusLongus, "Peroneus Longus", "M. fibularis longus", "Outer shin", "Fibula", "First metatarsal",
              "Foot eversion", "Primary actions: Everts and plantar-flexes the foot");
            A(MuscleId.Masseter, "Masseter", "M. masseter", "Cheek", "Zygomatic arch", "Mandible",
              "Jaw closure", "Primary actions: Elevates the mandible");
            A(MuscleId.Temporalis, "Temporalis", "M. temporalis", "Side of head", "Temporal fossa", "Mandible",
              "Jaw closure", "Primary actions: Elevates and retracts the mandible");
            A(MuscleId.OrbicularisOculi, "Orbicularis Oculi", "M. orbicularis oculi", "Around the eye", "Orbital margin", "Eyelid",
              "Eyelid closure", "Primary actions: Closes the eyelids");
            A(MuscleId.OrbicularisOris, "Orbicularis Oris", "M. orbicularis oris", "Around the mouth", "Maxilla, Mandible", "Lips",
              "Lip closure", "Primary actions: Closes and purses the lips");
            A(MuscleId.Frontalis, "Frontalis", "M. frontalis", "Forehead", "Galea aponeurotica", "Skin of brow",
              "Brow elevation", "Primary actions: Raises the eyebrows");
        }

        public static MuscleInfo Get(MuscleId id) { return db[id]; }
        public static IEnumerable<MuscleInfo> All { get { return db.Values; } }
    }
}
