// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using System;
using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;
using ktsu.Essentials;
using YamlDotNet.Core;

/// <summary>
/// Reads and writes AST documents as YAML files.
/// </summary>
/// <remarks>
/// Goes through <see cref="IFileSystemProvider"/> rather than <c>System.IO</c>, so the editor takes
/// its filesystem from the container and a caller can substitute one. Every failure is returned
/// rather than thrown: opening the wrong file is an ordinary thing for a user to do, and an
/// exception escaping a draw call takes the application down.
/// </remarks>
/// <param name="fileSystem">The filesystem to read and write through.</param>
/// <param name="serializer">Serializes an AST to YAML.</param>
/// <param name="deserializer">Reads an AST back from YAML.</param>
public sealed class DocumentStore(
	IFileSystemProvider fileSystem,
	YamlSerializer serializer,
	YamlDeserializer deserializer)
{
	/// <summary>
	/// The extension an AST document is stored with.
	/// </summary>
	public const string Extension = ".coder.yaml";

	/// <summary>
	/// Reads a document.
	/// </summary>
	/// <param name="path">The file to read.</param>
	/// <returns>What was read, or why it could not be.</returns>
	/// <remarks>
	/// Returns a result rather than throwing: opening the wrong file is an ordinary thing for a user
	/// to do, and the editor shows the reason rather than terminating.
	/// </remarks>
	public DocumentResult Load(string path)
	{
		Ensure.NotNull(path);

		if (!fileSystem.File.Exists(path))
		{
			return DocumentResult.Failed($"There is no file at {path}.");
		}

		string yaml;
		try
		{
			yaml = fileSystem.File.ReadAllText(path);
		}
		catch (IOException ex)
		{
			return DocumentResult.Failed($"Could not read {path}: {ex.Message}");
		}
		catch (UnauthorizedAccessException ex)
		{
			return DocumentResult.Failed($"Could not read {path}: {ex.Message}");
		}

		AstNode? root;
		try
		{
			root = deserializer.Deserialize(yaml);
		}
		catch (YamlException ex)
		{
			// YamlDotNet reports malformed input by throwing, and a user opening the wrong file is
			// the ordinary way to reach that. Letting it escape would take the editor down.
			return DocumentResult.Failed($"{path} is not a document this editor understands: {ex.Message}");
		}
		catch (InvalidOperationException ex)
		{
			return DocumentResult.Failed($"{path} is not a document this editor understands: {ex.Message}");
		}

		return root is null
			? DocumentResult.Failed($"{path} contains no recognisable AST node.")
			: DocumentResult.Loaded(root, path);
	}

	/// <summary>
	/// Writes a document.
	/// </summary>
	/// <param name="root">The AST to write.</param>
	/// <param name="path">The file to write it to.</param>
	/// <returns>What happened, or why it could not.</returns>
	public DocumentResult Save(AstNode root, string path)
	{
		Ensure.NotNull(root);
		Ensure.NotNull(path);

		try
		{
			string? directory = fileSystem.Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
			{
				fileSystem.Directory.CreateDirectory(directory);
			}

			fileSystem.File.WriteAllText(path, serializer.Serialize(root));
		}
		catch (IOException ex)
		{
			return DocumentResult.Failed($"Could not write {path}: {ex.Message}");
		}
		catch (UnauthorizedAccessException ex)
		{
			return DocumentResult.Failed($"Could not write {path}: {ex.Message}");
		}

		return DocumentResult.Loaded(root, path);
	}
}

/// <summary>
/// The outcome of reading or writing a document.
/// </summary>
/// <param name="Root">The AST, when the operation succeeded.</param>
/// <param name="Path">The file it came from or went to.</param>
/// <param name="Error">Why the operation failed, when it did.</param>
public sealed record DocumentResult(AstNode? Root, string? Path, string? Error)
{
	/// <summary>
	/// Gets a value indicating whether the operation succeeded.
	/// </summary>
	public bool Success => Error is null;

	/// <summary>
	/// Builds a successful result.
	/// </summary>
	/// <param name="root">The AST.</param>
	/// <param name="path">The file.</param>
	/// <returns>The result.</returns>
	public static DocumentResult Loaded(AstNode root, string path) => new(root, path, null);

	/// <summary>
	/// Builds a failed result.
	/// </summary>
	/// <param name="error">Why it failed, phrased for the status bar.</param>
	/// <returns>The result.</returns>
	public static DocumentResult Failed(string error) => new(null, null, error);
}
