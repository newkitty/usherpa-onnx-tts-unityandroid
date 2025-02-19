using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

public static class StreamingAssetsAPI
{
	private const string HIERARCHY_FILE = "StreamingAssetsHierarchy.txt";


	/// <summary>
	/// Wraps an IEnumerator-based Unity coroutine in a Task,
	/// allowing you to 'await' it in an async method.
	/// </summary>
	public static Task RunCoroutine(this MonoBehaviour runner, IEnumerator coroutine)
	{
		var tcs = new TaskCompletionSource<bool>();
		runner.StartCoroutine(RunCoroutineInternal(coroutine, tcs));
		return tcs.Task;
	}

	private static IEnumerator RunCoroutineInternal(IEnumerator coroutine, TaskCompletionSource<bool> tcs)
	{
		yield return coroutine;
		tcs.SetResult(true);
	}


	/// <summary>
	/// Retrieves the hierarchy of files (relative paths) under a given subfolder in
	/// StreamingAssets, as defined in <see cref="HIERARCHY_FILE"/>. 
	/// 
	/// This method is NOT a coroutine itself. Instead, it starts an internal 
	/// coroutine to load and filter the hierarchy. Once loading finishes, 
	/// the provided <paramref name="onComplete"/> callback is invoked with 
	/// the resulting list of file paths. If there is an error, it passes <c>null</c>.
	/// 
	/// <para>Usage example:</para>
	/// <code>
	/// void Start()
	/// {
	///     // Suppose 'this' is a MonoBehaviour
	///     var api = new StreamingAssetsHierarchyAPI();
	///     api.GetHierarchy(this, "Models", (files) =>
	///     {
	///         if (files == null)
	///         {
	///             Debug.LogError("Failed to retrieve files!");
	///             return;
	///         }
	///         Debug.Log("Received " + files.Count + " files in 'Models'.");
	///     });
	/// }
	/// </code>
	/// </summary>
	/// <param name="runner">A MonoBehaviour used to start the internal coroutine.</param>
	/// <param name="subfolder">
	/// The subfolder (relative to StreamingAssets root) to filter by. 
	/// If empty or null, it returns the entire hierarchy.
	/// </param>
	/// <param name="onComplete">
	/// Callback invoked once the list of files is ready. If an error occurs, <c>null</c> is passed.
	/// </param>
	public static void GetHierarchy( this MonoBehaviour runner, string subfolder, Action<List<string>> onComplete)
	{
		// Validate runner
		if (runner == null)
		{
			Debug.LogError("[StreamingAssetsHierarchyAPI] No MonoBehaviour provided to start coroutine!");
			onComplete?.Invoke(null);
			return;
		}

		// Start the internal coroutine
		runner.StartCoroutine(GetHierarchyCoroutine(subfolder, onComplete));
	}

	// The coroutine that actually fetches the hierarchy file
	public static IEnumerator GetHierarchyCoroutine(string subfolder, Action<List<string>> onComplete = null)
	{
		// 1) Load the entire hierarchy from the text file
		yield return GetHierarchyForSubfolder(subfolder, (list) =>
		{
			// This callback is invoked once the text is loaded & filtered
			onComplete?.Invoke(list);
		});
	}

	/// <summary>
	/// Reads the entire hierarchy from the generated file,
	/// filters for those starting with 'subfolder',
	/// and returns their relative paths through <paramref name="callback"/>.
	/// 
	/// Typically you won't call this method directly; instead, use <see cref="GetHierarchy"/>.
	/// 
	/// Example usage manually (if you wanted a coroutine):
	/// <code>
	/// yield return StartCoroutine(
	///     StreamingAssetsHierarchyAPI.GetHierarchyForSubfolder("Models", (list) =&gt; { ... })
	/// );
	/// </code>
	/// </summary>
	/// <param name="subfolder">
	/// The subfolder (relative to StreamingAssets root) to filter by. 
	/// If empty, returns all paths.
	/// </param>
	/// <param name="callback">
	/// Invoked with a list of paths, or <c>null</c> if there's an error.
	/// </param>
	public static IEnumerator GetHierarchyForSubfolder(string subfolder, Action<List<string>> callback)
	{
		string path = Path.Combine(Application.streamingAssetsPath, HIERARCHY_FILE);

		using (UnityWebRequest www = UnityWebRequest.Get(path))
		{
			yield return www.SendWebRequest();

			if (www.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"Failed to load {HIERARCHY_FILE}: {www.error}");
				callback?.Invoke(null);
				yield break;
			}

			// Parse lines
			string fileContent = www.downloadHandler.text;
			string[] allLines = fileContent.Split(
				new char[] { '\r', '\n' },
				StringSplitOptions.RemoveEmptyEntries
			);

			List<string> matched = new List<string>();

			if (string.IsNullOrEmpty(subfolder))
				subfolder = "";

			// We'll unify to forward slashes
			subfolder = subfolder.Replace("\\", "/").Trim().TrimEnd('/');

			foreach (var line in allLines)
			{
				// e.g. "Models/en_US-libritts_r-medium.onnx"
				// If subfolder is "Models", check if line.StartsWith("Models/")
				if (subfolder.Length == 0 || line.StartsWith(subfolder + "/"))
				{
					matched.Add(line);
				}
			}

			callback?.Invoke(matched);
		}
	}


	/// <summary>
	/// Copies a single file from StreamingAssets (relative path) to a specified 
	/// local filesystem path. This uses UnityWebRequest to handle jar:file:// 
	/// URIs on Android.
	/// 
	/// <para>Example usage:</para>
	/// <code>
	/// yield return StreamingAssetsAPI.CopyOneFile(
	///     "Models/data.json", 
	///     "/storage/emulated/0/Android/data/com.example.myapp/files/data.json",
	///     success => 
	///     {
	///         Debug.Log(success ? "File copied!" : "Copy failed.");
	///     }
	/// );
	/// </code>
	/// </summary>
	/// <param name="relativeFilePath">Path within StreamingAssets. E.g. "Models/data.json".</param>
	/// <param name="destinationFullPath">Full local file path to write to.</param>
	/// <param name="onComplete">Invoked with true if copy succeeded, false on error.</param>
	public static IEnumerator CopyFile( string relativeFilePath, string destinationFullPath, Action<bool> onComplete = null)
	{
		// Build full path to the file in StreamingAssets
		string srcUrl = Path.Combine(Application.streamingAssetsPath, relativeFilePath);
		string destUrl = destinationFullPath;
		if(File.Exists(destUrl))
		{
			Debug.Log($"已经存在File跳过[CopyOneFile] File {destUrl} 已经存在.");
			yield break;
		}
		using (UnityWebRequest www = UnityWebRequest.Get(srcUrl))
		{
			yield return www.SendWebRequest();

			if (www.result != UnityWebRequest.Result.Success)
			{
				Debug.LogError($"[CopyOneFile] Failed to get {relativeFilePath}: {www.error}");
				onComplete?.Invoke(false);
				yield break;
			}

			// Ensure the directory of destinationFullPath exists
			string parentDir = Path.GetDirectoryName(destinationFullPath);
			if (!Directory.Exists(parentDir))
			{
				Debug.Log($"[CopyOneFile] Creating directory {parentDir}");
				Directory.CreateDirectory(parentDir);
			}
			 // 检查目标文件是否已经存在
			if (!File.Exists(destinationFullPath))
			{
				// Write the file
				byte[] data = www.downloadHandler.data;
				File.WriteAllBytes(destinationFullPath, data);
				 Debug.Log($"[CopyOneFile] Copied {relativeFilePath} -> {destinationFullPath}");
			}
		  
			onComplete?.Invoke(true);
		}
	}

	/// <summary>
	/// Recursively copies *all files* from a given subfolder in StreamingAssets
	/// into the specified local directory (e.g., persistentDataPath).
	/// 
	/// It uses <see cref="GetHierarchyForSubfolder"/> to find all files,
	/// then calls <see cref="CopyOneFile"/> for each. 
	/// 
	/// Example usage:
	/// <code>
	/// yield return StreamingAssetsAPI.CopyDirectory(
	///     runner: this,
	///     subfolder: "Models",
	///     localRoot: Path.Combine(Application.persistentDataPath, "Models"),
	///     onComplete: () => { Debug.Log("Directory copied!"); }
	/// );
	/// </code>
	/// </summary>
	/// <param name="subfolder">Which subfolder in StreamingAssets to copy. E.g. "Models".</param>
	/// <param name="localRoot">
	/// The local directory path (e.g. persistentDataPath/Models) where files are written.
	/// </param>
	/// <param name="onComplete">Optional callback invoked when done.</param>
	public static IEnumerator CopyDirectory(string subfolder, string localRoot, Action onComplete = null )
	{
		// 1) Get the hierarchy for that subfolder
		bool done = false;
		List<string> fileList = null;
		localRoot = Path.Combine(Application.persistentDataPath, localRoot);
		yield return GetHierarchyForSubfolder(subfolder, list =>
		{
			fileList = list;
			done = true;
		});

		// Wait for callback
		while (!done) 
			yield return null;

		if (fileList == null)
		{
			Debug.LogError($"[CopyDirectory] Could not retrieve hierarchy for {subfolder}.");
			onComplete?.Invoke();
			yield break;
		}

		// e.g. fileList might contain ["Models/foo.txt", "Models/subdir/bar.json", ...]

		// 2) Copy each file
		for (int i = 0; i < fileList.Count; i++)
		{
			string relPath = fileList[i];
			// We want to remove "Models/" if subfolder = "Models"
			// so we only get the portion after that prefix
			string suffix = relPath;

			// unify slashes
			suffix = suffix.Replace("\\", "/");
			subfolder = subfolder.Replace("\\", "/").Trim().TrimEnd('/');

			if (subfolder.Length > 0 && suffix.StartsWith(subfolder + "/"))
			{
				// remove "Models/" prefix
				suffix = suffix.Substring(subfolder.Length + 1);
			}

			// Build destination path
			string dst = Path.Combine(localRoot, suffix);

			// yield return CopyOneFile:
			yield return CopyFile(relPath, dst);
		}

		Debug.Log($"[CopyDirectory] Copied {fileList.Count} files from '{subfolder}' to '{localRoot}'");
		onComplete?.Invoke();
	}
}