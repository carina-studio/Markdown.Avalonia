using Avalonia;
using Avalonia.Headless;
using Avalonia.Markup.Xaml;

namespace UnitTest.Base.Apps
{
    public class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        // Avalonia 12 no longer allows starting the framework on a background
        // thread (see UnitTestBase). Tests run on a HeadlessUnitTestSession built
        // from this AppBuilder. Skia + HarfBuzz are enabled (UseHeadlessDrawing
        // = false) so the image-based approval tests get real rendered pixels.
        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UseSkia()
                .UseHarfBuzz()
                .UseHeadless(new AvaloniaHeadlessPlatformOptions
                {
                    UseHeadlessDrawing = false
                });
    }
}
