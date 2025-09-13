namespace AvaloniaMpv;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;

public class MpvPlayer : IDisposable
{
    private nint _mpvContext = nint.Zero;
    private bool _disposed = false;
    internal nint _mpvRenderContext = nint.Zero;
    private MpvWakeupCallback? _wakeupCallback;
    private MpvOpenglGetProcAddressCallback? _procAddressCallback;
    private MpvRenderUpdateFn? _renderUpdateCallback;
    internal GlInterface? _glInterface = null;
    internal bool _redraw = false;
    private readonly Dictionary<string, (object, MpvFormat)> _mpvPropertyChangeEvents = new();

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    internal void Initialise()
    {
        if (_glInterface is null) { throw new Exception("OpenGL interface was null. Did you bind MpvPlayer to a MediaControl?"); }
        var mpv = LibMpv.mpv_create();
        _mpvContext = mpv;
        if (mpv == nint.Zero)
        {
            throw new Exception("Failed to create mpv context");
        }
        LibMpv.mpv_set_option_string(mpv, "vo", "libmpv");
        if (LibMpv.mpv_initialize(mpv) < 0)
        {
            Console.WriteLine("MPV failed to init");
        }
        LibMpv.mpv_request_log_messages(mpv, "debug");
        _procAddressCallback = GetProcAddress;
        var initParams = new MpvOpenglInitParams
        {
            get_proc_address = _procAddressCallback,
            get_proc_address_ctx = nint.Zero,
        };
        nint initParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvOpenglInitParams>());
        Marshal.StructureToPtr(initParams, initParamsPtr, false);
        var paramApiType = Marshal.StringToHGlobalAnsi("opengl");
        var enableAdvancedControl = 1;
        var enableAdvancedControlPtr = Marshal.AllocHGlobal(Marshal.SizeOf<int>());
        Marshal.WriteInt32(enableAdvancedControlPtr, enableAdvancedControl);
        MpvRenderParam[] renderParams = {
            new(){
                type = mpv_render_param_type.MPV_RENDER_PARAM_API_TYPE, data = paramApiType,
            },
            new() {
                type = mpv_render_param_type.MPV_RENDER_PARAM_OPENGL_INIT_PARAMS , data = initParamsPtr
            },
            new() {
                type = mpv_render_param_type.MPV_RENDER_PARAM_ADVANCED_CONTROL ,
                 data = enableAdvancedControlPtr
            },
            new()
        };
        nint mpv_gl;
        GCHandle handle = GCHandle.Alloc(renderParams, GCHandleType.Pinned);
        nint pParams = handle.AddrOfPinnedObject();
        int status = LibMpv.mpv_render_context_create(
            out mpv_gl,
            mpv,
            pParams
        );
        _mpvRenderContext = mpv_gl;
        handle.Free();
        _wakeupCallback = MpvEvent;
        _renderUpdateCallback = MpvRenderUpdate;
        LibMpv.mpv_set_wakeup_callback(mpv, _wakeupCallback, nint.Zero);
        LibMpv.mpv_render_context_set_update_callback(mpv_gl, _renderUpdateCallback, nint.Zero);
        //monitor some useful playback propertied
        LibMpv.mpv_observe_property(mpv, 0, "time-pos", MpvFormat.MPV_FORMAT_DOUBLE);
        Marshal.FreeHGlobal(paramApiType);
        Marshal.DestroyStructure<MpvOpenglInitParams>(initParamsPtr);
        Marshal.FreeHGlobal(enableAdvancedControlPtr);
    }
    private void MpvEvent(nint data)
    {
        while (true)
        {
            nint evPtr = LibMpv.mpv_wait_event(_mpvContext, 0.0);
            if (evPtr == nint.Zero) break;
            MpvEvent ev = Marshal.PtrToStructure<MpvEvent>(evPtr);
            if (ev.event_id == MpvEventId.MPV_EVENT_NONE) break;
            if (ev.event_id == MpvEventId.MPV_EVENT_PROPERTY_CHANGE)
            {
                var prop = Marshal.PtrToStructure<MpvEventProperty>(ev.data);
                var name = Marshal.PtrToStringAnsi(prop.name);
                if (name is null) continue;
                if (_mpvPropertyChangeEvents.TryGetValue(name, out (object ev, MpvFormat format) _data))
                {
                    switch (_data.format)
                    {
                        case MpvFormat.MPV_FORMAT_DOUBLE:
                            {
                                if (_data.ev is EventSource<double> eventSource)
                                {
                                    var value = Marshal.PtrToStructure<double>(prop.data);
                                    eventSource.Raise(this, value);
                                }
                                break;
                            }
                        case MpvFormat.MPV_FORMAT_FLAG:
                            {
                                if (_data.ev is EventSource<int> eventSource)
                                {
                                    var value = Marshal.PtrToStructure<int>(prop.data);
                                    eventSource.Raise(this, value);
                                }
                                break;
                            }
                    }
                }
                if (name == "time-pos" && prop.format == MpvFormat.MPV_FORMAT_DOUBLE)
                {
                    var time_pos = Marshal.PtrToStructure<double>(prop.data);
                }
            }
        }
    }
    private void MpvRenderUpdate(nint data)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var flags = LibMpv.mpv_render_context_update(_mpvRenderContext);
            if ((flags & (1 << 0)) != 0)
            {
                _redraw = true;
            }
        });
    }
    //marshal a command to mpv
    public void MpvCommand(string[] command)
    {
        Dispatcher.UIThread.Post(() =>
        {
            nint[] argPtrs = new nint[command.Length + 1];
            for (int i = 0; i < command.Length; i++)
            {
                argPtrs[i] = Marshal.StringToHGlobalAnsi(command[i]);
            }
            argPtrs[command.Length] = nint.Zero;
            nint argsPtr = Marshal.AllocHGlobal(nint.Size * argPtrs.Length);
            Marshal.Copy(argPtrs, 0, argsPtr, argPtrs.Length);
            int result = LibMpv.mpv_command(_mpvContext, argsPtr);
            for (int i = 0; i < command.Length; i++)
            {
                if (argPtrs[i] != nint.Zero)
                    Marshal.FreeHGlobal(argPtrs[i]);
            }
            Marshal.FreeHGlobal(argsPtr);
        });
    }
    public void RegisterEvent<T>(string property, MpvFormat format)
    {
        if (format != MpvFormat.MPV_FORMAT_DOUBLE && format != MpvFormat.MPV_FORMAT_FLAG && format != MpvFormat.MPV_FORMAT_INT64) throw new System.NotImplementedException("This format has not implemented");
        if (_mpvPropertyChangeEvents.ContainsKey(property)) throw new InvalidOperationException("Event with this property name is already registered.");
        LibMpv.mpv_observe_property(_mpvContext, 0, property, format);
        _mpvPropertyChangeEvents[property] = (new EventSource<T>(), format);
    }

    public EventSource<T> GetEvent<T>(string property)
    {
        if (_mpvPropertyChangeEvents.TryGetValue(property, out var evt) && evt is (EventSource<T> ev, MpvFormat format) data)
        {
            return ev;
        }
        else throw new Exception("Event with this property name is not registered");
    }

    public T? MpvGetProperty<T>(string property, MpvFormat format)
    {
        if (typeof(T) == typeof(string) && format == MpvFormat.MPV_FORMAT_STRING)
        {
            return (T?)(object?)MpvGetStringProperty(property);
        }

        var resultPtr = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        var ret = LibMpv.mpv_get_property(_mpvContext, property, format, resultPtr);
        if (ret < 0)
        {
            Marshal.FreeHGlobal(resultPtr);
            return default(T);
        }
        var result = Marshal.PtrToStructure<T>(resultPtr);
        Marshal.FreeHGlobal(resultPtr);
        return result;
    }

    private string? MpvGetStringProperty(string property)
    {
        var resultPtr = Marshal.AllocHGlobal(nint.Size);
        var ret = LibMpv.mpv_get_property(_mpvContext, property, MpvFormat.MPV_FORMAT_STRING, resultPtr);
        if (ret < 0)
        {
            Marshal.FreeHGlobal(resultPtr);
            return null;
        }

        var stringPtr = Marshal.ReadIntPtr(resultPtr);
        var result = Marshal.PtrToStringUTF8(stringPtr);
        LibMpv.mpv_free(stringPtr);
        Marshal.FreeHGlobal(resultPtr);
        return result;
    }
    private nint GetProcAddress(nint fn_ctx, [MarshalAs(UnmanagedType.LPStr)] string name)
    {
        //this should not be null
        return _glInterface!.GetProcAddress(name);
    }

    public void StartPlayback(string source)
    {
        string[] command = { "loadfile", source };
        MpvCommand(command);
    }

    public void TogglePlayPause()
    {
        string[] command = { "cycle", "pause" };
        MpvCommand(command);
    }

    public void SeekTo(double ms)
    {
        string[] command = { "seek", $"{ms}", "absolute" };
        MpvCommand(command);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                _glInterface = null;
                _redraw = false;
            }
            LibMpv.mpv_render_context_free(_mpvRenderContext);
            _mpvRenderContext = nint.Zero;
            LibMpv.mpv_destroy(_mpvContext);
            _mpvContext = nint.Zero;
            _disposed = true;
        }
    }
}

public class MediaControl : OpenGlControlBase
{
    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (MpvPlayer._redraw)
        {
            var size = this.Bounds;
            var w = (int)size.Width;
            var h = (int)size.Height;
            var flip_y = GCHandle.Alloc(1, GCHandleType.Pinned);
            MpvOpenGLFramebuffer framebuffer = new()
            {
                fbo = fb,
                width = w,
                height = h,
            };
            var framebufferPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvOpenGLFramebuffer>());
            Marshal.StructureToPtr(framebuffer, framebufferPtr, false);
            MpvRenderParam[] param = {
              new() {
                type = mpv_render_param_type.MPV_RENDER_PARAM_OPENGL_FBO,
                data = framebufferPtr,
              },
                new() {
                    type = mpv_render_param_type.MPV_RENDER_PARAM_FLIP_Y,
                    data = flip_y.AddrOfPinnedObject(),
                },
                new()
            };
            var handle = GCHandle.Alloc(param, GCHandleType.Pinned);
            var paramsP = handle.AddrOfPinnedObject();
            LibMpv.mpv_render_context_render(MpvPlayer._mpvRenderContext, paramsP);
            flip_y.Free();
            handle.Free();
            Marshal.FreeHGlobal(framebufferPtr);
            MpvPlayer._redraw = false;
        }
        RequestNextFrameRendering();
    }
    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        MpvPlayer._glInterface = gl;
        MpvPlayer.Initialise();
    }
    public static readonly StyledProperty<MpvPlayer> MpvPlayerProperty = AvaloniaProperty.Register<MediaControl, MpvPlayer>(nameof(MpvPlayer));
    public MpvPlayer MpvPlayer
    {
        get => GetValue(MpvPlayerProperty);
        set
        {
            SetValue(MpvPlayerProperty, value);
        }
    }
}
public class EventSource<T>
{
    public event EventHandler<EventArgs<T>>? Raised;
    internal void Raise(object sender, T value)
          => Raised?.Invoke(sender, new EventArgs<T>(value));

}

public class EventArgs<T> : EventArgs
{
    public T Value { get; }
    public EventArgs(T value) { Value = value; }
}
