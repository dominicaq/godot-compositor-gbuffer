// using Godot;
// using Godot.Collections;
// using System;

// namespace DeferredRenderer
// {
//     public class GBuffer
//     {
//         private readonly Array<Rid> _textures = [.. new Rid[4]];
//         private readonly Array<RDAttachmentFormat> _attachments = [.. new RDAttachmentFormat[4]];
//         private Vector2 _bufferSize;
//         private RenderingDevice _rd;

//         private bool _initialized = false;

//         //----------------------------------------------------------------------
//         // G-Buffer Accessors
//         //----------------------------------------------------------------------
//         public Vector2 GetSize() => _bufferSize;

//         public Rid GetAlbedoTexture()
//         {
//             AssertInitialized();
//             return _textures[0];
//         }

//         public Rid GetNormalTexture()
//         {
//             AssertInitialized();
//             return _textures[1];
//         }

//         public Rid GetMaterialTexture()
//         {
//             AssertInitialized();
//             return _textures[2];
//         }

//         public Rid GetDepthTexture()
//         {
//             AssertInitialized();
//             return _textures[3];
//         }

//         public Array<Rid> GetAllTextures()
//         {
//             AssertInitialized();
//             return _textures;
//         }

//         public Array<RDAttachmentFormat> GetAttachmentFormats()
//         {
//             AssertInitialized();
//             return _attachments;
//         }

//         //----------------------------------------------------------------------
//         // Life Cycle
//         //----------------------------------------------------------------------
//         public void Initialize(RenderingDevice rd, Vector2 size)
//         {
//             if (_initialized) return;

//             _bufferSize = size;
//             _rd = rd;
//             if (_rd == null)
//             {
//                 GD.PrintErr("Failed to create rendering device for GBuffer");
//                 return;
//             }

//             InitTextures();
//             _initialized = true;
//         }

//         public void Destroy()
//         {
//             AssertInitialized();

//             for (int i = 0; i < _textures.Count; i++)
//             {
//                 if (_textures[i].IsValid)
//                 {
//                     _rd?.FreeRid(_textures[i]);
//                     _textures[i] = new Rid();
//                 }
//             }

//             _rd = null;
//             _initialized = false;
//         }

//         //----------------------------------------------------------------------
//         // Texture Setup
//         //----------------------------------------------------------------------
//         private void InitTextures()
//         {
//             // Create base texture properties
//             var texFormat = new RDTextureFormat
//             {
//                 Width = (uint)_bufferSize.X,
//                 Height = (uint)_bufferSize.Y,
//                 Mipmaps = 1,
//                 Samples = RenderingDevice.TextureSamples.Samples1,
//                 UsageBits = (
//                     RenderingDevice.TextureUsageBits.ColorAttachmentBit |
//                     RenderingDevice.TextureUsageBits.SamplingBit)
//             };
//             var texView = new RDTextureView();
//             var emptyData = new Array<byte[]>();

//             // GBuffer 0: Albedo + Alpha
//             texFormat.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
//             _textures[0] = _rd.TextureCreate(texFormat, texView, emptyData);

//             // GBuffer 1: Normals (higher precision)
//             texFormat.Format = RenderingDevice.DataFormat.R16G16B16A16Sfloat;
//             _textures[1] = _rd.TextureCreate(texFormat, texView, emptyData);

//             // GBuffer 2: Material params (roughness/metallic/ao)
//             texFormat.Format = RenderingDevice.DataFormat.R8G8B8A8Unorm;
//             _textures[2] = _rd.TextureCreate(texFormat, texView, emptyData);

//             // GBuffer 3: Depth
//             texFormat.Format = RenderingDevice.DataFormat.D32Sfloat;
//             texFormat.UsageBits = (
//                 RenderingDevice.TextureUsageBits.DepthStencilAttachmentBit |
//                 RenderingDevice.TextureUsageBits.SamplingBit);
//             _textures[3] = _rd.TextureCreate(texFormat, texView, emptyData);

//             CreateAttachmentFormats();
//         }

//         private void CreateAttachmentFormats()
//         {
//             for (int i = 0; i < _textures.Count; i++)
//             {
//                 var format = _rd.TextureGetFormat(_textures[i]);
//                 var attachment = new RDAttachmentFormat
//                 {
//                     Format = format.Format,
//                     Samples = format.Samples,
//                     UsageFlags = (uint)format.UsageBits,
//                 };
//                 _attachments.Add(attachment);
//             }
//         }

//         //----------------------------------------------------------------------
//         // Utility
//         //----------------------------------------------------------------------
//         public void Resize(Vector2I newSize)
//         {
//             AssertInitialized();
//             if (newSize == _bufferSize) return;

//             _bufferSize = newSize;

//             // Free old textures
//             foreach (var texture in _textures)
//             {
//                 if (texture.IsValid)
//                     _rd.FreeRid(texture);
//             }

//             // Recreate with new size
//             InitTextures();
//         }

//         public RenderingDevice.DataFormat GetTextureFormat(int index)
//         {
//             AssertInitialized();
//             return index switch
//             {
//                 0 => RenderingDevice.DataFormat.R8G8B8A8Unorm,      // Albedo
//                 1 => RenderingDevice.DataFormat.R16G16B16A16Sfloat, // Normal
//                 2 => RenderingDevice.DataFormat.R8G8B8A8Unorm,      // Material
//                 3 => RenderingDevice.DataFormat.D32Sfloat,          // Depth
//                 _ => RenderingDevice.DataFormat.R8G8B8A8Unorm
//             };
//         }

//         private void AssertInitialized()
//         {
//             if (!_initialized)
//                 throw new InvalidOperationException("GBuffer not initialized. Call Initialize() first.");
//         }
//     }
// }
