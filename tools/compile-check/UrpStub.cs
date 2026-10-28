// Compile-check stub: only members used by the project, signatures copied from URP/RP Core 17.3 source.
using System;
namespace UnityEngine.Rendering
{
    public class VolumeComponent : ScriptableObject { }
    public class VolumeParameter<T> { public void Override(T x) { } }
    public class MinFloatParameter : VolumeParameter<float> { public MinFloatParameter(float v, float min) { } }
    public class ClampedFloatParameter : VolumeParameter<float> { public ClampedFloatParameter(float v, float a, float b) { } }
    public class BoolParameter : VolumeParameter<bool> { public BoolParameter(bool v) { } }
    public sealed class VolumeProfile : ScriptableObject
    {
        public T Add<T>(bool overrides = false) where T : VolumeComponent => null;
        public bool TryGet<T>(out T component) where T : VolumeComponent { component = null; return false; }
    }
    public class Volume : MonoBehaviour { public bool isGlobal { get; set; } public VolumeProfile sharedProfile = null; }
}
namespace UnityEngine.Rendering.Universal
{
    public abstract class ScriptableRendererData : ScriptableObject { }
    public class PostProcessData : ScriptableObject { }
    public class UniversalRendererData : ScriptableRendererData { public PostProcessData postProcessData = null; }
    public class UniversalRenderPipelineAsset : RenderPipelineAsset
    {
        public static UniversalRenderPipelineAsset Create(ScriptableRendererData rendererData = null) => null;
        protected override RenderPipeline CreatePipeline() => null;
        public bool supportsHDR { get; set; }
        public int msaaSampleCount { get; set; }
        public float renderScale { get; set; }
        public float shadowDistance { get; set; }
        public bool useSRPBatcher { get; set; }
    }
    public enum AntialiasingMode { None, FastApproximateAntialiasing, SubpixelMorphologicalAntiAliasing, TemporalAntiAliasing }
    public class UniversalAdditionalCameraData : MonoBehaviour
    {
        public bool renderShadows { get; set; }
        public bool renderPostProcessing { get; set; }
        public AntialiasingMode antialiasing { get; set; }
        public bool dithering { get; set; }
    }
    public static class CameraExtensions { public static UniversalAdditionalCameraData GetUniversalAdditionalCameraData(this Camera camera) => null; }
    public sealed class Bloom : VolumeComponent
    {
        public MinFloatParameter threshold = new MinFloatParameter(0.9f, 0f);
        public MinFloatParameter intensity = new MinFloatParameter(0f, 0f);
        public ClampedFloatParameter scatter = new ClampedFloatParameter(0.7f, 0f, 1f);
        public BoolParameter highQualityFiltering = new BoolParameter(false);
    }
    public enum TonemappingMode { None, Neutral, ACES, Custom }
    public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode> { public TonemappingModeParameter(TonemappingMode v) { } }
    public sealed class Tonemapping : VolumeComponent { public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None); }
}
