namespace Soundpacks_Framework.Tests
{
    public static class Program
    {
        public static int Main(string[] args)
        {
            var runner = new TestRunner();
            JsonTests.Register(runner);
            ManifestSerializerTests.Register(runner);
            PathPolicyTests.Register(runner);
            AtomicFileWriterTests.Register(runner);
            SoundpackContentDetectorTests.Register(runner);

            int failures = runner.RunAll();
            return failures == 0 ? 0 : 1;
        }
    }
}
