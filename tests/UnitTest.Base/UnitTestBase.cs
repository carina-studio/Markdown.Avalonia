using ApprovalTests;
using Avalonia.Headless;
using System.IO;
using System.Reflection;
using UnitTest.Base.Apps;
using UnitTest.Base.Utils;

namespace UnitTest.Base
{
    public class UnitTestBase
    {
        // Avalonia 12 forbids initializing the UI dispatcher on a background
        // thread, so the old "start the app on a new Thread" harness crashes with
        // "IDispatcherImpl belongs to a different thread". Instead we drive a
        // single headless session; [RunOnUI] marshals each test onto its UI thread.
        internal static readonly HeadlessUnitTestSession Session
            = HeadlessUnitTestSession.StartNew(typeof(App));

        static UnitTestBase()
        {
            var fwNm = Util.GetRuntimeName();
            Approvals.RegisterDefaultNamerCreation(() => new ChangeOutputPathNamer("Out"));
        }

        protected string AssetPath;

        public UnitTestBase()
        {
            var asm = Assembly.GetExecutingAssembly();
            AssetPath = Path.GetDirectoryName(asm.Location);
        }
    }
}
