# OSCmooth
> Create smoothed out animations that mimics IK Sync on avatars for OSC controlled parameters for VRChat.

https://user-images.githubusercontent.com/74634856/183157866-37a20b11-20b5-4cfe-a356-e2acfea23a68.mp4

## What is OSCmooth?
> Easily fix your choppy face-tracking, stuttery custom expressions, or other OSC controlled VRChat parameters by running your existing avatar setup through this tool!

OSCmooth is a VRCSDK tool to help easily convert your existing parameter driven animation setups to be smoothed out over the network, with the idea that OSC in its current state does not have any sort of officially supported network interpolation. This for all intents and purposes is designed to be an in-animator solution for that issue.

This tool started development after information released that IK Sync currently does *not* have any increased network frequency, contrary to information available on the VRC Docs, so using IK Sync only interpolates parameters. This means that the same or similar smoothing or interpolation system could replicate IK Sync without relying to heavily on the existing networking framework provided by VRChat.

## Getting Started
1. Download `OSCmooth.unitypackage` from [Releases](https://github.com/regzo2/OSCmooth/releases).
2. Import `OSCmooth.unitypackage` into your Unity project.
3. Access the tool from the top toolbar under Tools/OSCmooth.
4. (Optionally) View the [OSCmooth Workflow](#oscmooth-tool-workflow) section for a quick-start

## Features
>OSCmooth provides a neat interface to easily apply smoothing to multiple parameters at a time.

<p align="center">
  <img src="https://user-images.githubusercontent.com/74634856/182416532-5fb318d9-8b47-4812-b23e-b46318889882.png" alt="animated" />
</p>

Settings:
* Avatar
   - This is where you place your VRChat avatar or VRCAvatarDescriptor from the dropdown or from dragging the root of the Avatar into this slot.
* Layer
   - This is the 'Playable Layer' controller that the tool will use. It must be filled out on your VRC Avatar Descriptor in order to be used in the tool.
* Config
  - This is where you can load preset parameter configurations, either saved by you or shared by others.
- Use Playable Layer Parameters
  - This will pull all editable parameters from the selected 'Playable Layer' to be configured in `Parameter Configuration`
- Default Parameter Values
  - These are the default settings that all new parameters in the `Parameter Configuration` will pull from.
- Parameter Configuration
  - Parameter
    - The name of the parameter to be smoothed out in the Playable Layer
  - Smoothness
    - Local Smoothness
      - How much smoothness is applied to your viewpoint for this parameter.
    - Remote Smoothness
      - How much smoothness is applied that other users will experience for this parameter.
    - Proxy Conversion
      - Convert existing animations to work with the output Proxy parameter generated from this tool.

    - Flip Input/Output
      - A mostly redundant setting that will set the input to be the Proxy parameter and output to be the base parameter. Useful if an app outputs a proxy parameter.  
    
## How It Works
OSCmooth is intended to make creating so-called 'feedback loop smoothing' blendtrees much more automated and easier to integrate into existing animation setups. These smoothing blendtrees will take your existing base parameters and output a smoothed out `...OSCm_Proxy` parameter that can then be used in animations. 

<p align="center">
  <img src="https://user-images.githubusercontent.com/74634856/182492788-5d1f5e0b-b7b5-4388-875d-6b79b93ac562.gif" alt="animated" />
</p>

The tool applies a smoothing blendtree to each parameter you run through the `Parameter Configuration` in the tool, and optionally can convert your existing animations to automatically work with the smoothing blendtree.

<p align="center">
  <img src="https://user-images.githubusercontent.com/74634856/182493153-26198953-d134-4e75-b3b7-3b9f9fd84792.gif" alt="animated" />
</p>

## OSCmooth Tool Workflow
A little showcase on how you would typically add OSCmooth to any parameter:

<p align="center">
  <img src="https://user-images.githubusercontent.com/74634856/182496213-268c12e3-cf28-4262-bd75-980c7b0d9098.gif" alt="animated" />
</p>

And the resultant animation setup:

<p align="center">
  <img src="https://user-images.githubusercontent.com/74634856/182496410-62e6b9eb-6bf8-42a9-a254-c02f03254c2d.gif" alt="animated" />
</p>

## Roadmap

- [ ] Potentially decouple animation layer from base Playable Layer, and merge on build for dynamically added, non-destructive implementation
  - [ ] More generalized automatic setup, automatic implementation
  - [ ] Allow third-party tools/animations/etc. to directly implement their own parameters into the smoothing layer without user intervention.
- [ ] Implement frame-time detection and smoothing correction.
  - [ ] Simplify parameter setup

## Credits
- [Haï~](https://github.com/hai-vr/) for the innovative blend tree smoothing technique
- [Razgriz](https://github.com/rrazgriz) for coming up with the idea of compacting the huge animation setup using Direct blendtrees
- [VRCFT Discord](https://discord.gg/Fh4FNehzKn) members and everyone who needed smooth face tracking for pushing me to develop this tool!
