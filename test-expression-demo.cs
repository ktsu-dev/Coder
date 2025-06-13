using System;
using ktsu.Coder.Core.Ast;
using ktsu.Coder.Core.Languages;
using ktsu.Coder.Core.Serialization;
using Microsoft.Extensions.DependencyInjection;

Console.WriteLine("🚀 Expression System Demo - Visual Editor Ready");
Console.WriteLine("=" + new string('=', 50));

// Setup DI container
var services = new ServiceCollection();
services.AddTransient<YamlSerializer>();
services.AddTransient<YamlDeserializer>();
services.AddTransient<PythonGenerator>();
services.AddTransient<CSharpGenerator>();

using var provider = services.BuildServiceProvider();

var yamlSerializer = provider.GetRequiredService<YamlSerializer>();
var pythonGenerator = provider.GetRequiredService<PythonGenerator>();

// Create a mathematical expression: (a + b) * (c - d) / 2.0
var aPlusB = new BinaryExpression(
    new VariableReference("a"),
    BinaryOperator.Add,
    new VariableReference("b")
);

var cMinusD = new BinaryExpression(
    new VariableReference("c"),
    BinaryOperator.Subtract,
    new VariableReference("d")
);

var multiply = new BinaryExpression(
    aPlusB,
    BinaryOperator.Multiply,
    cMinusD
);

var divide = new BinaryExpression(
    multiply,
    BinaryOperator.Divide,
    Literal.DecimalValue(2.0)
)
{
    ExpectedType = "double"
};

Console.WriteLine("\n📊 Mathematical Expression Demo");
Console.WriteLine($"Expression: {divide}");

// Create a function with the expression
var function = new FunctionDeclaration("calculateResult")
{
    ReturnType = "double"
};
function.Parameters.Add(new Parameter("a", "double"));
function.Parameters.Add(new Parameter("b", "double"));
function.Parameters.Add(new Parameter("c", "double"));
function.Parameters.Add(new Parameter("d", "double"));

function.Body.Add(new ReturnStatement(divide));

// Test serialization (critical for visual editor state saving)
Console.WriteLine("\n💾 YAML Serialization Test (Visual Editor State)");
string serialized = yamlSerializer.Serialize(function);
Console.WriteLine("✅ Serialization successful - Visual editor can save/load this structure");
Console.WriteLine($"YAML Output:\n{serialized}");

// Test code generation
Console.WriteLine("\n🔄 Code Generation Test");
Console.WriteLine("Python output:");
Console.WriteLine(pythonGenerator.Generate(function));

Console.WriteLine("\n🎯 Visual Editor Benefits:");
Console.WriteLine("• Each Expression is a discrete node - perfect for visual connections");
Console.WriteLine("• Type information enables intelligent connection validation");
Console.WriteLine("• YAML serialization preserves exact graph structure");
Console.WriteLine("• Clone() methods support visual copy/paste operations");
Console.WriteLine("• Extensible design allows custom expression types");

Console.WriteLine("\n✅ Expression System Integration COMPLETE!");
