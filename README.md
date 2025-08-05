Position System to External Program
====

*Position System to External Program* is a **shader** and a **program** that lets you connect the position of standard DPS-like lights
to a robotic arm.

Other users may be able to remotely control the position and rotation of your robotic arm through a shared virtual space.

# User documentation

If you are a user looking to use this software, please [check out the end-user documentation instead](https://alleyway.hai-vr.dev/docs/products/position-system-to-external-program).

- **[📘 Open documentation](https://alleyway.hai-vr.dev/docs/products/position-system-to-external-program)**

&nbsp;

&nbsp;

&nbsp;

&nbsp;

&nbsp;

-----

# Developer documentation

The information below is for developers looking to maintain the application. If you are a user, [check out the end-user documentation instead](https://alleyway.hai-vr.dev/docs/products/position-system-to-external-program).

### Project structure

Main application execution projects:
- **program**: The main entry point is `program/Program.cs`; this bootstraps all dependencies.
- **application-loop**: The main application loop is in `application-loop/Routine.cs`.
  - When the UI window closes, the UI window will ask the application loop to exit the loop.
- **ui-imgui**: The default UI is in `ui-imgui/UiMainApplication.cs`.
  - This runs in a separate UI thread.
  - The framerate of the application loop does not depend on the refresh rate of the UI.
  - All actions from the UI thread that affect the main application are enqueued through `ui-imgui/UiActions.cs` to run in the main thread.

Core projects:
- **core** contains data structures shared by many projects in this solution.
- **decoder** contains the logic necessary to decode images into usable data.

External system projects:
- **extractor-gdi**, **extractor-openvr**, and **tcode** interact with various external system APIs.

### Data extraction procedure

Data extraction goes through this:
- We get a reference to a native texture handle. That texture is very large.
- Using that native texture, we extract a subregion of that texture into a byte array.
- We sample some of these pixels, turning them into zeroes and ones.
- Using that array of bits, we decode the data.

![DataExtraction.png](DataExtraction.png)

### Data

Data is made out of sequential 32-bit groups; least significant bit first, little endian.

Data visually starts at the top-left of the region, scans horizontally up until the layout's width, then vertically.
By default, the layout uses 16 columns, with a margin of 1 on every side.

On window rendering, it is drawn at the top left. On VR rendering, it is drawn centered vertically, located against the left edge of the left eye.
The size of the squares in VR is a fixed proportion of the vertical resolution to counteract the *Resolution Per Eye* setting.

By default, the shader outputs:
- 50% red for its true value, and 0% green. It is 50% so that it does not trigger bloom on post-processing heavy scenarios.
- A negative pixel (-10000, -10000, -10000, 1) for its false value, which is perceived as black. The pixel is made of negative values so that bloom will not affect the black pixels.

On the program side, we check for a red value above 110 (43%). Optionally, the green value is removed from the red value, in an attempt to cancel out bloom.

### Shader version 1.0.0

Adding new lines at the end is considered to be a breaking change because the checksum would change, and it would be vertically taller, compromising the vertical centering.
This is why there is reserved space for future use. 

| Group  | Description                                                                                                                                                       |
|--------|-------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **0**  | Checksum. See [checksum](#checksum) section below.                                                                                                                |
| **1**  | Time since level load, as given by `_Time.y`. Intended to let the decoder know when the data has changed<br>(i.e. for interpolation, or to detect data stalling). |
| **2**  | A version identifier (x: 1366692562).                                                                                                                             |
| **3**  | A version identifier (y: 1000000). Translates as 1 000 000, because the version is 1.0.0                                                                          |
| **4**  | Position of the 0th light (x), as given by `unity_4LightPosX0[0]`, **in the MeshRenderer's local space**.                                                         |
| **5**  | Position of the 0th light (y), as given by `unity_4LightPosY0[0]`, **in the MeshRenderer's local space**.                                                         |
| **6**  | Position of the 0th light (z), as given by `unity_4LightPosZ0[0]`, **in the MeshRenderer's local space**.                                                         |
| **7**  | Position of the 1st light (x), as given by `unity_4LightPosX0[1]`, **in the MeshRenderer's local space**.                                                         |
| **8**  | Position of the 1st light (y), as given by `unity_4LightPosY0[1]`, **in the MeshRenderer's local space**.                                                         |
| **9**  | Position of the 1st light (z), as given by `unity_4LightPosZ0[1]`, **in the MeshRenderer's local space**.                                                         |
| **10** | Position of the 2nd light (x), as given by `unity_4LightPosX0[2]`, **in the MeshRenderer's local space**.                                                         |
| **11** | Position of the 2nd light (y), as given by `unity_4LightPosY0[2]`, **in the MeshRenderer's local space**.                                                         |
| **12** | Position of the 2nd light (z), as given by `unity_4LightPosZ0[2]`, **in the MeshRenderer's local space**.                                                         |
| **13** | Position of the 3rd light (x), as given by `unity_4LightPosX0[3]`, **in the MeshRenderer's local space**.                                                         |
| **14** | Position of the 3rd light (y), as given by `unity_4LightPosY0[3]`, **in the MeshRenderer's local space**.                                                         |
| **15** | Position of the 3rd light (z), as given by `unity_4LightPosZ0[3]`, **in the MeshRenderer's local space**.                                                         |
| **16** | Color of the 0th light (r), as given by `unity_LightColor[0].x`.                                                                                                  |
| **17** | Color of the 0th light (g), as given by `unity_LightColor[0].y`.                                                                                                  |
| **18** | Color of the 0th light (b), as given by `unity_LightColor[0].z`.                                                                                                  |
| **19** | Color of the 0th light (a), as given by `unity_LightColor[0].w`.                                                                                                  |
| **20** | Color of the 1st light (r), as given by `unity_LightColor[1].x`.                                                                                                  |
| **21** | Color of the 1st light (g), as given by `unity_LightColor[1].y`.                                                                                                  |
| **22** | Color of the 1st light (b), as given by `unity_LightColor[1].z`.                                                                                                  |
| **23** | Color of the 1st light (a), as given by `unity_LightColor[1].w`.                                                                                                  |
| **24** | Color of the 2nd light (r), as given by `unity_LightColor[2].x`.                                                                                                  |
| **25** | Color of the 2nd light (g), as given by `unity_LightColor[2].y`.                                                                                                  |
| **26** | Color of the 2nd light (b), as given by `unity_LightColor[2].z`.                                                                                                  |
| **27** | Color of the 2nd light (a), as given by `unity_LightColor[2].w`.                                                                                                  |
| **28** | *Supposed to be* the color of the 3rd light (r), as given by `unity_LightColor[3].x`. See notes below\*                                                           |
| **29** | *Supposed to be* the color of the 3rd light (g), as given by `unity_LightColor[3].y`. See notes below\*                                                           |
| **30** | *Supposed to be* the color of the 3rd light (b), as given by `unity_LightColor[3].z`. See notes below\*                                                           |
| **31** | *Supposed to be* the color of the 3rd light (a), as given by `unity_LightColor[3].w`. See notes below\*                                                           |
| **32** | Attenuation of the 0th light, as given by `unity_4LightAtten0[0]`.                                                                                                |
| **33** | Attenuation of the 1st light, as given by `unity_4LightAtten0[1]`.                                                                                                |
| **34** | Attenuation of the 2nd light, as given by `unity_4LightAtten0[2]`.                                                                                                |
| **35** | Attenuation of the 3rd light, as given by `unity_4LightAtten0[3]`.                                                                                                |
| **36** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD position in world space.                                |
| **37** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD position in world space.                                |
| **38** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD position in world space.                                |
| **39** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **40** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD rotation in world space.                                |
| **41** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD rotation in world space.                                |
| **42** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD rotation in world space.                                |
| **43** | Unused, reserved for future non-breaking use. This is part of the checksum.<br/>This will probably be HMD rotation in world space.                                |
| **44** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **45** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **46** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **47** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **48** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **49** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **50** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |
| **51** | Unused, reserved for future non-breaking use. This is part of the checksum.                                                                                       |

\* *The value of `unity_LightColor[3]` may be disrupted if the scene contains a directional light due to a Unity quirk, so this value may not be trusted to detect point lights.*

### Checksum

The data can easily get corrupted if a SteamVR overlay overlaps the data region, or in some cases, game UI, post-processing, some rare transparent objects,
or special shaders may interfere with the data region.

When this happens, we need to detect this happening and disregard any decoded data.

To do this, we calculate a CRC-32 hash in the shader that this program will check.

If the check fails, we reuse the last known valid data.

The CRC-32 hash is based on groups 1 to 51 (inclusive). The data in 36 to 51 (inclusive) are not currently used.
However, including them as part of the checksum ensures that it is not a breaking change to add a few additional pieces of
new data in the shader for future versions.

### Third-party acknowledgements

Third party acknowledgements can also be found in the HThirdParty/ subfolder:
- Open HThirdParty/thirdparty-lookup.json
- For the full license text of the third party dependencies, open HThirdParty/THIRDPARTY-LICENSES/ folder

Included in source code form and DLLs:
- ImGui.NET SampleProgram @ https://github.com/ImGuiNET/ImGui.NET/tree/master/src/ImGui.NET.SampleProgram ([MIT license](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE)) by Eric Mellino and ImGui.NET contributors

Dependencies included through NuGet:
- Dear ImGui @ https://github.com/ocornut/imgui ([MIT license](https://github.com/ocornut/imgui/blob/master/LICENSE.txt)) by Omar Cornut
- ImGui.NET @ https://github.com/ImGuiNET/ImGui.NET ([MIT license](https://github.com/ImGuiNET/ImGui.NET/blob/master/LICENSE)) by Eric Mellino and ImGui.NET contributors
- Veldrid @ https://github.com/veldrid/veldrid ([MIT license](https://github.com/veldrid/veldrid/blob/master/LICENSE)) by Eric Mellino and Veldrid contributors
- Vortice.Windows @ https://github.com/amerkoleci/Vortice.Windows ([MIT license](https://github.com/amerkoleci/Vortice.Windows/blob/main/LICENSE)) by Amer Koleci and Contributors
- (there may be other implicit packages)

Asset dependencies:
- ProggyClean font @ http://www.proggyfonts.net/ ([MIT License (According to https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#creditslicenses-for-fonts-included-in-repository)](https://github.com/ocornut/imgui/blob/master/docs/FONTS.md#creditslicenses-for-fonts-included-in-repository)) by Tristan Grimmer
