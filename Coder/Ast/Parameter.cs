// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Ast;

using System;
using System.Globalization;
using System.Text;

/// <summary>
/// Represents a parameter in a function declaration.
/// </summary>
public class Parameter : AstNode
{
	/// <summary>
	/// Initializes a new instance of the <see cref="Parameter"/> class.
	/// </summary>
	public Parameter()
	{
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="Parameter"/> class with a name.
	/// </summary>
	/// <param name="name">The name of the parameter.</param>
	public Parameter(string name) => Name = name;

	/// <summary>
	/// Initializes a new instance of the <see cref="Parameter"/> class with a name and type.
	/// </summary>
	/// <param name="name">The name of the parameter.</param>
	/// <param name="type">The type of the parameter.</param>
	public Parameter(string name, string type)
	{
		Name = name;
		Type = type;
	}

	/// <summary>
	/// Gets or sets the name of the parameter.
	/// </summary>
	public string? Name { get; set; }

	/// <summary>
	/// Gets or sets the type of the parameter.
	/// </summary>
	public TypeReference? Type { get; set; }

	/// <summary>
	/// Gets or sets a value indicating whether the parameter is optional.
	/// </summary>
	public bool IsOptional { get; set; }

	/// <summary>
	/// Gets or sets the default value of the parameter if it is optional.
	/// </summary>
	/// <remarks>
	/// An expression rather than source text, for the reason <see cref="TypeReference"/> is
	/// structure: <c>False</c>, <c>false</c> and <c>None</c> are each one language's spelling, and a
	/// default written in one of them fails to compile, or throws when called, in the others. Each
	/// generator spells the expression with its own literal writer. <see cref="ParseDefaultValue"/>
	/// turns the text form older documents hold into one.
	/// </remarks>
	public Expression? DefaultValue { get; set; }

	/// <summary>
	/// Gets the type name of this node for serialization purposes.
	/// </summary>
	/// <returns>The name of the node type.</returns>
	public override string GetNodeTypeName() => "Parameter";

	/// <summary>
	/// Creates a deep clone of this parameter.
	/// </summary>
	/// <returns>A new instance of the parameter with the same properties.</returns>
	public override AstNode Clone()
	{
		Parameter clone = new()
		{
			Name = Name,
			Type = Type?.Clone(),
			IsOptional = IsOptional,
			DefaultValue = DefaultValue?.Clone() as Expression
		};

		// Copy metadata
		foreach ((string key, object? value) in Metadata)
		{
			clone.Metadata[key] = value;
		}

		return clone;
	}

	/// <summary>
	/// Reads a default value from its text form, as a document or the editor holds it.
	/// </summary>
	/// <param name="text">The text, or null or empty for no default.</param>
	/// <returns>
	/// A literal for <c>true</c> and <c>false</c> (in any case, so a document written for Python
	/// still reads), a whole or fractional number, or a double-quoted string; a reference to the
	/// name for anything else. Null when there is no text.
	/// </returns>
	/// <remarks>
	/// Anything that is not a recognisable literal stays a reference to a name, which is written
	/// verbatim, so a default that worked as text in its one target language still does.
	/// </remarks>
	public static Expression? ParseDefaultValue(string? text)
	{
		if (string.IsNullOrWhiteSpace(text))
		{
			return null;
		}

		string trimmed = text.Trim();

		if (bool.TryParse(trimmed, out bool flag))
		{
			return new LiteralExpression<bool>(flag);
		}

		if (int.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int number))
		{
			return new LiteralExpression<int>(number);
		}

		if (long.TryParse(trimmed, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long bigNumber))
		{
			return new LiteralExpression<long>(bigNumber);
		}

		if (LooksNumeric(trimmed)
			&& double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out double fraction)
			&& double.IsFinite(fraction))
		{
			return new LiteralExpression<double>(fraction);
		}

		if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
		{
			return new LiteralExpression<string>(Unescape(trimmed[1..^1]));
		}

		return new VariableReference(trimmed);
	}

	/// <summary>
	/// Writes a default value in the text form <see cref="ParseDefaultValue"/> reads.
	/// </summary>
	/// <param name="value">The default value.</param>
	/// <returns>
	/// Its text, which reads back as the same value; or null when there is no value, or when it is
	/// an expression with no text form.
	/// </returns>
	public static string? FormatDefaultValue(Expression? value) => value switch
	{
		LiteralExpression<bool> flag => flag.Value ? "true" : "false",
		LiteralExpression<int> number => number.Value.ToString(CultureInfo.InvariantCulture),
		LiteralExpression<long> bigNumber => bigNumber.Value.ToString(CultureInfo.InvariantCulture),
		LiteralExpression<double> fraction when double.IsFinite(fraction.Value) => FormatFraction(fraction.Value.ToString("R", CultureInfo.InvariantCulture)),
		LiteralExpression<float> fraction when float.IsFinite(fraction.Value) => FormatFraction(fraction.Value.ToString("R", CultureInfo.InvariantCulture)),
		LiteralExpression<string> text => $"\"{Escape(text.Value ?? string.Empty)}\"",
		VariableReference reference => reference.Name,
		_ => null,
	};

	/// <summary>
	/// Keeps a fractional number fractional in text, so <c>1.0</c> does not read back as a whole one.
	/// </summary>
	private static string FormatFraction(string text) =>
		text.AsSpan().IndexOfAny('.', 'E', 'e') >= 0 ? text : text + ".0";

	private static bool LooksNumeric(string text) =>
		text.Length > 0 && (char.IsAsciiDigit(text[0]) || (text.Length > 1 && (text[0] is '-' or '+' or '.') && (char.IsAsciiDigit(text[1]) || text[1] == '.')));

	private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);

	private static string Unescape(string text)
	{
		StringBuilder result = new(text.Length);
		for (int i = 0; i < text.Length; i++)
		{
			if (text[i] == '\\' && i + 1 < text.Length && text[i + 1] is '\\' or '"')
			{
				i++;
			}

			result.Append(text[i]);
		}

		return result.ToString();
	}
}
