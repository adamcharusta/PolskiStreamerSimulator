using System.Reflection;
using System.Reflection.Emit;

namespace PolskiStreamerSimulator.Tests.Architecture;

public class LayerDependencyTests
{
    private static readonly Assembly DomainAssembly = typeof(PolskiStreamerSimulator.Domain.AssemblyMarker).Assembly;

    private static readonly HashSet<string> ForbiddenTypes =
    [
        "System.Random",
        "System.DateTime",
        "System.DateTimeOffset",
        "System.Single",
        "System.Double",
        "System.Decimal",
    ];

    private static readonly HashSet<string> ForbiddenMathMethods = ["Sqrt", "Log", "Log10"];

    [Fact]
    public void Domain_has_no_references_outside_the_BCL()
    {
        var refs = DomainAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();
        Assert.All(refs, r => Assert.True(r.StartsWith("System", StringComparison.Ordinal) || r == "netstandard" || r == "mscorlib", r));
    }

    [Fact]
    public void Application_does_not_reference_Infrastructure_or_WebApp()
    {
        var refs = Assembly.Load("PolskiStreamerSimulator.Application")
            .GetReferencedAssemblies()
            .Select(a => a.Name!)
            .ToArray();
        Assert.DoesNotContain("PolskiStreamerSimulator.Infrastructure", refs);
        Assert.DoesNotContain("PolskiStreamerSimulator.WebApp", refs);
    }

    [Fact]
    public void Domain_IL_does_not_use_forbidden_types()
    {
        var offenders = new SortedSet<string>(StringComparer.Ordinal);
        // Coverage runs inject a coverlet hit tracker into the instrumented assembly; it is not Domain code.
        foreach (var type in DomainAssembly.GetTypes().Where(t => t.Namespace?.StartsWith("Coverlet.", StringComparison.Ordinal) != true))
        {
            InspectType(type, offenders);
        }

        Assert.True(offenders.Count == 0, "Forbidden usages in Domain:\n" + string.Join("\n", offenders));
    }

    private const BindingFlags All =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private static void InspectType(Type type, SortedSet<string> offenders)
    {
        foreach (var field in type.GetFields(All))
        {
            Check(field.FieldType, $"{type.FullName}.{field.Name} (field)", offenders);
        }

        foreach (var property in type.GetProperties(All))
        {
            Check(property.PropertyType, $"{type.FullName}.{property.Name} (property)", offenders);
        }

        var methods = type.GetMethods(All).Cast<MethodBase>().Concat(type.GetConstructors(All));
        foreach (var method in methods)
        {
            var where = $"{type.FullName}.{method.Name}";
            if (method is MethodInfo info)
            {
                Check(info.ReturnType, $"{where} (return)", offenders);
            }

            foreach (var parameter in method.GetParameters())
            {
                Check(parameter.ParameterType, $"{where} (parameter {parameter.Name})", offenders);
            }

            InspectBody(type, method, where, offenders);
        }
    }

    private static void InspectBody(Type type, MethodBase method, string where, SortedSet<string> offenders)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            return;
        }

        foreach (var local in body.LocalVariables)
        {
            Check(local.LocalType, $"{where} (local)", offenders);
        }

        var il = body.GetILAsByteArray() ?? [];
        var typeArgs = type.IsGenericType ? type.GetGenericArguments() : null;
        var methodArgs = method.IsGenericMethod ? method.GetGenericArguments() : null;
        var position = 0;
        while (position < il.Length)
        {
            var opCode = ReadOpCode(il, ref position);
            if (opCode.OperandType is OperandType.InlineR or OperandType.ShortInlineR
                || opCode == OpCodes.Conv_R4 || opCode == OpCodes.Conv_R8 || opCode == OpCodes.Conv_R_Un)
            {
                offenders.Add($"{where} (opcode {opCode.Name})");
            }

            if (opCode.OperandType is OperandType.InlineField or OperandType.InlineMethod
                or OperandType.InlineTok or OperandType.InlineType)
            {
                var token = BitConverter.ToInt32(il, position);
                var member = method.Module.ResolveMember(token, typeArgs, methodArgs);
                CheckMember(member, $"{where} (IL)", offenders);
            }

            position += OperandSize(opCode, il, position);
        }
    }

    private static void CheckMember(MemberInfo? member, string where, SortedSet<string> offenders)
    {
        switch (member)
        {
            case Type t:
                Check(t, where, offenders);
                break;
            case FieldInfo f:
                Check(f.DeclaringType!, where, offenders);
                Check(f.FieldType, where, offenders);
                break;
            case MethodBase m:
                if (m.DeclaringType == typeof(Math) && ForbiddenMathMethods.Contains(m.Name))
                {
                    offenders.Add($"{where}: System.Math.{m.Name}");
                }

                if (m.DeclaringType is not null)
                {
                    Check(m.DeclaringType, where, offenders);
                }

                if (m is MethodInfo mi)
                {
                    Check(mi.ReturnType, where, offenders);
                }

                foreach (var p in m.GetParameters())
                {
                    Check(p.ParameterType, where, offenders);
                }

                break;
            default:
                break;
        }
    }

    private static void Check(Type type, string where, SortedSet<string> offenders)
    {
        if (type.HasElementType)
        {
            Check(type.GetElementType()!, where, offenders);
            return;
        }

        if (type.IsGenericParameter)
        {
            return;
        }

        if (type.IsGenericType)
        {
            foreach (var argument in type.GetGenericArguments())
            {
                Check(argument, where, offenders);
            }
        }

        var name = type.IsGenericType ? type.GetGenericTypeDefinition().FullName : type.FullName;
        if (name is not null && ForbiddenTypes.Contains(name))
        {
            offenders.Add($"{where}: {name}");
        }
    }

    private static readonly Dictionary<short, OpCode> OpCodesByValue = typeof(OpCodes)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)!)
        .ToDictionary(o => o.Value);

    private static OpCode ReadOpCode(byte[] il, ref int position)
    {
        short value = il[position++];
        if (value == 0xFE)
        {
            value = unchecked((short)(0xFE00 | il[position++]));
        }

        return OpCodesByValue[value];
    }

    private static int OperandSize(OpCode opCode, byte[] il, int position) => opCode.OperandType switch
    {
        OperandType.InlineNone => 0,
        OperandType.ShortInlineBrTarget or OperandType.ShortInlineI or OperandType.ShortInlineVar => 1,
        OperandType.InlineVar => 2,
        OperandType.InlineI8 or OperandType.InlineR => 8,
        OperandType.InlineSwitch => 4 + (4 * BitConverter.ToInt32(il, position)),
        _ => 4,
    };
}
