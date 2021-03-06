#if !(UNITY_WEBPLAYER || UNITY_ANDROID || UNITY_WINRT || UNITY_WII || UNITY_WEBGL || UNITY_PS4 || UNITY_WSA)
#define CAN_HANDLE_SCREENSHOTS
#endif

#if UNITY_WEBPLAYER || UNITY_WINRT || UNITY_WII || UNITY_PS4 || UNITY_WSA
#define SAVE_IN_PLAYERPREFS
#endif

using UnityEngine;
using System.Collections.Generic;
using System.IO;


namespace AC
{

	#if !SAVE_IN_PLAYERPREFS

	public class SaveFileHandler_SystemFile : iSaveFileHandler
	{

		public string GetDefaultSaveLabel (int saveID)
		{
			string label = "Save " + saveID.ToString ();
			if (saveID == 0)
			{
				label = "Autosave";
			}

			label += GetTimeString (System.DateTime.Now);
			return label;
		}


		public void DeleteAll (int profileID)
		{
			List<SaveFile> allSaveFiles = GatherSaveFiles (profileID);
			foreach (SaveFile saveFile in allSaveFiles)
			{
				Delete (saveFile);
			}
		}


		public bool Delete (SaveFile saveFile)
		{
			string filename = saveFile.fileName;

			if (!string.IsNullOrEmpty (filename))
			{
				FileInfo t = new FileInfo (filename);
				if (t.Exists)
				{
					t.Delete ();

					#if CAN_HANDLE_SCREENSHOTS
					if (KickStarter.settingsManager.takeSaveScreenshots)
					{
						DeleteScreenshot (saveFile.screenshotFilename);
					}
					#endif

					ACDebug.Log ("File deleted: " + filename);
					return true;
				}
			}
			return false;
		}


		public void Save (SaveFile saveFile, string dataToSave)
		{
			string fullFilename = GetSaveDirectory () + Path.DirectorySeparatorChar.ToString () + GetSaveFilename (saveFile.saveID, saveFile.profileID);
			bool isSuccessful = false;

			try
			{
				StreamWriter writer;
				FileInfo t = new FileInfo (fullFilename);
				
				if (!t.Exists)
				{
					writer = t.CreateText ();
				}
				
				else
				{
					t.Delete ();
					writer = t.CreateText ();
				}
				
				writer.Write (dataToSave);
				writer.Close ();

				ACDebug.Log ("File written: " + fullFilename);
				isSuccessful = true;
			}
			catch (System.Exception e)
 			{
				ACDebug.LogWarning ("Could not save data to file '" + fullFilename + "'. Exception: " + e);
 			}

			KickStarter.saveSystem.OnFinishSaveRequest (saveFile, isSuccessful);
		}


		public void Load (SaveFile saveFile, bool doLog)
		{
			string _data = "";
			
			if (File.Exists (saveFile.fileName))
			{
				StreamReader r = File.OpenText (saveFile.fileName);
				
				string _info = r.ReadToEnd ();
				r.Close ();
				_data = _info;
			}
			
			if (doLog && _data != "")
			{
				ACDebug.Log ("File read: " + saveFile.fileName);
			}

			KickStarter.saveSystem.ReceiveDataToLoad (saveFile, _data);
		}


		public void Import (SaveFile saveFile, bool doLog)
		{
			string _data = "";
			
			if (File.Exists (saveFile.fileName))
			{
				StreamReader r = File.OpenText (saveFile.fileName);
				
				string _info = r.ReadToEnd ();
				r.Close ();
				_data = _info;
			}
			
			if (doLog && _data != "")
			{
				ACDebug.Log ("File read: " + saveFile.fileName);
			}

			KickStarter.saveSystem.ReceiveDataToImport (saveFile, _data);
		}


		public List<SaveFile> GatherSaveFiles (int profileID)
		{
			return GatherSaveFiles (profileID, false, -1, "", "");
		}


		public List<SaveFile> GatherImportFiles (int profileID, int boolID, string separateProjectName, string separateFilePrefix)
		{
			if (!string.IsNullOrEmpty (separateProjectName) && !string.IsNullOrEmpty (separateFilePrefix))
			{
				return GatherSaveFiles (profileID, true, boolID, separateProjectName, separateFilePrefix);
			}
			return null;
		}


		protected List<SaveFile> GatherSaveFiles (int profileID, bool isImport, int boolID, string separateProductName, string separateFilePrefix)
		{
			List<SaveFile> gatheredFiles = new List<SaveFile>();

			string saveDirectory = GetSaveDirectory (separateProductName);
			string filePrefix = (isImport) ? separateFilePrefix : KickStarter.settingsManager.SavePrefix;

			for (int i=0; i<50; i++)
			{
				string filename = filePrefix + KickStarter.saveSystem.GenerateSaveSuffix (i, profileID);
				string filenameWithExtention = filename + KickStarter.saveSystem.GetSaveExtension ();
				string fullFilename = saveDirectory + Path.DirectorySeparatorChar.ToString () + filenameWithExtention;
				
				if (File.Exists (fullFilename))
				{
					if (isImport && boolID >= 0)
					{
						string allData = LoadFile (fullFilename, false);
						if (!KickStarter.saveSystem.DoImportCheck (allData, boolID))
						{
							continue;
						}
					}

					int updateTime = 0;
					bool isAutoSave = false;
					string label = ((isImport) ? "Import " : "Save ") + i.ToString ();
					if (i == 0)
					{
						label = "Autosave";
						isAutoSave = true;
					}

					if (KickStarter.settingsManager.saveTimeDisplay != SaveTimeDisplay.None)
					{
						DirectoryInfo dir = new DirectoryInfo (saveDirectory);
						FileInfo[] info = dir.GetFiles (filenameWithExtention);

						if (info != null && info.Length > 0)
						{
							if (!isAutoSave)
							{
								System.TimeSpan t = info[0].LastWriteTime - new System.DateTime (2015, 1, 1);
								updateTime = (int) t.TotalSeconds;
							}

							label += GetTimeString (info[0].LastWriteTime);
						}
					}

					Texture2D screenShot = null;
					string screenshotFilename = "";
					if (KickStarter.settingsManager.takeSaveScreenshots)
					{
						screenshotFilename = saveDirectory + Path.DirectorySeparatorChar.ToString () + filename + ".jpg";
						screenShot = LoadScreenshot (screenshotFilename);
					}
				
					gatheredFiles.Add (new SaveFile (i, profileID, label, fullFilename, isAutoSave, screenShot, screenshotFilename, updateTime));
				}
			}

			return gatheredFiles;
		}


		public void SaveScreenshot (SaveFile saveFile)
		{
			#if CAN_HANDLE_SCREENSHOTS
			if (saveFile.screenShot != null)
			{
				string fullFilename = GetSaveDirectory () + Path.DirectorySeparatorChar.ToString () + GetSaveFilename (saveFile.saveID, saveFile.profileID, ".jpg");

				byte[] bytes = saveFile.screenShot.EncodeToJPG ();
				File.WriteAllBytes (fullFilename, bytes);
				ACDebug.Log ("Saved screenshot: " + fullFilename);
			}
			else
			{
				ACDebug.LogWarning ("Cannot save screenshot - SaveFile's screenshot variable is null.");
			}
			#endif
		}


		protected void DeleteScreenshot (string sceenshotFilename)
		{
			#if CAN_HANDLE_SCREENSHOTS
			if (File.Exists (sceenshotFilename))
			{
				File.Delete (sceenshotFilename);
			}
			#endif
		}


		protected Texture2D LoadScreenshot (string fileName)
		{
			#if CAN_HANDLE_SCREENSHOTS
			if (File.Exists (fileName))
			{
				byte[] bytes = File.ReadAllBytes (fileName);
				Texture2D screenshotTex = new Texture2D (Screen.width, Screen.height, TextureFormat.RGB24, false);
				screenshotTex.LoadImage (bytes);

				return screenshotTex;
			}
			#endif
			return null;
		}


		protected string GetSaveFilename (int saveID, int profileID = -1, string extensionOverride = "")
		{
			if (profileID == -1)
			{
				profileID = Options.GetActiveProfileID ();
			}

			string extension = (!string.IsNullOrEmpty (extensionOverride)) ? extensionOverride : KickStarter.saveSystem.GetSaveExtension ();
			return KickStarter.settingsManager.SavePrefix + KickStarter.saveSystem.GenerateSaveSuffix (saveID, profileID) + extension;
		}


		protected string GetSaveDirectory (string separateProjectName = "")
		{
			string normalSaveDirectory = Application.persistentDataPath;

			if (!string.IsNullOrEmpty (separateProjectName))
			{
				string[] s = normalSaveDirectory.Split ('/');
				string currentProjectName = s[s.Length - 1];
				return normalSaveDirectory.Replace (currentProjectName, separateProjectName);
			}

			return normalSaveDirectory;
		}


		protected string GetTimeString (System.DateTime dateTime)
		{
			if (KickStarter.settingsManager.saveTimeDisplay != SaveTimeDisplay.None)
			{
				if (KickStarter.settingsManager.saveTimeDisplay == SaveTimeDisplay.CustomFormat)
				{
					string creationTime = dateTime.ToString (KickStarter.settingsManager.customSaveFormat);
					return " (" + creationTime + ")";
				}
				else
				{
					string creationTime = dateTime.ToShortDateString ();
					if (KickStarter.settingsManager.saveTimeDisplay == SaveTimeDisplay.TimeAndDate)
					{
						creationTime += " " + dateTime.ToShortTimeString ();
					}
					return " (" + creationTime + ")";
				}
			}

			return "";
		}


		protected string LoadFile (string fullFilename, bool doLog = true)
		{
			string _data = "";
			
			if (File.Exists (fullFilename))
			{
				StreamReader r = File.OpenText (fullFilename);

				string _info = r.ReadToEnd ();
				r.Close ();
				_data = _info;
			}
			
			if (_data != "" && doLog)
			{
				ACDebug.Log ("File Read: " + fullFilename);
			}
			return (_data);
		}

	}

	#endif

}