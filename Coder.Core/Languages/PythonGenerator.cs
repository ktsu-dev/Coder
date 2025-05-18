// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Languages;

using System;
using System.Text;
using ktsu.Coder.Core.Ast;

/// <summary>
/// Generates Python code from AST nodes.
/// </summary>
public class PythonGenerator : LanguageGeneratorBase
{
    /// <summary>
    /// Gets the unique identifier for this language generator.
    /// </summary>
    public override string LanguageId => "python";
    
    /// <summary>
    /// Gets the display name for this language generator.
    /// </summary>
    public override string DisplayName => "Python";
    
    /// <summary>
    /// Gets the file extension (without the dot) used for this language.
    /// </summary>
    public override string FileExtension => "py";
    
    /// <summary>
    /// Generates Python code with proper indentation.
    /// </summary>
    /// <param name="node">The AST node to generate code from.</param>
    /// <param name="builder">The string builder to append code to.</param>
    /// <param name="indentLevel">The current indentation level.</param>
    protected override void GenerateInternal(AstNode node, StringBuilder builder, int indentLevel)
    {
        switch (node)
        {
            case FunctionDeclaration funcDecl:
                GenerateFunctionDeclaration(funcDecl, builder, indentLevel);
                break;
                
            case ReturnStatement returnStmt:
                GenerateReturnStatement(returnStmt, builder, indentLevel);
                break;
                
            case AstLeafNode<string> strLeaf:
                builder.Append($"\"{EscapeString(strLeaf.Value ?? string.Empty)}\"");
                break;
                
            case AstLeafNode<int> intLeaf:
                builder.Append(intLeaf.Value);
                break;
                
            case AstLeafNode<bool> boolLeaf:
                builder.Append(boolLeaf.Value ? "True" : "False");
                break;
                
            default:
                throw new NotSupportedException($"Unsupported node type for Python generation: {node.GetNodeTypeName()}");
        }
    }
    
    private void GenerateFunctionDeclaration(FunctionDeclaration funcDecl, StringBuilder builder, int indentLevel)
    {
        // Function signature
        Indent(builder, indentLevel);
        builder.Append("def ");
        builder.Append(funcDecl.Name ?? "unnamed_function");
        builder.Append('(');
        
        // Parameters
        for (int i = 0; i < funcDecl.Parameters.Count; i++)
        {
            var param = funcDecl.Parameters[i];
            
            if (i > 0)
            {
                builder.Append(", ");
            }
            
            builder.Append(param.Name ?? $"param{i}");
            
            // Add type hints if available
            if (param.Type != null)
            {
                builder.Append(": ");
                builder.Append(PythonTypeFromGenericType(param.Type));
            }
            
            // Add default value if optional
            if (param.IsOptional && param.DefaultValue != null)
            {
                builder.Append(" = ");
                builder.Append(param.DefaultValue);
            }
        }
        
        builder.Append(')');
        
        // Add return type hint if available
        if (funcDecl.ReturnType != null)
        {
            builder.Append(" -> ");
            builder.Append(PythonTypeFromGenericType(funcDecl.ReturnType));
        }
        
        builder.AppendLine(":");
        
        // Function body
        if (funcDecl.Body.Count == 0)
        {
            // Empty function body needs a pass statement
            Indent(builder, indentLevel + 1);
            builder.AppendLine("pass");
        }
        else
        {
            // Generate each statement in the body
            foreach (var statement in funcDecl.Body)
            {
                GenerateInternal(statement, builder, indentLevel + 1);
                builder.AppendLine();
            }
        }
    }
    
    private void GenerateReturnStatement(ReturnStatement returnStmt, StringBuilder builder, int indentLevel)
    {
        Indent(builder, indentLevel);
        builder.Append("return");
        
        if (returnStmt.Expression != null)
        {
            builder.Append(' ');
            GenerateInternal(returnStmt.Expression, builder, 0); // No indentation for the expression
        }
    }
    
    private string PythonTypeFromGenericType(string genericType)
    {
        return genericType.ToLowerInvariant() switch
        {
            "int" => "int",
            "string" => "str",
            "bool" => "bool",
            "float" => "float",
            "double" => "float",
            "void" => "None",
            _ => genericType
        };
    }
    
    private string EscapeString(string value)
    {
        return value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", "\\n")
            .Replace("\r", "\\r")
            .Replace("\t", "\\t");
    }
} 