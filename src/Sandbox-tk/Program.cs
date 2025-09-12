using OpenTK.Graphics.OpenGL4;
using AvaloniaMpv;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Runtime.InteropServices;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Collections.Concurrent;

public class Window : GameWindow
{
    private ConcurrentQueue<Action> _eventQueue = new();
    private nint _mpvContext = nint.Zero;
    private nint _mpvRenderContext = nint.Zero;
    private bool _redraw = false;
    public Window(int width, int height, string title)
        : base(GameWindowSettings.Default, new NativeWindowSettings()
        {
            ClientSize = (width, height),
            Title = title
        })
    { }

    protected override void OnLoad()
    {
        base.OnLoad();
        GL.ClearColor(0.4f, 0.2f, 0.3f, 1.0f); // dark blue-ish background

        var mpv = LibMpv.mpv_create();
        _mpvContext = mpv;
        if (mpv == nint.Zero)
        {
            Console.WriteLine("Some crazy error");
        }
        LibMpv.mpv_set_option_string(mpv, "vo", "libmpv");
        if (LibMpv.mpv_initialize(mpv) < 0)
        {
            Console.WriteLine("MPV failed to init");
        }
        LibMpv.mpv_request_log_messages(mpv, "debug");
        MpvOpenglGetProcAddressCallback procCallback = Window.GetProcAddress;
        var initParams = new MpvOpenglInitParams
        {
            get_proc_address = procCallback,
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
        LibMpv.mpv_set_wakeup_callback(mpv, MpvEvent, nint.Zero);
        //render callback;
        LibMpv.mpv_render_context_set_update_callback(mpv_gl, MpvRenderUpdate, nint.Zero);
        string[] command = { "loadfile", "/home/noble/Videos/Superman.2025.1080p.WebDl.English.Msubs.MoviesMod.cafe.mkv" };
        nint[] argPtrs = new nint[command.Length + 1];
        for (int i = 0; i < command.Length; i++)
        {
            argPtrs[i] = Marshal.StringToHGlobalAnsi(command[i]);
        }
        argPtrs[command.Length] = nint.Zero; // null terminator

        nint argsPtr = Marshal.AllocHGlobal(nint.Size * argPtrs.Length);
        Marshal.Copy(argPtrs, 0, argsPtr, argPtrs.Length);
        int result = LibMpv.mpv_command_async(mpv, 0, argsPtr);
        for (int i = 0; i < command.Length; i++)
        {
            if (argPtrs[i] != nint.Zero)
                Marshal.FreeHGlobal(argPtrs[i]);
        }
        Marshal.FreeHGlobal(argsPtr);
        Marshal.FreeHGlobal(paramApiType);
        Marshal.DestroyStructure<MpvOpenglInitParams>(initParamsPtr);
        Marshal.FreeHGlobal(enableAdvancedControlPtr);
    }

    protected override void OnRenderFrame(FrameEventArgs args)
    {
        base.OnRenderFrame(args);
        // MakeCurrent();
        GL.Clear(ClearBufferMask.ColorBufferBit); // clear screen
        while (_eventQueue.TryDequeue(out var action))
        {
            action();
        }

        if (_redraw)
        {
            var size = (System.Drawing.Size)this.Size;
            var w = size.Width;
            var h = size.Height;
            var flip_y = GCHandle.Alloc(1, GCHandleType.Pinned);
            MpvOpenGLFramebuffer framebuffer = new()
            {
                fbo = 0,
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
            LibMpv.mpv_render_context_render(_mpvRenderContext, paramsP);
            handle.Free();
            Marshal.FreeHGlobal(framebufferPtr);
            _redraw = false;
        }
        SwapBuffers(); // show what we drew
    }

    public static nint GetProcAddress(nint fn_ctx, [MarshalAs(UnmanagedType.LPStr)] string name)
    {
        return GLFW.GetProcAddress(name);
    }

    private void MpvRenderUpdate(nint data)
    {
        _eventQueue.Enqueue(() =>
        {
            var flags = LibMpv.mpv_render_context_update(_mpvRenderContext);
            if ((flags & (1 << 0)) != 0)
            {
                _redraw = true;
            }
        });
    }
    private void MpvEvent(nint data)
    {
        //i can probably do this safely on any thread
        while (true)
        {
            nint evPtr = LibMpv.mpv_wait_event(_mpvContext, 0.0);
            if (evPtr == nint.Zero) break;
            MpvEvent ev = Marshal.PtrToStructure<MpvEvent>(evPtr);

            if (ev.event_id == MpvEventId.MPV_EVENT_NONE) break;
            if (ev.event_id == MpvEventId.MPV_EVENT_LOG_MESSAGE)
            {
                nint msgPtr = ev.data;
                var msg = Marshal.PtrToStructure<MpvEventLogMessage>(msgPtr);
                var text = Marshal.PtrToStringAnsi(msg.text);
                if (text is null) break;
                if (text.Contains("DR image"))
                    Console.WriteLine($"Recieved Log Message {text}");
            }
        }
    }
}

public static class Program
{
    public static void Main(string[] args)
    {
        using var w = new Window(800, 600, "LearnOpenGL with OpenTK");
        w.Run();
    }
}
