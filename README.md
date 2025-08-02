base-serial-sample
====

I'm sharing a program with some friends.

They can audit the program's contents here.

### Data extraction prodedure

Data extraction goes through this:
- We get a reference to a native texture. That texture is very large.
- Using that native texture, we extract an oversized byte array from that texture. This texture may be oversized because some APIs
  may only output or deal with square textures.
- Using that oversized byte array, we further extract another byte array from the specific region that contains our data.
- We sample some of these pixels, turning them into zeroes and ones.
- Using that array of bits, we decode the data.

![DataExtraction.png](DataExtraction.png)

### Third-party acknowlegements

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
