using System;
using System.IO;
using System.Collections.Generic;
using System.Web.Script.Serialization;
using System.Text.RegularExpressions;

public class FakeCli {
    public static void Main(string[] args) {
        var prompt=Console.In.ReadToEnd();
        var json=new JavaScriptSerializer();
        var index=prompt.IndexOf("Snapshot:\n",StringComparison.Ordinal);
        var rows=json.Deserialize<List<Dictionary<string,object>>>(prompt.Substring(index+10));
        var advice=new List<object>();
        foreach(var row in rows) {
            if((string)row["name"]=="GqProbe") advice.Add(new { id=(string)row["id"],recommendation="close",confidence=99,reason="Disposable background test workload, safe to close normally." });
        }
        var result=new {summary="Assessment complete using a deterministic provider fixture.",workloads=advice};
        File.WriteAllText(Path.Combine(Environment.GetEnvironmentVariable("GAMEQUIET_TEST_ROOT"),"provider-cwd.txt"),Environment.CurrentDirectory);
        if(Array.IndexOf(args,"--print")>=0) Console.WriteLine(json.Serialize(new {structured_output=result,is_error=false}));
        else Console.WriteLine(json.Serialize(new {type="item.completed",item=new {type="agent_message",text=json.Serialize(result)}}));
    }
}
