using System;
using System.Collections.Generic;
using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Imported appearances are independent of the pet's saved identity/personality.
    // Motion clips are locally authored prototypes; facial blend shapes are not present.
    public sealed class ImportedCatView : MonoBehaviour
    {
        public Vector3 HeadPosition=>head!=null?head.position:transform.position+Vector3.up*.7f;
        Vector3 renderedMouth,mouthLocal,crownLocal,snoutLocal;bool mouthSampled;
        public Vector3 CrownPosition=>head.TransformPoint(crownLocal);
        public Vector3 SnoutDirection=>head.TransformDirection(snoutLocal).normalized;
        public float FoodStandOff {get;private set;}
        public readonly List<Collider> ContactSolids=new List<Collider>();
        readonly List<Mesh> contactMeshes=new List<Mesh>();
        public Vector3 MouthPosition=>mouthSampled?renderedMouth:head!=null?head.TransformPoint(mouthLocal):HeadPosition+transform.forward*.18f;
        bool handEating,handNuzzle,handAttention;
        public void SetHandFood(Vector3 target,bool eat,bool nuzzle=false,bool attend=false){handEating=eat;handNuzzle=nuzzle;handAttention=attend;studyFoodTarget=target;studyFeedTargetWeight=eat||nuzzle?1:0;}
        public const string ResourcePath = "ImportedCat/Tabby";
        public string LoadedResourcePath { get; private set; }
        public bool Ready { get; private set; }
        public float ModelScale { get; private set; }
        public float LastSpeed { get; private set; }
        public float WalkPhase => walkPhase;
        public string ActivePose { get; private set; } = "Idle";
        public float StudyFeedDistance { get; private set; }
        public SkinnedMeshRenderer Skin { get; private set; }
        GameObject model;
        Transform visualRoot;
        Animation player;
        Transform head, neck;
        Transform chest;
        readonly Transform[] foreUpper = new Transform[2], foreLower = new Transform[2], forePaw = new Transform[2];
        readonly Vector3[] studyPawContacts = new Vector3[2];
        readonly Quaternion[] studyPawRotations = new Quaternion[2];
        readonly Dictionary<string, AnimationState> clips = new Dictionary<string, AnimationState>();
        readonly Dictionary<string, float> weights = new Dictionary<string, float>();
        Vector3 gaze;
        float gazeTimer, gazeWeight, gazeYaw, gazePitch, petTimer, stretchTimer, strokeBlend, pawTimer, walkPhase, clock;
        Vector3 presentationVelocity;
        
        Quaternion baseHead, baseNeck;
        bool sampled;
        Vector3 studyFoodTarget;
        float studyFeedTargetWeight, studyFeedWeight;
        CatAlertMotion alertMotion;
        bool alertStudy;
        bool bathPose,bathShy;
        float bathBlend,bathShyBlend;
        public void SetBathPose(bool active,bool shy){bathPose=active;bathShy=shy;}
        Transform[] studyReferenceBones;
        Vector3[] studyReferencePositions, studyReferenceScales;
        Quaternion[] studyReferenceRotations;

        public bool Build(string resourcePath = ResourcePath)
        {
            var asset = Resources.Load<GameObject>(resourcePath);
            if (asset == null) return false;
            LoadedResourcePath = resourcePath;
            visualRoot = new GameObject("Imported model scale").transform;
            visualRoot.SetParent(transform, false);
            model = Instantiate(asset, visualRoot, false);
            model.name = "Imported cat: " + resourcePath;
            foreach (var bone in model.GetComponentsInChildren<Transform>())
            {
                if (bone.name == "Head") head = bone;
                if (bone.name == "Neck") neck = bone;
                if (bone.name == "Chest") chest = bone;
                for (int side = 0; side < 2; side++)
                {
                    string suffix = side == 0 ? ".L" : ".R";
                    if (bone.name == "ForeUpper" + suffix) foreUpper[side] = bone;
                    if (bone.name == "ForeLower" + suffix) foreLower[side] = bone;
                    if (bone.name == "ForePaw" + suffix) forePaw[side] = bone;
                }
            }
            Skin = model.GetComponentInChildren<SkinnedMeshRenderer>();
            if (Skin == null || head == null || neck == null)
            { Debug.LogError("Imported cat has no valid skin/head/neck."); Destroy(visualRoot.gameObject); return false; }
            var material = Resources.Load<Material>(resourcePath == ResourcePath ? "ImportedCat/TabbyMaterial" : ImportedCatCatalog.MaterialFor(resourcePath));
            if (material != null) Skin.sharedMaterial = material;
            player = model.GetComponent<Animation>() ?? model.AddComponent<Animation>();
            player.Stop();
            var existingClips = new List<string>();
            foreach (AnimationState state in player) existingClips.Add(state.name);
            foreach (string name in existingClips) player.RemoveClip(name);
            player.playAutomatically = false;
            Skin.quality = SkinQuality.Bone4;
            player.cullingType = AnimationCullingType.AlwaysAnimate;
            foreach (var clip in Resources.LoadAll<AnimationClip>(resourcePath))
            {
                if (clip.name.StartsWith("__preview__", StringComparison.Ordinal)) continue;
                string name = clip.name;
                if (name.Contains("|")) name = name.Substring(name.LastIndexOf('|') + 1);
                player.AddClip(clip, name);
                var state = player[name];
                state.wrapMode = WrapMode.Loop; state.speed = 0; state.enabled = true;
                state.weight = name == "Idle" ? 1 : 0;
                clips[name] = state; weights[name] = state.weight;
            }
            foreach (string name in new[] { "Idle", "Walk", "Sit", "Sleep", "Eat", "Paw", "Groom", "Stretch" })
                if (!clips.ContainsKey(name))
                { Debug.LogError("Imported cat missing clip: " + name); Destroy(visualRoot.gameObject); return false; }
            Ready = true;
            SamplePose(0, "Idle", 0);
            // Normalize the evaluated skin, after the first animation sample. Imported FBX
            // bounds can include a unit conversion which the animation root overrides.
            if (visualRoot.InverseTransformPoint(head.position).z < 0)
                visualRoot.localRotation = Quaternion.Euler(0, 180, 0);
            var baked = new Mesh(); Skin.BakeMesh(baked);
            var bounds = BakedWorldBounds(baked.bounds);
            ModelScale = 1.02f / bounds.size.y;
            visualRoot.localScale = Vector3.one * ModelScale;
            bounds = BakedWorldBounds(baked.bounds);
            visualRoot.localPosition -= new Vector3(0, bounds.min.y - transform.position.y, 0);
            Skin.localBounds = baked.bounds;
            var safeBounds = Skin.localBounds; safeBounds.Expand(safeBounds.size * .65f); Skin.localBounds = safeBounds;
            Skin.updateWhenOffscreen = false;
            CalibrateContacts(baked);Destroy(baked);
            var headHit=head.gameObject.AddComponent<SphereCollider>();headHit.radius=.22f/Mathf.Max(.001f,head.lossyScale.x);head.gameObject.AddComponent<TouchTarget>().kind="cat-head";
            studyReferenceBones = model.GetComponentsInChildren<Transform>();
            studyReferencePositions = new Vector3[studyReferenceBones.Length];
            studyReferenceScales = new Vector3[studyReferenceBones.Length];
            studyReferenceRotations = new Quaternion[studyReferenceBones.Length];
            for (int i = 0; i < studyReferenceBones.Length; i++)
            {
                studyReferencePositions[i] = studyReferenceBones[i].localPosition;
                studyReferenceScales[i] = studyReferenceBones[i].localScale;
                studyReferenceRotations[i] = studyReferenceBones[i].localRotation;
            }
            Debug.Log("Imported cat evaluated height: " + bounds.size.y + ", normalization: " + ModelScale);
            return true;
        }

        void CalibrateContacts(Mesh baked)
        {
            var headPoints=new List<Vector3>();var bodyPoints=new List<Vector3>();
            Vector3 lip=head.position+transform.forward*.18f,crown=head.position+Vector3.up*.18f;
            float front=float.MinValue,top=float.MinValue;
            foreach(var v in baked.vertices)
            {
                var world=Skin.transform.TransformPoint(v);var d=world-head.position;var local=transform.InverseTransformPoint(world);
                float x=Vector3.Dot(d,transform.right),z=Vector3.Dot(d,transform.forward),y=d.y;
                if(Mathf.Abs(x)<.32f&&z>-.12f&&z<.40f&&y>-.12f&&y<.38f)headPoints.Add(world);
                if(Mathf.Abs(x)<.045f&&local.y>.50f&&local.y<.92f&&z>front){front=z;lip=world;}
                if(Mathf.Abs(x)<.07f&&Mathf.Abs(z)<.12f&&y>top){top=y;crown=world;}
                if(Mathf.Abs(local.x)<.30f&&local.z>-.38f&&local.z<transform.InverseTransformPoint(head.position).z-.14f&&local.y>.2f&&local.y<.78f)bodyPoints.Add(world);
            }
            // Average the front lip band so an asymmetric triangle cannot make
            // the cat aim a cheek at the food.
            Vector3 lipSum=Vector3.zero;int lipCount=0;
            foreach(var v in baked.vertices)
            {
                var world=Skin.transform.TransformPoint(v);var d=world-head.position;
                if(Mathf.Abs(Vector3.Dot(d,transform.right))<.045f&&transform.InverseTransformPoint(world).y>.50f&&transform.InverseTransformPoint(world).y<.92f&&Vector3.Dot(d,transform.forward)>front-.018f){lipSum+=world;lipCount++;}
            }
            if(lipCount>0)lip=lipSum/lipCount;
            lip-=transform.forward*.008f+Vector3.up*.035f;
            mouthLocal=head.InverseTransformPoint(lip);crownLocal=head.InverseTransformPoint(crown);
            snoutLocal=head.InverseTransformDirection(transform.forward);
            FoodStandOff=Mathf.Clamp(Vector3.Dot(lip-transform.position,transform.forward)-.06f,.38f,.75f);
            ContactSolids.Add(ContactGeometry.Convex("Head contact surface",head,headPoints,contactMeshes));
            ContactSolids.Add(ContactGeometry.Convex("Body contact surface",transform,bodyPoints,contactMeshes));
            Debug.Log("CAT CONTACT "+LoadedResourcePath+" head="+transform.InverseTransformPoint(head.position)+" lip="+transform.InverseTransformPoint(lip)+" crown="+transform.InverseTransformPoint(crown)+" stand="+FoodStandOff);
        }
        public void SeparateFeedingHead(System.Collections.Generic.IEnumerable<Collider> hands,float maximumShift)
        {
            ContactGeometry.SeparateBone(head,ContactSolids[0],hands,maximumShift);
            renderedMouth=head.TransformPoint(mouthLocal);mouthSampled=true;
        }
        void OnDestroy(){foreach(var mesh in contactMeshes)if(mesh!=null)Destroy(mesh);}

        Bounds BakedWorldBounds(Bounds local)
        {
            var bounds = new Bounds(Skin.transform.TransformPoint(local.center), Vector3.zero);
            for (int i = 0; i < 8; i++)
                bounds.Encapsulate(Skin.transform.TransformPoint(local.center + Vector3.Scale(local.extents,
                    new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1))));
            return bounds;
        }

        public void React() { petTimer = 1.8f; }
        public void Stroke(float strength)
        {
            if(strokeBlend<.1f)petTimer=1.5f;
            strokeBlend=Mathf.Max(strokeBlend,Mathf.Clamp01(strength));
        }
        public void TriggerPaw() { pawTimer = 1.6f; }
        public void LookAt(Vector3 target, float weight)
        { gaze = target; gazeTimer = 2.5f; gazeWeight = Mathf.Clamp01(weight); }
        public void Stretch() { stretchTimer = 2.8f; }

        CatPlayMotion playMotion;bool playingPose;
        WandPhase playPhase;float playProgress,playDistance;Vector3 playAttention;
        public void TickPlay(WandPhase phase,float progress,float distance,Vector3 attention)
        {
            SamplePose(0,"Idle",0);playingPose=true;
            if(playMotion==null)playMotion=new CatPlayMotion(transform,true);
            playPhase=phase;playProgress=progress;playDistance=distance;playAttention=attention;
        }
        public bool HouseholdMotion;
        public float HouseholdHeading;
        public void Tick(WorldState state, float dt)
        {
            playMotion?.Restore();playingPose=false;
            if (!Ready || dt <= 0) return;
            Vector3 desired = HouseholdMotion?new Vector3(state.pet.x,0,state.pet.z):PetPresentationPath.Target(state);
            Vector3 delta = desired - transform.position;
            var before = transform.position;
            // This smoothing is visual only; the saved simulation position is unchanged.
            transform.position = HouseholdMotion?desired:Vector3.SmoothDamp(before, desired,
                ref presentationVelocity, .12f, 1.45f, dt);
            float distance = Vector3.Distance(before, transform.position);
            LastSpeed = distance / dt;
            if (delta.sqrMagnitude > .0004f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation,
                    Quaternion.LookRotation(delta.normalized), dt * 240);
            if(handEating||handNuzzle||handAttention)
            {var foodDirection=Vector3.ProjectOnPlane(studyFoodTarget-transform.position,Vector3.up);if(foodDirection.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(foodDirection),dt*160);}
            else if(HouseholdMotion)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.Euler(0,HouseholdHeading,0),dt*240);
            float moving = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.015f, .20f, LastSpeed));
            // The baked stance travels .22 model units over 65% of a cycle.
            // Advance from measured displacement so gait stops when the cat stops.
            walkPhase = Mathf.Repeat(walkPhase + distance / (.22f / .65f * ModelScale), 1);
            string pose = "Idle";
            switch (state.activity)
            {
                case Activity.Sleep: case Activity.Hide: pose = "Sleep"; break;
                case Activity.Watch: case Activity.Greet: pose = "Sit"; break;
                case Activity.Eat: case Activity.Drink: pose = "Eat"; break;
                case Activity.Groom: pose = "Groom"; break;
                case Activity.Play: pose = "Paw"; break;
            }
            // Palm feeding owns the neck target; the floor-bowl Eat clip pulls it down.
            if(handEating&&moving<.05f)pose="Idle";else if(handNuzzle&&moving<.05f)pose="Sit";
            studyFeedWeight=Mathf.Lerp(studyFeedWeight,studyFeedTargetWeight,1-Mathf.Exp(-dt*5));
            if (stretchTimer > 0 && moving < .05f && pose != "Sleep") pose = "Stretch";
            if (pawTimer > 0 && moving < .05f && pose != "Sleep") pose = "Paw";
            if(bathPose&&moving<.05f)pose="Idle";
            bathBlend=Mathf.MoveTowards(bathBlend,bathPose?1:0,dt*3);bathShyBlend=Mathf.MoveTowards(bathShyBlend,bathPose&&bathShy?1:0,dt*3);
            stretchTimer = Mathf.Max(0, stretchTimer - dt);
            pawTimer = Mathf.Max(0, pawTimer - dt);
            strokeBlend = Mathf.MoveTowards(strokeBlend, 0, dt * 1.4f);
            SamplePose(dt, pose, moving);
        }

        // The independent study owns position and heading. Sample its gait from the
        // distance it actually travelled instead of moving it a second time here.
        public void TickStudy(float dt, string pose, float distance, float speed)
        {
            if (!Ready || dt <= 0) return;
            LastSpeed = speed;
            walkPhase = Mathf.Repeat(walkPhase + distance / (.22f / .65f * ModelScale), 1);
            float moving = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.02f, .24f, speed));
            studyFeedWeight = Mathf.Lerp(studyFeedWeight, studyFeedTargetWeight, 1 - Mathf.Exp(-dt * 4));
            SamplePose(dt, pose, moving);
        }

        // A separate locomotion experiment: authored idle is only the reference
        // pose; world-space contacts and articulated legs own the study gait.
        public void TickAlertStudy(float dt, float speed, float caution, float startle, Vector3 attention)
        {
            if (!Ready || dt <= 0) return;
            alertStudy = true;
            LastSpeed = speed;
            RestoreStudyReference();
            LookAt(attention, 1);
            SamplePose(dt, "Idle", 0);
            if (alertMotion == null) alertMotion = new CatAlertMotion(transform, model.transform);
            alertMotion.Tick(dt, speed, caution, startle);
        }

        public float AlertContactError => alertMotion == null ? 0 : alertMotion.ContactError;
        public int AlertSteps => alertMotion == null ? 0 : alertMotion.Steps;

        public void SetStudyFeedingTarget(Vector3 target, float weight)
        { studyFoodTarget = target; studyFeedTargetWeight = Mathf.Clamp01(weight); }

        public void ResetStudy()
        {
            alertStudy = false;
            if (alertMotion != null) alertMotion.Dispose();
            alertMotion = null;
            RestoreStudyReference();
            clock = walkPhase = LastSpeed = gazeTimer = gazeYaw = gazePitch = studyFeedWeight = studyFeedTargetWeight = 0;
            SamplePose(0, "Idle", 0);
        }

        void RestoreStudyReference()
        {
            if (studyReferenceBones == null) return;
            for (int i = 0; i < studyReferenceBones.Length; i++)
            {
                studyReferenceBones[i].localPosition = studyReferencePositions[i];
                studyReferenceBones[i].localRotation = studyReferenceRotations[i];
                studyReferenceBones[i].localScale = studyReferenceScales[i];
            }
        }

        // Used by the isolated visual regression player; never writes pet state/saves.
        public void PreviewPose(string pose, float seconds, float phase = 0)
        { clock = seconds; walkPhase = phase; SamplePose(0, pose, pose == "Walk" ? 1 : 0); }

        void SamplePose(float dt, string pose, float moving)
        {
            clock += dt; ActivePose = moving > .5f ? "Walk" : pose;
            float sum = 0;
            foreach (var entry in clips)
            {
                float target = entry.Key == "Walk" ? moving : entry.Key == pose ? 1 - moving : 0;
                weights[entry.Key] = dt == 0 ? target : Mathf.Lerp(weights[entry.Key], target, 1 - Mathf.Exp(-dt * 5));
                sum += weights[entry.Key];
            }
            foreach (var entry in clips)
            {
                entry.Value.enabled = true;
                entry.Value.weight = weights[entry.Key] / Mathf.Max(.0001f, sum);
                entry.Value.normalizedTime = entry.Key == "Walk" ? walkPhase : clock / entry.Value.length;
            }
            player.Sample();
            baseHead = head.localRotation; baseNeck = neck.localRotation;
            sampled = true;
            gazeTimer = Mathf.Max(0, gazeTimer - dt); petTimer = Mathf.Max(0, petTimer - dt);
            float yaw = 0, pitch = 0;
            if (gazeTimer > 0 && pose != "Sleep" && pose != "Eat")
            {
                Vector3 dir = transform.InverseTransformDirection(gaze - head.position).normalized;
                yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -35, 35) * gazeWeight;
                pitch = Mathf.Clamp(-Mathf.Asin(dir.y) * Mathf.Rad2Deg, -20, 20) * gazeWeight;
            }
            pitch -= Mathf.Sin(Mathf.Clamp01(petTimer / 1.8f) * Mathf.PI) * 9 + strokeBlend * 6;
            float attentionRate = alertStudy ? 18 : 4;
            gazeYaw = Mathf.Lerp(gazeYaw, yaw, 1 - Mathf.Exp(-dt * attentionRate));
            gazePitch = Mathf.Lerp(gazePitch, pitch, 1 - Mathf.Exp(-dt * attentionRate));
        }

        void LateUpdate()
        {
            if (!Ready || !sampled) return;
            if(playingPose&&playMotion!=null){playMotion.Apply(playPhase,playProgress,playDistance,playAttention);return;}
            if (alertStudy && alertMotion != null) alertMotion.Apply();
            // Reapply after Unity's animation evaluation. Rotation axes are world-space,
            // independent of Blender/FBX bone roll conventions.
            neck.localRotation = baseNeck;
            head.localRotation = baseHead;
            if (alertStudy && alertMotion != null) alertMotion.ApplyAttentionPose(neck, head);
            neck.rotation = Quaternion.AngleAxis(gazeYaw * .3f, transform.up) * neck.rotation;
            head.rotation = Quaternion.AngleAxis(gazeYaw * .7f, transform.up)
                * Quaternion.AngleAxis(gazePitch, transform.right) * head.rotation;
            if(bathBlend>.001f)
            {
                // Deliberately bounded, additive wash reactions; paws remain in the sampled stance.
                neck.rotation=Quaternion.AngleAxis(bathShyBlend*7,transform.right)*neck.rotation;
                head.rotation=Quaternion.AngleAxis(bathShyBlend*24,transform.up)*Quaternion.AngleAxis(bathShyBlend*8-bathBlend*3,transform.right)*head.rotation;
            }
            if (studyFeedWeight > .001f && chest != null)
            {
                // Bring the neck towards the actual bowl while retaining ground
                // contacts. This overlay is confined to the independent study.
                for (int side = 0; side < 2; side++)
                    if (forePaw[side] != null) { studyPawContacts[side] = forePaw[side].position; studyPawRotations[side] = forePaw[side].rotation; }
                Vector3 contactTarget=studyFoodTarget+(handNuzzle?transform.right*Mathf.Sin(clock*3)*.055f:Vector3.zero);
                // Aim the actual snout, then solve the neck using the calibrated
                // head-local lip point. Body turn must finish before leaning in.
                var flat=Vector3.ProjectOnPlane(contactTarget-transform.position,Vector3.up).normalized;
                float facing=Vector3.Dot(transform.forward,flat);
                float lean=studyFeedWeight*Mathf.InverseLerp(.65f,.96f,facing);
                for(int pass=0;pass<3;pass++)
                {
                    var muzzleDirection=handEating?(transform.forward*.35f-Vector3.up*.94f):contactTarget-head.position;
                    if(muzzleDirection.sqrMagnitude>.00001f)
                        head.rotation=Quaternion.Slerp(head.rotation,Quaternion.FromToRotation(SnoutDirection,muzzleDirection)*head.rotation,lean*.65f);
                    var offset=head.TransformPoint(mouthLocal)-head.position;
                    var snoutRotation=head.rotation;
                    SolveTwoBone(chest,neck,head,contactTarget-offset,lean);
                    head.rotation=snoutRotation;
                }
                for (int side = 0; side < 2; side++)
                    if (foreUpper[side] != null && foreLower[side] != null && forePaw[side] != null)
                    {
                        SolveTwoBone(foreUpper[side], foreLower[side], forePaw[side], studyPawContacts[side], 1);
                        forePaw[side].rotation = studyPawRotations[side];
                    }
                StudyFeedDistance = Vector3.Distance(head.TransformPoint(mouthLocal), studyFoodTarget);
            }
            renderedMouth=head.TransformPoint(mouthLocal);mouthSampled=true;
        }

        static void SolveTwoBone(Transform upper, Transform lower, Transform end, Vector3 goal, float weight)
        {
            Vector3 start = upper.position, joint = lower.position, tip = end.position;
            float first = Vector3.Distance(start, joint), second = Vector3.Distance(joint, tip);
            Vector3 direction = goal - start;
            if (direction.sqrMagnitude < .00001f || first < .001f || second < .001f) return;
            float reach = Mathf.Clamp(direction.magnitude, Mathf.Abs(first - second) + .0001f, first + second - .0001f);
            Vector3 axis = direction.normalized;
            Vector3 bend = Vector3.ProjectOnPlane(joint - start, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(Vector3.up, axis);
            if (bend.sqrMagnitude < .000001f) bend = Vector3.ProjectOnPlane(Vector3.right, axis);
            float along = (first * first + reach * reach - second * second) / (2 * reach);
            float height = Mathf.Sqrt(Mathf.Max(0, first * first - along * along));
            Vector3 newJoint = start + axis * along + bend.normalized * height;
            upper.rotation = Quaternion.Slerp(upper.rotation, Quaternion.FromToRotation(joint - start, newJoint - start) * upper.rotation, weight);
            Vector3 newTip = start + axis * reach;
            lower.rotation = Quaternion.Slerp(lower.rotation, Quaternion.FromToRotation(end.position - lower.position, newTip - lower.position) * lower.rotation, weight);
        }
    }
}
