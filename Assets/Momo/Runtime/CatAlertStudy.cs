using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace Momo
{
    // Focused study: attention, short approach, interruption, turn and withdrawal.
    // Times are animation direction for comparison, not measured cat latencies.
    public sealed class CatAlertStudy : MonoBehaviour
    {
        ImportedCatView cat;
        Transform root, stimulus;
        Camera cameraView;
        float age, headingVelocity, measuredSpeed;
        Vector3 attention, noise = new Vector3(.4f, .45f, -1.4f);
        Vector3 withdrawFrom;
        bool reviewing, interrupted, injectedNoise, callOnly;
        string caption;
        float contactError;
        int completedSteps;
        bool finished;

        void Start()
        {
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = 60;
            cameraView = new GameObject("Alert study camera").AddComponent<Camera>();
            cameraView.backgroundColor = new Color(.88f, .84f, .77f);
            cameraView.clearFlags = CameraClearFlags.SolidColor;
            cameraView.fieldOfView = 34;
            cameraView.transform.position = new Vector3(.6f, 1.4f, -5.4f);
            cameraView.transform.LookAt(new Vector3(-.15f, .5f, .1f));
            var light = new GameObject("Soft daylight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f; light.shadows = LightShadows.Soft;
            light.shadowStrength = .4f; light.shadowBias = .015f;
            light.transform.rotation = Quaternion.Euler(45, -30, 0);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.65f, .67f, .69f);
            Make("Floor", PrimitiveType.Cube, new Vector3(0, -.08f, 0), new Vector3(7, .16f, 4), new Color(.68f, .56f, .43f));
            Make("Mat", PrimitiveType.Cube, new Vector3(0, .006f, 0), new Vector3(5.8f, .012f, 2.6f), new Color(.87f, .78f, .66f));
            stimulus = Make("Food scent", PrimitiveType.Cylinder, new Vector3(1.3f, .035f, .18f), new Vector3(.32f, .028f, .32f), new Color(.47f, .66f, .67f)).transform;
            root = new GameObject("Alert study tabby").transform;
            cat = root.gameObject.AddComponent<ImportedCatView>();
            if (!cat.Build()) throw new Exception("Missing imported tabby.");
            reviewing = Debug.isDebugBuild && Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-behavior-review") >= 0;
            Restart(true);
            if (reviewing) StartCoroutine(Record());
        }

        static GameObject Make(string name, PrimitiveType type, Vector3 p, Vector3 s, Color c)
        {
            var obj = GameObject.CreatePrimitive(type); obj.name = name; obj.transform.position = p; obj.transform.localScale = s;
            var mat = new Material(Shader.Find("Standard")); mat.color = c; mat.SetFloat("_Glossiness", .08f);
            obj.GetComponent<Renderer>().sharedMaterial = mat;
            return obj;
        }

        void Restart(bool interruption)
        {
            age = 0; headingVelocity = 0; interrupted = interruption; injectedNoise = callOnly = finished = false;
            contactError = 0; completedSteps = 0;
            root.position = new Vector3(-1.6f, 0, .18f);
            root.rotation = Quaternion.Euler(0, 60, 0);
            cat.ResetStudy();
            attention = stimulus.position + Vector3.up * .40f;
            withdrawFrom = new Vector3(.42f, 0, .18f);
        }

        void Update()
        {
            if (cat == null) return;
            if (!reviewing)
            {
                if (Input.GetKeyDown(KeyCode.R) || Input.GetKeyDown(KeyCode.F)) Restart(true);
                if (Input.GetKeyDown(KeyCode.C)) Restart(false);
                if (Input.GetKeyDown(KeyCode.Space))
                { withdrawFrom = root.position; injectedNoise = true; callOnly = false; age = 4.2f; }
                if (Input.GetMouseButtonDown(0))
                {
                    var ray = cameraView.ScreenPointToRay(Input.mousePosition);
                    if (new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float enter))
                    { attention = ray.GetPoint(enter) + Vector3.up * .4f; callOnly = true; age = 0; }
                }
            }
            float dt = Mathf.Min(Time.deltaTime, .1f);
            age += dt;
            Vector3 before = root.position;
            float caution = .35f, startle = 0;
            if (callOnly)
            {
                caption = "HEARD YOU  /  looks first; no commanded destination";
                caution = .25f;
            }
            else if (age < .65f)
            {
                caption = "NOTICE  /  head and ears locate the scent";
                caution = .3f;
                if (age > .2f) Turn(90, dt);
            }
            else if (age < 2.05f)
            {
                caption = "APPROACH  /  short, light steps";
                Travel(new Vector3(-1.6f, 0, .18f), new Vector3(-.62f, 0, .18f), age - .65f, 1.4f, dt);
                caution = .45f;
            }
            else if (age < 2.7f)
            {
                caption = "CHECK  /  pause and look to the side";
                attention = age < 2.40f ? new Vector3(.35f, .5f, -.65f) : stimulus.position + Vector3.up * .35f;
                caution = .6f;
            }
            else if (age < 4.2f)
            {
                caption = "TRY AGAIN  /  another short approach";
                Travel(new Vector3(-.62f, 0, .18f), new Vector3(.42f, 0, .18f), age - 2.7f, 1.5f, dt);
                attention = stimulus.position + Vector3.up * .28f; caution = .4f;
            }
            else if ((interrupted || injectedNoise) && age < 5.0f)
            {
                caption = "NEW SOUND  /  stop, locate, prepare to move";
                attention = noise; startle = age < 4.48f ? 1 : .6f; caution = .75f;
                if (age > 4.35f) Turn(-90, dt);
            }
            else if ((interrupted || injectedNoise) && age < 6.0f)
            {
                caption = "WITHDRAW  /  turns away and makes space";
                Travel(withdrawFrom, withdrawFrom + Vector3.left * .69f, age - 5.0f, 1, dt);
                attention = noise; startle = .3f; caution = .65f;
            }
            else
            {
                caption = interrupted || injectedNoise ? "WATCH  /  keeps distance and reassesses" : "WATCH  /  close enough to investigate";
                attention = interrupted || injectedNoise ? noise : stimulus.position + Vector3.up * .3f;
                caution = Mathf.Lerp(.55f, .25f, Mathf.Clamp01((age - 6.0f) / 1.2f));
                if ((interrupted || injectedNoise) && age > 6.25f)
                    Turn(Mathf.Atan2(noise.x - root.position.x, noise.z - root.position.z) * Mathf.Rad2Deg, dt);
                finished = age >= (interrupted || injectedNoise ? 8.0f : 6.2f);
            }
            measuredSpeed = Vector3.Distance(before, root.position) / Mathf.Max(.001f, dt);
            cat.TickAlertStudy(dt, measuredSpeed, caution, startle, attention);
        }

        void Turn(float heading, float dt)
        {
            float yaw = Mathf.SmoothDampAngle(root.eulerAngles.y, heading, ref headingVelocity, .095f, 300, dt);
            root.rotation = Quaternion.Euler(0, yaw, 0);
        }

        void Travel(Vector3 from, Vector3 to, float t, float duration, float dt)
        {
            const float ramp = .20f;
            float peak = Vector3.Distance(from, to) / (duration - ramp);
            float covered;
            t = Mathf.Clamp(t, 0, duration);
            if (t < ramp) covered = peak * (t / 2 - ramp * Mathf.Sin(Mathf.PI * t / ramp) / (2 * Mathf.PI));
            else if (t < duration - ramp) covered = peak * (t - ramp / 2);
            else
            {
                float tail = t - duration + ramp;
                covered = peak * (duration - 1.5f * ramp + tail / 2 + ramp * Mathf.Sin(Mathf.PI * tail / ramp) / (2 * Mathf.PI));
            }
            Vector3 direction = (to - from).normalized;
            Turn(Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg, dt);
            root.position = from + direction * covered;
        }

        void OnGUI()
        {
            GUI.color = new Color(.2f, .19f, .17f);
            GUI.skin.label.fontSize = 21;
            GUI.Label(new Rect(24, 18, Screen.width - 48, 34), "CAT MOTION V3  |  curious and alert");
            GUI.skin.label.fontSize = 17;
            GUI.Label(new Rect(24, 54, Screen.width - 48, 34), caption);
            if (!reviewing)
                GUI.Label(new Rect(24, Screen.height - 48, Screen.width - 48, 34), "R / F: replay     C: calm approach     Space: interrupt     Click: attention only");
        }

        IEnumerator Record()
        {
            Application.runInBackground = true;
            Screen.SetResolution(1000, 720, FullScreenMode.Windowed);
            Time.captureFramerate = 30;
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, "../../../../artifacts/cat-behavior-study/" +
                (Array.IndexOf(Environment.GetCommandLineArgs(), "--cat-alert-bones") >= 0 ? "v3-debug" : "v3")));
            Directory.CreateDirectory(dir);
            string pass = Path.Combine(dir, "PASS.txt");
            if (File.Exists(pass)) File.Delete(pass);
            yield return new WaitForSecondsRealtime(.4f);
            int frame = 0;
            var report = new System.Text.StringBuilder();
            foreach (bool interruption in new[] { false, true })
            {
                Restart(interruption);
                report.AppendLine((interruption ? "Interrupted" : "Calm") + " starts at frame " + frame);
                while (!finished && age < 10)
                {
                    yield return new WaitForEndOfFrame();
                    contactError = Mathf.Max(contactError, cat.AlertContactError);
                    completedSteps = cat.AlertSteps;
                    if (frame % 30 == 0)
                    {
                        var mesh = new Mesh(); cat.Skin.BakeMesh(mesh);
                        if (!float.IsFinite(mesh.bounds.size.x) || mesh.bounds.size.sqrMagnitude > 100) throw new Exception("Invalid study deformation.");
                        Destroy(mesh);
                    }
                    var shot = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, "frame-" + (frame++).ToString("D5") + ".png"), shot.EncodeToPNG());
                    Destroy(shot);
                }
                if (!finished || completedSteps < 8 || contactError > .035f) throw new Exception("Incomplete or unreachable alert episode: steps=" + completedSteps + " contact error=" + contactError);
                report.AppendLine("duration=" + age.ToString("F2") + " steps=" + completedSteps + " max contact error=" + contactError.ToString("F3"));
            }
            // Contact error is reported, not mislabelled as a naturalness score.
            File.WriteAllText(Path.Combine(dir, "timings.txt"), report.ToString());
            File.WriteAllText(Path.Combine(dir, "FRAMECOUNT.txt"), frame.ToString());
            File.WriteAllText(pass, "Two focused episodes rendered; lateral gait, world contact planning, alert attention and withdrawal. Locally authored procedural prototype; subjective naturalness and mature animation asset replacement remain open.\n");
            Debug.Log("CAT ALERT STUDY COMPLETE " + report);
            Application.Quit();
        }
    }
}
