// Parts of this code are loosely based on this talk by cnlohr titled:
//     "Game Made for VR on a $1 Processor?", cnlohr, published on 2023-02-23
//     https://youtu.be/VnZac6lA1_k?t=828
//
// Although no code from the following link was directly used, this file and its repository was referenced during study:
// https://github.com/cnlohr/swadge-vrchat-bridge/blob/db33f403d3dcfe81524320bbf736a78e9c1a169d/bridgeapp/bridgeapp.c
//
// This file contains snippets from cnlohr/shadertrixx: https://github.com/cnlohr/shadertrixx/blob/main/LICENSE
Shader "Hai/PositionSystemToExternalProgram"
{
    Properties
    {
    	_EncodedSquareSize("Encoded Square Size", Float) = 4.0
    	_IsTestScript("Force draw in test script", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="AlphaTest+105" }
        LOD 100

        Pass
        {
        	Tags {"LightMode"="ForwardBase"}
        	
        	ZTest Always // Try to draw even in the VR mask
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            float _EncodedSquareSize;
            float _IsTestScript;
            
            uniform float _VRChatMirrorMode;
            uniform float _VRChatCameraMode;
            
// -----------------------------------------------------------------------------------------------------------------
// [[ BEGIN THIRD PARTY SECTION -- LICENSE ONLY APPLIES TO THIS SECTION ]]
            // The following section is based on cnlohr/shadertrixx
            
/**
https://github.com/cnlohr/shadertrixx/blob/main/LICENSE
MIT License

Copyright (c) 2021 cnlohr, et. al.

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
**/
            // From https://github.com/cnlohr/shadertrixx?tab=readme-ov-file#the-most-important-trick
            #define glsl_mod(x,y) (((x)-(y)*floor((x)/(y))))

            // From https://github.com/cnlohr/shadertrixx?tab=readme-ov-file#detecting-if-you-are-on-desktop-vr-camera-etc
			bool isRightEye()
			{
				#if defined(USING_STEREO_MATRICES)
					return unity_StereoEyeIndex == 1;
				#else
					return false;
				#endif
			}

            bool isMirror()
			{
            	// https://github.com/cnlohr/shadertrixx/blob/main/README.md#are-you-in-a-mirror
				return unity_CameraProjection[2][0] != 0.f || unity_CameraProjection[2][1] != 0.f;
			}

// [[ END THIRD PARTY SECTION ]]
// -----------------------------------------------------------------------------------------------------------------

            static const uint VENDOR = 1366692562;
            static const uint VERSION = 1000000; // 1 000 000 is 1.0.0
            static const uint CANARY = 1431677610;
            
			static const int GROUP_32 = 32;
            
			static const int GROUP_Checksum = 0;
			static const int GROUP_Time = 1;
			static const int GROUP_VendorCheck = 2;
			static const int GROUP_VersionSemver = 3;
			static const int GROUP_LightPositionStart = 4;
			static const int GROUP_LightColorStart = 16;
			static const int GROUP_LightAttenuationStart = 32;
			static const int GROUP_HmdPositionStart = 36;
			static const int GROUP_HmdRotationStart = 40;
			static const int GROUP_Canary = 51;
			static const int GROUP_LENGTH = 52;
            
			static const int checksumDataSize = 1;
            static const int timeDataSize = 1;
            static const int vendorDataSize = 1;
            static const int versionDataSize = 1;
            static const int posDataSize = 4 * 3;
            static const int colorDataSize = 4 * 4;
            static const int attenDataSize = 4;
            static const int canaryDataSize = 1;
            
			static const float GrayLevel = 0.5;
            
			static const int SERIALIZE_NumberOfColumns = 16;
			static const int MARGIN = 1;
			
			static const uint CRC32_POLYNOMIAL = 0xEDB88320u;
            
            uint NthBit(uint uval, int bit) {
				return uval & (uint)(1 << bit);
			}

            uint getData(float groupY)
            {
				if (groupY < GROUP_Checksum + checksumDataSize)
				{
            		// This is meant to be part of the checksum, but we don't encode it here:
            		// As recursive functions not allowed, this branch should not be entered as it is handled by the caller.
					
            		return 0;
				}
				else if (groupY < GROUP_Time + timeDataSize)
				{
                	float data = _Time;
                	return asuint(data);
				}
				else if (groupY < GROUP_VendorCheck + vendorDataSize)
				{
					// note to self: Don't make the mistake again of casting this to float
                	return VENDOR;
				}
				else if (groupY < GROUP_VersionSemver + versionDataSize)
				{
					// note to self: Don't make the mistake again of casting this to float
                	return VERSION;
				}
                else if (groupY < GROUP_LightPositionStart + posDataSize)
                {
                	int lightN = (int)floor((groupY - GROUP_LightPositionStart) / 3);
                    float3 xyzPos = float3(unity_4LightPosX0[lightN], unity_4LightPosY0[lightN], unity_4LightPosZ0[lightN]);
                	xyzPos = mul(unity_WorldToObject, float4(xyzPos, 1));

                    float componentOfThisVector = glsl_mod(groupY - GROUP_LightPositionStart, 3);
                	float data = xyzPos[componentOfThisVector];
                	return asuint(data);
                }
            	else if (groupY < GROUP_LightColorStart + colorDataSize)
            	{
                	int lightN = (int)floor((groupY - GROUP_LightColorStart) / 4);
                    half4 rgba = unity_LightColor[lightN];

                    float componentOfThisVector = glsl_mod(groupY - GROUP_LightColorStart, 4);
                	float data = rgba[componentOfThisVector];
                	return asuint(data);
            	}
            	else if (groupY < GROUP_LightAttenuationStart + attenDataSize)
            	{
                	int lightN = (int)(groupY - GROUP_LightAttenuationStart);
            		
                	float data = unity_4LightAtten0[lightN];
                	return asuint(data);
            	}
            	// Notice the more or equal, we don't want to write over the reserved values.
				else if (groupY >= GROUP_Canary && groupY < GROUP_Canary + canaryDataSize)
				{
					// note to self: Don't make the mistake again of casting this to float
                	return CANARY;
				}
            	else
            	{
                    return 0;
            	}
            }
			
			uint CRC32UpdateByte(uint crc, uint byte_val)
			{
				uint temp = crc ^ byte_val;
				for (int i = 0; i < 8; i++)
				{
				    if (temp & 1)
				        temp = (temp >> 1) ^ CRC32_POLYNOMIAL;
				    else
				        temp = temp >> 1;
				}
				return temp;
			}
            
			uint CRC32UpdateUint(uint crc, uint value)
			{
				crc = CRC32UpdateByte(crc, value & 0xFF);
				crc = CRC32UpdateByte(crc, (value >> 8) & 0xFF);
				crc = CRC32UpdateByte(crc, (value >> 16) & 0xFF);
				crc = CRC32UpdateByte(crc, (value >> 24) & 0xFF);
				return crc;
			}
            
            v2f vert (appdata v)
            {
                v2f o;
            	
            	// The mesh has 4 vertices, all with a different vertex color.
                if (v.color.r > 0) { o.uv = float2(0, 0); }
                else if (v.color.g > 0) { o.uv = float2(1, 0); }
                else if (v.color.b > 0) { o.uv = float2(1, 1); }
                else { o.uv = float2(0, 1); }

#if defined(USING_STEREO_MATRICES)
            	float yShift = 0.5;
                float relativeY2 = _ScreenParams.y / 1000;
#else
            	float yShift = 0.0;
                float relativeY2 = 2;
#endif
            	
            	float makeBigger = _IsTestScript > 0.5 ? 10 : 1;
            	float serialize_numberOfLines = ceil((GROUP_LENGTH * 32.0) / SERIALIZE_NumberOfColumns);
                float relativeX = (makeBigger * (SERIALIZE_NumberOfColumns + MARGIN * 2) * _EncodedSquareSize / _ScreenParams.x) * relativeY2;
                float relativeY = (makeBigger * (serialize_numberOfLines + MARGIN * 2) * _EncodedSquareSize / _ScreenParams.y) * relativeY2;
            	
				// - Makes it full screen
            	// - Make the geometry as close to the camera as possible.
            	o.vertex = float4(o.uv.x * relativeX, o.uv.y * relativeY, UNITY_NEAR_CLIP_VALUE, 1);

            	o.vertex += float4(-1, (yShift - 0.5) * 2, 0, 0);
            	o.vertex.y -= relativeY * yShift;
				
                o.uv = o.uv
            		* float2(SERIALIZE_NumberOfColumns + MARGIN * 2, serialize_numberOfLines + MARGIN * 2)
            		- float2(MARGIN, MARGIN);
                
                o.color = v.color;
                return o;
            }
            
            fixed4 frag(v2f i) : SV_Target
            {
				#if defined(USING_STEREO_MATRICES)
				// ^^ IsVR?
            		if (isRightEye())
            		{
            			clip(-1);
            			return half4(0, 0, 0, 0);
            		}
            	
				#else
				// ^^ IsNotVR?
            		if (
            			_VRChatCameraMode != 1 // Is VR handheld camera
            			&& _ScreenParams.x == _ScreenParams.y // Wild guess: The cameras we want to render to are almost never square
            			)
            		{
            			clip(-1);
            			return half4(0, 0, 0, 0);
            		}
				#endif
            	
            	if (isMirror())
	            {
		            clip(-1);
            		return half4(0, 0, 0, 0);
	            }
            	
            	float serialize_numberOfLines = ceil((GROUP_LENGTH * 32.0) / SERIALIZE_NumberOfColumns);
            	
            	// We need black margins to avoid the main texture contaminating the neighbouring pixels too.
            	// Also, pixels shouldn't be pure white, because it will cause bloom to contaminate the neighbouring pixels.

            	// In addition, all brightness comparisons in the decoder should expect values that vary from expected, as there
            	// is still a possibility that transparency or other shader effects will write over our pixels
            	// (example: it happens in some of Lura's worlds such as the flying airship with the orange lamps).
            	
            	if (i.uv.x < 0 || i.uv.x >= SERIALIZE_NumberOfColumns)
            	{
            		// The -10000 prevents bloom. Negative values consume bloom, so it will always be a black pixel.
            		return half4(-10000, -10000, -10000, 1);
            	}
            	else if (i.uv.y < 0 || i.uv.y >= serialize_numberOfLines)
            	{
            		return half4(-10000, -10000, -10000, 1);
            	}
            	
                float2 serialize = floor(i.uv);
	            int k = (int)floor(serialize.y * SERIALIZE_NumberOfColumns + serialize.x);
                float2 group = floor(float2(glsl_mod(k, GROUP_32), k / GROUP_32));

				if (group.y < GROUP_Checksum + checksumDataSize)
				{
    				uint crc = 0xFFFFFFFFu;
            		for (int w = GROUP_Time; w < GROUP_LENGTH; w++)
            		{
            			crc = CRC32UpdateUint(crc, getData(w));
            		}
            		crc = crc ^ 0xFFFFFFFFu;
					
					uint result = NthBit(crc, group.x);
					if (result) return half4(GrayLevel, 0, 0, 1);
					else return half4(-10000, -10000, -10000, 1);
				}
            	else
            	{
            		uint data = getData(group.y);
					
					uint result = NthBit(data, group.x);
					if (result) return half4(GrayLevel, 0, 0, 1);
					else return half4(-10000, -10000, -10000, 1);
            	}
            }
            
            ENDCG
        } // Pass

		Pass
		{
		    Name "ShadowCaster"
		    Tags {"LightMode"="ShadowCaster"}
		    
		    CGPROGRAM
		    #pragma vertex vert_empty
		    #pragma fragment frag_empty
		    
		    float4 vert_empty() : SV_POSITION { return 0; }
		    fixed4 frag_empty() : SV_Target { clip(-1); return 0; }
		    ENDCG
		}

    } // SubShader
} // Shader
