using System;
using System.IO;
using System.Threading;
public class OllamaProbe {
    public static void Main(string[] args) {
        var memory=new byte[20*1024*1024];
        for(int i=0;i<memory.Length;i+=4096)memory[i]=1;
        File.AppendAllText(Environment.GetEnvironmentVariable("GAMEQUIET_PROBE_LOG"),"started:"+System.Diagnostics.Process.GetCurrentProcess().Id+":"+string.Join(" ",args)+Environment.NewLine);
        Thread.Sleep(Timeout.Infinite);
        GC.KeepAlive(memory);
    }
}
