// Copyright (c) 2023-2026 ktsu-dev contributors

namespace ktsu.Coder.Test.Editor;

using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.ExceptionServices;
using ktsu.Coder.Ast;
using ktsu.Coder.Editor;
using ktsu.Coder.Serialization;
using ktsu.Essentials;
using ktsu.Essentials.FileSystemProviders.Native;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Tests for what <see cref="DocumentStore"/> leaves on disk when a write does not finish.
/// </summary>
/// <remarks>
/// A write that fails part of the way through is simulated over the real
/// <see cref="NativeFileSystemProvider"/>: every call is forwarded to it, except that writing text
/// puts the first half down and then throws, which is what a full disk does. Everything else, the
/// move and the delete included, is the real filesystem's.
/// </remarks>
[TestClass]
public sealed class DocumentStoreTests
{
	private string root = string.Empty;

	/// <summary>
	/// Gives each test its own directory, so one cannot see another's files.
	/// </summary>
	[TestInitialize]
	public void SetUp()
	{
		root = Path.Combine(Path.GetTempPath(), $"coder-store-{Guid.NewGuid():N}");
		Directory.CreateDirectory(root);
	}

	/// <summary>
	/// Removes the directory the test wrote into.
	/// </summary>
	[TestCleanup]
	public void TearDown()
	{
		if (Directory.Exists(root))
		{
			Directory.Delete(root, recursive: true);
		}
	}

	private static DocumentStore NewStore(IFileSystemProvider fileSystem) =>
		new(fileSystem, new YamlSerializer(), new YamlDeserializer());

	private string PathIn(string name) => Path.Combine(root, name + DocumentStore.Extension);

	private string[] TemporaryFiles() => Directory.GetFiles(root, "*.tmp");

	/// <summary>
	/// Tests that a save which fails midway leaves the document that was already there, rather than
	/// the truncated start of the new one.
	/// </summary>
	[TestMethod]
	public void Save_ThatFailsMidway_LeavesTheExistingDocumentIntact()
	{
		string path = PathIn("widget");
		Assert.IsTrue(NewStore(new NativeFileSystemProvider()).Save(new ClassDeclaration("Original"), path).Success);
		byte[] before = File.ReadAllBytes(path);

		DocumentResult result = NewStore(FailingWrites.Over(new NativeFileSystemProvider()))
			.Save(new ClassDeclaration("Replacement"), path);

		Assert.IsFalse(result.Success);
		StringAssert.Contains(result.Error, "Could not write", StringComparison.Ordinal);
		CollectionAssert.AreEqual(before, File.ReadAllBytes(path), "the last good copy should survive a failed save");
		Assert.AreEqual(0, TemporaryFiles().Length, "a failed save should not leave its temporary file behind");
	}

	/// <summary>
	/// Tests that an export which fails midway leaves the source that was already there.
	/// </summary>
	[TestMethod]
	public void Export_ThatFailsMidway_LeavesTheExistingFileIntact()
	{
		string path = Path.Combine(root, "Widget.cs");
		File.WriteAllText(path, "class Original { }");

		DocumentResult result = NewStore(FailingWrites.Over(new NativeFileSystemProvider()))
			.Export("class Replacement { }", path);

		Assert.IsFalse(result.Success);
		Assert.AreEqual("class Original { }", File.ReadAllText(path));
		Assert.AreEqual(0, TemporaryFiles().Length, "a failed export should not leave its temporary file behind");
	}

	/// <summary>
	/// Tests that a save that succeeds replaces the document and leaves nothing else beside it.
	/// </summary>
	[TestMethod]
	public void Save_ReplacesTheDocumentAndLeavesNoTemporaryFile()
	{
		DocumentStore store = NewStore(new NativeFileSystemProvider());
		string path = PathIn("widget");
		Assert.IsTrue(store.Save(new ClassDeclaration("Original"), path).Success);

		Assert.IsTrue(store.Save(new ClassDeclaration("Replacement"), path).Success);

		DocumentResult reopened = store.Load(path);
		Assert.IsTrue(reopened.Success, reopened.Error);
		Assert.AreEqual("Replacement", ((ClassDeclaration)reopened.Root!).Name);
		Assert.AreEqual(0, TemporaryFiles().Length);
		Assert.AreEqual(1, Directory.GetFiles(root).Length, "only the document itself should be in the directory");
	}

	/// <summary>
	/// Forwards every call to a real filesystem, except that writing text fails halfway through.
	/// </summary>
	/// <remarks>
	/// Public and unsealed because <see cref="DispatchProxy"/> derives from it at run time.
	/// </remarks>
	public class FailingWrites : DispatchProxy
	{
		private object target = null!;

		/// <summary>
		/// Wraps a filesystem so that its text writes fail partway.
		/// </summary>
		/// <param name="fileSystem">The filesystem to forward to.</param>
		/// <returns>The wrapped filesystem.</returns>
		public static IFileSystemProvider Over(IFileSystemProvider fileSystem) => Wrap(fileSystem);

		private static T Wrap<T>(T target)
			where T : class
		{
			T proxy = Create<T, FailingWrites>();
			((FailingWrites)(object)proxy).target = target;
			return proxy;
		}

		/// <inheritdoc/>
		protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
		{
			ArgumentNullException.ThrowIfNull(targetMethod);

			if (target is IFile file && targetMethod.Name == nameof(IFile.WriteAllText) && args is [string path, string text, ..])
			{
				file.WriteAllText(path, text[..(text.Length / 2)]);
				throw new IOException("There is not enough space on the disk.");
			}

			object? result;
			try
			{
				result = targetMethod.Invoke(target, args);
			}
			catch (TargetInvocationException ex) when (ex.InnerException is not null)
			{
				ExceptionDispatchInfo.Throw(ex.InnerException);
				throw;
			}

			return result is IFile inner ? Wrap(inner) : result;
		}
	}
}
