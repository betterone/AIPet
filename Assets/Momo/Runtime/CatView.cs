using System;
using UnityEngine;
using Momo.Core;

namespace Momo
{
    // Articulated character built entirely from code with personality-driven lifelike motion.
    public sealed class CatView : MonoBehaviour
    {
        public CatBreedProfile CurrentProfile { get; private set; }
        public ImportedCatView Imported { get; private set; }

        public Vector3 HeadPosition=>Imported!=null?Imported.HeadPosition:head!=null?head.position:transform.position+Vector3.up*.7f;
        public Vector3 MouthPosition=>Imported!=null?Imported.MouthPosition:head!=null?head.TransformPoint(new Vector3(0,-.14f,.29f)):HeadPosition+transform.forward*.22f;
        public Vector3 CrownPosition=>Imported!=null?Imported.CrownPosition:HeadPosition+Vector3.up*.24f;
        public Vector3 SnoutDirection=>Imported!=null?Imported.SnoutDirection:head!=null?head.forward:transform.forward;
        public float FoodStandOff=>Imported!=null?Imported.FoodStandOff:Mathf.Clamp(Vector3.Dot(MouthPosition-transform.position,transform.forward)+.055f,.48f,.85f);
        public System.Collections.Generic.IEnumerable<Collider> ContactSolids=>Imported!=null?Imported.ContactSolids:FallbackContactSolids();
        readonly System.Collections.Generic.List<Collider> fallbackSolids=new System.Collections.Generic.List<Collider>();
        readonly System.Collections.Generic.List<Mesh> fallbackMeshes=new System.Collections.Generic.List<Mesh>();
        System.Collections.Generic.IEnumerable<Collider> FallbackContactSolids()=>fallbackSolids;
        Vector3 headRest;
        bool bathPose,bathShy;
        public void SetBathPose(bool active,bool shy)
        {bathPose=active;bathShy=shy;if(Imported!=null)Imported.SetBathPose(active,shy);}
        void BuildFallbackContacts()
        {
            foreach(var mesh in fallbackMeshes)if(mesh!=null)Destroy(mesh);fallbackMeshes.Clear();fallbackSolids.Clear();
            var skull=new System.Collections.Generic.List<Vector3>();var torso=new System.Collections.Generic.List<Vector3>();
            foreach(var filter in GetComponentsInChildren<MeshFilter>())
            {
                var points=filter.name=="Round head"||filter.name.StartsWith("Muzzle ")?skull:filter.name=="Torso"||filter.name=="Chest"?torso:null;
                if(points!=null)foreach(var vertex in filter.sharedMesh.vertices)points.Add(filter.transform.TransformPoint(vertex));
            }
            fallbackSolids.Add(ContactGeometry.Convex("Head contact surface",head,skull,fallbackMeshes));
            fallbackSolids.Add(ContactGeometry.Convex("Body contact surface",body,torso,fallbackMeshes));
            headRest=head.localPosition;
        }
        public void SeparateFeedingHead(System.Collections.Generic.IEnumerable<Collider> hands,float maximumShift)
        {
            if(Imported!=null){Imported.SeparateFeedingHead(hands,maximumShift);return;}
            if(head!=null&&fallbackSolids.Count>0)ContactGeometry.SeparateBone(head,fallbackSolids[0],hands,maximumShift);
        }
        void OnDestroy(){foreach(var mesh in fallbackMeshes)if(mesh!=null)Destroy(mesh);}
        public bool HandEating {get;private set;}
        Vector3 handFood;bool handNuzzle,handAttention;
        public void SetHandFood(Vector3 target,bool eat,bool nuzzle=false,bool attend=false)
        {handFood=target;HandEating=eat;handNuzzle=nuzzle;handAttention=attend;if(Imported!=null)Imported.SetHandFood(target,eat,nuzzle,attend);}
        void HeadTarget(Transform bone)
        {var c=bone.gameObject.AddComponent<SphereCollider>();c.radius=.22f/Mathf.Max(.001f,bone.lossyScale.x);bone.gameObject.AddComponent<TouchTarget>().kind="cat-head";}
        Transform body, head, tail, tailTip, leftEye, rightEye, leftEar, rightEar, muzzle;
        Transform[] legs = new Transform[4];
        Vector3[] legOrigins = new Vector3[4];

        // Motion blends
        float sleepBlend, sitBlend, walkBlend, petGlow;
        float petArchBlend, purrBreath, strokeBlend, pawTimer;
        float slowBlinkBlend, slowBlinkTimer;
        float earTwitchL, earTwitchR, twitchCooldown;
        float stretchBlend, stretchTimer;
        float yawnBlend, yawnTimer;
        float gaitPhase;
        Vector3 presentationVelocity;

        // Dynamic Look-At
        Vector3 lookTargetWorld;
        float lookTargetWeight;
        float lookDecayTimer;

        public void Build(int coat)
        {
            Build(BreedArchetypes.Get(coat));
        }

        public void Build(CatBreedProfile profile, bool useImported = false)
        {
            GetComponent<CatBathEffects>()?.ResetAppearance();
            playMotion=null;
            CurrentProfile = profile ?? BreedArchetypes.Get(0);
            presentationVelocity = Vector3.zero;
            gaitPhase = 0;
            foreach (Transform child in transform) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (Imported != null) { Imported.enabled = false; Destroy(Imported); Imported = null; }
            string breedModel = ImportedCatCatalog.ForBreed(CurrentProfile.id);
            if ((useImported && ModelAvailability.CanUseImportedAppearance(CurrentProfile.id)) || breedModel != null)
            {
                Imported = gameObject.AddComponent<ImportedCatView>();
                if (Imported.Build(breedModel ?? ImportedCatView.ResourcePath))
                {
                    var collider = GetComponent<CapsuleCollider>() ?? gameObject.AddComponent<CapsuleCollider>();
                    collider.center = new Vector3(0, .48f, 0); collider.radius = .34f; collider.height = 1.02f;
                    var touch = GetComponent<TouchTarget>() ?? gameObject.AddComponent<TouchTarget>(); touch.kind = "cat";
                    return;
                }
                Destroy(Imported); Imported = null;
                Debug.LogWarning("Imported cat unavailable; using original character.");
            }

            // Parse palette
            Material fur = Art.Mat("Cat coat", CurrentProfile.hexCoat);
            Material secondary = Art.Mat("Cat secondary", CurrentProfile.hexSecondary);
            Material marking = Art.Mat("Cat marking", CurrentProfile.hexMarking);
            Material nose = Art.Mat("Cat nose", CurrentProfile.hexNose, 0.25f);
            Material eyeL = Art.Mat("Eye Left", CurrentProfile.hexEyeLeft, 0.08f);
            Material eyeR = Art.Mat("Eye Right", CurrentProfile.hexEyeRight, 0.08f);
            Material creamLight = Art.Mat("Catchlight", "#FFFFFF", 0.05f);
            Material pinkInnerEar = Art.Mat("Inner Ear", "#F0B4A8", 0.3f);

            // Proportions from profile
            float bw = CurrentProfile.bodyGirth;
            float bl = CurrentProfile.bodyLength;
            float hw = CurrentProfile.headWidth;
            float hh = CurrentProfile.headHeight;
            float legX = CurrentProfile.legScaleX;
            float legY = CurrentProfile.legScaleY;
            float tl = CurrentProfile.tailLength;
            float tt = CurrentProfile.tailThickness;
            float earS = CurrentProfile.earScale;
            int pattern = CurrentProfile.patternType;

            // Body assembly
            body = Art.Pivot("Body", transform, new Vector3(0, 0.48f * legY, 0));
            Art.Sphere("Torso", body, Vector3.zero, new Vector3(0.55f * bw, 0.58f * bw, 0.88f * bl), fur);

            // Chest & belly
            Material chestMat = (pattern == 1 && CurrentProfile.hexSecondary == CurrentProfile.hexCoat) ? fur : secondary;
            Art.Sphere("Chest", body, new Vector3(0, 0.05f, 0.27f * bl), new Vector3(0.51f * bw, 0.59f * bw, 0.42f), chestMat);

            // Pattern markings on body
            if (pattern == 0) // Tabby
            {
                for (int i = 0; i < 3; i++)
                    Art.Sphere("Back stripe", body, new Vector3(0, 0.26f * bw, (-0.24f + i * 0.19f) * bl), new Vector3(0.44f * bw, 0.045f, 0.075f), marking);
            }
            else if (pattern == 3) // Tuxedo back coat
            {
                Art.Sphere("Tuxedo jacket", body, new Vector3(0, 0.12f * bw, -0.05f * bl), new Vector3(0.56f * bw, 0.50f * bw, 0.86f * bl), fur);
            }
            else if (pattern == 4) // Calico patches
            {
                Art.Sphere("Calico patch 1", body, new Vector3(0.18f * bw, 0.20f * bw, 0.15f * bl), new Vector3(0.28f * bw, 0.26f * bw, 0.35f * bl), secondary);
                Art.Sphere("Calico patch 2", body, new Vector3(-0.16f * bw, 0.22f * bw, -0.18f * bl), new Vector3(0.26f * bw, 0.22f * bw, 0.32f * bl), marking);
            }

            // Head assembly
            head = Art.Pivot("Head", body, new Vector3(0, 0.28f * bw, 0.42f * bl));
            Material headBaseMat = (pattern == 2 || pattern == 5) ? secondary : fur;
            Art.Sphere("Round head", head, Vector3.zero, new Vector3(0.66f * hw, 0.59f * hh, 0.56f), headBaseMat);

            // Mask patterns on face
            if (pattern == 2) // Siamese Point
            {
                Art.Sphere("Siamese mask", head, new Vector3(0, -0.02f, 0.18f), new Vector3(0.46f * hw, 0.44f * hh, 0.32f), marking);
            }
            else if (pattern == 3) // Tuxedo Mask
            {
                Art.Sphere("Tuxedo forehead", head, new Vector3(0, 0.14f * hh, 0.05f), new Vector3(0.64f * hw, 0.35f * hh, 0.50f), fur);
            }
            else if (pattern == 5) // Ragdoll V-mask
            {
                Art.Sphere("Ragdoll face cap", head, new Vector3(0, 0.16f * hh, 0.08f), new Vector3(0.65f * hw, 0.36f * hh, 0.48f), marking);
            }

            // Ears
            Material earOuter = (pattern == 2 || pattern == 5) ? marking : fur;
            leftEar = Art.Pivot("Left Ear Pivot", head, new Vector3(-0.22f * hw, 0.20f * hh, -0.035f));
            leftEar.localScale = Vector3.one * earS;
            Art.Ear(leftEar, Vector3.zero, earOuter, pinkInnerEar);

            rightEar = Art.Pivot("Right Ear Pivot", head, new Vector3(0.22f * hw, 0.20f * hh, -0.035f));
            rightEar.localScale = Vector3.one * earS;
            Art.Ear(rightEar, Vector3.zero, earOuter, pinkInnerEar);

            // Muzzle & nose
            muzzle = Art.Pivot("Muzzle", head, Vector3.zero);
            Material muzzleMat = (pattern == 2) ? marking : secondary;
            Art.Sphere("Muzzle left", muzzle, new Vector3(-0.105f * hw, -0.10f * hh, 0.235f), new Vector3(0.24f * hw, 0.19f, 0.15f), muzzleMat);
            Art.Sphere("Muzzle right", muzzle, new Vector3(0.105f * hw, -0.10f * hh, 0.235f), new Vector3(0.24f * hw, 0.19f, 0.15f), muzzleMat);
            Art.Sphere("Little nose", muzzle, new Vector3(0, -0.065f * hh, 0.318f), new Vector3(0.087f, 0.058f, 0.047f), nose);

            // Eyes (Heterochromia supported!)
            leftEye = Art.Shape("Left eye", PrimitiveType.Sphere, head, new Vector3(-0.155f * hw, 0.05f * hh, 0.254f), new Vector3(0.08f, 0.13f, 0.04f), eyeL).transform;
            rightEye = Art.Shape("Right eye", PrimitiveType.Sphere, head, new Vector3(0.155f * hw, 0.05f * hh, 0.254f), new Vector3(0.08f, 0.13f, 0.04f), eyeR).transform;

            // Catchlights in eyes
            foreach (float x in new[] { -0.166f * hw, 0.144f * hw })
                Art.Sphere("Eye catchlight", head, new Vector3(x, 0.077f * hh, 0.277f), Vector3.one * 0.027f, creamLight);

            // Whiskers
            for (int side = -1; side <= 1; side += 2)
            {
                for (int j = 0; j < 2; j++)
                {
                    var whisker = Art.Shape("Whisker", PrimitiveType.Cube, head,
                        new Vector3(side * 0.28f * hw, -0.09f * hh - j * 0.055f, 0.265f),
                        new Vector3(0.23f, 0.01f, 0.012f), marking);
                    whisker.transform.localRotation = Quaternion.Euler(0, 0, side * (8 - j * 16));
                }
            }

            // Legs with socks
            Material pawMat = (pattern == 0 || pattern == 3 || pattern == 4 || pattern == 5) ? secondary : fur;
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -0.19f : 0.19f) * bw;
                float z = (i < 2 ? 0.24f : -0.28f) * bl;
                legs[i] = Art.Pivot("Leg " + i, transform, new Vector3(x, 0.42f * legY, z));
                legOrigins[i] = legs[i].localPosition;

                Material legFur = (pattern == 2 && i >= 2) ? marking : fur;
                Art.Sphere("Leg", legs[i], new Vector3(0, -0.15f * legY, 0), new Vector3(0.185f * legX, 0.38f * legY, 0.19f * legX), legFur);
                Art.Sphere("Sock paw", legs[i], new Vector3(0, -0.32f * legY, 0.035f), new Vector3(0.20f * legX, 0.15f, 0.27f * legX), pawMat);
            }

            // Tail
            Material tailMat = (pattern == 2) ? marking : fur;
            tail = Art.Pivot("Tail", body, new Vector3(0, 0.06f, -0.4f * bl));
            Art.Sphere("Tail base", tail, new Vector3(0, 0.19f * tl, -0.15f * tl), new Vector3(0.15f * tt, 0.5f * tl, 0.18f * tt), tailMat);

            tailTip = Art.Pivot("Tail tip", tail, new Vector3(0, 0.40f * tl, -0.17f * tl));
            Art.Sphere("Tail curl", tailTip, new Vector3(0.06f, 0.08f * tl, 0.03f), new Vector3(0.16f * tt, 0.28f * tl, 0.17f * tt), tailMat);
            if (pattern == 0)
                Art.Sphere("Tail marking", tailTip, new Vector3(0.1f, 0.15f * tl, 0.06f), new Vector3(0.15f * tt, 0.13f * tl, 0.14f * tt), marking);

            // Collider & Target
            var col = gameObject.GetComponent<CapsuleCollider>() ?? gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.5f * legY, 0.1f);
            col.radius = 0.42f * bw;
            col.height = 1.15f * bl;
            var target = gameObject.GetComponent<TouchTarget>() ?? gameObject.AddComponent<TouchTarget>();
            target.kind = "cat";
            HeadTarget(head);BuildFallbackContacts();
        }

        void FaceFood(float dt)
        {
            if(!HandEating&&!handNuzzle&&!handAttention)return;Vector3 dir=handFood-transform.position;dir.y=0;
            if(dir.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(dir),dt*200);
        }
        public void React()
        {
            if (Imported != null) { Imported.React(); return; }
            petGlow = 1f;
            petArchBlend = 1f;
            purrBreath = 1.8f;
            slowBlinkTimer = 2.0f; // Initiate trust slow-blink
        }
        public void Stroke(float strength)
        {
            if (Imported != null) { Imported.Stroke(strength); return; }
            if(strokeBlend<.1f)slowBlinkTimer=2f;
            strokeBlend = Mathf.Max(strokeBlend, Mathf.Clamp01(strength));
        }
        public void TriggerPaw()
        {
            if (Imported != null) { Imported.TriggerPaw(); return; }
            pawTimer = 1.6f;
        }

        public void LookAt(Vector3 targetWorld, float weight = 1f)
        {
            if (Imported != null) { Imported.LookAt(targetWorld, weight); return; }
            lookTargetWorld = targetWorld;
            lookTargetWeight = Mathf.Clamp01(weight);
            lookDecayTimer = 3.5f;
        }

        public void TriggerStretch()
        {
            if (Imported != null) { Imported.Stretch(); return; }
            stretchTimer = 2.4f;
        }

        public void TriggerYawn()
        {
            if (Imported != null) { Imported.React(); return; }
            yawnTimer = 1.8f;
        }

        CatPlayMotion playMotion;
        public void TickPlay(WandPhase phase,float progress,float distance,Vector3 attention)
        {
            if(Imported!=null){Imported.TickPlay(phase,progress,distance,attention);return;}
            if(playMotion==null)playMotion=new CatPlayMotion(transform,false);
            playMotion.Apply(phase,progress,distance,attention);
        }
        public bool HouseholdMotion;
        public float HouseholdHeading;
        public void Tick(WorldState s, float dt)
        {
            playMotion?.Restore();
            if (Imported != null) { Imported.HouseholdMotion=HouseholdMotion;Imported.HouseholdHeading=HouseholdHeading;Imported.Tick(s, dt); return; }
            if (body == null || head == null) return;
            head.localPosition=headRest;

            Vector3 desired = HouseholdMotion?new Vector3(s.pet.x,0,s.pet.z):PetPresentationPath.Target(s);
            Vector3 delta = desired - transform.position;
            var previous = transform.position;
            transform.position = HouseholdMotion?desired:Vector3.SmoothDamp(previous, desired,
                ref presentationVelocity, .12f, 1.45f, dt);
            gaitPhase = Mathf.Repeat(gaitPhase + Vector3.Distance(previous, transform.position) / .48f, 1);

            bool walking = Vector3.Distance(previous,transform.position)>dt*.015f;
            if (delta.sqrMagnitude > 0.004f)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(delta.normalized), dt * 7);
            else if (s.activity == Activity.Greet&&!HandEating&&!handNuzzle&&!handAttention)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, 160, 0), dt * 2);

            if(HouseholdMotion&&!HandEating&&!handNuzzle&&!handAttention)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.Euler(0,HouseholdHeading,0),dt*240);
            FaceFood(dt);
            float time = Time.time;
            float playRate = CurrentProfile != null ? Mathf.Lerp(0.8f, 1.4f, CurrentProfile.personality.playfulness) : 1f;
            float curiosityRate = CurrentProfile != null ? Mathf.Lerp(0.7f, 1.3f, CurrentProfile.personality.curiosity) : 1f;

            // State blends
            sleepBlend = Mathf.MoveTowards(sleepBlend, s.activity == Activity.Sleep ? 1 : 0, dt * 1.8f);
            sitBlend = Mathf.MoveTowards(sitBlend, s.activity == Activity.Watch || s.activity == Activity.Groom || s.activity == Activity.Greet ? 1 : 0, dt * 2);
            walkBlend = Mathf.MoveTowards(walkBlend, walking ? 1 : 0, dt * 4);
            petGlow = Mathf.Max(0, petGlow - dt * 0.5f);
            petArchBlend = Mathf.MoveTowards(petArchBlend, 0, dt * 0.7f);
            purrBreath = Mathf.MoveTowards(purrBreath, 0, dt * 0.5f);
            strokeBlend = Mathf.MoveTowards(strokeBlend, 0, dt * 1.4f);
            pawTimer = Mathf.Max(0, pawTimer - dt);

            // Micro behaviors decay
            if (lookDecayTimer > 0)
            {
                lookDecayTimer -= dt;
                if (lookDecayTimer <= 0) lookTargetWeight = 0;
            }
            if (slowBlinkTimer > 0)
            {
                slowBlinkTimer -= dt;
                // Double-phase slow blink bell curve
                slowBlinkBlend = Mathf.Sin(Mathf.Clamp01((2.0f - slowBlinkTimer) / 2.0f) * Mathf.PI);
            }
            else
            {
                slowBlinkBlend = 0;
            }

            if (stretchTimer > 0)
            {
                stretchTimer -= dt;
                stretchBlend = Mathf.Sin(Mathf.Clamp01((2.4f - stretchTimer) / 2.4f) * Mathf.PI);
            }
            else stretchBlend = 0;

            if (yawnTimer > 0)
            {
                yawnTimer -= dt;
                yawnBlend = Mathf.Sin(Mathf.Clamp01((1.8f - yawnTimer) / 1.8f) * Mathf.PI);
            }
            else yawnBlend = 0;

            // Ear twitch countdown
            twitchCooldown -= dt;
            if (twitchCooldown <= 0)
            {
                twitchCooldown = UnityEngine.Random.Range(4f, 10f);
                if (UnityEngine.Random.value > 0.5f) earTwitchL = 1f; else earTwitchR = 1f;
            }
            earTwitchL = Mathf.MoveTowards(earTwitchL, 0, dt * 8f);
            earTwitchR = Mathf.MoveTowards(earTwitchR, 0, dt * 8f);

            // Breathing & Purring
            float breathSpeed = 2f + purrBreath * 2.5f;
            float breathAmp = 0.012f + purrBreath * 0.018f + strokeBlend * .012f;
            float breath = Mathf.Sin(time * breathSpeed) * breathAmp;

            float archOffset = petArchBlend * 0.05f + strokeBlend * .035f - stretchBlend * 0.07f;
            body.localPosition = new Vector3(0, Mathf.Lerp(0.48f, 0.23f, sleepBlend) - sitBlend * 0.05f + breath + archOffset, 0);
            body.localRotation = Quaternion.Euler(-sitBlend * 18 - petArchBlend * 10 - strokeBlend * 5 + stretchBlend * 16, 0, sleepBlend * 12);
            body.localScale = new Vector3(1 + breath * 0.6f, 1 + breath * 1.2f, 1);

            // Legs gait
            for (int i = 0; i < 4; i++)
            {
                float gait = Mathf.Sin(gaitPhase * Mathf.PI * 2 + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 26 * walkBlend;
                float stretchLeg = (i < 2 ? stretchBlend * -24f : stretchBlend * 32f);
                legs[i].localRotation = Quaternion.Euler(gait + sleepBlend * 78 + (i >= 2 ? sitBlend * 70 : 0) + stretchLeg, 0, 0);
                legs[i].localPosition = legOrigins[i] + new Vector3(0, -sleepBlend * 0.14f - (i >= 2 ? sitBlend * 0.15f : 0), 0);
            }

            // Head base rotation
            bool nibble = HandEating || s.activity == Activity.Eat || s.activity == Activity.Drink;
            float groom = s.activity == Activity.Groom ? Mathf.Sin(time * 3) * 18 : 0;
            float baseHeadPitch = sleepBlend * 20 + (nibble ? 25 + Mathf.Sin(time * 6) * 8 : 0) + groom - petArchBlend * 15 - yawnBlend * 18;
            float baseHeadYaw = Mathf.Sin(time * (0.45f * curiosityRate)) * 10 * (1 - sleepBlend);
            float baseHeadRoll = Mathf.Sin(time * 0.7f) * 3 + petGlow * 12+(handNuzzle?Mathf.Sin(time*3)*10:0);

            Quaternion baseHeadRot = Quaternion.Euler(baseHeadPitch, baseHeadYaw, baseHeadRoll);

            // Dynamic Look-At blending
            if (lookTargetWeight > 0.01f && sleepBlend < 0.3f)
            {
                Vector3 toTarget = lookTargetWorld - head.position;
                Vector3 localDir = body.InverseTransformDirection(toTarget.normalized);
                float targetYaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -60, 60);
                float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localDir.y, -0.9f, 0.9f)) * Mathf.Rad2Deg, -35, 45);
                Quaternion lookRot = Quaternion.Euler(targetPitch, targetYaw, 0);
                head.localRotation = Quaternion.Slerp(baseHeadRot, lookRot, lookTargetWeight);
            }
            else
            {
                head.localRotation = Quaternion.Slerp(head.localRotation, baseHeadRot, dt * 6f);
            }

            if(HandEating)head.position+=Vector3.ClampMagnitude(handFood-MouthPosition,.12f);

            // Ears: independent radar & twitch
            if (leftEar != null && rightEar != null)
            {
                float earLRot = Mathf.Sin(time * 1.3f) * 4 + earTwitchL * 28;
                float earRRot = -Mathf.Sin(time * 1.5f + 0.4f) * 4 - earTwitchR * 28;
                leftEar.localRotation = Quaternion.Euler(earLRot, 0, earTwitchL * 15);
                rightEar.localRotation = Quaternion.Euler(earRRot, 0, -earTwitchR * 15);
            }

            // Tail motion
            float tailSpeed = (1.4f * playRate) + petGlow * 3f;
            float tailAmp = (sleepBlend > 0.5f ? 3 : 22) + petGlow * 14;
            tail.localRotation = Quaternion.Euler(sleepBlend * 65 - petArchBlend * 20, Mathf.Sin(time * tailSpeed) * tailAmp, sitBlend * 20);
            tailTip.localRotation = Quaternion.Euler(Mathf.Sin(time * (tailSpeed * 1.5f)) * 12, 0, Mathf.Sin(time) * 14);

            // Blink & Slow-blink system
            float regularBlink = Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 0.8f)), 32);
            float totalClose = Mathf.Max(sleepBlend, Mathf.Max(regularBlink, slowBlinkBlend * 0.85f + yawnBlend));
            float eyeHeight = Mathf.Lerp(0.13f, 0.014f, Mathf.Clamp01(totalClose));
            leftEye.localScale = new Vector3(0.08f, eyeHeight, 0.04f);
            rightEye.localScale = leftEye.localScale;

            // Play batting reaction
            if (s.activity == Activity.Play || pawTimer > 0)
            {
                legs[0].localRotation = Quaternion.Euler(-25 - Mathf.Max(0, Mathf.Sin(time * 3)) * 40, 0, 0);
            }
        }

        public void TickBoutique(float dt, float timeOffset, bool isFocused, Vector3? lookTarget = null)
        {
            if (body == null || head == null) return;
            head.localPosition=headRest;
            float time = Time.time + timeOffset;
            float playRate = CurrentProfile != null ? Mathf.Lerp(0.8f, 1.4f, CurrentProfile.personality.playfulness) : 1f;
            float curiosityRate = CurrentProfile != null ? Mathf.Lerp(0.7f, 1.3f, CurrentProfile.personality.curiosity) : 1f;

            // In boutique, cat sits comfortably on cushion
            sitBlend = isFocused ? Mathf.MoveTowards(sitBlend, 1f, dt * 3f) : 1f;
            walkBlend = 0f;
            float idleDrowse = (CurrentProfile != null && CurrentProfile.personality.playfulness < 0.4f) ? 0.35f : 0f;
            sleepBlend = isFocused ? Mathf.MoveTowards(sleepBlend, idleDrowse, dt * 1.5f) : 0f;
            if(!isFocused)
            {
                // Returning from a close-up must not carry a pet/stretch or
                // tracked-look animation back into the quiet nine-cat grid.
                petGlow=petArchBlend=purrBreath=stretchBlend=yawnBlend=0f;
                stretchTimer=yawnTimer=lookDecayTimer=lookTargetWeight=0f;
            }

            petGlow = Mathf.Max(0, petGlow - dt * 0.5f);
            petArchBlend = Mathf.MoveTowards(petArchBlend, 0, dt * 0.7f);
            purrBreath = Mathf.MoveTowards(purrBreath, 0, dt * 0.5f);

            if (isFocused && lookTarget.HasValue)
            {
                LookAt(lookTarget.Value, 0.9f);
            }

            if (lookDecayTimer > 0)
            {
                lookDecayTimer -= dt;
                if (lookDecayTimer <= 0) lookTargetWeight = 0;
            }
            if (slowBlinkTimer > 0)
            {
                slowBlinkTimer -= dt;
                slowBlinkBlend = Mathf.Sin(Mathf.Clamp01((2.0f - slowBlinkTimer) / 2.0f) * Mathf.PI);
            }
            else slowBlinkBlend = 0;

            if (stretchTimer > 0)
            {
                stretchTimer -= dt;
                stretchBlend = Mathf.Sin(Mathf.Clamp01((2.4f - stretchTimer) / 2.4f) * Mathf.PI);
            }
            else stretchBlend = 0;

            if (yawnTimer > 0)
            {
                yawnTimer -= dt;
                yawnBlend = Mathf.Sin(Mathf.Clamp01((1.8f - yawnTimer) / 1.8f) * Mathf.PI);
            }
            else yawnBlend = 0;

            // Periodic ear twitch
            twitchCooldown -= dt;
            if (twitchCooldown <= 0)
            {
                twitchCooldown = UnityEngine.Random.Range(3f, 8f);
                if (UnityEngine.Random.value > 0.5f) earTwitchL = 1f; else earTwitchR = 1f;
            }
            earTwitchL = Mathf.MoveTowards(earTwitchL, 0, dt * 8f);
            earTwitchR = Mathf.MoveTowards(earTwitchR, 0, dt * 8f);

            // Breathing & Purring
            float breathSpeed = 1.8f + purrBreath * 2.5f;
            float breathAmp = 0.012f + purrBreath * 0.018f;
            // The nine small portraits should read as still companions, not nine
            // heads bobbing in sync. Keep breathing and expressive movement for
            // the selected close-up; grid cats can still blink and twitch ears.
            float breath = isFocused ? Mathf.Sin(time * breathSpeed) * breathAmp : 0f;

            float archOffset = petArchBlend * 0.05f - stretchBlend * 0.07f;
            body.localPosition = new Vector3(0, Mathf.Lerp(0.48f, 0.23f, sleepBlend) - sitBlend * 0.05f + breath + archOffset, 0);
            body.localRotation = Quaternion.Euler(-sitBlend * 18 - petArchBlend * 10 + stretchBlend * 16, 0, sleepBlend * 12);
            body.localScale = new Vector3(1 + breath * 0.6f, 1 + breath * 1.2f, 1);

            // Legs
            for (int i = 0; i < 4; i++)
            {
                float stretchLeg = (i < 2 ? stretchBlend * -24f : stretchBlend * 32f);
                legs[i].localRotation = Quaternion.Euler(sleepBlend * 78 + (i >= 2 ? sitBlend * 70 : 0) + stretchLeg, 0, 0);
                legs[i].localPosition = legOrigins[i] + new Vector3(0, -sleepBlend * 0.14f - (i >= 2 ? sitBlend * 0.15f : 0), 0);
            }

            // Head base rotation
            float baseHeadPitch = sleepBlend * 20 - petArchBlend * 15 - yawnBlend * 18;
            float baseHeadYaw = isFocused ? Mathf.Sin(time * (0.45f * curiosityRate)) * 14 * (1 - sleepBlend) : 0f;
            float baseHeadRoll = isFocused ? Mathf.Sin(time * 0.7f) * 4 + petGlow * 12 : 0f;
            Quaternion baseHeadRot = Quaternion.Euler(baseHeadPitch, baseHeadYaw, baseHeadRoll);

            // Dynamic Look-At
            if (lookTargetWeight > 0.01f && sleepBlend < 0.3f)
            {
                Vector3 toTarget = lookTargetWorld - head.position;
                Vector3 localDir = body.InverseTransformDirection(toTarget.normalized);
                float targetYaw = Mathf.Clamp(Mathf.Atan2(localDir.x, localDir.z) * Mathf.Rad2Deg, -60, 60);
                float targetPitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(localDir.y, -0.9f, 0.9f)) * Mathf.Rad2Deg, -35, 45);
                Quaternion lookRot = Quaternion.Euler(targetPitch, targetYaw, 0);
                head.localRotation = Quaternion.Slerp(baseHeadRot, lookRot, lookTargetWeight);
            }
            else
            {
                head.localRotation = isFocused ? Quaternion.Slerp(head.localRotation, baseHeadRot, dt * 6f) : baseHeadRot;
            }
            if(bathPose)
            {
                // Small head/ear reactions preserve the planted stance and authoritative position.
                head.localRotation*=Quaternion.Euler(bathShy?10:-3,bathShy?24:0,bathShy?-8:0);
                if(bathShy&&leftEar!=null)leftEar.localRotation*=Quaternion.Euler(-12,0,0);
            }

            // Ears
            if (leftEar != null && rightEar != null)
            {
                float earLRot = Mathf.Sin(time * 1.3f) * 4 + earTwitchL * 28;
                float earRRot = -Mathf.Sin(time * 1.5f + 0.4f) * 4 - earTwitchR * 28;
                leftEar.localRotation = Quaternion.Euler(earLRot, 0, earTwitchL * 15);
                rightEar.localRotation = Quaternion.Euler(earRRot, 0, -earTwitchR * 15);
            }

            // Tail
            float tailSpeed = (1.4f * playRate) + petGlow * 3f;
            float tailAmp = (sleepBlend > 0.5f ? 4 : 22) + petGlow * 14;
            tail.localRotation = Quaternion.Euler(sleepBlend * 65 - petArchBlend * 20, Mathf.Sin(time * tailSpeed) * tailAmp, sitBlend * 20);
            tailTip.localRotation = Quaternion.Euler(Mathf.Sin(time * (tailSpeed * 1.5f)) * 12, 0, Mathf.Sin(time) * 14);

            // Blink & Slow-blink
            float regularBlink = Mathf.Pow(Mathf.Max(0, Mathf.Sin(time * 0.8f)), 32);
            float totalClose = Mathf.Max(sleepBlend, Mathf.Max(regularBlink, slowBlinkBlend * 0.85f + yawnBlend));
            float eyeHeight = Mathf.Lerp(0.13f, 0.014f, Mathf.Clamp01(totalClose));
            leftEye.localScale = new Vector3(0.08f, eyeHeight, 0.04f);
            rightEye.localScale = leftEye.localScale;
        }
    }
}
