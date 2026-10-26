// Minimal NUnit runner for the Unity-free Core tests (used by check.sh; Unity's Test Runner is the real one).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

public static class CoreTestRunner
{
    public static int Main()
    {
        int pass = 0, fail = 0;
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "UtsavNights.Tests"))
        {
            foreach (MethodInfo m in type.GetMethods())
            {
                var cases = new List<object[]>();
                if (m.GetCustomAttributes(typeof(TestAttribute), false).Any())
                {
                    ParameterInfo[] p = m.GetParameters();
                    if (p.Length == 0)
                    {
                        cases.Add(new object[0]);
                    }
                    else
                    {
                        var source = (ValueSourceAttribute)p[0].GetCustomAttributes(typeof(ValueSourceAttribute), false).First();
                        var values = (IEnumerable)type.GetMethod(source.SourceName, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic).Invoke(null, null);
                        foreach (object v in values)
                        {
                            cases.Add(new[] { v });
                        }
                    }
                }

                foreach (TestCaseAttribute tc in m.GetCustomAttributes(typeof(TestCaseAttribute), false))
                {
                    cases.Add(tc.Arguments);
                }

                foreach (TestCaseSourceAttribute src in m.GetCustomAttributes(typeof(TestCaseSourceAttribute), false))
                {
                    var values = (IEnumerable)type.GetField(src.SourceName, BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public).GetValue(null);
                    foreach (object v in values)
                    {
                        cases.Add((object[])v);
                    }
                }

                foreach (object[] args in cases)
                {
                    string name = type.Name + "." + m.Name + (args.Length > 0 ? "(" + string.Join(",", args) + ")" : string.Empty);
                    try
                    {
                        m.Invoke(Activator.CreateInstance(type), args);
                        pass++;
                    }
                    catch (TargetInvocationException e)
                    {
                        fail++;
                        Console.WriteLine("FAIL " + name + ": " + e.InnerException?.Message);
                    }
                }
            }
        }

        Console.WriteLine($"Core tests: passed={pass} failed={fail}");
        return fail == 0 ? 0 : 1;
    }
}
