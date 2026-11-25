using System.Reflection;
using System.Runtime.Loader;

// Intentionally no reference to the modules or Harmony. Lazer inspects all
// assembly types before constructing the carrier that loads embedded Harmony.
int checks = 0;
void Check(bool condition, string label)
{
	if (!condition) throw new Exception(label);
	Console.WriteLine("PASS " + label);
	checks++;
}
string folder = Path.GetDirectoryName(Path.GetFullPath(args[0]))!;
AssemblyLoadContext.Default.Resolving += (context, name) =>
{
	string dependency = Path.Combine(folder, name.Name + ".dll");
	if (File.Exists(dependency)) return context.LoadFromAssemblyPath(dependency);
	return null;
};
Check(!File.Exists(Path.Combine(folder, "0Harmony.dll")), "no adjacent Harmony DLL");
Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "0Harmony"), "Harmony absent before discovery");
List<(Assembly Assembly, Type Carrier)> modules = new();
foreach (string path in args)
{
	Assembly assembly = Assembly.LoadFrom(Path.GetFullPath(path));
	Type[] types = assembly.GetTypes();
	Type carrier = types.Single(t => t.IsPublic && t.BaseType?.FullName == "osu.Game.Rulesets.Ruleset");
	modules.Add((assembly, carrier));
	Check(types.Length > 0, assembly.GetName().Name + " cold GetTypes succeeds");
}
Check(!AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "0Harmony"), "discovery of all module types requires no Harmony");
foreach ((Assembly assembly, Type carrier) in modules)
{
	Check(Activator.CreateInstance(carrier) != null, assembly.GetName().Name + " carrier constructs");
	Type integration = assembly.GetType(carrier.Namespace + ".Integration", true)!;
	Check((bool)integration.GetProperty("Installed")!.GetValue(null)!, assembly.GetName().Name + " all patches installed");
	Check(!assembly.GetReferencedAssemblies().Any(a => a.Name == "osu.Game.Rulesets.Shred" || modules.Any(m => m.Assembly != assembly && m.Assembly.GetName().Name == a.Name)), assembly.GetName().Name + " no dependency on other optional DLL or Shred");
}
Assembly harmony = AppDomain.CurrentDomain.GetAssemblies().Single(a => a.GetName().Name == "0Harmony");
Check(harmony.Location.Length == 0, "one shared Harmony loaded from embedded resource");
Console.WriteLine($"SUCCESS: {checks} cold discovery checks");
