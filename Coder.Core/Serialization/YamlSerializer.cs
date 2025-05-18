// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Serialization;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;
using ktsu.Coder.Core.Ast;
using System.Collections.Generic;

/// <summary>
/// Handles serialization of AST nodes to YAML format.
/// </summary>
public class YamlSerializer
{
    private readonly ISerializer _serializer;

    /// <summary>
    /// Initializes a new instance of the <see cref="YamlSerializer"/> class.
    /// </summary>
    public YamlSerializer()
    {
        _serializer = new SerializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .DisableAliases()
            .ConfigureDefaultValuesHandling(DefaultValuesHandling.OmitNull | DefaultValuesHandling.OmitEmptyCollections)
            .Build();
    }

    /// <summary>
    /// Serializes an AST node into a YAML string.
    /// </summary>
    /// <param name="node">The AST node to serialize.</param>
    /// <returns>A YAML string representation of the AST node.</returns>
    public string Serialize(AstNode node)
    {
        if (node == null)
        {
            throw new ArgumentNullException(nameof(node));
        }

        // Create a dictionary to represent the YAML structure
        var root = new Dictionary<string, object>();
        SerializeNode(node, root);

        // Serialize the dictionary to YAML
        return _serializer.Serialize(root);
    }

    private void SerializeNode(AstNode node, Dictionary<string, object> target)
    {
        // Add the node type and properties to the dictionary
        var nodeKey = node.GetNodeTypeName();
        var nodeData = new Dictionary<string, object>();

        // Handle leaf nodes
        if (node is AstLeafNode<string> stringLeaf && stringLeaf.Value != null)
        {
            target[nodeKey] = stringLeaf.Value;
            return;
        }
        else if (node is AstLeafNode<int> intLeaf)
        {
            target[nodeKey] = intLeaf.Value;
            return;
        }
        else if (node is AstLeafNode<bool> boolLeaf)
        {
            target[nodeKey] = boolLeaf.Value;
            return;
        }
        
        // Handle function declarations and parameters
        if (node is FunctionDeclaration funcDecl)
        {
            if (funcDecl.Name != null)
            {
                nodeData["name"] = funcDecl.Name;
            }
            
            if (funcDecl.ReturnType != null)
            {
                nodeData["returnType"] = funcDecl.ReturnType;
            }
            
            if (funcDecl.Parameters.Count > 0)
            {
                var parameters = new List<Dictionary<string, object>>();
                foreach (var param in funcDecl.Parameters)
                {
                    var paramData = new Dictionary<string, object>();
                    if (param.Name != null)
                    {
                        paramData["name"] = param.Name;
                    }
                    
                    if (param.Type != null)
                    {
                        paramData["type"] = param.Type;
                    }
                    
                    if (param.IsOptional)
                    {
                        paramData["isOptional"] = param.IsOptional;
                        
                        if (param.DefaultValue != null)
                        {
                            paramData["defaultValue"] = param.DefaultValue;
                        }
                    }
                    
                    parameters.Add(paramData);
                }
                
                nodeData["parameters"] = parameters;
            }
            
            if (funcDecl.Body.Count > 0)
            {
                var bodyStatements = new List<Dictionary<string, object>>();
                foreach (var statement in funcDecl.Body)
                {
                    var statementData = new Dictionary<string, object>();
                    SerializeNode(statement, statementData);
                    bodyStatements.Add(statementData);
                }
                
                nodeData["body"] = bodyStatements;
            }
        }
        else if (node is Parameter param)
        {
            if (param.Name != null)
            {
                nodeData["name"] = param.Name;
            }
            
            if (param.Type != null)
            {
                nodeData["type"] = param.Type;
            }
            
            if (param.IsOptional)
            {
                nodeData["isOptional"] = param.IsOptional;
                
                if (param.DefaultValue != null)
                {
                    nodeData["defaultValue"] = param.DefaultValue;
                }
            }
        }
        else if (node is ReturnStatement returnStmt)
        {
            if (returnStmt.Expression != null)
            {
                var expressionData = new Dictionary<string, object>();
                SerializeNode(returnStmt.Expression, expressionData);
                nodeData["expression"] = expressionData;
            }
        }
        
        // Handle composite nodes
        if (node is AstCompositeNode compositeNode)
        {
            foreach (var (key, childNode) in compositeNode.Children)
            {
                if (!key.Equals("Expression", StringComparison.OrdinalIgnoreCase)) // Already handled above
                {
                    var childData = new Dictionary<string, object>();
                    SerializeNode(childNode, childData);
                    nodeData[key.ToLowerInvariant()] = childData;
                }
            }
        }
        
        // Add metadata if present
        if (node.Metadata != null && node.Metadata.Count > 0)
        {
            nodeData["metadata"] = node.Metadata;
        }
        
        target[nodeKey] = nodeData;
    }
} 