"""Actual javac + installed Android/Unity references; JVM URL checks, never JNI proof."""
import os
import hashlib
from pathlib import Path
import subprocess
import unittest

ROOT = Path(__file__).resolve().parents[2]
UNITY = Path('C:/Program Files/Unity/Hub/Editor/6000.5.3f1/Editor/Data/PlaybackEngines/AndroidPlayer')
OUTPUT = Path('C:/Work/scratch/remote-config-service-2026-10-14/client/repair/java-boundary')

class NativeAdapterTests(unittest.TestCase):
    def test_actual_managed_host_cancel_destroy_races_without_unity_native_execution(self):
        # Execute the shipping Request/OnDestroy methods, but bypass native construction.
        # This is a managed boundary probe, never a Unity runtime or device assertion.
        copy = OUTPUT.parents[1] / 'repair-source'
        host = copy / 'integration/RemoteConfig/Client/UnityConfigurationHost.cs'
        self.assertEqual(hashlib.sha256(host.read_bytes()).digest(), hashlib.sha256((ROOT / 'integration/RemoteConfig/Client/UnityConfigurationHost.cs').read_bytes()).digest())
        subprocess.run(['rtk','proxy','dotnet','build','integration/RemoteConfig/Client/ClientBridgeCompile.csproj','-c','Release','--nologo','-p:DefineConstants=UNITY_EDITOR'],cwd=copy,check=True,capture_output=True)
        probe = OUTPUT.parent / 'managed-host-boundary'; probe.mkdir(parents=True,exist_ok=True)
        references = {
            'ClientBridgeCompile': copy / 'integration/RemoteConfig/Client/bin/Release/netstandard2.1/ClientBridgeCompile.dll',
            'GamePlatform.Configuration': copy / 'src/GamePlatform.Configuration/bin/Release/netstandard2.1/GamePlatform.Configuration.dll',
            'GamePlatform.Configuration.Client': copy / 'src/GamePlatform.Configuration.Client/bin/Release/netstandard2.1/GamePlatform.Configuration.Client.dll',
            'UnityEngine.CoreModule': UNITY.parents[1] / 'Managed/UnityEngine/UnityEngine.CoreModule.dll',
            'Unity.Scripting': UNITY.parents[1] / 'Managed/UnityEngine/Unity.Scripting.dll',
        }
        project = '<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><TargetFramework>net9.0</TargetFramework><OutputType>Exe</OutputType><EnableDefaultCompileItems>false</EnableDefaultCompileItems></PropertyGroup><ItemGroup><Compile Include="Probe.cs"/>'
        project += ''.join(f'<Reference Include="{name}"><HintPath>{path.as_posix()}</HintPath></Reference>' for name,path in references.items())
        (probe / 'Probe.csproj').write_text(project+'</ItemGroup></Project>',encoding='utf-8')
        (probe / 'Probe.cs').write_text('''using System;using System.Collections.Concurrent;using System.Reflection;using System.Runtime.CompilerServices;using System.Threading;using System.Threading.Tasks;
using GamePlatform.Configuration.Client;using GamePlatform.Configuration.Unity;
class Probe {
 static readonly BindingFlags Flags=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Field(object host,string name,object value)=>host.GetType().GetField(name,Flags).SetValue(host,value);
 static int Main(){ThreadPool.SetMinThreads(4,4);var type=typeof(UnityConfigurationHost);var request=type.GetMethod("Request",Flags);var destroy=type.GetMethod("OnDestroy",Flags);
  for(int i=0;i<500;i++){var host=RuntimeHelpers.GetUninitializedObject(type);Field(host,"sync",new object());Field(host,"actions",new ConcurrentQueue<Action>());
   foreach(var name in new[]{"pending","delays"})Field(host,name,Activator.CreateInstance(type.GetField(name,Flags).FieldType));
   Field(host,"policy",new ConfigurationClientPolicy(new Uri("https://normal.example/app/"),new Uri("https://control.example/app/")));
   using var cancellation=new CancellationTokenSource();var pending=(Task<string>)request.Invoke(host,new object[]{new Action<string>(_=>throw new Exception("native_dispatch_forbidden")),cancellation.Token});
   using var start=new ManualResetEventSlim();var cancel=Task.Run(()=>{start.Wait();cancellation.Cancel();});var teardown=Task.Run(()=>{start.Wait();destroy.Invoke(host,null);});start.Set();
   if(!Task.WhenAll(cancel,teardown).Wait(TimeSpan.FromSeconds(5))||!pending.IsCanceled){Console.Error.WriteLine("teardown_race_failed:"+i);return 1;}
  }Console.WriteLine("shipping_managed_host_cancel_destroy_races=500 UnityNative=unrun");return 0;
 }
}''',encoding='utf-8')
        subprocess.run(['rtk','proxy','dotnet','build',str(probe/'Probe.csproj'),'-c','Release','--nologo'],check=True,capture_output=True)
        result=subprocess.run(['rtk','proxy','dotnet',str(probe/'bin/Release/net9.0/Probe.dll')],check=True,capture_output=True,text=True,timeout=30)
        self.assertIn('shipping_managed_host_cancel_destroy_races=500 UnityNative=unrun',result.stdout);print(result.stdout.strip())

    def test_real_java_compile_and_origin_path_policy(self):
        OUTPUT.mkdir(parents=True, exist_ok=True)
        jar = UNITY / 'SDK/platforms/android-36/android.jar'
        unity = UNITY / 'Variations/il2cpp/Release/Classes/classes.jar'
        javac = UNITY / 'OpenJDK/bin/javac.exe'
        java = UNITY / 'OpenJDK/bin/java.exe'
        for path in (jar, unity, javac, java):
            self.assertTrue(path.is_file(), str(path))
        probe = OUTPUT / 'BoundaryProbe.java'
        probe.write_text('''import java.lang.reflect.*; import java.net.URI;
import com.gindemit.configuration.ConfigurationHttp;
public final class BoundaryProbe {
 public static void main(String[] args) throws Exception {
  ConfigurationHttp value = new ConfigurationHttp("https://normal.example/app/", "https://control.example/app/", false);
  Method slot = ConfigurationHttp.class.getDeclaredMethod("slot", URI.class); slot.setAccessible(true);
  int accepted = 0; int rejected = 0;
  String sha = new String(new char[64]).replace('\\0', 'a');
  for (String url : new String[]{"https://normal.example/app/current.json", "https://normal.example/app/assets/"+sha+".json", "https://control.example/app/control.json"}) { slot.invoke(value, URI.create(url)); accepted++; }
  for (String url : new String[]{"https://other.example/app/current.json", "http://normal.example/app/current.json", "https://normal.example/other/current.json", "https://normal.example/app/../escape", "https://normal.example/app/current.json?account=private", "https://user:secret@normal.example/app/current.json", "https://normal.example/app/control.json"}) {
   try { slot.invoke(value, URI.create(url)); throw new AssertionError(url); } catch (InvocationTargetException expected) { if (!(expected.getCause() instanceof IllegalArgumentException)) throw expected; rejected++; }
  }
  value.dispose();
  java.net.CookieHandler.setDefault(new java.net.CookieManager());
  try { new ConfigurationHttp("https://normal.example/app/", "https://control.example/app/", false); throw new AssertionError("ambient cookies"); } catch (IllegalStateException expected) { rejected++; } finally { java.net.CookieHandler.setDefault(null); }
  System.out.println("accepted="+accepted+" rejected="+rejected+" JNI=unrun");
 }
}
''', encoding='utf-8')
        classpath = os.pathsep.join(map(str, (jar, unity)))
        source = ROOT / 'integration/RemoteConfig/Client/ConfigurationHttp.java'
        subprocess.run(['rtk', 'proxy', str(javac), '--release', '8', '-classpath', classpath, '-d', str(OUTPUT), str(source), str(probe)], check=True)
        result = subprocess.run(['rtk', 'proxy', str(java), '-classpath', os.pathsep.join((str(OUTPUT), classpath)), 'BoundaryProbe'], check=True, text=True, capture_output=True)
        self.assertIn('accepted=3 rejected=8 JNI=unrun', result.stdout)
        print(result.stdout.strip())

if __name__ == '__main__': unittest.main()
