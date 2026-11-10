using System;
using System.Collections.Generic;
using UnityEngine;

namespace Momo
{
    // Study-only contact planner. Not mocap, and not a production run/jump rig.
    // Lateral sequence: LH -> LF -> RH -> RF. Completing a swing is independent
    // of root speed, so stopping does not freeze a paw in midair.
    internal sealed class CatAlertMotion
    {
        sealed class Leg
        {
            public Transform upper, lower, ankle, paw;
            public Vector3 neutral, anchor, from, to, ankleOffset;
            public float offset, swingAge, swingDuration;
            public bool swinging;
            public bool reportedReach;
            public bool wantsStep;
            public Quaternion pawRotation, ankleRotation;
        }
        readonly Transform root, pelvis, spine, chest;
        readonly Transform[] ears = new Transform[2], tail = new Transform[5];
        readonly Leg[] legs = new Leg[4];
        readonly LineRenderer[] debugLines = new LineRenderer[4];
        readonly int[] legOrder = { 0, 1, 2, 3 };
        readonly Transform[] referenceBones;
        readonly Vector3[] referencePositions, referenceScales;
        readonly Quaternion[] referenceRotations;
        float dt, speed, caution, startle, cycle, bodyMove, lastYaw, turnSpeed;
        public float ContactError { get; private set; }
        public int Steps { get; private set; }

        public CatAlertMotion(Transform root, Transform model)
        {
            this.root = root;
            lastYaw = root.eulerAngles.y;
            var bones = new Dictionary<string, Transform>();
            foreach (var bone in model.GetComponentsInChildren<Transform>()) bones[bone.name] = bone;
            referenceBones = new List<Transform>(bones.Values).ToArray();
            referencePositions = new Vector3[referenceBones.Length];
            referenceScales = new Vector3[referenceBones.Length];
            referenceRotations = new Quaternion[referenceBones.Length];
            for (int i = 0; i < referenceBones.Length; i++)
            {
                referencePositions[i] = referenceBones[i].localPosition;
                referenceScales[i] = referenceBones[i].localScale;
                referenceRotations[i] = referenceBones[i].localRotation;
            }
            pelvis = bones["Pelvis"]; spine = bones["Spine"]; chest = bones["Chest"];
            ears[0] = bones["Ear.L"]; ears[1] = bones["Ear.R"];
            for (int i = 0; i < 5; i++) tail[i] = bones["Tail" + (i + 1)];
            for (int i = 0; i < 4; i++)
            {
                bool front = i < 2;
                string prefix = front ? "Fore" : "Hind", side = i % 2 == 0 ? ".L" : ".R";
                var leg = legs[i] = new Leg { upper = bones[prefix + "Upper" + side], lower = bones[prefix + "Lower" + side], paw = bones[prefix + "Paw" + side] };
                leg.ankle = front ? null : bones["HindAnkle" + side];
                leg.offset = front ? (i == 0 ? .25f : .75f) : (i == 2 ? 0 : .5f);
                leg.neutral = root.InverseTransformPoint(leg.paw.position);
                leg.anchor = leg.paw.position;
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-alert-bones") >= 0)
                    Debug.Log("ALERT LEG " + prefix + side + " neutral=" + leg.neutral.ToString("F3") + " upper=" + root.InverseTransformPoint(leg.upper.position).ToString("F3") + " lower=" + root.InverseTransformPoint(leg.lower.position).ToString("F3"));
                if (leg.ankle != null) leg.ankleOffset = root.InverseTransformVector(leg.ankle.position - leg.paw.position);
                if (Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-alert-bones") >= 0)
                {
                    var line = new GameObject("Study leg diagnostic").AddComponent<LineRenderer>();
                    line.positionCount = 4; line.startWidth = line.endWidth = .025f;
                    line.material = new Material(Shader.Find("Sprites/Default"));
                    line.startColor = line.endColor = i < 2 ? Color.red : Color.blue;
                    debugLines[i] = line;
                }
            }
        }

        public void Tick(float dt, float speed, float caution, float startle)
        {
            this.dt = dt; this.speed = speed;
            turnSpeed = Mathf.DeltaAngle(lastYaw, root.eulerAngles.y) / dt;
            lastYaw = root.eulerAngles.y;
            this.caution = Mathf.MoveTowards(this.caution, Mathf.Max(caution, Mathf.Abs(turnSpeed) > 15 ? .65f : 0), dt * 4);
            this.startle = Mathf.MoveTowards(this.startle, startle, dt * 10);
            bodyMove = Mathf.Lerp(bodyMove, Mathf.Clamp01(Mathf.Max(speed / .65f, Mathf.Abs(turnSpeed) / 500)), 1 - Mathf.Exp(-dt * 12));
        }

        public void Apply()
        {
            // Constant FBX channels may be omitted by the importer. Restore the
            // whole reference pose explicitly; IK must never build on last
            // frame's rotated/transferred joints.
            for (int i = 0; i < referenceBones.Length; i++)
            {
                referenceBones[i].localPosition = referencePositions[i];
                referenceBones[i].localRotation = referenceRotations[i];
                referenceBones[i].localScale = referenceScales[i];
            }
            float oldCycle = cycle;
            // Shorter cautious steps; the cycle is speed dependent, not a fixed
            // walk clip played faster. Stance contacts remain in world space.
            float stride = Mathf.Lerp(.40f, .34f, caution);
            if (speed > .025f || Mathf.Abs(turnSpeed) > 10) cycle += dt * Mathf.Max(speed / stride, Mathf.Abs(turnSpeed) / 180);
            float wave = cycle * Mathf.PI * 2;
            pelvis.position += Vector3.down * (.12f * caution + .045f * bodyMove + .035f * startle)
                + root.right * (.009f * Mathf.Sin(wave) * bodyMove);
            spine.rotation = Quaternion.AngleAxis(2.2f * Mathf.Sin(wave) * bodyMove, root.up) * spine.rotation;
            chest.rotation = Quaternion.AngleAxis(-1.8f * Mathf.Sin(wave) * bodyMove, root.up) * chest.rotation;
            ContactError = 0;
            int swingingCount = 0;
            foreach (var leg in legs) if (leg.swinging) swingingCount++;
            Array.Sort(legOrder, (a, b) =>
            {
                if (legs[a].swinging != legs[b].swinging) return legs[a].swinging ? -1 : 1;
                float gapA = (root.TransformPoint(legs[a].neutral) - legs[a].anchor).sqrMagnitude;
                float gapB = (root.TransformPoint(legs[b].neutral) - legs[b].anchor).sqrMagnitude;
                return gapB.CompareTo(gapA);
            });
            foreach (int i in legOrder)
            {
                var leg = legs[i];
                leg.pawRotation = leg.paw.rotation;
                if (leg.ankle != null) leg.ankleRotation = leg.ankle.rotation;
                Vector3 neutral = root.TransformPoint(leg.neutral);
                bool lift = Mathf.Floor(oldCycle - leg.offset - .62f) != Mathf.Floor(cycle - leg.offset - .62f);
                if ((speed > .025f || Mathf.Abs(turnSpeed) > 10) && lift) leg.wantsStep = true;
                // When stationary, replant one paw at a time for turns/settling.
                bool settle = speed < .08f && swingingCount == 0 && Vector3.Distance(neutral, leg.anchor) > .065f;
                bool urgent = Vector3.Distance(neutral, leg.anchor) > .13f && swingingCount < 2;
                if (!leg.swinging && swingingCount < 2 && (leg.wantsStep || settle || urgent))
                {
                    leg.wantsStep = false;
                    leg.swinging = true; swingingCount++;
                    leg.swingAge = 0;
                    leg.swingDuration = settle ? .14f : Mathf.Clamp(.38f * stride / Mathf.Max(.25f, speed), .12f, .22f);
                    leg.from = leg.anchor;
                    leg.to = neutral + root.forward * (settle ? 0 : Mathf.Min(.17f, speed * leg.swingDuration * .8f));
                    leg.to.y = leg.anchor.y;
                }
                Vector3 goal = leg.anchor;
                if (leg.swinging)
                {
                    leg.swingAge += dt;
                    if (Mathf.Abs(turnSpeed) > 10)
                    {
                        // A pivot changes the landing position while the paw is
                        // airborne. Retarget it to the turning body, rather than
                        // planting in the stale pre-turn direction.
                        float lookAhead = Mathf.Min(.07f, Mathf.Max(0, leg.swingDuration - leg.swingAge));
                        leg.to = root.position + Quaternion.AngleAxis(turnSpeed * lookAhead, Vector3.up) * (neutral - root.position);
                        leg.to.y = leg.anchor.y;
                    }
                    float u = Mathf.Clamp01(leg.swingAge / leg.swingDuration);
                    goal = Vector3.Lerp(leg.from, leg.to, u * u * (3 - 2 * u))
                        + Vector3.up * (.034f * Mathf.Pow(Mathf.Max(0, Mathf.Sin(u * Mathf.PI)), 1.3f));
                    if (u >= 1) { goal = leg.to; leg.anchor = leg.to; leg.swinging = false; swingingCount--; Steps++; }
                }
                // The generated rig has no separate scapula: translate the
                // shoulder joint a little during protraction, then solve the arm.
                if (i < 2 && leg.swinging)
                    leg.upper.position += root.forward * (.020f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(leg.swingAge / leg.swingDuration)));
                Vector3 endGoal = goal;
                Transform end = leg.paw;
                if (leg.ankle != null)
                { end = leg.ankle; endGoal += root.TransformVector(leg.ankleOffset); }
                Solve(leg.upper, leg.lower, end, endGoal, root.forward * (i < 2 ? -1 : 1));
                if (leg.ankle != null) leg.ankle.rotation = leg.ankleRotation;
                leg.paw.rotation = leg.pawRotation;
                float error = Vector3.Distance(leg.paw.position, goal);
                if (!float.IsFinite(error)) throw new InvalidOperationException("Non-finite leg contact: " + i);
                if (error > .035f && !leg.reportedReach)
                {
                    leg.reportedReach = true;
                    Debug.LogWarning("ALERT REACH leg=" + i + " error=" + error + " speed=" + speed + " neutral gap=" + Vector3.Distance(neutral, goal) + " swing=" + leg.swinging + " frame=" + Time.frameCount + " cycle=" + cycle + " root=" + root.position + " neutral=" + neutral + " goal=" + goal);
                }
                ContactError = Mathf.Max(ContactError, error);
                if (debugLines[i] != null)
                {
                    debugLines[i].SetPositions(new[] { leg.upper.position, leg.lower.position, leg.paw.position, goal + Vector3.up * .10f });
                }
            }
            for (int i = 0; i < ears.Length; i++)
                ears[i].rotation = Quaternion.AngleAxis((i == 0 ? -1 : 1) * startle * 22, root.forward)
                    * Quaternion.AngleAxis(-caution * 7 + startle * 15, root.right) * ears[i].rotation;
            // Alert tail has a small distal movement, not a continuous wag.
            for (int i = 0; i < tail.Length; i++)
                tail[i].rotation = Quaternion.AngleAxis(-caution * (i < 3 ? 9 : 4) - startle * 4, root.right)
                    * Quaternion.AngleAxis((i > 2 ? 2.5f : .7f) * Mathf.Sin(wave - i * .6f) * bodyMove, root.up) * tail[i].rotation;
        }

        public void ApplyAttentionPose(Transform neck, Transform head)
        {
            // Bring the resting, upward-facing muzzle into an attentive profile.
            // This is a study posture, not a permanently fearful crouch.
            neck.rotation = Quaternion.AngleAxis(caution * 9 + startle * 3, root.right) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(caution * 5, root.right) * head.rotation;
        }

        public void Dispose()
        {
            foreach (var line in debugLines) if (line != null) UnityEngine.Object.Destroy(line.gameObject);
        }

        static void Solve(Transform upper, Transform lower, Transform end, Vector3 goal, Vector3 pole)
        {
            Vector3 start = upper.position, joint = lower.position, tip = end.position;
            float a = Vector3.Distance(start, joint), b = Vector3.Distance(joint, tip);
            Vector3 delta = goal - start;
            if (delta.sqrMagnitude < .000001f || a < .001f || b < .001f) return;
            float reach = Mathf.Clamp(delta.magnitude, Mathf.Abs(a - b) + .0001f, a + b - .0001f);
            Vector3 axis = delta.normalized, bend = Vector3.ProjectOnPlane(pole, axis).normalized;
            float along = (a * a + reach * reach - b * b) / (2 * reach);
            Vector3 newJoint = start + axis * along + bend * Mathf.Sqrt(Mathf.Max(0, a * a - along * along));
            upper.rotation = Quaternion.FromToRotation(joint - start, newJoint - start) * upper.rotation;
            lower.rotation = Quaternion.FromToRotation(end.position - lower.position, start + axis * reach - lower.position) * lower.rotation;
        }
    }
}
