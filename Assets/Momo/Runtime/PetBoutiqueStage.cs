using System;
using UnityEngine;
using Momo.Core;

namespace Momo
{
    public sealed class BoutiqueTarget : MonoBehaviour
    {
        public int index;
    }

    public sealed class PetBoutiqueStage : MonoBehaviour
    {
        public MomoApp app;
        public Camera boutiqueCamera;
        public Light boutiqueLight;

        public CatView[] cats = new CatView[9];
        Transform[] cubbies = new Transform[9];
        float[] timeOffsets = new float[9];

        public int SelectedIndex { get; private set; } = -1;
        public bool IsZoomed => SelectedIndex >= 0;

        Vector3 targetCamPos;
        Quaternion targetCamRot;
        float targetCamFov;

        Vector3? currentLookTarget;
        float lookTargetDecay;

        public const int StageLayer = 30;
        Bounds[] portraitBounds = new Bounds[9];
        bool pointerStartedOnStage;
        Vector2 pointerStart;
        const float BaseY = 45f;
        static readonly Vector3 OverviewCamPos = new Vector3(0, BaseY + 0.05f, -6.15f);
        const float OverviewFov = 48f;
        const float ZoomedFov = 32f;

        public void Build(MomoApp app)
        {
            this.app = app;
            transform.position = new Vector3(0, BaseY, 0);

            // Lighting
            var lightObj = new GameObject("Boutique Sunlight");
            lightObj.transform.SetParent(transform, false);
            boutiqueLight = lightObj.AddComponent<Light>();
            boutiqueLight.type = LightType.Directional;
            boutiqueLight.transform.rotation = Quaternion.Euler(42, -25, 0);
            boutiqueLight.color = new Color(1f, 0.96f, 0.90f);
            boutiqueLight.intensity = 0.72f;
            boutiqueLight.cullingMask = 1 << StageLayer;
            boutiqueLight.shadows = LightShadows.Soft;
            boutiqueLight.shadowStrength = 0.35f;

            // Camera
            var camObj = new GameObject("Boutique Camera");
            camObj.transform.SetParent(transform, false);
            boutiqueCamera = camObj.AddComponent<Camera>();
            boutiqueCamera.cullingMask = 1 << StageLayer;
            boutiqueCamera.clearFlags = CameraClearFlags.SolidColor;
            boutiqueCamera.backgroundColor = new Color(0.89f, 0.91f, 0.89f);
            boutiqueCamera.nearClipPlane = 0.1f;
            boutiqueCamera.farClipPlane = 40f;
            boutiqueCamera.fieldOfView = OverviewFov;
            boutiqueCamera.transform.position = OverviewCamPos;
            boutiqueCamera.transform.LookAt(new Vector3(0, BaseY, 0));

            targetCamPos = OverviewCamPos;
            targetCamRot = boutiqueCamera.transform.rotation;
            targetCamFov = OverviewFov;

            // Clear the full screen as well as the inset stage viewport; otherwise the
            // previous frame survives outside camera.rect during grid/detail changes.
            var background = new GameObject("Boutique backdrop camera").AddComponent<Camera>();
            background.transform.SetParent(transform, false);
            background.clearFlags = CameraClearFlags.SolidColor;
            background.backgroundColor = GameHudStyle.Paper;
            background.cullingMask = 0;
            background.depth = -10;

            // Warm plaster back wall
            Material wallMat = Art.Mat("Boutique wall", "#DCE5E0");
            Art.Cube("Back wall", transform, new Vector3(0, 0, 0.85f), new Vector3(12f, 15f, 0.3f), wallMat);

            // Materials for cubbies
            Material oak = Art.Mat("Boutique oak", "#C49C72", 0.18f);
            Material darkOak = Art.Mat("Boutique dark oak", "#947454", 0.22f);
            Material cushionMat = Art.Mat("Linen cushion", "#BED3C8", 0.08f);
            Material plateMat = Art.Mat("Nameplate wood", "#9C7652", 0.25f);

            // Grid parameters (3 cols x 3 rows)
            float[] colX = new float[] { -1.65f, 0f, 1.65f };
            float[] rowY = new float[] { 2.10f, 0.0f, -2.10f };

            for (int i = 0; i < 9; i++)
            {
                int c = i % 3;
                int r = i / 3;
                float x = colX[c];
                float y = rowY[r];

                Transform cubby = Art.Pivot("Cubby_" + i, transform, new Vector3(x, y, 0));
                cubbies[i] = cubby;
                timeOffsets[i] = i * 2.37f;

                // Frame geometry: top, bottom, left, right, back
                float width = 1.45f;
                float height = 1.55f;
                float depth = 0.95f;
                float thick = 0.07f;

                Art.Cube("Bottom", cubby, new Vector3(0, -height / 2 + thick / 2, 0), new Vector3(width, thick, depth), oak);
                Art.Cube("Top", cubby, new Vector3(0, height / 2 - thick / 2, 0), new Vector3(width, thick, depth), oak);
                Art.Cube("Left", cubby, new Vector3(-width / 2 + thick / 2, 0, 0), new Vector3(thick, height, depth), oak);
                Art.Cube("Right", cubby, new Vector3(width / 2 - thick / 2, 0, 0), new Vector3(thick, height, depth), oak);
                Art.Cube("Back", cubby, new Vector3(0, 0, depth / 2 - thick / 2), new Vector3(width - thick * 2, height - thick * 2, thick), darkOak);

                // Linen cushion
                Art.Cube("Cushion", cubby, new Vector3(0, -height / 2 + 0.13f, 0.05f), new Vector3(1.15f, 0.15f, 0.76f), cushionMat);

                // Nameplate below cubby
                Art.Cube("Nameplate", cubby, new Vector3(0, -height / 2 - 0.12f, -0.05f), new Vector3(0.95f, 0.16f, 0.04f), plateMat);

                // Clickable target collider covering the whole cubby
                var colObj = new GameObject("CubbyCollider");
                colObj.transform.SetParent(cubby, false);
                colObj.transform.localPosition = new Vector3(0, 0, -0.05f);
                var box = colObj.AddComponent<BoxCollider>();
                box.size = new Vector3(width, height + 0.25f, depth + 0.4f);
                var target = colObj.AddComponent<BoutiqueTarget>();
                target.index = i;

                // 3D Cat
                var catObj = new GameObject("BoutiqueCat_" + i);
                catObj.transform.SetParent(cubby, false);
                catObj.transform.localPosition = new Vector3(0, -height / 2 + 0.22f, 0.08f);
                catObj.transform.localRotation = Quaternion.Euler(0, 180, 0);
                catObj.transform.localScale = Vector3.one * .82f;

                var catView = catObj.AddComponent<CatView>();
                catView.Build(BreedArchetypes.Get(i));
                cats[i] = catView;
                var bounds = new Bounds(catObj.transform.position + Vector3.up * .6f, Vector3.zero);
                foreach (var renderer in catObj.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(renderer.bounds);
                // Reserve room for breathing, ear turns, petting and stretching.
                bounds.Expand(new Vector3(.28f, .32f, .20f));
                portraitBounds[i] = bounds;
            }

            foreach (var child in GetComponentsInChildren<Transform>()) child.gameObject.layer = StageLayer;
            if (app.room != null && app.room.sun != null) app.room.sun.cullingMask &= ~(1 << StageLayer);
            gameObject.SetActive(false);
        }

        public void Open(int initialIndex = -1)
        {
            gameObject.SetActive(true);
            boutiqueCamera.enabled = true;
            if (app.room != null && app.room.cameraView != null)
                app.room.cameraView.enabled = false;

            if (initialIndex >= 0 && ModelAvailability.CanSelectInBoutique(initialIndex))
                SelectCat(initialIndex);
            else
                ReturnToGrid();
            pointerStartedOnStage = false;
            SnapCamera();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            if (boutiqueCamera != null) boutiqueCamera.enabled = false;
            if (app.room != null && app.room.cameraView != null)
                app.room.cameraView.enabled = true;
        }

        public void SelectCat(int index)
        {
            if (index < 0 || index >= 9 || !ModelAvailability.CanSelectInBoutique(index)) return;
            SelectedIndex = index;

            for (int i = 0; i < cubbies.Length; i++) cubbies[i].gameObject.SetActive(i == index);
            SetFrameVisible(false);
            currentLookTarget = null;
            Reframe();
            // No flight through neighbouring cubbies when changing companions.
            SnapCamera();

            cats[index].React();
        }

        public void ReturnToGrid()
        {
            SelectedIndex = -1;
            for(int i=0;i<cubbies.Length;i++)cubbies[i].gameObject.SetActive(ModelAvailability.IsVisibleInBoutique(i));
            SetFrameVisible(true);
            currentLookTarget = null;
            Reframe();
            SnapCamera();
        }

        void SetFrameVisible(bool visible)
        {
            foreach (var cubby in cubbies)
                foreach (Transform part in cubby)
                    if (part.name == "Top" || part.name == "Left" || part.name == "Right" || part.name == "Back" || part.name == "Bottom" || part.name == "Nameplate")
                        part.gameObject.SetActive(visible);
        }

        public static float FitDistance(Bounds bounds, float aspect, float fov)
        {
            float tangent = Mathf.Tan(fov * .5f * Mathf.Deg2Rad);
            return Mathf.Max(bounds.extents.y / tangent, bounds.extents.x / (tangent * Mathf.Max(.1f, aspect))) + bounds.extents.z + .12f;
        }

        public void SetViewport(Rect viewport)
        {
            if (boutiqueCamera.rect == viewport) return;
            boutiqueCamera.rect = viewport;
            boutiqueCamera.aspect = Screen.width * viewport.width / Mathf.Max(1, Screen.height * viewport.height);
            Reframe();
            SnapCamera();
        }

        void Reframe()
        {
            Bounds bounds = IsZoomed ? portraitBounds[SelectedIndex] : new Bounds(new Vector3(0, BaseY - .03f, 0), new Vector3(5.25f, 6.55f, 1.3f));
            targetCamFov = IsZoomed ? ZoomedFov : OverviewFov;
            targetCamPos = bounds.center + Vector3.back * FitDistance(bounds, boutiqueCamera.aspect, targetCamFov);
            targetCamRot = Quaternion.identity;
        }

        void SnapCamera()
        {
            boutiqueCamera.transform.SetPositionAndRotation(targetCamPos, targetCamRot);
            boutiqueCamera.fieldOfView = targetCamFov;
        }

        public void PetSelected()
        {
            if (SelectedIndex < 0 || SelectedIndex >= 9) return;
            cats[SelectedIndex].React();
        }

        public void TeaseSelected()
        {
            if (SelectedIndex < 0 || SelectedIndex >= 9) return;
            currentLookTarget = boutiqueCamera.transform.position + boutiqueCamera.transform.right * .35f;
            lookTargetDecay = 3f;
            if (boutiqueCamera != null)
                cats[SelectedIndex].LookAt(boutiqueCamera.transform.position, 1f);
        }

        public void StretchSelected()
        {
            if (SelectedIndex < 0 || SelectedIndex >= 9) return;
            cats[SelectedIndex].TriggerStretch();
        }

        public void AdoptSelected(string customName)
        {
            if (SelectedIndex < 0 || SelectedIndex >= 9 || !ModelAvailability.CanSelectInBoutique(SelectedIndex)) return;
            int adoptedIndex = SelectedIndex;
            app.AdoptBreed(customName, adoptedIndex);
        }

        public Vector3 GetCubbyScreenPosition(int index)
        {
            if (index < 0 || index >= 9 || cubbies[index] == null || boutiqueCamera == null)
                return Vector3.zero;
            Vector3 nameplateWorld = cubbies[index].position + new Vector3(0, -0.96f, 0);
            return boutiqueCamera.WorldToScreenPoint(nameplateWorld);
        }

        public int NextSelectable(int current,int direction)
        {
            for(int offset=1;offset<=9;offset++)
            {
                int index=(current+direction*offset)%9;if(index<0)index+=9;
                if(ModelAvailability.CanSelectInBoutique(index))return index;
            }
            return current;
        }

        void Update()
        {
            if (!gameObject.activeInHierarchy || boutiqueCamera == null) return;
            float dt = Time.unscaledDeltaTime;

            // Smooth camera transition (Dolly Zoom)
            boutiqueCamera.transform.position = Vector3.Lerp(boutiqueCamera.transform.position, targetCamPos, 1 - Mathf.Exp(-dt * 7.5f));
            boutiqueCamera.transform.rotation = Quaternion.Slerp(boutiqueCamera.transform.rotation, targetCamRot, 1 - Mathf.Exp(-dt * 7.5f));
            boutiqueCamera.fieldOfView = Mathf.Lerp(boutiqueCamera.fieldOfView, targetCamFov, 1 - Mathf.Exp(-dt * 7.5f));

            if (lookTargetDecay > 0)
            {
                lookTargetDecay -= dt;
                if (lookTargetDecay <= 0) currentLookTarget = null;
            }

            // Animate all 9 cats independently
            for (int i = 0; i < 9; i++)
            {
                if (cats[i] == null || !cats[i].gameObject.activeInHierarchy) continue;
                bool isFocused = (i == SelectedIndex);
                Vector3? lookAt = isFocused ? currentLookTarget : null;
                cats[i].TickBoutique(Mathf.Min(dt, 0.1f), timeOffsets[i], isFocused, lookAt);
            }
        }

        // Pointer handler invoked by MomoApp
        public void HandlePointer(Vector2 screenPos, bool down, bool held, bool up, bool overUi)
        {
            if (!gameObject.activeInHierarchy || boutiqueCamera == null) return;

            if (down)
            {
                pointerStartedOnStage = !overUi && boutiqueCamera.pixelRect.Contains(screenPos);
                pointerStart = screenPos;
            }
            bool stageGesture = pointerStartedOnStage && !overUi && boutiqueCamera.pixelRect.Contains(screenPos);
            if (up) pointerStartedOnStage = false;
            if (!stageGesture) return;
            Ray ray = boutiqueCamera.ScreenPointToRay(screenPos);

            if (IsZoomed)
            {
                // In Zoomed mode: real-time Look-At tracking
                if ((down || held) && new Plane(Vector3.forward, cats[SelectedIndex].transform.position + Vector3.back * .7f).Raycast(ray, out float enter))
                {
                    currentLookTarget = ray.GetPoint(enter);
                    lookTargetDecay = 2.5f;
                }

                if (down && !overUi)
                {
                    if (cats[SelectedIndex].GetComponent<Collider>().Raycast(ray, out RaycastHit hit, 30f))
                    {
                        var target = hit.collider.GetComponentInParent<TouchTarget>();
                        if (target != null && target.kind == "cat")
                        {
                            PetSelected();
                        }
                    }
                }
            }
            else
            {
                // In Overview mode: click on cubby or cat to zoom in
                if (up && Vector2.Distance(pointerStart, screenPos) < Screen.dpi * .08f + 12f)
                {
                    if (Physics.Raycast(ray, out RaycastHit hit, 40f, 1 << StageLayer))
                    {
                        var boutiqueTarget = hit.collider.GetComponent<BoutiqueTarget>();
                        if (boutiqueTarget != null)
                        {
                            SelectCat(boutiqueTarget.index);
                        }
                        else
                        {
                            var catTarget = hit.collider.GetComponentInParent<CatView>();
                            if (catTarget != null)
                            {
                                for (int i = 0; i < 9; i++)
                                    if (cats[i] == catTarget) { SelectCat(i); break; }
                            }
                        }
                    }
                }
            }
        }
    }
}
