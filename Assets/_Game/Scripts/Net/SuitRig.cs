using UnityEngine;

namespace Backrooms.Net
{
    /// Animates the hazmat suit's skeleton in code (the model has no animation clips): arms down at the sides,
    /// legs and arms swinging with the walk, a deeper stride and lean when running, bent knees when crouching,
    /// and the head following where the player looks. All rotations are made in the avatar's own space
    /// (x right, y up, z forward), on top of each bone's rest pose.
    public sealed class SuitRig : MonoBehaviour
    {
        struct Bone
        {
            public Transform T;
            public Quaternion Rest;   // rest rotation in the avatar's space
        }

        Transform space;   // the avatar root: yaw only
        Bone hips, spine, neck, head, lThigh, rThigh, lShin, rShin, lFoot, rFoot, lArm, rArm, lFore, rFore;
        Vector3 hipsRest;
        float phase, crouch, run, moving, sit;
        bool ready, ragdoll;

        void Awake()
        {
            space = transform.parent != null ? transform.parent : transform;
            hips = Find("Hips");
            spine = Find("Spine1");
            neck = Find("Neck");
            head = Find("Head");
            lThigh = Find("LeftUpLeg"); rThigh = Find("RightUpLeg");
            lShin = Find("LeftLeg"); rShin = Find("RightLeg");
            lFoot = Find("LeftFoot"); rFoot = Find("RightFoot");
            lArm = Find("LeftArm"); rArm = Find("RightArm");
            lFore = Find("LeftForeArm"); rFore = Find("RightForeArm");
            ready = hips.T != null && lThigh.T != null && rThigh.T != null && lArm.T != null && rArm.T != null;
            if (hips.T != null) hipsRest = hips.T.localPosition;
        }

        Bone Find(string name)
        {
            var t = Deep(transform, name);
            return new Bone { T = t, Rest = t != null ? Quaternion.Inverse(space.rotation) * t.rotation : Quaternion.identity };
        }

        static Transform Deep(Transform t, string name)
        {
            if (t.name == name) return t;
            foreach (Transform c in t)
            {
                var f = Deep(c, name);
                if (f != null) return f;
            }
            return null;
        }

        /// speed: m/s over the ground; pitchUp: degrees the player looks up (negative = down).
        public void Animate(float speed, bool crouching, bool running, float pitchUp, float dt, bool sitting = false)
        {
            if (!ready || ragdoll) return;
            float k = 1f - Mathf.Exp(-dt * 8f);
            sit = Mathf.Lerp(sit, sitting ? 1f : 0f, k);
            crouch = Mathf.Lerp(crouch, crouching || sitting ? 1f : 0f, k);
            run = Mathf.Lerp(run, running && speed > 2.5f ? 1f : 0f, k);
            moving = Mathf.Lerp(moving, Mathf.Clamp01(speed / 1.2f), k);
            float stride = Mathf.Lerp(1.5f, 2.4f, run) * Mathf.Lerp(1f, 0.7f, crouch);
            phase = Mathf.Repeat(phase + speed / stride * dt * Mathf.PI * 2f, Mathf.PI * 2f);

            float swing = Mathf.Lerp(22f, 38f, run) * moving;
            float sL = Mathf.Sin(phase), sR = Mathf.Sin(phase + Mathf.PI);
            // Thighs swing forward (negative about x), shins bend back on the forward swing; crouching bends both.
            float thighBase = Mathf.Lerp(-55f * crouch, -88f, sit), kneeBase = Mathf.Lerp(100f * crouch, 105f, sit);
            float lT = thighBase - swing * sL, rT = thighBase - swing * sR;
            float lK = kneeBase + moving * Mathf.Lerp(18f, 45f, run) * Mathf.Max(0f, Mathf.Cos(phase));
            float rK = kneeBase + moving * Mathf.Lerp(18f, 45f, run) * Mathf.Max(0f, Mathf.Cos(phase + Mathf.PI));
            Set(lThigh, X(lT));
            Set(rThigh, X(rT));
            Set(lShin, X(lT + lK));
            Set(rShin, X(rT + rK));
            Set(lFoot, X(lT + lK - kneeBase * 0.45f - lK * 0.5f));
            Set(rFoot, X(rT + rK - kneeBase * 0.45f - rK * 0.5f));

            // Hips drop when crouching and bob with each step; the body leans into a run or a crouch.
            float bob = moving * Mathf.Lerp(0.025f, 0.05f, run) * Mathf.Abs(Mathf.Cos(phase));
            hips.T.localPosition = hipsRest;
            hips.T.position += space.up * (-0.3f * crouch - bob);
            float lean = (6f * moving + 12f * run + 20f * crouch) * (1f - sit);
            Set(spine, X(lean));

            // Arms hang down at the sides (the model is in a T pose) and swing opposite to the legs.
            float armSwing = swing * 1.1f;
            var lDown = Quaternion.AngleAxis(78f, Vector3.forward);
            var rDown = Quaternion.AngleAxis(-78f, Vector3.forward);
            float elbow = Mathf.Lerp(12f, 70f, run) + 25f * crouch;
            Set(lArm, X(armSwing * sL) * lDown);
            Set(rArm, X(armSwing * sR) * rDown);
            Set(lFore, X(armSwing * sL - elbow) * lDown);
            Set(rFore, X(armSwing * sR - elbow) * rDown);

            // The head follows the look (the neck takes a little of it).
            float look = Mathf.Clamp(-pitchUp, -70f, 70f) - lean;
            Set(neck, X(lean + look * 0.3f));
            Set(head, X(lean + look));
        }

        public void EnableRagdoll() => ragdoll = true;

        /// A gesture layered over a settled idle pose. Animate establishes the base body every frame first.
        public void ApplyEmote(int id, float time)
        {
            if (!ready || ragdoll || id == SuitEmote.None) return;
            float beat = Mathf.Sin(time * 8f), slow = Mathf.Sin(time * 3f);
            Quaternion downL = Quaternion.AngleAxis(78f, Vector3.forward);
            Quaternion downR = Quaternion.AngleAxis(-78f, Vector3.forward);
            switch (id)
            {
                case SuitEmote.Wave:
                    Set(rArm, X(-150f));
                    Set(rFore, X(-30f) * Quaternion.AngleAxis(Mathf.Sin(time * 10f) * 35f, Vector3.forward));
                    break;
                case SuitEmote.ThumbsUp:
                    Set(rArm, X(-45f) * Quaternion.AngleAxis(-25f, Vector3.forward));
                    Set(rFore, X(-105f));
                    break;
                case SuitEmote.Point:
                    Set(rArm, X(-88f));
                    Set(rFore, X(-3f));
                    break;
                case SuitEmote.ComeHere:
                    Set(rArm, X(-75f)); Set(rFore, X(-55f + beat * 18f));
                    Set(spine, X(-5f)); break;
                case SuitEmote.Dance:
                    hips.T.position += space.up * (Mathf.Abs(beat) * 0.07f);
                    Set(hips, Quaternion.AngleAxis(slow * 10f, Vector3.up));
                    Set(spine, X(beat * 8f) * Quaternion.AngleAxis(beat * 12f, Vector3.forward));
                    Set(lArm, X(-65f + beat * 35f) * Quaternion.AngleAxis(35f, Vector3.forward));
                    Set(rArm, X(-65f - beat * 35f) * Quaternion.AngleAxis(-35f, Vector3.forward));
                    Set(lFore, X(-90f)); Set(rFore, X(-90f));
                    Set(lThigh, X(beat * 15f)); Set(rThigh, X(-beat * 15f));
                    break;
                case SuitEmote.Sit:
                    Set(lArm, X(-40f) * downL); Set(rArm, X(-40f) * downR);
                    Set(lFore, X(-55f) * downL); Set(rFore, X(-55f) * downR);
                    break;
                case SuitEmote.Laugh:
                    Set(spine, X(-8f + beat * 5f));
                    Set(head, X(-18f + beat * 4f));
                    Set(lArm, X(-45f + beat * 4f) * downL); Set(rArm, X(-45f - beat * 4f) * downR);
                    Set(lFore, X(-115f)); Set(rFore, X(-115f));
                    break;
                case SuitEmote.Salute:
                    Set(rArm, X(-100f) * Quaternion.AngleAxis(-32f, Vector3.forward));
                    Set(rFore, X(-120f)); Set(head, X(-5f)); break;
                case SuitEmote.Facepalm:
                    Set(rArm, X(-100f) * Quaternion.AngleAxis(-25f, Vector3.forward));
                    Set(rFore, X(-150f)); Set(head, X(25f)); Set(spine, X(8f)); break;
                case SuitEmote.Floss:
                    Set(spine, Quaternion.AngleAxis(beat * 20f, Vector3.forward));
                    Set(hips, X(10f));
                    Set(lArm, X(-32f + beat * 12f) * Quaternion.AngleAxis(beat * 30f, Vector3.forward));
                    Set(rArm, X(-32f + beat * 12f) * Quaternion.AngleAxis(beat * 30f, Vector3.forward));
                    Set(lFore, X(-25f) * downL); Set(rFore, X(-25f) * downR);
                    break;
                case SuitEmote.TakeL:
                    Set(rArm, X(-110f) * Quaternion.AngleAxis(-38f, Vector3.forward));
                    Set(rFore, Quaternion.AngleAxis(-135f, Vector3.right) * Quaternion.AngleAxis(-35f, Vector3.up));
                    Set(head, X(-8f)); break;
            }
        }

        static Quaternion X(float degrees) => Quaternion.AngleAxis(degrees, Vector3.right);

        void Set(Bone b, Quaternion extra)
        {
            if (b.T != null) b.T.rotation = space.rotation * (extra * b.Rest);
        }
    }
}
