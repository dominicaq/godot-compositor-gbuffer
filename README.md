# Godot 4.5 Deferred Renderer

A custom deferred rendering solution that provides G-Buffer texture access.

## ⚠️ Important

**This bypasses Godot's lighting pipeline.** You'll need to implement your own lighting in shaders.

## What It Does

Extracts G-Buffer textures from Godot's forward renderer:
- Color buffer
- Normal/Roughness buffer
- Depth buffer

## Setup

1. Add scripts to your Godot 4.5 project
2. Create a Compositor resource
3. Add the GBufferBuilder effect
4. Use textures in your compute shaders

## Shader Bindings
```glsl
layout(set = 0, binding = 0, rgba8) uniform image2D color_image;
layout(set = 0, binding = 1) uniform sampler2D normal_roughness_texture;
layout(set = 0, binding = 2) uniform sampler2D depth_texture;
```

# Requirements
- Godot 4.5+
- Forward+ renderer enabled
- Compute shader support
- C# enabled
