# Godot 4.5 Deferred Renderer Example

A custom deferred rendering solution that provides an example of scene texture access using the forward+ renderer.
This repo has two examples, `GBufferBuilder.cs` demonstrates just grabbing the scene render textures. `ExamplePass.cs` demonstrates
deferred lighting with these textures using a compute shader.

## Why?

I'm aware of the bandwidth and performance implications of deferred rendering, but for my specific use case I needed direct access to these textures. As of Godot 4.5 there is no officially supported deferred renderer. However, godot gives access to these textures with `CompositorEffects`, making this possible.

## What It Does

Extracts scene render textures from Godot's forward+ renderer:
- Color buffer
- Normal/Roughness buffer
- Depth buffer

## Setup

1. Add scripts to your Godot 4.5 project
2. Create a Compositor resource
3. Add desired compositor effect
4. Use textures in your compute shaders

## Shader Bindings (glsl)
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
