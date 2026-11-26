using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace TerritoryIO.View
{
    /// <summary>
    /// Screen sharpness settings that live outside the game itself:
    /// anti-aliasing (MSAA), render scale, native resolution on phones and frame rate.
    /// With URP these values live on the URP pipeline asset, so in the Editor the original
    /// values are put back when you stop Play (Restore), and your asset is not changed.
    /// </summary>
    public static class DisplaySetup
    {
        private static bool saved;
        private static int savedMsaa;
        private static float savedScale;

        public static void Apply(int msaa, float renderScale, bool nativeResolutionOnMobile, int targetFrameRate)
        {
            msaa = msaa >= 8 ? 8 : msaa >= 4 ? 4 : msaa >= 2 ? 2 : 1;

            // Built-in render pipeline uses QualitySettings; URP ignores it and uses its asset.
            QualitySettings.antiAliasing = msaa > 1 ? msaa : 0;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (!saved)
                {
                    savedMsaa = urp.msaaSampleCount;
                    savedScale = urp.renderScale;
                    saved = true;
                }
                urp.msaaSampleCount = msaa;
                urp.renderScale = Mathf.Clamp(renderScale, 0.5f, 2f);
            }

            if (nativeResolutionOnMobile && Application.isMobilePlatform)
            {
                int a = Display.main.systemWidth, b = Display.main.systemHeight;
                bool landscape = Screen.width >= Screen.height;
                int w = landscape ? Mathf.Max(a, b) : Mathf.Min(a, b);
                int h = landscape ? Mathf.Min(a, b) : Mathf.Max(a, b);
                if (w > 0 && h > 0 && (Screen.width < w || Screen.height < h))
                    Screen.SetResolution(w, h, FullScreenMode.FullScreenWindow);
            }

            Application.targetFrameRate = targetFrameRate;
        }

        /// <summary>Puts the URP asset back the way it was (called when the game object is destroyed).</summary>
        public static void Restore()
        {
            if (!saved) return;
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                urp.msaaSampleCount = savedMsaa;
                urp.renderScale = savedScale;
            }
            saved = false;
        }
    }
}
