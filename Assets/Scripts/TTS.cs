using SherpaOnnx;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class TTS : MonoBehaviour
{
	// #if UNITY_ANDROID
	// const string dll = "sherpa-onnx-c-api";
	// //const string dll = "_Internal";
	// //const string dll = "sherpa-onnx-jni" ;
	// [DllImport(dll) ]
	// private static extern IntPtr SherpaOnnxCreateOfflineTts(ref OfflineTtsConfig config);
	// [DllImport(dll) ]
	// private static extern void SherpaOnnxDestroyOfflineTts(IntPtr handle);
	// [DllImport(dll) ]
	// private static extern int SherpaOnnxOfflineTtsSampleRate(IntPtr handle);
	// [DllImport(dll) ]
	// private static extern int SherpaOnnxOfflineTtsNumSpeakers(IntPtr handle);
	// [DllImport(dll) ]
	// private static extern IntPtr SherpaOnnxOfflineTtsGenerate(IntPtr handle, [MarshalAs (UnmanagedType.LPArray, ArraySubType = UnmanagedType .I1)] byte[] utf8Text, int sid, float speed);
	// [DllImport(dll, CallingConvention = CallingConvention.Cdecl) ]
	
	// private static extern IntPtr SherpaOnnxOfflineTtsGenerateWithCallback(IntPtr handle, [MarshalAs (UnmanagedType.LPArray, ArraySubType = UnmanagedType.I1)] byte[] utf8Text, int sid, float speed, OfflineTtsCallback callback);

	// #endif
	OfflineTtsConfig config;
	OfflineTts ot;
	OfflineTtsGeneratedAudio otga;
	OfflineTtsCallback callback;

	string Model;
	string Lexicon;
	string Tokens;
	string DataDir;
	string DictDir;
	string RuleFsts;
	public int SpeakerId = 1;
	public float Speed = 1f;

	int SampleRate = 22050;
	AudioSource audioSource;
	List<float> audioData = new List<float>();

	public Dropdown dpSpeakers;
	public InputField content;
	public InputField speakerid;
	public InputField speed;
	public Button generate;
	public AudioClip audioClip = null;
	/// <summary>
	/// 当前要读取的索引位置
	/// </summary>
	private int curAudioClipPos = 0;
	int modelIndex;

	public bool isInit = false;

 	//put voice models directory path relative to the streaming assets folder	
	 [SerializeField] public string modelsDir;
	 //put espeak-ng data directory path relative to the streaming assets folder
	//[SerializeField] public string espeakDir;
	// Start is called before the first frame update
	async void Start()
	{
		 if( Application.platform == RuntimePlatform.Android )
		{
			Debug.Log("running android copy process!");
			await this.RunCoroutine( StreamingAssetsAPI.CopyDirectory(
			modelsDir,      // subfolder in StreamingAssets
			 modelsDir ,
			() => { 
				Debug.Log( modelsDir+ ": Directory copied!"); 
				} ) );

			// await this.RunCoroutine( StreamingAssetsAPI.CopyDirectory(
			// espeakDir,      // subfolder in StreamingAssets
			// BuildPath( espeakDir ),
			// () => { Debug.Log( espeakDir +": Directory copied!"); } ) );

		}
		Loom.Initialize();
		audioSource = GetComponent<AudioSource>();
		string[] models = Enum.GetNames(typeof(Model));
		for (int i = 0; i < models.Length; i++)
		{
			Debug.Log(models[i]);
			dpSpeakers.options.Add(new Dropdown.OptionData(models[i]));
		}
		dpSpeakers.value = 0;
		dpSpeakers.captionText.text = models[0];
		OnDropDownValueChanged(0);
		dpSpeakers.onValueChanged.AddListener(OnDropDownValueChanged);

		generate.onClick.AddListener(() =>
		{
			audioSource.loop = false;
			audioSource.Stop();
			audioSource.clip = null;
			audioData.Clear();
			curAudioClipPos = 0;

			SpeakerId = Convert.ToInt32(speakerid.text);
			Speed = Convert.ToSingle(speed.text);
			if(!isInit)
			{
				Debug.LogWarning("未完成初始化");
				return;
			}
			// Loom.RunAsync(() =>
			// {
			// 	Generate(content.text);
			// });
				StartCoroutine(Generate(content.text));
		});
	}
 private string BuildPath(string relativePath)
	{
		if (string.IsNullOrEmpty(relativePath))
		{
			return "";
		}
		if( Application.platform == RuntimePlatform.Android )
		{
			return Path.Combine(Application.persistentDataPath, relativePath);
		}
		else
			return Path.Combine(Application.streamingAssetsPath, relativePath);
	}
	void OnDropDownValueChanged(int index)
	{
		Debug.Log("index:" + index);
		modelIndex = index;
		switch (index)
		{
		   case 0:
				Model = "models/vits-zh-hf-theresa/theresa.onnx";
				Lexicon = "models/vits-zh-hf-theresa/lexicon.txt";
				Tokens = "models/vits-zh-hf-theresa/tokens.txt";
				//DataDir = "";
				DictDir = "models/vits-zh-hf-theresa/dict";
				// RuleFsts = Application.streamingAssetsPath + "/vits-zh-hf-theresa/phone.fst" + ","
				// + Application.streamingAssetsPath + "/vits-zh-hf-theresa/date.fst" + ","
				// + Application.streamingAssetsPath + "/vits-zh-hf-theresa/number.fst";
				RuleFsts = BuildPath("models/vits-zh-hf-theresa/phone.fst")  + ","
				+ BuildPath("models/vits-zh-hf-theresa/date.fst")  + ","
				+ BuildPath( "models/vits-zh-hf-theresa/number.fst") ;
				SpeakerId= 804;
				Speed = 1;
				break;
			// case 1:
			// 	Model = "models/vits-zh-aishell3/vits-aishell3.onnx";
			// 	Lexicon = "models/vits-zh-aishell3/lexicon.txt";
			// 	Tokens = "models/vits-zh-aishell3/tokens.txt";
			// 	//DataDir = "";
			// 	//DictDir = "";
			// 	RuleFsts = BuildPath( "models/vits-zh-aishell3/phone.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-aishell3/date.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-aishell3/number.fst") ;
			// 	SpeakerId= 174;
			// 	Speed = 1.5f;
			// 	break;


			// case 2:
			// 	//666和99
			// 	Model = "models/vits-zh-hf-eula/eula.onnx";
			// 	Lexicon = "models/vits-zh-hf-eula/lexicon.txt";
			// 	Tokens = "models/vits-zh-hf-eula/tokens.txt";
			// 	//DataDir = "models/";
			// 	DictDir = "models/vits-zh-hf-eula/dict";
			// 	RuleFsts = BuildPath( "models/vits-zh-hf-eula/phone.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-hf-eula/date.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-hf-eula/number.fst") ;
			// 	SpeakerId= 804;
			// 	Speed = 1;
			// 	break;

			// case 3:
			// 	Model = "models/vits-zh-hf-keqing/keqing.onnx";
			// 	Lexicon = "models/vits-zh-hf-keqing/lexicon.txt";
			// 	Tokens = "models/vits-zh-hf-keqing/tokens.txt";
			// 	//DataDir = "models/";
			// 	DictDir = "models/vits-zh-hf-keqing/dict";
			// 	RuleFsts = BuildPath( "models/vits-zh-hf-keqing/phone.fst")  + ","
			// 		+ BuildPath( "models/vits-zh-hf-keqing/date.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-hf-keqing/number.fst") ;
			// 	SpeakerId= 804;
			// 	Speed = 1;
			// 	break;
			// case 4:
			// 	Model = "models/vits-zh-hf-bronya/bronya.onnx";
			// 	Lexicon = "models/vits-zh-hf-bronya/lexicon.txt";
			// 	Tokens = "models/vits-zh-hf-bronya/tokens.txt";
			// 	//DataDir = "models/";
			// 	DictDir = "models/vits-zh-hf-bronya/dict";
			// 	RuleFsts = BuildPath( "models/vits-zh-hf-bronya/phone.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-hf-bronya/date.fst")  + ","
			// 	+ BuildPath( "models/vits-zh-hf-bronya/number.fst") ;
			// 	SpeakerId= 804;
			// 	Speed = 1;
			// 	break;
			//  case 1:
			// 	Model = "models/sherpa-onnx-vits-zh-ll/model.onnx";
			// 	Lexicon = "models/sherpa-onnx-vits-zh-ll/lexicon.txt";
			// 	Tokens = "models/sherpa-onnx-vits-zh-ll/tokens.txt";
			// 	//DataDir = "models/";
			// 	DictDir = "models/sherpa-onnx-vits-zh-ll/dict";
			// 	RuleFsts = BuildPath( "models/sherpa-onnx-vits-zh-ll/phone.fst")  + ","
			// 	+ BuildPath( "models/sherpa-onnx-vits-zh-ll/date.fst") + ","
			// 	+ BuildPath( "models/sherpa-onnx-vits-zh-ll/number.fst");
			// 	SpeakerId= 5;
			// 	Speed = 1;
			// 	break;
		}
		// Loom.RunAsync(() =>
		// {
		// 	Init();
		// });
		InitAsync().Wait();
	}

	async Task InitAsync()
	{
		// #if UNITY_ANDROID
		// if (Application.platform == RuntimePlatform.Android)
		// {
		//    try
		// 	{
		// 		AndroidJavaClass system = new AndroidJavaClass("java.lang.System");
		// 		system.CallStatic("loadLibrary", "sherpa-onnx-c-api");
		// 	}
		// 	catch (Exception e)
		// 	{
		// 		Debug.LogError($"加载SO库失败: {e.Message}");
		// 		return;
		// 	}
		// }
		// #endif
		isInit = false;
		Debug.LogWarning("初始化改为异步，大概5秒左右，如果点击按钮太快，可能没反应？");
		if (ot != null)
		{
			ot.Dispose();
		}
		if (otga != null)
		{
			otga.Dispose();
		}
		config = new OfflineTtsConfig();
	   Debug.Log($"BuildPath(Model)="+BuildPath(Model));
		if(!File.Exists(BuildPath(Model)))
		{
			Debug.LogError($"文件不存在：{BuildPath(Model)}");
			return;
		}
		config.Model.Vits.Model = BuildPath(Model);// Path.Combine(Application.streamingAssetsPath, Model);
		Debug.Log($"BuildPath(Lexicon)="+BuildPath(Lexicon));
		if(!File.Exists(BuildPath(Lexicon)))
		{
			Debug.LogError($"文件不存在：{BuildPath(Lexicon)}");
			return;
		}
		config.Model.Vits.Lexicon = BuildPath(Lexicon);//Path.Combine(Application.streamingAssetsPath, Lexicon);
		Debug.Log($"BuildPath(Tokens)="+BuildPath(Tokens));
		if(!File.Exists(BuildPath(Tokens)))
		{
			Debug.LogError($"文件不存在：{BuildPath(Tokens)}");
			return;
		}
		config.Model.Vits.Tokens = BuildPath(Tokens);//Path.Combine(Application.streamingAssetsPath, Tokens);
		//config.Model.Vits.DataDir = Path.Combine(Application.streamingAssetsPath, DataDir);
		

		//vits-zh-aishell3没有dict
		if (modelIndex != 1)
		{
			Debug.Log($"BuildPath(DictDir)="+BuildPath(DictDir));
			if(!Directory.Exists(BuildPath(DictDir)))
			{
				Debug.LogError($"Directory不存在：{BuildPath(DictDir)}");
				return;
			}
			config.Model.Vits.DictDir = BuildPath(DictDir);//Path.Combine(Application.streamingAssetsPath, DictDir);
		}
		config.Model.Vits.NoiseScale = 0.667f;
		config.Model.Vits.NoiseScaleW = 0.8f;
		config.Model.Vits.LengthScale = 1f;
		config.Model.NumThreads = 12;
		config.Model.Debug = 1;
		//支持cuda但C#不知道怎么用
		config.Model.Provider = "cpu";
		config.RuleFsts = RuleFsts;
		config.MaxNumSentences = 1;
		ot = new OfflineTts(config);
		SampleRate = ot.SampleRate;
		callback = new OfflineTtsCallback(MyCallback);
		isInit = true;
	}

	public IEnumerator Generate(string content)
	{
		Debug.Log("tts Generate");
		if (ot == null)
		{
			Debug.Log("tts ot == null");
			isInit = false;
			 yield return InitAsync();
		}
		Debug.Log("tts Generate content："+content); 
		bool generationDone = false;
		OfflineTtsGeneratedAudio generated = null;
		Thread t = new Thread(() =>
		{
			try
			{
				generated = ot.Generate(content, Speed, SpeakerId);
			}
			catch (Exception ex)
			{
				Debug.LogError($"Generation failed: {ex.Message}");
				Debug.LogError($"Check model paths: {config.Model.Vits.Model}");
			}
			finally
			{
				generationDone = true;
			}
		});
		t.Start();

		// Wait until the thread signals it's done
		yield return new WaitUntil(() => generationDone);
		// 先进行空值检查
		if (generated == null)
		{
			Debug.LogWarning("Generated audio object is null");
			yield break;
		}
		
		// Back on the main thread, we create the AudioClip and play it
		if (generated.Samples == null || generated.Samples.Length == 0)
		{
			Debug.LogWarning("Generated empty audio for a sentence. Skipping...");
			yield break;
		}
		float[] generatedSamples = generated.Samples;
		AudioClip clip = AudioClip.Create(
			"SherpaOnnxTTS-SentenceAsync",
			generatedSamples.Length,
			1,
			ot.SampleRate,
			false
		);
		clip.SetData(generatedSamples, 0);

		audioSource.clip = clip;
		audioSource.Play();
		Debug.Log($"Playing sentence: \"{content}\"  length = {clip.length:F2}s");

		// Wait until playback finishes
		while (audioSource.isPlaying)
			yield return null;
		// otga = ot.GenerateWithCallback(content, Speed, SpeakerId, callback);
		// string fileName = DateTime.Now.ToFileTime().ToString() + ".wav";
		// bool ok = otga.SaveToWaveFile(Application.streamingAssetsPath + "/" + fileName);
		// if (ok)
		// {
		// 	Debug.Log(fileName + " Save succeeded!");
		// }
		// else
		// {
		// 	Debug.Log("Failed");
		// }
	}

	int MyCallback(IntPtr samples, int n)
	{
		float[] tempData = new float[n];
		Marshal.Copy(samples, tempData, 0, n);
		audioData.AddRange(tempData);
		Debug.Log("n:" + n);
		Loom.QueueOnMainThread(() =>
		{
			if (!audioSource.isPlaying && audioData.Count > SampleRate * 2)
			{
				audioClip = AudioClip.Create("SynthesizedAudio", SampleRate * 2, 1,
					SampleRate, true, (float[] data) =>
				{
					ExtractAudioData(data);
				});
				audioSource.clip = audioClip;
				audioSource.loop = true;
				audioSource.Play();
			}
		});
		return n;
	}

	bool ExtractAudioData(float[] data)
	{
		if (data == null || data.Length == 0)
		{
			return false;
		}
		bool hasData = false;//是否真的读取到数据
		int dataIndex = 0;//当前要写入的索引位置
		if (audioData != null && audioData.Count > 0)
		{
			while (curAudioClipPos < audioData.Count && dataIndex < data.Length)
			{
				data[dataIndex] = audioData[curAudioClipPos];
				curAudioClipPos++;
				dataIndex++;
				hasData = true;
			}
		}

		//剩余部分填0
		while (dataIndex < data.Length)
		{
			data[dataIndex] = 0;
			dataIndex++;
		}
		return hasData;
	}

	private void OnApplicationQuit()
	{
		if (ot != null)
		{
			ot.Dispose();
		}
		if (otga != null)
		{
			otga.Dispose();
		}
	}
}