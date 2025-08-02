using System.Runtime.InteropServices;
using Hai.PositionSystemToExternalProgram.Core;
using Hai.PositionSystemToExternalProgram.Extractor.OVR;
using SharpGen.Runtime;
using Valve.VR;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Mathematics;
using static Vortice.Direct3D11.D3D11;

namespace Hai.PositionSystemToExternalProgram.Extractors.OVR;

public class OpenVrExtractor
{
    private const bool ReuseMirrorTexture = true;
    
    private readonly OpenVrStarter _ovrStarter;
    private readonly EyeResource _left = new();
    private readonly EyeResource _right = new();
    
    private ID3D11Device _device;
    private ID3D11DeviceContext _context;
    private bool _isOpenVrReady;

    private bool _isDeviceInitialized;
    
    private ID3D11Texture2D _texture2DID;
    private int _texture2DID_W;
    private int _texture2DID_H;
    private int _extractionIteration;
    private byte[] _monochromaticData;
    private byte[] _monochromaticDataB;
    private byte[] _marshalData;
    private byte[] _marshalDataB;

    public class EyeResource
    {
        public IntPtr pResourceView;
        public ID3D11ShaderResourceView tResourceView;
        public ID3D11ShaderResourceView resourceView;
        public ID3D11Texture2D resource;
        public Texture2DDescription desc2d;

        public void Release()
        {
            if (pResourceView == 0) return;
            
            tResourceView.Release();
            resourceView.Release();
            resource.Release();
            pResourceView = 0;
        }
    }

    public OpenVrExtractor(OpenVrStarter ovrStarter)
    {
        _ovrStarter = ovrStarter;
        _ovrStarter.OnStarted += () =>
        {
            _isOpenVrReady = true;
        };
        _ovrStarter.OnExited += () =>
        {
            // TODO: ReleaseMirrorTextureD3D11???
            
            _isOpenVrReady = false;
            _left.Release();
            _right.Release();
        };
        // TODO: ReleaseMirrorTextureD3D11 when we exit this app?
    }

    private bool TryInitializeDevice()
    {
        if (_isDeviceInitialized) return true;
        
        Result hr = D3D11CreateDevice(
            null,
            DriverType.Hardware,
            DeviceCreationFlags.None,
            new []{ FeatureLevel.Level_11_0 },
            // https://github.com/amerkoleci/Vortice.Windows/blob/eded19beac7d0b80373b10253414fa5a233b5c43/src/samples/HelloDirect3D11/D3D11GraphicsDevice.cs#L105C1-L107C80
            out var tempDevice,
            out var tempContext
        );
        if (!hr.Success) return false;
        
        // If we don't do this, _device and _context may spontaneously become null
        _device = tempDevice.QueryInterface<ID3D11Device1>();
        _context = tempContext.QueryInterface<ID3D11DeviceContext1>();
        _isDeviceInitialized = true;
        
        return true;
    }

    private bool TryInitializeOpenVrResources()
    {
        if (!_isDeviceInitialized) return false;
        if (!_isOpenVrReady) return false;
        
        var leftResolved = ResolveEyeResources(_left, EVREye.Eye_Left);
        var rightResolved = ResolveEyeResources(_right, EVREye.Eye_Right);
        
        return leftResolved && rightResolved;
    }

    private bool ResolveEyeResources(EyeResource eyeResource, EVREye vrEye)
    {
        var doesNotNeedToBeResolvedAgain = eyeResource.pResourceView != 0 && ReuseMirrorTexture;
        if (doesNotNeedToBeResolvedAgain) return true;
        
        var error = OpenVR.Compositor.GetMirrorTextureD3D11(vrEye, _device.NativePointer, ref eyeResource.pResourceView);
        if (error != EVRCompositorError.None) return false;
        
        eyeResource.tResourceView = new ID3D11ShaderResourceView(eyeResource.pResourceView);
        eyeResource.resourceView = eyeResource.tResourceView.QueryInterface<ID3D11ShaderResourceView>();
        
        eyeResource.resource = eyeResource.resourceView.Resource.QueryInterface<ID3D11Texture2D>();
        eyeResource.desc2d = eyeResource.resource.Description;
        
        return true;
    }
    
    public ExtractionResult Extract(ExtractionSource source, int x, int y, int w, int h)
    {
        var isInitialized = TryInitializeDevice() && TryInitializeOpenVrResources();
        if (!isInitialized)
        {
            return new ExtractionResult
            {
                Success = false
            };
        }
        
        var back = 1; // The back value must be 1. If it's 0, it will only output a black texture.
        var box = new Box(x, y, 0, x + w, y + h, back);
        
        var eyeResource = source == ExtractionSource.RightEye ? _right : _left;
        
        var captureWidth = box.Width;
        var captureHeight = box.Height;
        if (_texture2DID == null || _texture2DID_W != captureWidth || _texture2DID_H != captureHeight)
        {
            _texture2DID?.Release();

            _texture2DID_W = captureWidth;
            _texture2DID_H = captureHeight;
            _texture2DID = _device.CreateTexture2D(
                width: captureWidth,
                height: captureHeight,
                format: eyeResource.desc2d.Format,
                usage: ResourceUsage.Staging,
                cpuAccessFlags: CpuAccessFlags.Read,
                bindFlags: BindFlags.None,
                miscFlags: ResourceOptionFlags.Shared,
                mipLevels: 1
            );
        }
        
        _context.CopySubresourceRegion(_texture2DID, 0, 0, 0, 0, eyeResource.resource, 0, box);
        
        var result = _context.Map(_texture2DID, 0, MapMode.Read, MapFlags.None, out MappedSubresource mappedResource);
        if (result.Success)
        {
            var desc2dHeight = captureHeight;
            var desc2dWidth = captureWidth;
            var marshalDataSize = desc2dHeight * desc2dWidth * 4;
            var monochromaticDataSize = desc2dHeight * desc2dWidth;
            if (_monochromaticData == null || _monochromaticData.Length != monochromaticDataSize)
            {
                _monochromaticData = new byte[monochromaticDataSize];
                _monochromaticDataB = new byte[monochromaticDataSize];
                _marshalData = new byte[marshalDataSize];
                _marshalDataB = new byte[marshalDataSize];
            }

            // We write in B, because A was returned in the last iteration, and the UI thread might be currently reading it.
            // This should give enough time for the UI thread to finish reading the data in A;
            // it's unlikely we're doing two iterations while the UI thread is still reading it.
            Marshal.Copy(mappedResource.DataPointer, _marshalDataB, 0, _marshalDataB.Length);
            for (var iMarshal = 0; iMarshal < _marshalData.Length; iMarshal += 4)
            {
                var iMonochromatic = iMarshal / 4;
                _monochromaticDataB[iMonochromatic] = _marshalData[iMarshal + 1]; // This is the green channel
                
                var isPureBlackPixel = _marshalDataB[iMarshal] == 0 && _marshalDataB[iMarshal + 1] == 0 && _marshalDataB[iMarshal + 2] == 0;
                if (isPureBlackPixel)
                {
                    _marshalDataB[iMarshal] = 255;
                }
                _marshalDataB[iMarshal + 3] = 255;
            }
            (_monochromaticData, _monochromaticDataB) = (_monochromaticDataB, _monochromaticData);
            (_marshalData, _marshalDataB) = (_marshalDataB, _marshalData);
            
            _context.Unmap(_texture2DID, 0);

            _extractionIteration++;
        }
        
        return new ExtractionResult
        {
            Success = true,
            MonochromaticData = _monochromaticData,
            ColorData = _marshalData,
            Width = captureWidth,
            Height = captureHeight,
            Iteration = _extractionIteration
        };
    }
}