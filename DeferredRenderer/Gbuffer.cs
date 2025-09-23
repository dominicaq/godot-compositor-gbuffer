// using Godot;
// using System;

// namespace DeferredRenderer
// {
// 	public partial class Gbuffer : Node
// 	{
// 		private static Gbuffer _instance;
// 		public static Gbuffer Instance
// 		{
// 			get
// 			{
// 				if (_instance == null)
// 				{
// 					GD.PrintErr("GBuffer singleton not initialized. Make sure it's added to autoload.");
// 				}
// 				return _instance;
// 			}
// 			private set => _instance = value;
// 		}

// 		[Export] public bool Enabled = true;
// 		[Export] public Vector2I BufferSize = new Vector2I(1920, 1080);

// 		[Signal] public delegate void GbufferReadyEventHandler();

// 		// GBuffer textures
// 		private Rid[] _textures = new Rid[4];
// 		private Rid _renderPass;
// 		private RenderingDevice _rd;
// 		private bool _initialized = false;
// 		private GbufferRenderer _renderer;

// 		public override void _EnterTree()
// 		{
// 			if (_instance != null && _instance != this)
// 			{
// 				GD.PrintErr("Multiple GBuffer instances detected. GBuffer should be a singleton.");
// 				QueueFree();
// 				return;
// 			}

// 			Instance = this;
// 			GD.Print("GBuffer singleton initialized");
// 		}

// 		public override void _Ready()
// 		{
// 			_rd = RenderingServer.CreateLocalRenderingDevice();
// 			if (_rd == null)
// 			{
// 				GD.PrintErr("Failed to create rendering device for GBuffer");
// 				return;
// 			}

// 			InitializeTextures();
// 			SetupCompositor();
// 		}

// 		private void InitializeTextures()
// 		{
// 			var textureFormat = new RdTextureFormat();
// 			textureFormat.Width = (uint)BufferSize.X;
// 			textureFormat.Height = (uint)BufferSize.Y;
// 			textureFormat.Depth = 1;
// 			textureFormat.ArrayLayers = 1;
// 			textureFormat.Mipmaps = 1;
// 			textureFormat.TextureType = RenderingDevice.TextureType.Type2D;
// 			textureFormat.Samples = RenderingDevice.TextureSamples.Samples1;
// 			textureFormat.UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
// 									  RenderingDevice.TextureUsageBits.ColorAttachmentBit;

// 			// Albedo + AO (RGBA8)
// 			textureFormat.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
// 			_textures[0] = _rd.TextureCreate(textureFormat, new RdTextureView());

// 			// Normal XY (RG16F) - Z reconstructed, octahedral encoding
// 			textureFormat.Format = RenderingDevice.DataFormat.R16G16Sfloat;
// 			_textures[1] = _rd.TextureCreate(textureFormat, new RdTextureView());

// 			// Material properties (RGBA8) - metallic, roughness, emission, flags
// 			textureFormat.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
// 			_textures[2] = _rd.TextureCreate(textureFormat, new RdTextureView());

// 			// Depth (D32F)
// 			textureFormat.Format = RenderingDevice.DataFormat.D32Sfloat;
// 			textureFormat.UsageBits = RenderingDevice.TextureUsageBits.SamplingBit |
// 									  RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit;
// 			_textures[3] = _rd.TextureCreate(textureFormat, new RdTextureView());

// 			CreateRenderPass();
// 			_initialized = true;

// 			GD.Print($"GBuffer textures initialized: {BufferSize}");
// 		}

// 		private void CreateRenderPass()
// 		{
// 			var attachments = new Godot.Collections.Array<RdAttachmentFormat>();

// 			// Color attachments (Albedo, Normal, Material)
// 			for (int i = 0; i < 3; i++)
// 			{
// 				var attachment = new RdAttachmentFormat();
// 				attachment.Format = GetTextureFormat(i);
// 				attachment.Samples = RenderingDevice.TextureSamples.Samples1;
// 				attachment.LoadOp = RenderingDevice.AttachmentLoadOp.Clear;
// 				attachment.StoreOp = RenderingDevice.AttachmentStoreOp.Store;
// 				attachment.StencilLoadOp = RenderingDevice.AttachmentLoadOp.DontCare;
// 				attachment.StencilStoreOp = RenderingDevice.AttachmentStoreOp.DontCare;
// 				attachment.InitialLayout = RenderingDevice.TextureLayout.Undefined;
// 				attachment.FinalLayout = RenderingDevice.TextureLayout.ColorAttachmentOptimal;
// 				attachments.Add(attachment);
// 			}

// 			// Depth attachment
// 			var depthAttachment = new RdAttachmentFormat();
// 			depthAttachment.Format = RenderingDevice.DataFormat.D32Sfloat;
// 			depthAttachment.Samples = RenderingDevice.TextureSamples.Samples1;
// 			depthAttachment.LoadOp = RenderingDevice.AttachmentLoadOp.Clear;
// 			depthAttachment.StoreOp = RenderingDevice.AttachmentStoreOp.Store;
// 			depthAttachment.StencilLoadOp = RenderingDevice.AttachmentLoadOp.DontCare;
// 			depthAttachment.StencilStoreOp = RenderingDevice.AttachmentStoreOp.DontCare;
// 			depthAttachment.InitialLayout = RenderingDevice.TextureLayout.Undefined;
// 			depthAttachment.FinalLayout = RenderingDevice.TextureLayout.DepthStencilAttachmentOptimal;
// 			attachments.Add(depthAttachment);

// 			_renderPass = _rd.RenderPassCreate(attachments, new Godot.Collections.Array<RdSubpassDependency>());
// 		}

// 		private RenderingDevice.DataFormat GetTextureFormat(int index)
// 		{
// 			return index switch
// 			{
// 				0 => RenderingDevice.DataFormat.R8G8B8A8Unorm,  // Albedo
// 				1 => RenderingDevice.DataFormat.R16G16Sfloat,    // Normal
// 				2 => RenderingDevice.DataFormat.R8G8B8A8Unorm,   // Material
// 				_ => RenderingDevice.DataFormat.R8G8B8A8Unorm
// 			};
// 		}

// 		private void SetupCompositor()
// 		{
// 			var compositor = GetViewport().GetCompositor();
// 			if (compositor == null)
// 			{
// 				compositor = new Compositor();
// 				GetViewport().SetCompositor(compositor);
// 			}

// 			_renderer = new GbufferRenderer();
// 			_renderer.SetGbufferTarget(this);
// 			compositor.AddCompositorEffect(_renderer, true);
// 		}

// 		// Called by the compositor when GBuffer is populated
// 		internal void OnGbufferPopulated()
// 		{
// 			EmitSignal(SignalName.GbufferReady);
// 		}

// 		// Public API for accessing textures
// 		public Rid GetAlbedoTexture() => _initialized ? _textures[0] : new Rid();
// 		public Rid GetNormalTexture() => _initialized ? _textures[1] : new Rid();
// 		public Rid GetMaterialTexture() => _initialized ? _textures[2] : new Rid();
// 		public Rid GetDepthTexture() => _initialized ? _textures[3] : new Rid();

// 		public Rid[] GetAllTextures() => _initialized ? (Rid[])_textures.Clone() : new Rid[4];
// 		public Rid GetRenderPass() => _renderPass;
// 		public RenderingDevice GetRenderingDevice() => _rd;

// 		public bool IsReady => _initialized;
// 		public Vector2I GetSize() => BufferSize;

// 		// Resize GBuffer (recreates textures)
// 		public void Resize(Vector2I newSize)
// 		{
// 			if (newSize == BufferSize) return;

// 			BufferSize = newSize;

// 			if (_initialized)
// 			{
// 				// Free old textures
// 				foreach (var texture in _textures)
// 				{
// 					if (texture.IsValid)
// 						_rd.FreeRid(texture);
// 				}

// 				if (_renderPass.IsValid)
// 					_rd.FreeRid(_renderPass);

// 				// Recreate with new size
// 				InitializeTextures();
// 				GD.Print($"GBuffer resized to: {BufferSize}");
// 			}
// 		}

// 		// Static convenience methods
// 		public static bool IsInitialized => _instance != null && _instance._initialized;

// 		public static Rid GetAlbedo() => Instance?.GetAlbedoTexture() ?? new Rid();
// 		public static Rid GetNormal() => Instance?.GetNormalTexture() ?? new Rid();
// 		public static Rid GetMaterial() => Instance?.GetMaterialTexture() ?? new Rid();
// 		public static Rid GetDepth() => Instance?.GetDepthTexture() ?? new Rid();

// 		public static void SetEnabled(bool enabled)
// 		{
// 			if (Instance != null)
// 				Instance.Enabled = enabled;
// 		}

// 		public static void ResizeBuffer(Vector2I size)
// 		{
// 			Instance?.Resize(size);
// 		}

// 		public override void _ExitTree()
// 		{
// 			if (_rd != null && _initialized)
// 			{
// 				foreach (var texture in _textures)
// 				{
// 					if (texture.IsValid)
// 						_rd.FreeRid(texture);
// 				}

// 				if (_renderPass.IsValid)
// 					_rd.FreeRid(_renderPass);
// 			}

// 			if (_instance == this)
// 			{
// 				_instance = null;
// 				GD.Print("GBuffer singleton destroyed");
// 			}
// 		}
// 	}

// 	[GlobalClass]
// 	internal partial class GbufferRenderer : CompositorEffect
// 	{
// 		private Gbuffer _gbufferTarget;

// 		public void SetGbufferTarget(Gbuffer gbuffer)
// 		{
// 			_gbufferTarget = gbuffer;
// 		}

// 		public override void _RenderCallback(int effectCallbackType, RenderData renderData)
// 		{
// 			if (_gbufferTarget == null || !_gbufferTarget.Enabled || !_gbufferTarget.IsReady)
// 				return;

// 			var rd = renderData.GetRenderSceneData().GetRenderingDevice();
// 			if (rd == null) return;

// 			if ((EffectCallbackTypeEnum)effectCallbackType == EffectCallbackTypeEnum.PreOpaquePass)
// 			{
// 				PopulateGbuffer(rd, renderData);
// 			}
// 		}

// 		private void PopulateGbuffer(RenderingDevice rd, RenderData renderData)
// 		{
// 			var textures = _gbufferTarget.GetAllTextures();
// 			var renderPass = _gbufferTarget.GetRenderPass();

// 			if (!renderPass.IsValid) return;

// 			// Create framebuffer for this frame
// 			var framebuffer = rd.FramebufferCreate(textures, renderPass);

// 			// Clear colors for each attachment
// 			var clearColors = new Godot.Collections.Array<Color>();
// 			clearColors.Add(Colors.Black);                        // Albedo (black)
// 			clearColors.Add(new Color(0.5f, 0.5f, 0.0f, 0.0f)); // Normal (encoded 0,0,1)
// 			clearColors.Add(Colors.Black);                        // Material (no metallic/rough)
// 			clearColors.Add(Colors.White);                        // Not used for depth

// 			// Begin GBuffer render pass
// 			var drawList = rd.DrawListBegin(framebuffer,
// 										   RenderingDevice.InitialAction.Clear,
// 										   RenderingDevice.FinalAction.Read,
// 										   RenderingDevice.InitialAction.Clear,
// 										   RenderingDevice.FinalAction.Read,
// 										   clearColors);

// 			// At this point, Godot will automatically render all visible geometry
// 			// using materials that have GBuffer-compatible shaders
// 			// The rendering system handles:
// 			// - Frustum culling
// 			// - Material sorting and batching
// 			// - LOD selection
// 			// - Instancing

// 			rd.DrawListEnd();
// 			rd.Submit();

// 			// Notify that GBuffer is ready for sampling
// 			_gbufferTarget.OnGbufferPopulated();

// 			rd.FreeRid(framebuffer);
// 		}
// 	}
// }
