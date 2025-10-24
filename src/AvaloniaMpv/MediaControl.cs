namespace AvaloniaMpv;

using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using static LibMpv;
using System.IO; // added for log file
using System.Globalization; // ensure mpv numbers use '.' decimal

public class MpvPlayer : IDisposable
{
    private nint _mpvContext = nint.Zero;
    private bool _disposed = false;
    private MpvWakeupCallback? _wakeupCallback;
    private MpvOpenglGetProcAddressCallback? _procAddressCallback;
    private MpvRenderUpdateFn? _renderUpdateCallback;
    private AutoResetEvent _eventSignal = new(false);
    private Task? _backgroundWorkerTask = null;
    private CancellationTokenSource _backgroundWorkerCancellationTokenSource = new();
    internal GlInterface? _glInterface = null;
    internal MediaControl? _mediaControl = null;
    internal nint _mpvRenderContext = nint.Zero;
    internal ConcurrentQueue<CustomEventType> _eventQueue = new();
    private readonly Dictionary<string, (object, MpvFormat)> _mpvPropertyChangeEvents = new();
    private readonly string _mpvLogPath; // path to mpv log file

    public string MpvLogPath => _mpvLogPath;

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    public MpvPlayer()
    {
        // Prepare mpv log file path under user-local data dir
        var logDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Mpv.Avalonia");
        try { Directory.CreateDirectory(logDir); } catch { }
        _mpvLogPath = Path.Combine(logDir, "mpv.log");
        try { if (File.Exists(_mpvLogPath)) File.Delete(_mpvLogPath); } catch { }

        var mpv = mpv_create();
        _mpvContext = mpv;
        if (mpv.isNullPtr())
        {
            throw new Exception("Failed to create mpv context");
        }
        // Use libmpv render API for embedding
        mpv_set_option_string(mpv, "vo", "libmpv");
        // Write mpv logs to file with maximum verbosity before initialize
        mpv_set_option_string(mpv, "log-file", _mpvLogPath);
        mpv_set_option_string(mpv, "msg-level", "all=trace");

        if (mpv_initialize(mpv) < 0)
        {
            Console.WriteLine("MPV failed to init");
        }
        Console.WriteLine($"mpv logs -> {_mpvLogPath}");
        mpv_request_log_messages(mpv, "trace");
    }
    internal void Initialise()
    {
        if (_glInterface is null) 
        { 
            throw new Exception("OpenGL interface was null. Did you bind MpvPlayer to a MediaControl?"); 
        }
        _procAddressCallback = GetProcAddress;
        //possibly reusing this player, only thing that should be replaced is the mpv openGL context;
        if (!_mpvRenderContext.isNullPtr())
        {
            mpv_render_context_free(_mpvRenderContext);
        }

        // Try creating a render context with desktop OpenGL, then fallback to OpenGL ES
        unsafe
        {
            nint ctx = nint.Zero;
            // Try desktop OpenGL
            {
                var initParams = new MpvOpenglInitParams
                {
                    get_proc_address = Marshal.GetFunctionPointerForDelegate(_procAddressCallback),
                    get_proc_address_ctx = nint.Zero,
                };
                var enableAdvancedControl = 1;
                byte[] managedParamApiType = Encoding.UTF8.GetBytes("opengl\0");
                fixed (byte* paramApiType = managedParamApiType)
                {
                    MpvRenderParam[] renderParams = {
                        new(){ type = mpv_render_param_type.MPV_RENDER_PARAM_API_TYPE, data = (void*)paramApiType },
                        new(){ type = mpv_render_param_type.MPV_RENDER_PARAM_OPENGL_INIT_PARAMS , data = &initParams },
                        new(){ type = mpv_render_param_type.MPV_RENDER_PARAM_ADVANCED_CONTROL , data = &enableAdvancedControl },
                        new()
                    };
                    fixed (MpvRenderParam* ParamPtr = &renderParams[0])
                    {
                        int status = mpv_render_context_create(out ctx, _mpvContext, ParamPtr);
                        if (status < 0)
                        {
                            Console.WriteLine($"mpv_render_context_create failed with api 'opengl': {status}");
                            ctx = nint.Zero;
                        }
                    }
                }
            }
            // Fallback to OpenGL ES if needed
            if (ctx == nint.Zero)
            {
                var initParams = new MpvOpenglInitParams
                {
                    get_proc_address = Marshal.GetFunctionPointerForDelegate(_procAddressCallback),
                    get_proc_address_ctx = nint.Zero,
                };
                var enableAdvancedControl = 1;
                byte[] managedParamApiType = Encoding.UTF8.GetBytes("opengl-es\0");
                fixed (byte* paramApiType = managedParamApiType)
                {
                    MpvRenderParam[] renderParams = {
                        new(){ type = mpv_render_param_type.MPV_RENDER_PARAM_API_TYPE, data = (void*)paramApiType },
                        new(){ type = mpv_render_param_type.MPV_RENDER_PARAM_OPENGL_INIT_PARAMS , data = &initParams },
                        new(){ type = mpv_render_param_type.MPV_RENDER_PARAM_ADVANCED_CONTROL , data = &enableAdvancedControl },
                        new()
                    };
                    fixed (MpvRenderParam* ParamPtr = &renderParams[0])
                    {
                        int status = mpv_render_context_create(out ctx, _mpvContext, ParamPtr);
                        if (status < 0)
                        {
                            Console.WriteLine($"mpv_render_context_create failed with api 'opengl-es': {status}");
                            ctx = nint.Zero;
                        }
                    }
                }
            }
            _mpvRenderContext = ctx;
        }

        if (_mpvRenderContext == nint.Zero)
        {
            throw new Exception("Failed to create mpv OpenGL render context (opengl/opengl-es)");
        }

        _wakeupCallback = MpvEvent;
        _renderUpdateCallback = MpvRenderUpdate;
        mpv_set_wakeup_callback(_mpvContext, Marshal.GetFunctionPointerForDelegate(_wakeupCallback), nint.Zero);
        mpv_render_context_set_update_callback(_mpvRenderContext, Marshal.GetFunctionPointerForDelegate(_renderUpdateCallback), nint.Zero);

        //Start the background worker
        _backgroundWorkerTask = Task.Run(() => BackgroundWorker(_backgroundWorkerCancellationTokenSource.Token));
    }
    private void MpvEvent(nint data)
    {
        // signal immediately from mpv thread
        _eventQueue.Enqueue(CustomEventType.Wakeup);
        _eventSignal.Set();
    }
    private void MpvRenderUpdate(nint data)
    {
        // signal immediately from mpv thread
        _eventQueue.Enqueue(CustomEventType.Render);
        _eventSignal.Set();
    }
    //marshal a command to mpv
    public void MpvCommand(string[] command)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_mpvContext.isNullPtr()) throw new Exception("Mpv context not properly initialised.");
            nint[] argPtrs = new nint[command.Length + 1];
            for (int i = 0; i < command.Length; i++)
            {
                argPtrs[i] = Marshal.StringToCoTaskMemUTF8(command[i]);
            }
            argPtrs[command.Length] = nint.Zero;
            nint argsPtr = Marshal.AllocHGlobal(nint.Size * argPtrs.Length);
            Marshal.Copy(argPtrs, 0, argsPtr, argPtrs.Length);
            int result = mpv_command(_mpvContext, argsPtr);
            for (int i = 0; i < command.Length; i++)
            {
                if (argPtrs[i] != nint.Zero)
                    Marshal.FreeCoTaskMem(argPtrs[i]);
            }
            Marshal.FreeHGlobal(argsPtr);
        });
    }
    public void RegisterEvent<T>(string property, MpvFormat format)
    {
        if (_mpvContext.isNullPtr()) throw new Exception("Mpv context not properly initialised.");
        if (format != MpvFormat.MPV_FORMAT_DOUBLE && format != MpvFormat.MPV_FORMAT_FLAG && format != MpvFormat.MPV_FORMAT_INT64) throw new System.NotImplementedException("This format has not implemented");
        if (_mpvPropertyChangeEvents.ContainsKey(property)) throw new InvalidOperationException("Event with this property name is already registered.");
        mpv_observe_property(_mpvContext, 0, property, format);
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
        if (_mpvContext.isNullPtr()) throw new Exception("Mpv context not properly initialised.");
        if (typeof(T) == typeof(string) && format == MpvFormat.MPV_FORMAT_STRING)
        {
            return (T?)(object?)MpvGetStringProperty(property);
        }

        var resultPtr = Marshal.AllocHGlobal(Marshal.SizeOf<T>());
        var ret = mpv_get_property(_mpvContext, property, format, resultPtr);
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
        if (_mpvContext.isNullPtr()) throw new Exception("Mpv context not properly initialised.");
        var resultPtr = Marshal.AllocHGlobal(nint.Size);
        var ret = mpv_get_property(_mpvContext, property, MpvFormat.MPV_FORMAT_STRING, resultPtr);
        if (ret < 0)
        {
            Marshal.FreeHGlobal(resultPtr);
            return null;
        }
        var stringPtr = Marshal.ReadIntPtr(resultPtr);
        var result = Marshal.PtrToStringUTF8(stringPtr);
        mpv_free(stringPtr);
        Marshal.FreeHGlobal(resultPtr);
        return result;
    }
    private nint GetProcAddress(nint fn_ctx, string name)
    {
        //this should not be null
        return _glInterface!.GetProcAddress(name);
    }
    private void BackgroundWorker(CancellationToken ctx)
    {
        while (true)
        {
            bool redraw = false;
            _eventSignal.WaitOne();
            if (ctx.IsCancellationRequested) return;
            CustomEventType _ev;
            while (_eventQueue.TryDequeue(out _ev))
            {
                if (ctx.IsCancellationRequested) return;
                switch (_ev)
                {
                    case CustomEventType.Wakeup:
                        while (true)
                        {
                            if (_mpvContext.isNullPtr()) continue;
                            nint evPtr = mpv_wait_event(_mpvContext, 0.0);
                            if (evPtr == nint.Zero) break;
                            MpvEvent ev = Marshal.PtrToStructure<MpvEvent>(evPtr);
                            if (ev.event_id == MpvEventId.MPV_EVENT_NONE) break;
                            if (ev.event_id == MpvEventId.MPV_EVENT_PROPERTY_CHANGE)
                            {
                                var prop = Marshal.PtrToStructure<MpvEventProperty>(ev.data);
                                var name = Marshal.PtrToStringUTF8(prop.name);
                                if (name is null) continue;
                                if (prop.data == nint.Zero) continue;
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
                            }
                            else if (ev.event_id == MpvEventId.MPV_EVENT_LOG_MESSAGE)
                            {
                                var log = Marshal.PtrToStructure<MpvEventLogMessage>(ev.data);
                                var prefix = Marshal.PtrToStringUTF8(log.prefix) ?? string.Empty;
                                var level = Marshal.PtrToStringUTF8(log.level) ?? string.Empty;
                                var text = Marshal.PtrToStringUTF8(log.text) ?? string.Empty;
                                Console.WriteLine($"[mpv {level}] {prefix}: {text}");
                                try { File.AppendAllText(_mpvLogPath, $"[mpv {level}] {prefix}: {text}\n"); } catch { }
                            }
                        }
                        break;
                    case CustomEventType.Render:
                        if (_mpvRenderContext.isNullPtr()) continue;
                        var flags = mpv_render_context_update(_mpvRenderContext);
                        if ((flags & (1 << 0)) != 0)
                        {
                            redraw = true;
                        }
                        break;
                }
            }
            if (redraw)
            {
                if (ctx.IsCancellationRequested) return;
                //trigger render on UI thread
                Dispatcher.UIThread.Post(() =>
                {
                    _mediaControl?.TriggerRender();
                });
            }
        }
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
        string pos = ms.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string[] command = { "seek", pos, "absolute" };
        MpvCommand(command);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
            }

            mpv_set_wakeup_callback(_mpvContext, nint.Zero, nint.Zero);
            if (!_mpvRenderContext.isNullPtr())
                mpv_render_context_set_update_callback(_mpvRenderContext, nint.Zero, nint.Zero);
            foreach (var p in _mpvPropertyChangeEvents.Values)
            {
                if (p.Item1 is IDisposable disposable) disposable.Dispose();
            }
            _mpvPropertyChangeEvents.Clear();
            mpv_unobserve_property(_mpvContext, 0);
            _backgroundWorkerCancellationTokenSource.Cancel();
            _eventSignal.Set();
            _backgroundWorkerTask?.Wait(300);
            _mpvPropertyChangeEvents.Clear();
            if (!_mpvRenderContext.isNullPtr())
            {
                mpv_render_context_free(_mpvRenderContext);
                _mpvRenderContext = nint.Zero;
            }
            if (!_mpvContext.isNullPtr())
            {
                mpv_destroy(_mpvContext);
                _mpvContext = nint.Zero;
            }
            _disposed = true;
        }
    }
}

public class MediaControl : OpenGlControlBase
{
    protected unsafe override void OnOpenGlRender(GlInterface gl, int fb)
    {
        var size = this.Bounds;
        // Use pixel size for retina/HiDPI displays (e.g., macOS)
        var scale = this.VisualRoot?.RenderScaling ?? 1.0;
        var w = Math.Max(1, (int)Math.Round(size.Width * scale));
        var h = Math.Max(1, (int)Math.Round(size.Height * scale));
        var flip_y = 1;
        MpvOpenGLFramebuffer framebuffer = new()
        {
            fbo = fb,
            width = w,
            height = h,
        };
        MpvRenderParam[] param = {
              new() {
                type = mpv_render_param_type.MPV_RENDER_PARAM_OPENGL_FBO,
                data = &framebuffer,
              },
                new() {
                    type = mpv_render_param_type.MPV_RENDER_PARAM_FLIP_Y,
                    data = &flip_y,
                },
                new()
            };
        fixed (MpvRenderParam* p = &param[0])
        {
            if (MpvPlayer._mpvRenderContext.isNullPtr()) return;
            mpv_render_context_render(MpvPlayer._mpvRenderContext, p);
        }
    }

    internal void TriggerRender()
    {
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        base.OnOpenGlInit(gl);
        MpvPlayer._glInterface = gl;
        MpvPlayer._mediaControl = this;
        MpvPlayer.Initialise();
        // Kick an initial render
        RequestNextFrameRendering();
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
internal enum CustomEventType
{
    Wakeup,
    Render,
}

internal static class NintExtensions
{
    public static bool isNullPtr(this nint p) => p == nint.Zero;
}
