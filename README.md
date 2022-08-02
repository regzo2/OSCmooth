# OSCmooth
>Create smooth parameters that mimick IK Sync for OSC or general use.

## Getting Started
1. Download `OSCmooth.unitypackage` release from [Releases](https://github.com/regzo2/OSCmooth/releases).
2. Import `OSCmooth.unitypackage` into your Unity project.
3. Access the tool from the top toolbar under Tools/OSCmooth.

## Features
>OSCmooth provides a neat interface to easily apply smoothing to multiple parameters at a time.

![image](https://user-images.githubusercontent.com/74634856/182416532-5fb318d9-8b47-4812-b23e-b46318889882.png)

Settings:
* Avatar
   - This is where you place your VRChat avatar or VRCAvatarDescriptor from the dropdown or from dragging the root of the Avatar into this slot.
* Config
  - This is where you can load preset parameter configurations, either saved by you or shared by others.
- Use Playable Layer Parameters
  - This will pull all editable parameters to be configured in `Parameter Configuration`
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
    
## OSCmooth Demo

1. Place an avatar in the Avatar slot by dragging from the scene or selecting from the dropdown.

![OSCmooth loading avatar](https://user-images.githubusercontent.com/74634856/182417503-bad95b46-0472-4c22-bb84-5351727375dc.gif)
