using Godot;

namespace DeferredRenderer
{
    public partial class RendererSetup : WorldEnvironment
    {
        private GBufferBuilder _gBufferBuilder;
        private RayTracer _rayTracer;

        public override void _Ready()
        {
            SetupDeferredRenderer();
        }

        private void SetupDeferredRenderer()
        {
            // Create a new Compositor if one doesn't exist
            Compositor ??= new Compositor();

            // Create the effects
            _gBufferBuilder = new GBufferBuilder();
            _rayTracer = new RayTracer();

            // Link them together
            _rayTracer.SetGBufferBuilder(_gBufferBuilder);

            // Add effects to the compositor
            var compositorEffects = Compositor.CompositorEffects;

            // Resize the array to hold our effects
            compositorEffects.Resize(2);

            // Add the GBuffer builder first (index 0)
            compositorEffects[0] = _gBufferBuilder;

            // Add the ray tracer second (index 1)
            compositorEffects[1] = _rayTracer;

            GD.Print("Deferred renderer setup complete");
        }
    }
}
