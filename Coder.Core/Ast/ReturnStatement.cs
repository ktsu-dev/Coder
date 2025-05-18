// Copyright (c) ktsu.dev
// All rights reserved.
// Licensed under the MIT license.

namespace ktsu.Coder.Core.Ast;

using System.Collections.Generic;

/// <summary>
/// Represents a return statement in a function body.
/// </summary>
public class ReturnStatement : AstCompositeNode
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnStatement"/> class.
    /// </summary>
    public ReturnStatement()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ReturnStatement"/> class with a value.
    /// </summary>
    /// <param name="value">The value to return.</param>
    public ReturnStatement(object value)
    {
        if (value is AstNode node)
        {
            SetChild("Expression", node);
        }
        else if (value is int intValue)
        {
            SetChild("Expression", new AstLeafNode<int>(intValue));
        }
        else if (value is string stringValue)
        {
            SetChild("Expression", new AstLeafNode<string>(stringValue));
        }
        else if (value is bool boolValue)
        {
            SetChild("Expression", new AstLeafNode<bool>(boolValue));
        }
        else
        {
            SetChild("Expression", new AstLeafNode<string>(value.ToString() ?? string.Empty));
        }
    }

    /// <summary>
    /// Gets the expression being returned.
    /// </summary>
    public AstNode? Expression => GetChild("Expression");

    /// <summary>
    /// Sets the expression being returned.
    /// </summary>
    /// <param name="expression">The expression to set.</param>
    public void SetExpression(AstNode expression)
    {
        SetChild("Expression", expression);
    }

    /// <summary>
    /// Gets the type name of this node for serialization purposes.
    /// </summary>
    /// <returns>The name of the node type.</returns>
    public override string GetNodeTypeName()
    {
        return "ReturnStatement";
    }

    /// <summary>
    /// Creates a deep clone of this return statement.
    /// </summary>
    /// <returns>A new instance of the return statement with the same properties and cloned children.</returns>
    public override AstNode Clone()
    {
        var clone = new ReturnStatement
        {
            Metadata = Metadata != null ? new Dictionary<string, object?>(Metadata) : null
        };

        // Clone the children dictionary which includes the Expression
        foreach (var (key, child) in Children)
        {
            clone.Children[key] = child.Clone();
        }

        return clone;
    }
} 