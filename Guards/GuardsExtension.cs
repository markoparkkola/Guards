using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using System.Reflection;

namespace Guards
{
    [AttributeUsage(AttributeTargets.Method)]
    public class GuardsAttribute : Attribute
    {
        public GuardsAttribute(params string[] tests)
        {
            Tests = tests;
        }

        internal string[] Tests { get; init; }
    }

    public class GuardsException : Exception
    {
        public GuardsException(string test)
            : base(test)
        {

        }
    }

    public static class Guards
    {
        public static (bool Success, string? Error) Test(object? instance, 
            MethodCallExpression methodCall, 
            string[] tests)
        {
            foreach (var test in tests)
            {
                var parameterDeclarations = GetParameterDeclarations(test, methodCall.Method, methodCall.Arguments);
                if (parameterDeclarations == null)
                {
                    return (false, test);
                }

                var code = @$"
namespace GuardsTestNS
{{
    public static class GuardsTest
    {{
        {parameterDeclarations}

        public static bool Run() => {test};
    }}
}}";

                var tree = CSharpSyntaxTree.ParseText(code);
                var systemRefLocation = typeof(object).GetTypeInfo().Assembly.Location;
                var systemReference = MetadataReference.CreateFromFile(systemRefLocation);

                var compilation = CSharpCompilation.Create(null)
                    .WithOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, false))
                    .AddReferences(systemReference)
                    .AddSyntaxTrees(tree);

                using (var ms = new MemoryStream())
                {
                    var compilationResult = compilation.Emit(ms);
                    if (!compilationResult.Success)
                        return (false, test);

                    ms.Seek(0, SeekOrigin.Begin);
                    var assembly = Assembly.Load(ms.ToArray());

                    var type = assembly.GetType("GuardsTestNS.GuardsTest")!;
                    var method = type.GetMethod("Run")!;
                    if (!(bool)method.Invoke(null, null)!)
                    {
                        return (false, test);
                    }
                }
            
            }
            return (true, null);
        }

        private static string? GetParameterDeclarations(string test, MethodInfo method, ReadOnlyCollection<Expression> arguments)
        {
            var tree = CSharpSyntaxTree.ParseText(test);
            if (!tree.TryGetRoot(out var root))
            {
                return null;
            }

            var methodArgumentValues = method.GetParameters().Select((p, i) => new { Identifier = p.Name!, Value = arguments[i] })?.ToDictionary(key => key.Identifier, value => value.Value);
            var identifiers = root.DescendantNodes().OfType<IdentifierNameSyntax>().Select(x => x.Identifier.ValueText);
            var parameterIdentifiers = method.GetParameters().Where(x => identifiers.Contains(x.Name));

            return String.Join("\r\n", identifiers.Select(i =>
            {
                if (methodArgumentValues?.TryGetValue(i, out var value) ?? false)
                {
                    if (value is ConstantExpression ce)
                        return $"private static readonly {value.Type.FullName} {i} = {ce.Value};";
                }
                return "";
            }));
        }
    }

    public static class GuardsExtension
    {
        public static void Call<T>(this T instance, Expression<Action<T>> expression)
        {
            var methodCall = (MethodCallExpression)expression.Body;
            var attributes = methodCall.Method.GetCustomAttributes(typeof(GuardsAttribute), false).Cast<GuardsAttribute>();
            if (attributes.Any())
            {
                var tests = attributes.SelectMany(x => x.Tests).ToArray();
                var results = Guards.Test(instance, methodCall, tests);
                if (!results.Success)
                {
                    throw new GuardsException(results.Error!);
                }
            }

            var type = expression.Body.GetType();
            var deleg = expression.Compile();
            deleg.Invoke(instance);
        }

        public static TResult Call<T, TResult>(this T instance, Expression<Func<T, TResult>> expression)
        {
            var methodCall = (MethodCallExpression)expression.Body;
            var attributes = methodCall.Method.GetCustomAttributes(typeof(GuardsAttribute), false).Cast<GuardsAttribute>();
            if (attributes.Any())
            {
                var tests = attributes.SelectMany(x => x.Tests).ToArray();
                var results = Guards.Test(instance, methodCall, tests);
                if (!results.Success)
                {
                    throw new GuardsException(results.Error!);
                }
            }

            var type = expression.Body.GetType();
            var deleg = expression.Compile();
            return deleg.Invoke(instance);
        }
    }
}
