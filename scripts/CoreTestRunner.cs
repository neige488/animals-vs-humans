using System;
using System.Reflection;
class CoreTestRunner {
 static int Main() {
  int failed=0, passed=0;
  foreach(var type in Assembly.GetExecutingAssembly().GetTypes()) foreach(var method in type.GetMethods()) {
   if(!Attribute.IsDefined(method, typeof(NUnit.Framework.TestAttribute))) continue;
   try { method.Invoke(Activator.CreateInstance(type), null); Console.WriteLine("PASS "+method.Name); passed++; }
   catch(Exception ex) { Console.WriteLine("FAIL "+method.Name+" " +(ex.InnerException ?? ex)); failed++; }
  }
  Console.WriteLine($"{passed} passed, {failed} failed (standalone core tests; not Unity physics)");
  return failed>0 || passed==0 ? 1 : 0;
 }
}
