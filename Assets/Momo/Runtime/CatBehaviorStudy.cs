using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Momo
{
    // An independent, disposable behavior-and-animation study. It never loads or saves a pet.
    public sealed class CatBehaviorStudy : MonoBehaviour
    {
        enum SceneCase { BusyCall, FriendlyCall, WaryFood, FamiliarFood, FullFood, PlayBurst }
        enum Phase { Idle, Notice, Assess, Approach, Pause, Eat, Recover, Finished }
        enum Response { Ignore, NoticeOnly, Approach }

        static Response CallResponse(float trust, float energy, float stress, bool busy)
        {
            if (stress > .7f || energy < .2f) return Response.Ignore;
            if (busy || trust < .72f || energy < .38f) return Response.NoticeOnly;
            return Response.Approach;
        }

        static bool InterestedInFood(float hunger, float stress)
        { return hunger > .42f && stress < .9f; }

        readonly Dictionary<string, string> poses = new Dictionary<string, string>();
        readonly string[] names = { "Busy call", "Friendly call", "Wary food", "Familiar food", "Full food", "Self-started toy approach" };
        Transform catRoot;
        GameObject cubeModel;
        ImportedCatView tabby;
        Animation animator;
        GameObject bowl, food, ball;
        Camera studyCamera;
        SceneCase sceneCase;
        Phase phase;
        float phaseAge, totalAge, speed, headingVelocity;
        float maximumAcceleration, maximumTurnRate, lastMeasuredSpeed, lastHeading;
        Vector3 moveStart, moveDirection;
        float moveLength, moveAge, movePeak, moveCruiseTime, moveRampUp, moveRampDown, moveDuration;
        Vector3 target;
        bool reviewing, sawPause, ate, moved, refused, original;
        string reason = "";
        string reviewDirectory;

        void Start()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-behavior-v2") < 0 &&
                Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-behavior-original") < 0)
            {
                gameObject.AddComponent<CatAlertStudy>();
                enabled = false;
                return;
            }
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            original = Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-behavior-original") >= 0;
            BuildStage();
            Setup(SceneCase.BusyCall);
            reviewing = Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-behavior-review") >= 0;
            if (reviewing) StartCoroutine(Review());
        }

        void BuildStage()
        {
            var cameraObject = new GameObject("Study camera");
            studyCamera = cameraObject.AddComponent<Camera>();
            studyCamera.clearFlags = CameraClearFlags.SolidColor;
            studyCamera.backgroundColor = new Color(.91f, .86f, .78f);
            studyCamera.fieldOfView = 34;
            studyCamera.transform.position = new Vector3(2.65f, 2.1f, -6.2f);
            studyCamera.transform.LookAt(new Vector3(0, .42f, 0));
            var lightObject = new GameObject("Soft key light");
            var light = lightObject.AddComponent<Light>(); light.type = LightType.Directional;
            light.color = new Color(1f, .94f, .83f); light.intensity = 1.25f;
            light.shadows = LightShadows.Soft; light.shadowStrength = .42f; light.shadowBias = .015f;
            QualitySettings.shadows = ShadowQuality.All; QualitySettings.shadowDistance = 18;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.62f, .64f, .67f);
            lightObject.transform.rotation = Quaternion.Euler(48, -30, 0);
            Primitive("Floor", PrimitiveType.Cube, new Vector3(0, -.08f, 0), new Vector3(8, .16f, 4.8f), new Color(.63f, .47f, .35f));
            Primitive("Rug", PrimitiveType.Cube, new Vector3(0, .008f, 0), new Vector3(6.8f, .018f, 2.8f), new Color(.88f, .78f, .64f));
            bowl = Primitive("Food bowl", PrimitiveType.Cylinder, new Vector3(1.65f, .055f, .45f), new Vector3(.42f, .05f, .42f), new Color(.48f, .65f, .67f));
            food = Primitive("Food", PrimitiveType.Sphere, new Vector3(1.65f, .105f, .45f), new Vector3(.26f, .035f, .26f), new Color(.47f, .27f, .16f));
            ball = Primitive("Toy ball", PrimitiveType.Sphere, new Vector3(.6f, .13f, -.65f), Vector3.one * .22f, new Color(.84f, .32f, .25f));
            var source = Resources.Load<GameObject>("StudyCat/animal-cat");
            if (source == null) throw new Exception("Kenney cat model is missing from Resources/StudyCat.");
            catRoot = new GameObject("Kenney study cat").transform;
            var model = Instantiate(source, catRoot);
            cubeModel = model;
            model.name = "Kenney Cube Pets / animal-cat";
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) throw new Exception("Kenney cat has no renderer.");
            var material = new Material(Shader.Find("Standard"));
            material.mainTexture = Resources.Load<Texture2D>("StudyCat/colormap");
            material.SetFloat("_Glossiness", .08f);
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers) { renderer.sharedMaterial = material; bounds.Encapsulate(renderer.bounds); }
            float scale = .88f / Mathf.Max(.01f, bounds.size.y);
            model.transform.localScale = Vector3.one * scale;
            model.transform.localPosition = new Vector3(-bounds.center.x * scale, -bounds.min.y * scale, -bounds.center.z * scale);
            animator = model.GetComponent<Animation>();
            if (animator == null) animator = model.AddComponent<Animation>();
            foreach (var clip in Resources.LoadAll<AnimationClip>("StudyCat/animal-cat"))
            {
                var shortName = clip.name.ToLowerInvariant();
                int separator = shortName.LastIndexOf('|');
                if (separator >= 0) shortName = shortName.Substring(separator + 1);
                if (shortName.StartsWith("__preview")) continue;
                animator.AddClip(clip, shortName);
                poses[shortName] = shortName;
            }
            if (!poses.ContainsKey("idle") || !poses.ContainsKey("walk") || !poses.ContainsKey("run") || !poses.ContainsKey("eat"))
                throw new Exception("Kenney model lacks a required idle/walk/run/eat clip. Found: " + string.Join(", ", poses.Keys));
            Debug.Log("CAT STUDY MODEL clips=" + string.Join(", ", poses.Keys) + " originalHeight=" + bounds.size.y);
            Pose("idle", 0);
            var tabbyObject = new GameObject("Articulated study cat");
            tabbyObject.transform.SetParent(catRoot, false);
            tabby = tabbyObject.AddComponent<ImportedCatView>();
            if (!tabby.Build()) throw new Exception("The articulated tabby failed to load.");
            cubeModel.SetActive(original);
            tabbyObject.SetActive(!original);
        }

        static GameObject Primitive(string name, PrimitiveType kind, Vector3 position, Vector3 scale, Color color)
        {
            var obj = GameObject.CreatePrimitive(kind); obj.name = name;
            obj.transform.position = position; obj.transform.localScale = scale;
            var material = new Material(Shader.Find("Standard")); material.color = color;
            material.SetFloat("_Glossiness", .08f);
            obj.GetComponent<Renderer>().sharedMaterial = material;
            return obj;
        }

        void Setup(SceneCase next)
        {
            sceneCase = next;
            catRoot.position = new Vector3(-2.15f, 0, .35f);
            catRoot.rotation = Quaternion.Euler(0, 90, 0);
            ball.transform.position = new Vector3(.6f, .13f, -.65f);
            ball.SetActive(next == SceneCase.PlayBurst);
            bool withFood = next == SceneCase.WaryFood || next == SceneCase.FamiliarFood || next == SceneCase.FullFood;
            bowl.SetActive(withFood); food.SetActive(withFood);
            sawPause = ate = moved = refused = false; totalAge = 0;
            speed = headingVelocity = lastMeasuredSpeed = maximumAcceleration = maximumTurnRate = 0;
            lastHeading = catRoot.eulerAngles.y;
            tabby.ResetStudy();
            target = original ? new Vector3(1.4f, 0, .38f) : ContactRootTarget(bowl.transform.position, .54f);
            if (next == SceneCase.BusyCall)
            {
                if (CallResponse(.4f, .7f, .2f, true) != Response.NoticeOnly) throw new Exception("Busy call decision changed.");
                reason = "Heard the call while occupied; stays put";
                SetPhase(Phase.Notice);
            }
            else if (next == SceneCase.FriendlyCall)
            {
                if (CallResponse(.9f, .8f, .1f, false) != Response.Approach) throw new Exception("Friendly call decision changed.");
                reason = "Familiar and willing; approaches by choice";
                target = new Vector3(-.35f, 0, -.65f);
                SetPhase(Phase.Notice);
            }
            else if (next == SceneCase.FullFood)
            {
                if (InterestedInFood(.15f, .2f)) throw new Exception("Full cat food decision changed.");
                reason = "Already full; notices food but does not come";
                SetPhase(Phase.Notice);
            }
            else if (next == SceneCase.PlayBurst)
            {
                reason = original ? "Original short run" : "A short self-started approach and paw tap";
                target = original ? new Vector3(.55f, 0, -.55f) : ContactRootTarget(ball.transform.position, .52f);
                SetPhase(Phase.Notice);
            }
            else
            {
                if (!InterestedInFood(.8f, next == SceneCase.WaryFood ? .55f : .1f)) throw new Exception("Hungry cat food decision changed.");
                reason = next == SceneCase.WaryFood ? "Unfamiliar food: watches, approaches, pauses to check" : "Familiar meal: brief check, then walks over";
                SetPhase(Phase.Notice);
            }
            Debug.Log("CAT STUDY START " + names[(int)next] + " / " + reason);
        }

        void SetPhase(Phase next)
        {
            phase = next; phaseAge = 0;
            switch (next)
            {
                case Phase.Notice: Pose("idle", .2f); break;
                case Phase.Assess: Pose("idle", .25f); break;
                case Phase.Approach:
                    BeginMove();
                    Pose(sceneCase == SceneCase.PlayBurst ? "run" : "walk", .22f); break;
                case Phase.Pause: Pose("idle", .25f); sawPause = true; break;
                case Phase.Eat: Pose("eat", .25f); ate = true; break;
                case Phase.Recover: Pose("idle", .3f); break;
                case Phase.Finished: Pose("idle", .3f); break;
            }
            Debug.Log("CAT STUDY PHASE " + names[(int)sceneCase] + " / " + next + " at " + totalAge.ToString("F2"));
        }

        void Pose(string name, float fade)
        {
            if (!poses.ContainsKey(name)) return;
            if (fade <= 0) animator.Play(name); else animator.CrossFade(name, fade);
        }

        void Update()
        {
            if (catRoot == null) return;
            if (!reviewing)
            {
                if (Input.GetKeyDown(KeyCode.B))
                {
                    original = !original;
                    cubeModel.SetActive(original); tabby.gameObject.SetActive(!original);
                    Setup(sceneCase);
                }
                if (Input.GetKeyDown(KeyCode.Alpha1)) Setup(SceneCase.BusyCall);
                if (Input.GetKeyDown(KeyCode.Alpha2)) Setup(SceneCase.FriendlyCall);
                if (Input.GetKeyDown(KeyCode.Alpha3)) Setup(SceneCase.WaryFood);
                if (Input.GetKeyDown(KeyCode.Alpha4)) Setup(SceneCase.FamiliarFood);
                if (Input.GetKeyDown(KeyCode.Alpha5)) Setup(SceneCase.FullFood);
                if (Input.GetKeyDown(KeyCode.Alpha6)) Setup(SceneCase.PlayBurst);
            }
            float dt = Mathf.Min(Time.deltaTime, .1f);
            var previousPosition = catRoot.position;
            totalAge += dt; phaseAge += dt;
            if (phase == Phase.Notice)
            {
                Vector3 attention = sceneCase == SceneCase.BusyCall || sceneCase == SceneCase.FriendlyCall
                    ? new Vector3(-.35f, 0, -.65f)
                    : sceneCase == SceneCase.PlayBurst ? ball.transform.position : bowl.transform.position;
                if (original) Face(attention, dt, .65f);
                else tabby.LookAt(attention + Vector3.up * .35f, 1);
                float duration = sceneCase == SceneCase.WaryFood ? 1.2f : .6f;
                if (phaseAge >= duration)
                {
                    if (sceneCase == SceneCase.BusyCall || sceneCase == SceneCase.FullFood)
                    { refused = true; SetPhase(Phase.Recover); }
                    else if (sceneCase == SceneCase.WaryFood) SetPhase(Phase.Assess);
                    else SetPhase(Phase.Approach);
                }
            }
            else if (phase == Phase.Assess)
            {
                if (phaseAge >= 1.5f) { target = new Vector3(.1f, 0, .38f); SetPhase(Phase.Approach); }
            }
            else if (phase == Phase.Approach)
            {
                float cruise = sceneCase == SceneCase.PlayBurst ? (original ? 2.15f : 1.2f) : sceneCase == SceneCase.WaryFood ? .62f : .95f;
                var before = catRoot.position;
                if (original)
                {
                    speed = cruise;
                    Face(target, dt, sceneCase == SceneCase.PlayBurst ? 7 : 4);
                    catRoot.position = Vector3.MoveTowards(before, target, speed * dt);
                }
                else MoveSoftly(target, dt);
                if (Vector3.Distance(before, catRoot.position) > .0001f) moved = true;
                if (original ? Vector3.Distance(catRoot.position, target) < .015f : moveAge >= moveDuration - .0001f)
                {
                    if (sceneCase == SceneCase.WaryFood && !sawPause) SetPhase(Phase.Pause);
                    else if (sceneCase == SceneCase.WaryFood || sceneCase == SceneCase.FamiliarFood) SetPhase(Phase.Eat);
                    else SetPhase(Phase.Recover);
                }
            }
            else if (phase == Phase.Pause)
            {
                if (original) Face(bowl.transform.position, dt, 1.2f);
                else tabby.LookAt(bowl.transform.position + Vector3.up * .2f, .75f);
                if (phaseAge >= 1.35f)
                {
                    target = original ? new Vector3(1.38f, 0, .38f) : ContactRootTarget(bowl.transform.position, .54f);
                    SetPhase(Phase.Approach);
                }
            }
            else if (phase == Phase.Eat)
            {
                if (phaseAge >= 2.7f) SetPhase(Phase.Recover);
            }
            else if (phase == Phase.Recover && phaseAge >= (!original && sceneCase == SceneCase.PlayBurst ? 2.2f : 1.2f)) SetPhase(Phase.Finished);

            if (!original)
            {
                float distance = Vector3.Distance(previousPosition, catRoot.position);
                float measured = distance / Mathf.Max(.001f, dt);
                maximumAcceleration = Mathf.Max(maximumAcceleration, Mathf.Abs(measured - lastMeasuredSpeed) / Mathf.Max(.001f, dt));
                maximumTurnRate = Mathf.Max(maximumTurnRate, Mathf.Abs(Mathf.DeltaAngle(lastHeading, catRoot.eulerAngles.y)) / Mathf.Max(.001f, dt));
                lastMeasuredSpeed = measured; lastHeading = catRoot.eulerAngles.y;
                string pose = phase == Phase.Eat ? "Eat" : phase == Phase.Recover && sceneCase == SceneCase.PlayBurst ? "Paw" : "Idle";
                tabby.SetStudyFeedingTarget(food.transform.position - catRoot.forward * .07f, phase == Phase.Eat ? 1 : 0);
                tabby.TickStudy(dt, pose, distance, measured);
            }
        }

        Vector3 ContactRootTarget(Vector3 stimulus, float reach)
        {
            stimulus.y = 0;
            Vector3 direction = stimulus - catRoot.position; direction.y = 0;
            return stimulus - direction.normalized * reach;
        }

        void BeginMove()
        {
            moveStart = catRoot.position;
            moveDirection = target - moveStart; moveDirection.y = 0;
            moveLength = moveDirection.magnitude;
            moveDirection = moveDirection.normalized;
            moveAge = 0;
            moveRampUp = sceneCase == SceneCase.WaryFood ? .85f : .65f;
            moveRampDown = .70f;
            float cruise = sceneCase == SceneCase.PlayBurst ? 1.2f : sceneCase == SceneCase.WaryFood ? .62f : .95f;
            movePeak = Mathf.Min(cruise, 2 * moveLength / (moveRampUp + moveRampDown));
            moveCruiseTime = Mathf.Max(0, moveLength / Mathf.Max(.001f, movePeak) - (moveRampUp + moveRampDown) / 2);
            moveDuration = moveRampUp + moveCruiseTime + moveRampDown;
            Debug.Log("CAT STUDY MOVE distance=" + moveLength + " duration=" + moveDuration);
        }

        void MoveSoftly(Vector3 point, float dt)
        {
            float desiredHeading = Mathf.Atan2(moveDirection.x, moveDirection.z) * Mathf.Rad2Deg;
            float yaw = Mathf.SmoothDampAngle(catRoot.eulerAngles.y, desiredHeading, ref headingVelocity, .24f, 120, dt);
            catRoot.rotation = Quaternion.Euler(0, yaw, 0);
            // Integrate a cosine velocity ramp. Speed and acceleration are zero
            // at the endpoints, with a real stop rather than an endless slow crawl.
            moveAge = Mathf.Min(moveAge + dt, moveDuration);
            float covered;
            if (moveAge < moveRampUp)
            {
                float angle = Mathf.PI * moveAge / moveRampUp;
                speed = movePeak * (1 - Mathf.Cos(angle)) / 2;
                covered = movePeak * (moveAge / 2 - moveRampUp * Mathf.Sin(angle) / (2 * Mathf.PI));
            }
            else if (moveAge < moveRampUp + moveCruiseTime)
            {
                speed = movePeak;
                covered = movePeak * (moveRampUp / 2 + moveAge - moveRampUp);
            }
            else
            {
                float elapsed = moveAge - moveRampUp - moveCruiseTime;
                float angle = Mathf.PI * elapsed / moveRampDown;
                speed = movePeak * (1 + Mathf.Cos(angle)) / 2;
                covered = movePeak * (moveRampUp / 2 + moveCruiseTime + elapsed / 2 + moveRampDown * Mathf.Sin(angle) / (2 * Mathf.PI));
            }
            catRoot.position = moveStart + moveDirection * Mathf.Min(moveLength, covered);
            tabby.LookAt(point + Vector3.up * .4f, .5f);
        }

        void Face(Vector3 point, float dt, float turnRate)
        {
            var offset = point - catRoot.position; offset.y = 0;
            if (offset.sqrMagnitude < .002f) return;
            catRoot.rotation = Quaternion.RotateTowards(catRoot.rotation, Quaternion.LookRotation(offset), turnRate * 120 * dt);
        }

        void OnGUI()
        {
            GUI.color = new Color(.22f, .19f, .17f);
            GUI.skin.label.fontSize = 20;
            GUI.skin.button.fontSize = 15;
            GUI.Label(new Rect(24, 18, Screen.width - 48, 35), "CAT MOTION " + (original ? "V1 / original" : "V2 / articulated") + "  |  " + names[(int)sceneCase]);
            GUI.Label(new Rect(24, 50, Screen.width - 48, 35), reason);
            GUI.Label(new Rect(24, 80, Screen.width - 48, 35), "Now: " + phase + (reviewing ? "" : "   |   B: compare original / revised"));
            if (reviewing) return;
            for (int i = 0; i < names.Length; i++)
                if (GUI.Button(new Rect(24 + (i % 3) * 170, Screen.height - 108 + (i / 3) * 44, 160, 38), (i + 1) + "  " + names[i]))
                    Setup((SceneCase)i);
        }

        IEnumerator Review()
        {
            Application.runInBackground = true;
            Screen.SetResolution(1000, 720, FullScreenMode.Windowed);
            Time.captureFramerate = 30;
            reviewDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../artifacts/cat-behavior-study/" + (original ? "v1-comparison" : "v2")));
            Directory.CreateDirectory(reviewDirectory);
            var passPath = Path.Combine(reviewDirectory, "PASS.txt");
            if (File.Exists(passPath)) File.Delete(passPath);
            yield return new WaitForSecondsRealtime(.5f);
            int frame = 0;
            var timings = new System.Text.StringBuilder();
            foreach (SceneCase next in Enum.GetValues(typeof(SceneCase)))
            {
                Setup(next);
                float maxFeedDistance = 0;
                timings.AppendLine(names[(int)next] + " starts at frame " + frame);
                while (phase != Phase.Finished && totalAge < 25f)
                {
                    yield return new WaitForEndOfFrame();
                    if (!original && phase == Phase.Eat && phaseAge > 1.2f)
                        maxFeedDistance = Mathf.Max(maxFeedDistance, tabby.StudyFeedDistance);
                    if (!original && frame % 30 == 0)
                    {
                        var mesh = new Mesh(); tabby.Skin.BakeMesh(mesh);
                        if (!float.IsFinite(mesh.bounds.size.x) || !float.IsFinite(mesh.bounds.size.y) || !float.IsFinite(mesh.bounds.size.z) || mesh.bounds.size.sqrMagnitude > 100)
                            throw new Exception("Invalid articulated deformation in " + next);
                        Destroy(mesh);
                    }
                    var shot = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(reviewDirectory, "frame-" + (frame++).ToString("D5") + ".png"), shot.EncodeToPNG());
                    Destroy(shot);
                }
                if (phase != Phase.Finished) throw new Exception("Study case timed out: " + next);
                bool shouldMove = next == SceneCase.FriendlyCall || next == SceneCase.WaryFood || next == SceneCase.FamiliarFood || next == SceneCase.PlayBurst;
                if (moved != shouldMove || ate != (next == SceneCase.WaryFood || next == SceneCase.FamiliarFood) ||
                    sawPause != (next == SceneCase.WaryFood) || refused != (next == SceneCase.BusyCall || next == SceneCase.FullFood))
                    throw new Exception("Study branch failed: " + next + " moved=" + moved + " ate=" + ate + " paused=" + sawPause + " refused=" + refused);
                Debug.Log("CAT STUDY PASS " + next + " duration=" + totalAge.ToString("F2"));
                if (!original && (maximumAcceleration > 4f || maximumTurnRate > 125f))
                    throw new Exception("Motion discontinuity: acceleration=" + maximumAcceleration + ", turn=" + maximumTurnRate);
                if (!original && maxFeedDistance > .16f)
                    throw new Exception("Muzzle misses food: " + maxFeedDistance);
                timings.AppendLine("duration=" + totalAge.ToString("F2") + " max acceleration=" + maximumAcceleration.ToString("F2") + " max turn=" + maximumTurnRate.ToString("F2") + " muzzle distance=" + maxFeedDistance.ToString("F3"));
            }
            File.WriteAllText(Path.Combine(reviewDirectory, "timings.txt"), timings.ToString());
            File.WriteAllText(Path.Combine(reviewDirectory, "FRAMECOUNT.txt"), frame.ToString());
            File.WriteAllText(passPath, "Six rendered episodes passed; revised movement starts/stops gradually, attention uses the head before the body, and gait phase follows measured displacement. No jump or authored running animation; human naturalness acceptance remains open.\n");
            Debug.Log("CAT BEHAVIOR STUDY COMPLETE");
            Application.Quit();
        }
    }
}
