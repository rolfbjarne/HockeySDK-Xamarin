﻿using System;
using System.Runtime.InteropServices;
using Foundation;
using ObjCRuntime;
using System.Threading.Tasks;

namespace HockeyApp.iOS
{
	public partial class BITHockeyManager
	{
		private static bool startedManager = false;
		private static readonly object setupLock= new object();
		private static bool terminateOnUnobservedTaskException;

		public static bool TerminateOnUnobservedTaskException
		{
			get { return terminateOnUnobservedTaskException; }
			set { terminateOnUnobservedTaskException = value; }
		}

		[DllImport ("libc")]
		private static extern int sigaction (Signal sig, IntPtr act, IntPtr oact);

		private enum Signal {
			SIGBUS = 10,
			SIGSEGV = 11
		}

		private BITHockeyManager () {}

		public void StartManager()
		{
			if (startedManager) return;

			lock (setupLock)
			{
				if (startedManager) return;

				try {
				} finally {
					Mono.Runtime.RemoveSignalHandlers ();
					try {
						// Enable crash reporting libraries
						DoStartManager();
					} finally {
						Mono.Runtime.InstallSignalHandlers ();
					}
				}

				AppDomain.CurrentDomain.UnhandledException += (sender, e) => ThrowExceptionAsNative(e.ExceptionObject);
				TaskScheduler.UnobservedTaskException += (sender, e) => 
				{
					if (terminateOnUnobservedTaskException)
					{
						ThrowExceptionAsNative(e.Exception);
					}
				};

				startedManager = true;
			}
		}

		private void ThrowExceptionAsNative(Exception exception)
		{
			ConvertToNsExceptionAndAbort (exception);
		}

		private void ThrowExceptionAsNative(object exception)
		{
			ConvertToNsExceptionAndAbort (exception);
		}

        #if __UNIFIED__
		[DllImport(global::ObjCRuntime.Constants.FoundationLibrary, EntryPoint = "NSGetUncaughtExceptionHandler")]
        #else
        [DllImport(global::MonoTouch.Constants.FoundationLibrary, EntryPoint = "NSGetUncaughtExceptionHandler")]
        #endif
		private static extern IntPtr NSGetUncaughtExceptionHandler();

		private delegate void ReporterDelegate(IntPtr ex);

//		static void ConvertToNsExceptionAndAbort(object e)
//		{
//			var nse = new NSException(".NET Exception", e.ToString(), null);
//			var uncaught = NSGetUncaughtExceptionHandler();
//			var dele = (ReporterDelegate)Marshal.GetDelegateForFunctionPointer(uncaught, typeof(ReporterDelegate));
//			dele(nse.Handle);
//		}

		private void ConvertToNsExceptionAndAbort(object e)
		{	
			var name = "Managed Xamarin.iOS .NET Exception";
			var msg = e.ToString();

			var ex = e as Exception;
			if (ex != null) {
				name = ex.GetType ().FullName;
				if (ex.StackTrace != null) {
					msg = msg.Insert (msg.IndexOf('\n'), "Xamarin Exception Stack:");
				}
			}
			name = name.Replace("%", "%%"); 
			msg = msg.Replace("%", "%%");
			var nse = new NSException(name, msg, null);
			var sel = new Selector("raise");
			global::Xamarin.ObjCRuntime.Messaging.void_objc_msgSend(nse.Handle, sel.Handle);
		}
	}
}

namespace Xamarin.ObjCRuntime {
    internal static class Messaging {
        const string LIBOBJC_DYLIB = "/usr/lib/libobjc.dylib";

        [DllImport (LIBOBJC_DYLIB, EntryPoint="objc_msgSend")]
        internal extern static void void_objc_msgSend (IntPtr receiver, IntPtr selector);
    }
}

