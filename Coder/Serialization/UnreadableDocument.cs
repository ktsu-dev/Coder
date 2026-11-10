// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Serialization;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Builds the failure for a YAML document that cannot be read, and carries it out of the nodes it is
/// nested in, collecting the key of each on the way so the message can say where it went wrong.
/// </summary>
/// <remarks>
/// The failure is an <see cref="InvalidDataException"/> throughout, with what it has learned kept in
/// <see cref="Exception.Data"/>, so nothing but the standard exception ever leaves the deserializer.
/// </remarks>
internal static class UnreadableDocument
{
	private const string ProblemKey = "ktsu.Coder.Problem";
	private const string UnknownNodeKey = "ktsu.Coder.UnknownNode";
	private const string PathKey = "ktsu.Coder.Path";

	/// <summary>
	/// Creates the failure for a key that names no node.
	/// </summary>
	/// <param name="nodeType">The key.</param>
	/// <returns>The failure.</returns>
	public static InvalidDataException UnknownNode(string nodeType)
	{
		InvalidDataException ex = Create($"unknown node '{nodeType}'");
		ex.Data[UnknownNodeKey] = nodeType;
		return ex;
	}

	/// <summary>
	/// Creates the failure for a property whose value cannot be read.
	/// </summary>
	/// <param name="problem">What is wrong with it.</param>
	/// <returns>The failure.</returns>
	public static InvalidDataException Value(string problem) => Create(problem);

	/// <summary>
	/// Records that the failure happened inside the node written under <paramref name="nodeType"/>.
	/// </summary>
	/// <param name="ex">The failure.</param>
	/// <param name="nodeType">The enclosing node's key.</param>
	/// <returns><see langword="true"/> when <paramref name="ex"/> is one of these failures.</returns>
	public static bool Enclose(InvalidDataException ex, string nodeType)
	{
		if (ex.Data[PathKey] is not List<string> path)
		{
			return false;
		}

		path.Insert(0, nodeType);
		return true;
	}

	/// <summary>
	/// Gets whether the failure is that <paramref name="nodeType"/> itself, read at the top of the
	/// call that caught it, names no node.
	/// </summary>
	/// <param name="ex">The failure.</param>
	/// <param name="nodeType">The key that was read.</param>
	/// <returns>Whether the key itself is the unknown node.</returns>
	public static bool IsUnknownNode(InvalidDataException ex, string nodeType) =>
		ex.Data[PathKey] is List<string> { Count: 1 } && (ex.Data[UnknownNodeKey] as string) == nodeType;

	/// <summary>
	/// Gives a failure a message naming where it is, e.g.
	/// <c>functionDeclaration/returnStatement: unknown node 'ifStatement'</c>.
	/// </summary>
	/// <param name="ex">The failure.</param>
	/// <returns>The failure to throw, or <paramref name="ex"/> when it is not one of these.</returns>
	public static InvalidDataException Described(InvalidDataException ex)
	{
		if (ex.Data[PathKey] is not List<string> path || ex.Data[ProblemKey] is not string problem)
		{
			return ex;
		}

		// The unknown node's own key is the innermost entry of the path, and is already in the problem.
		int depth = ex.Data[UnknownNodeKey] is null ? path.Count : path.Count - 1;
		string message = depth <= 0 ? problem : $"{string.Join('/', path.GetRange(0, depth))}: {problem}";
		return new InvalidDataException(message, ex);
	}

	private static InvalidDataException Create(string problem)
	{
		InvalidDataException ex = new(problem);
		ex.Data[ProblemKey] = problem;
		ex.Data[PathKey] = new List<string>();
		return ex;
	}
}
