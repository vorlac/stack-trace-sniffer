using Microsoft.Diagnostics.Runtime;
using System.Diagnostics;
using System.Reflection;

if (args.Length == 3
 && int.TryParse(args[0], out int pid)
 && int.TryParse(args[1], out int threadID)
 && int.TryParse(args[2], out int sampleInterval)
) {
    // We're being called from the Process.Start call below.
    ThreadSampler.Start(pid, threadID, sampleInterval);
}
else {
    // Start ThreadSampler in another process, with 100ms sampling interval
    ProcessStartInfo startInfo = new(
        Path.ChangeExtension(Assembly.GetExecutingAssembly().Location, ".exe"),
        Process.GetCurrentProcess().Id + " " + Thread.CurrentThread.ManagedThreadId + " 100"
    ) {
        RedirectStandardOutput = true,
        CreateNoWindow = true
    };

    using (Process? proc = Process.Start(startInfo)) {
        Debug.Assert(proc != null);
        proc.OutputDataReceived += (sender, args) => Console.WriteLine(
            args.Data != "" ? $"  {args.Data}" : "New stack trace:"
        );
        proc.BeginOutputReadLine();
        // Do some work to test the stack trace samplings 
        Demo.DemoStackTrace();
        // Kill the worker process when we're done.
        proc.Kill();
    }
}

internal abstract class Demo
{
    public static void DemoStackTrace() {
        for (int i = 0; i < 10; i++) {
            Method1();
            Method2();
            Method3();
        }
    }

    private static void Method1() {
        Foo();
    }

    private static void Method2() {
        Foo();
    }

    private static void Method3() {
        Foo();
    }

    private static void Foo() => Thread.Sleep(100);
}

public static class ThreadSampler
{
    public static void Start(int pid, int threadID, int sampleInterval) {
        DataTarget target = DataTarget.AttachToProcess(pid, false);
        ClrRuntime runtime = target.ClrVersions[0].CreateRuntime();

        while (true) {
            // Flush cached data, otherwise we'll get old execution info.
            runtime.FlushCachedData();

            foreach (ClrThread thread in runtime.Threads.Where(
                    thread => thread.ManagedThreadId == threadID
                )
            ) {
                Console.WriteLine(); // Signal new stack trace
                foreach (ClrStackFrame frame in thread.EnumerateStackTrace().Take(100))
                    if (frame.Kind == ClrStackFrameKind.ManagedMethod)
                        Console.WriteLine("    " + frame.ToString());

                break;
            }

            Thread.Sleep(sampleInterval);
        }
    }
}
