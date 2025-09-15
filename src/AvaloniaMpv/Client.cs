using System.Runtime.InteropServices;
namespace AvaloniaMpv;
internal unsafe static class LibMpv
{
#if WINDOWS
    private const string LibName = "libmpv2.dll";
#else
    private const string LibName = "mpv";
#endif
    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_clients_init(nint mpctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_set_option_string(
       nint ctx,
       [MarshalAs(UnmanagedType.LPStr)] string name,
       [MarshalAs(UnmanagedType.LPStr)] string data
   );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(nint ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mpv_create();

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_clients_destroy(nint mpctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_shutdown_clients(nint mpctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool mp_is_shutting_down(nint mpctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool mp_clients_all_initialized(nint mpctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool mp_client_id_exists(nint mpctx, long id);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_client_broadcast_event(nint mpctx, int @event, nint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mp_client_send_event(
        nint mpctx,
        [MarshalAs(UnmanagedType.LPStr)] string client_name,
        ulong reply_userdata,
        int @event,
        nint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mp_client_send_event_dup(
        nint mpctx,
        [MarshalAs(UnmanagedType.LPStr)] string client_name,
        int @event,
        nint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_client_property_change(
        nint mpctx,
        [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_client_send_property_changes(nint mpctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mp_new_client(nint clients, [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_client_set_weak(nint ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mp_client_get_log(nint ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mp_client_get_global(nint ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mp_client_broadcast_event_external(nint api, int @event, nint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint node_get_alloc(nint node);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool mp_set_main_render_context(nint client_api, nint ctx, bool active);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mp_client_api_acquire_render_context(nint ca);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void kill_video_async(nint client_api);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate int mpv_stream_cb_open_ro_fn(
        nint user_data,
        [MarshalAs(UnmanagedType.LPStr)] string uri,
        out nint stream);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    [return: MarshalAs(UnmanagedType.I1)]
    public static extern bool mp_streamcb_lookup(
        nint g,
        [MarshalAs(UnmanagedType.LPStr)] string protocol,
        out nint out_user_data,
        out mpv_stream_cb_open_ro_fn out_fn);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_request_log_messages(nint ctx, [MarshalAs(UnmanagedType.LPStr)] string min_level);


    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_create(
            out nint res,
            nint mpv,
            MpvRenderParam* @params
        );

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command_async(nint ctx, UInt64 reply_userdata, nint args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(nint ctx, nint args);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_set_wakeup_callback(nint ctx, MpvWakeupCallback cb, nint d);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern nint mpv_wait_event(nint ctx, double timeout);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_set_update_callback(nint ctx, MpvRenderUpdateFn callback, nint callback_ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern UInt64 mpv_render_context_update(nint ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_render_context_render(nint ctx, MpvRenderParam* @params);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_get_property(nint ctx, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat mpvFormat, nint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(nint data);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_observe_property(nint mpv, UInt64 reply_userdata, [MarshalAs(UnmanagedType.LPStr)] string name, MpvFormat format);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(nint ctx);

    [DllImport(LibName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_destroy(nint ctx);
}


public enum mpv_render_param_type
{
    MPV_RENDER_PARAM_INVALID = 0,
    MPV_RENDER_PARAM_API_TYPE = 1,
    MPV_RENDER_PARAM_OPENGL_INIT_PARAMS = 2,
    MPV_RENDER_PARAM_OPENGL_FBO = 3,
    MPV_RENDER_PARAM_FLIP_Y = 4,
    MPV_RENDER_PARAM_DEPTH = 5,
    MPV_RENDER_PARAM_ICC_PROFILE = 6,
    MPV_RENDER_PARAM_AMBIENT_LIGHT = 7,
    MPV_RENDER_PARAM_X11_DISPLAY = 8,
    MPV_RENDER_PARAM_WL_DISPLAY = 9,
    MPV_RENDER_PARAM_ADVANCED_CONTROL = 10,
    MPV_RENDER_PARAM_NEXT_FRAME_INFO = 11,
    MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 12,
    MPV_RENDER_PARAM_SKIP_RENDERING = 13,
    MPV_RENDER_PARAM_DRM_DISPLAY = 14,
    MPV_RENDER_PARAM_DRM_DRAW_SURFACE_SIZE = 15,
    MPV_RENDER_PARAM_DRM_DISPLAY_V2 = 16,
    MPV_RENDER_PARAM_SW_SIZE = 17,
    MPV_RENDER_PARAM_SW_FORMAT = 18,
    MPV_RENDER_PARAM_SW_STRIDE = 19,
    MPV_RENDER_PARAM_SW_POINTER = 20,
};
[StructLayout(LayoutKind.Sequential)]
public unsafe struct MpvRenderParam
{
    public mpv_render_param_type type;
    public void* data;
}

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate nint MpvOpenglGetProcAddressCallback(
    nint ctx,
    [MarshalAs(UnmanagedType.LPStr)] string name
);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void MpvRenderUpdateFn(
    nint cb_ctx
);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
public delegate void MpvWakeupCallback(nint data);

[StructLayout(LayoutKind.Sequential)]
public struct MpvOpenglInitParams
{
    public nint get_proc_address;
    public nint get_proc_address_ctx;
}

[StructLayout(LayoutKind.Sequential)]
public struct MpvEvent
{
    public MpvEventId event_id;
    public int error;
    public UInt64 reply_userdata;
    public nint data;
};
[StructLayout(LayoutKind.Sequential)]
public struct MpvEventProperty
{
    public nint name;
    public MpvFormat format;
    public nint data;
}
[StructLayout(LayoutKind.Sequential)]
public struct MpvOpenGLFramebuffer
{
    public int fbo;
    public int width;
    public int height;
    public int internal_format;
}

public enum MpvEventId
{
    MPV_EVENT_NONE = 0,
    MPV_EVENT_SHUTDOWN = 1,
    MPV_EVENT_LOG_MESSAGE = 2,
    MPV_EVENT_GET_PROPERTY_REPLY = 3,
    MPV_EVENT_SET_PROPERTY_REPLY = 4,
    MPV_EVENT_COMMAND_REPLY = 5,
    MPV_EVENT_START_FILE = 6,
    MPV_EVENT_END_FILE = 7,
    MPV_EVENT_FILE_LOADED = 8,
    MPV_EVENT_IDLE = 11,
    MPV_EVENT_TICK = 14,
    MPV_EVENT_CLIENT_MESSAGE = 16,
    MPV_EVENT_VIDEO_RECONFIG = 17,
    MPV_EVENT_AUDIO_RECONFIG = 18,
    MPV_EVENT_SEEK = 20,
    MPV_EVENT_PLAYBACK_RESTART = 21,
    MPV_EVENT_PROPERTY_CHANGE = 22,
    MPV_EVENT_QUEUE_OVERFLOW = 24,
    MPV_EVENT_HOOK = 25,
}

[StructLayout(LayoutKind.Sequential)]
public struct MpvEventLogMessage
{
    public nint prefix;
    public nint level;
    public nint text;
    public MpvLogLevel log_level;
};
public enum MpvLogLevel
{
    MPV_LOG_LEVEL_NONE = 0,    /// "no"    - disable absolutely all messages
    MPV_LOG_LEVEL_FATAL = 10,   /// "fatal" - critical/aborting errors
    MPV_LOG_LEVEL_ERROR = 20,   /// "error" - simple errors
    MPV_LOG_LEVEL_WARN = 30,   /// "warn"  - possible problems
    MPV_LOG_LEVEL_INFO = 40,   /// "info"  - informational message
    MPV_LOG_LEVEL_V = 50,   /// "v"     - noisy informational message
    MPV_LOG_LEVEL_DEBUG = 60,   /// "debug" - very noisy technical information
    MPV_LOG_LEVEL_TRACE = 70,   /// "trace" - extremely noisy
};

public enum MpvFormat
{
    MPV_FORMAT_NONE = 0,
    MPV_FORMAT_STRING = 1,
    MPV_FORMAT_OSD_STRING = 2,
    MPV_FORMAT_FLAG = 3,
    MPV_FORMAT_INT64 = 4,
    MPV_FORMAT_DOUBLE = 5,
    MPV_FORMAT_NODE = 6,
    MPV_FORMAT_NODE_ARRAY = 7,
    MPV_FORMAT_NODE_MAP = 8,
    MPV_FORMAT_BYTE_ARRAY = 9
}
