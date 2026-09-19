using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PlaybackTools.Mpv
{
    /// <summary>
    /// Raw P/Invoke wrapper around libmpv's client + render APIs, using the
    /// OpenGL render backend for GPU-accelerated rendering.
    ///
    /// This replaces the earlier software-render version entirely. Rendering
    /// now happens inside GLWpfControl's Render event (UI thread, GL context
    /// current), so there's no dedicated render thread or WriteableBitmap
    /// locking needed - GLWpfControl's own render loop drives the cadence,
    /// and mpv just renders into whichever FBO is currently bound.
    /// </summary>
    public sealed class MpvPlayer : IDisposable
    {
        private const string MpvDll = "libmpv-2.dll";

        [StructLayout(LayoutKind.Sequential)]
        private struct MpvRenderParam
        {
            public int Type;
            public IntPtr Data;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MpvOpenGlInitParams
        {
            public IntPtr GetProcAddress;
            public IntPtr GetProcAddressCtx;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MpvOpenGlFbo
        {
            public int Fbo;
            public int W;
            public int H;
            public int InternalFormat;
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr GetProcAddressDelegate(IntPtr ctx, IntPtr name);

        [DllImport(MpvDll)] private static extern IntPtr mpv_create();
        [DllImport(MpvDll)] private static extern int mpv_initialize(IntPtr ctx);
        [DllImport(MpvDll)] private static extern int mpv_set_option_string(IntPtr ctx, byte[] name, byte[] data);
        [DllImport(MpvDll)] private static extern int mpv_set_property_string(IntPtr ctx, byte[] name, byte[] data);
        [DllImport(MpvDll)] private static extern IntPtr mpv_get_property_string(IntPtr ctx, byte[] name);
        [DllImport(MpvDll)] private static extern void mpv_free(IntPtr data);
        [DllImport(MpvDll)] private static extern int mpv_command(IntPtr ctx, IntPtr[] args);
        [DllImport(MpvDll)] private static extern void mpv_terminate_destroy(IntPtr ctx);
        [DllImport(MpvDll)] private static extern int mpv_render_context_create(out IntPtr res, IntPtr mpv, MpvRenderParam[] parameters);
        [DllImport(MpvDll)] private static extern int mpv_render_context_render(IntPtr renderCtx, MpvRenderParam[] parameters);
        [DllImport(MpvDll)] private static extern void mpv_render_context_free(IntPtr renderCtx);
        [DllImport(MpvDll)] private static extern IntPtr mpv_error_string(int error);

        [DllImport("opengl32.dll")] private static extern IntPtr wglGetProcAddress(string name);
        [DllImport("kernel32.dll")] private static extern IntPtr GetModuleHandle(string moduleName);
        [DllImport("kernel32.dll")] private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        private const int MPV_RENDER_PARAM_API_TYPE = 1;
        private const int MPV_RENDER_PARAM_OPENGL_INIT_PARAMS = 2;
        private const int MPV_RENDER_PARAM_OPENGL_FBO = 3;
        private const int MPV_RENDER_PARAM_FLIP_Y = 4;
        private const int MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME = 12;

        private static string ErrorString(int code)
        {
            IntPtr ptr = mpv_error_string(code);
            return ptr == IntPtr.Zero ? $"error {code}" : (Marshal.PtrToStringUTF8(ptr) ?? $"error {code}");
        }

        private IntPtr _ctx;
        private IntPtr _renderCtx;

        private GetProcAddressDelegate? _getProcAddressDelegate;

        public void InitializeOpenGL(bool startPaused = false)
        {
            _ctx = mpv_create();
            if (_ctx == IntPtr.Zero)
                throw new InvalidOperationException("mpv_create failed - is libmpv-2.dll present and the correct architecture (x64)?");

            SetOption("vo", "libmpv"); 
            SetOption("hwdec", "no");
            SetOption("keep-open", "yes"); 

            if (startPaused)
                SetOption("pause", "yes");

            int initResult = mpv_initialize(_ctx);
            if (initResult < 0)
                throw new InvalidOperationException($"mpv_initialize failed with error code {initResult}");

            _getProcAddressDelegate = GetGLProcAddress; 
            IntPtr getProcAddressPtr = Marshal.GetFunctionPointerForDelegate(_getProcAddressDelegate);

            var initParams = new MpvOpenGlInitParams
            {
                GetProcAddress = getProcAddressPtr,
                GetProcAddressCtx = IntPtr.Zero
            };

            IntPtr initParamsPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvOpenGlInitParams>());
            IntPtr apiTypePtr = AllocUtf8String("opengl");
            try
            {
                Marshal.StructureToPtr(initParams, initParamsPtr, false);

                var createParams = new MpvRenderParam[]
                {
                    new MpvRenderParam { Type = MPV_RENDER_PARAM_API_TYPE, Data = apiTypePtr },
                    new MpvRenderParam { Type = MPV_RENDER_PARAM_OPENGL_INIT_PARAMS, Data = initParamsPtr },
                    new MpvRenderParam { Type = 0, Data = IntPtr.Zero } // terminator
                };

                int createResult = mpv_render_context_create(out _renderCtx, _ctx, createParams);
                if (createResult < 0)
                    throw new InvalidOperationException($"mpv_render_context_create (opengl) failed: {ErrorString(createResult)}");
            }
            finally
            {
                Marshal.FreeHGlobal(initParamsPtr);
                Marshal.FreeHGlobal(apiTypePtr);
            }
        }

        // mpv calls this to resolve GL function pointers for the context we handed it.
        private static IntPtr GetGLProcAddress(IntPtr ctx, IntPtr namePtr)
        {
            string? name = Marshal.PtrToStringAnsi(namePtr);
            if (string.IsNullOrEmpty(name)) return IntPtr.Zero;

            IntPtr address = wglGetProcAddress(name);

            long addr = address.ToInt64();
            if (address == IntPtr.Zero || addr == 1 || addr == 2 || addr == 3 || addr == -1)
            {
                IntPtr module = GetModuleHandle("opengl32.dll");
                address = GetProcAddress(module, name);
            }

            return address;
        }

        public void RenderIntoFbo(int fbo, int width, int height)
        {
            if (_renderCtx == IntPtr.Zero) return;

            var fboStruct = new MpvOpenGlFbo { Fbo = fbo, W = width, H = height, InternalFormat = 0 };
            IntPtr fboPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvOpenGlFbo>());

            var flipArray = new int[] { 1 }; // GL framebuffers are typically bottom-up; flip so video comes out right-side up
            GCHandle flipHandle = GCHandle.Alloc(flipArray, GCHandleType.Pinned);

            var blockArray = new int[] { 0 }; // don't block waiting for display timing we don't actually have
            GCHandle blockHandle = GCHandle.Alloc(blockArray, GCHandleType.Pinned);

            try
            {
                Marshal.StructureToPtr(fboStruct, fboPtr, false);

                var renderParams = new MpvRenderParam[]
                {
                    new MpvRenderParam { Type = MPV_RENDER_PARAM_OPENGL_FBO, Data = fboPtr },
                    new MpvRenderParam { Type = MPV_RENDER_PARAM_FLIP_Y, Data = flipHandle.AddrOfPinnedObject() },
                    new MpvRenderParam { Type = MPV_RENDER_PARAM_BLOCK_FOR_TARGET_TIME, Data = blockHandle.AddrOfPinnedObject() },
                    new MpvRenderParam { Type = 0, Data = IntPtr.Zero } // terminator
                };

                int renderResult = mpv_render_context_render(_renderCtx, renderParams);
                if (renderResult < 0)
                    System.Diagnostics.Debug.WriteLine($"[MpvPlayer] GL render failed: {ErrorString(renderResult)}");
            }
            finally
            {
                Marshal.FreeHGlobal(fboPtr);
                flipHandle.Free();
                blockHandle.Free();
            }
        }

        /// <summary>Sets a startup option. Only takes effect if called before Initialize's internal mpv_initialize call.</summary>
        public void SetOption(string name, string value)
        {
            int result = mpv_set_option_string(_ctx, ToUtf8(name), ToUtf8(value));
            if (result < 0)
                System.Diagnostics.Debug.WriteLine($"[MpvPlayer] SetOption '{name}={value}' failed: {ErrorString(result)}");
        }

        /// <summary>Sets a property at runtime (e.g. "pause", "loop-file", "volume").</summary>
        public void SetProperty(string name, string value)
        {
            if (_ctx == IntPtr.Zero) return;
            int result = mpv_set_property_string(_ctx, ToUtf8(name), ToUtf8(value));
            if (result < 0)
                System.Diagnostics.Debug.WriteLine($"[MpvPlayer] SetProperty '{name}={value}' failed: {ErrorString(result)}");
        }

        /// <summary>Reads a property at runtime as a string (e.g. "time-pos", "duration").</summary>
        public string? GetProperty(string name)
        {
            if (_ctx == IntPtr.Zero) return null;
            IntPtr resultPtr = mpv_get_property_string(_ctx, ToUtf8(name));
            if (resultPtr == IntPtr.Zero) return null;

            string? value = Marshal.PtrToStringUTF8(resultPtr);
            mpv_free(resultPtr);
            return value;
        }

        public bool HasActiveClip { get; private set; }

        public void LoadFile(string path, double? startSeconds = null)
        {
            string start = (startSeconds.HasValue && startSeconds.Value > 0)
                ? startSeconds.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : "0";

            SetProperty("start", start);
            Command("loadfile", path);
            HasActiveClip = true;
        }

        public void Stop()
        {
            Command("stop");
            HasActiveClip = false;
        }

        public void Command(params string[] args)
        {
            if (_ctx == IntPtr.Zero)
                throw new InvalidOperationException("MpvPlayer not initialized.");

            var ptrs = new IntPtr[args.Length + 1];
            try
            {
                for (int i = 0; i < args.Length; i++)
                    ptrs[i] = AllocUtf8String(args[i]);

                ptrs[args.Length] = IntPtr.Zero; // mpv_command expects a null-terminated array

                int result = mpv_command(_ctx, ptrs);
                if (result < 0)
                    System.Diagnostics.Debug.WriteLine($"[MpvPlayer] Command '{string.Join(' ', args)}' failed: {ErrorString(result)}");
            }
            finally
            {
                foreach (var p in ptrs)
                    if (p != IntPtr.Zero) Marshal.FreeHGlobal(p);
            }
        }

        private static byte[] ToUtf8(string s) => Encoding.UTF8.GetBytes(s + "\0");

        private static IntPtr AllocUtf8String(string s)
        {
            var bytes = ToUtf8(s);
            IntPtr ptr = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, ptr, bytes.Length);
            return ptr;
        }

        public void Dispose()
        {
            if (_renderCtx != IntPtr.Zero)
            {
                mpv_render_context_free(_renderCtx);
                _renderCtx = IntPtr.Zero;
            }

            if (_ctx != IntPtr.Zero)
            {
                mpv_terminate_destroy(_ctx);
                _ctx = IntPtr.Zero;
            }
        }
    }
}