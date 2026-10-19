// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Editor;

using System;
using ktsu.Coder.Ast;
using ktsu.Coder.Serialization;
using ktsu.Essentials;

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

		// An interrupted save or a newly created file leaves nothing in it, and the deserializer
		// refuses empty text as a bad argument, which would read to the user as a parse failure.
		if (string.IsNullOrWhiteSpace(yaml))
		{
			return DocumentResult.Failed($"{path} is empty.");
		}

		AstNode? root;
		try
		{
			root = deserializer.Deserialize(yaml);
		}
		catch (Exception ex) when (ex is not OutOfMemoryException)
		{
			return DocumentResult.Failed($"{path} is not a document this editor understands: {ex.Message}");
		}

		return root is null
			? DocumentResult.Failed($"{path} contains no recognisable AST node.")
			: DocumentResult.Loaded(root, path);
	}

	/// <summary>
	/// Writes generated source alongside the document it came from.
	/// </summary>
	/// <param name="code">The source to write.</param>
	/// <param name="path">The file to write it to.</param>
	/// <returns>What happened, or why it could not.</returns>
	/// <remarks>
	/// Generated source is not a document — it cannot be read back into the editor — so this writes
	/// text rather than going through the serializer. It is here rather than in the application
	/// because this is the type that owns the filesystem and the reporting of what went wrong with it.
	/// </remarks>
	public DocumentResult Export(string code, string path)
	{
		Ensure.NotNull(code);
		Ensure.NotNull(path);

		try
		{
			EnsureDirectoryOf(path);
			WriteInPlaceOf(path, code);
		}
		catch (IOException ex)
		{
			return DocumentResult.Failed(CouldNotWrite(path, ex));
		}
		catch (UnauthorizedAccessException ex)
		{
			return DocumentResult.Failed(CouldNotWrite(path, ex));
		}

		// Nothing was loaded, so the result carries the path alone: what the caller wants to know is
		// where the source went.
		return new DocumentResult(null, path, null);
	}

	/// <summary>
	/// Creates the directory a file is about to be written into, if it is not there yet.
	/// </summary>
	/// <param name="path">The file about to be written.</param>
	private void EnsureDirectoryOf(string path)
	{
		string? directory = fileSystem.Path.GetDirectoryName(path);
		if (!string.IsNullOrEmpty(directory) && !fileSystem.Directory.Exists(directory))
		{
			fileSystem.Directory.CreateDirectory(directory);
		}
	}

	/// <summary>
	/// Writes a file so that a write which fails part of the way through leaves what was there before.
	/// </summary>
	/// <param name="path">The file to write.</param>
	/// <param name="text">What to write into it.</param>
	/// <remarks>
	/// Writing over the file itself truncates it before the first byte goes in, so a full disk, a
	/// crash or a sync drive dropping out midway destroys the last good copy along with the new one.
	/// The text goes into a file of its own beside the target, in the same directory so the move is a
	/// rename rather than a copy, and only a complete file replaces the old one. A temporary file that
	/// could not be completed is removed rather than left for the user to find.
	/// </remarks>
	private void WriteInPlaceOf(string path, string text)
	{
		string temporary = $"{path}.{Guid.NewGuid():N}.tmp";
		try
		{
			fileSystem.File.WriteAllText(temporary, text);
			fileSystem.File.Move(temporary, path, overwrite: true);
		}
		catch
		{
			DeleteIfPresent(temporary);
			throw;
		}
	}

	/// <summary>
	/// Removes a file left behind by a write that did not finish, without masking why it did not.
	/// </summary>
	/// <param name="path">The file to remove.</param>
	private void DeleteIfPresent(string path)
	{
		try
		{
			if (fileSystem.File.Exists(path))
			{
				fileSystem.File.Delete(path);
			}
		}
		catch (IOException)
		{
			// The write's own failure is the one worth reporting.
		}
		catch (UnauthorizedAccessException)
		{
			// The write's own failure is the one worth reporting.
		}
	}

	/// <summary>
	/// Phrases a failed write for the status bar.
	/// </summary>
	/// <param name="path">The file that could not be written.</param>
	/// <param name="ex">Why it could not.</param>
	/// <returns>The message, which says the file on disk was not touched, because it was not.</returns>
	private static string CouldNotWrite(string path, Exception ex) =>
		$"Could not write {path}: {ex.Message} Any existing file there was left as it was.";

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
			EnsureDirectoryOf(path);
			WriteInPlaceOf(path, serializer.Serialize(root));
		}
		catch (IOException ex)
		{
			return DocumentResult.Failed(CouldNotWrite(path, ex));
		}
		catch (UnauthorizedAccessException ex)
		{
			return DocumentResult.Failed(CouldNotWrite(path, ex));
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
