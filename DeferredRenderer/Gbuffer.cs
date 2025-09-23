using Godot;
using Godot.Collections;
using System;

namespace DeferredRenderer
{
    public static class GBuffer
    {
        // Properties
        public const int DEFAULT_SCREEN_WIDTH = 1920;
        public const int DEFAULT_SCREEN_HEIGHT = 1080;
        [Export] public static Vector2I BufferSize { get; set; } = new Vector2I(
            DEFAULT_SCREEN_WIDTH,
            DEFAULT_SCREEN_HEIGHT
        );

        private static RenderingDevice _rd;
        private static readonly Rid[] _textures = new Rid[4];
        private static bool _initialized = false;

        //----------------------------------------------------------------------
        // G-Buffer Accessors
        //----------------------------------------------------------------------
        public static Vector2I GetSize() => BufferSize;

        public static Rid GetAlbedoTexture()
        {
            AssertInitialized();
            return _textures[0];
        }

        public static Rid GetNormalTexture()
        {
            AssertInitialized();
            return _textures[1];
        }

        public static Rid GetMaterialTexture()
        {
            AssertInitialized();
            return _textures[2];
        }

        public static Rid GetDepthTexture()
        {
            AssertInitialized();
            return _textures[3];
        }

        public static Rid[] GetAllTextures()
        {
            AssertInitialized();
            return (Rid[])_textures.Clone();
        }

        //----------------------------------------------------------------------
        // Life Cycle
        //----------------------------------------------------------------------
        public static void Initialize()
        {
            if (_initialized) return;

            _rd = RenderingServer.GetRenderingDevice();
            if (_rd == null)
            {
                GD.PrintErr("Failed to create rendering device for GBuffer");
                return;
            }

            InitTextures();
            _initialized = true;
        }

        public static void Destroy()
        {
            AssertInitialized();

            for (int i = 0; i < _textures.Length; i++)
            {
                if (_textures[i].IsValid)
                {
                    _rd?.FreeRid(_textures[i]);
                    _textures[i] = new Rid();
                }
            }

            // Clean up rendering device
            _rd.Free();
            _rd = null;
            _initialized = false;
        }

        //----------------------------------------------------------------------
        // Texture Setup
        //----------------------------------------------------------------------
        private static void InitTextures()
        {
            // Create base texture properties
            var texFormat = new RDTextureFormat
            {
                Width = (uint)BufferSize.X,
                Height = (uint)BufferSize.Y,
                Mipmaps = 1,
                Samples = RenderingDevice.TextureSamples.Samples1,
                UsageBits = (
                    RenderingDevice.TextureUsageBits.ColorAttachmentBit |
                    RenderingDevice.TextureUsageBits.SamplingBit)
            };
            var texView = new RDTextureView();
            var emptyData = new Array<byte[]>();

            // GBuffer 0: Albedo + Alpha
            texFormat.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
            _textures[0] = _rd.TextureCreate(texFormat, texView, emptyData);

            // GBuffer 1: Normals (higher precision)
            texFormat.Format = RenderingDevice.DataFormat.R16G16B16A16Sfloat;
            _textures[1] = _rd.TextureCreate(texFormat, texView, emptyData);

            // GBuffer 2: Material params (roughness/metallic/ao)
            texFormat.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
            _textures[2] = _rd.TextureCreate(texFormat, texView, emptyData);

            // GBuffer 3: Depth as sampled texture
            texFormat.Format = RenderingDevice.DataFormat.D32Sfloat;
            texFormat.UsageBits = (
                RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
                RenderingDevice.TextureUsageBits.SamplingBit);
            _textures[3] = _rd.TextureCreate(texFormat, texView, emptyData);
        }

        //----------------------------------------------------------------------
        // Utility
        //----------------------------------------------------------------------
        public static void Resize(Vector2I newSize)
        {
            AssertInitialized();
            if (newSize == BufferSize) return;

            BufferSize = newSize;

            // Free old textures
            foreach (var texture in _textures)
            {
                if (texture.IsValid)
                    _rd.FreeRid(texture);
            }

            // Recreate with new size
            InitTextures();
        }

        public static RenderingDevice.DataFormat GetTextureFormat(int index)
        {
            AssertInitialized();
            return index switch
            {
                0 => RenderingDevice.DataFormat.R8G8B8A8Unorm,  // Albedo
                1 => RenderingDevice.DataFormat.R16G16Sfloat,   // Normal
                2 => RenderingDevice.DataFormat.R8G8B8A8Unorm,  // Material
                _ => RenderingDevice.DataFormat.R8G8B8A8Unorm
            };
        }

        private static void AssertInitialized()
        {
            if (_initialized)
            {
                return;
            }

            throw new InvalidOperationException("GBuffer not initialized. Call GBuffer.Initialize() first.");
        }
    }
}
