using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace PhotoApp
{
    // Class:       RedditImage
    // Purpose:     Holds an image and metadata associated with that image
    public class RedditImage
    {
        public BitmapImage Image { get; set; }
        public string URL { get; set; }
        public string FileName { get; set; }
        public bool IsNSFW { get; set; }
    }

    // Class:       SubredditImages
    // Purpose:     Represents a collection of images pulled down form a Subreddit
    [Serializable]
    public class SubredditImages
    {
        [NonSerialized]
        public List<RedditImage> ImageList;

        public List<RedditImage> Images { get { return ImageList; } set { ImageList = value; } }

        public string SubredditName { get; set; }
        public string Label
        {
            get
            {
                string SubredditLabel = "";
                string Count = "0";

                int Hidden = 0;
                if (Images != null)
                {
                    Count = Images.Count.ToString();

                    foreach (RedditImage image in Images)
                    {
                        if (image.IsNSFW)
                            Hidden++;
                    }
                }

                if (Hidden == 0)
                    SubredditLabel = SubredditName + " [" + Count + "]";
                else
                    SubredditLabel = SubredditName + " [" + Count + " / " + Hidden.ToString() + "]";

                return SubredditLabel;
            }
            set { }
        }

        public bool Selected { get; set; }

        // Constructor with SubredditName setter (and empty image List)
        public SubredditImages(string Name)
        {
            SubredditName = Name;
            Images = new List<RedditImage>();
            Selected = true;
        }

        // Constructor with SubredditName setter (and empty image List)
        public SubredditImages(string Name, bool IsSelected)
        {
            SubredditName = Name;
            Images = new List<RedditImage>();
            Selected = IsSelected;
        }

        // Constructor with SubredditName and Image List setters
        public SubredditImages(string Name, List<RedditImage> ImageList)
        {
            SubredditName = Name;
            Images = ImageList;
            Selected = true;
        }
    }

    // Class:       AppSettings
    // Purpose:     Used to serialize a collection of user settings
    [Serializable]
    public class AppSettings
    {
        public int SFWFilter { get; set; }
        public int ErrorFilter { get; set; }
        public int PostCount { get; set; }
        public double ZoomWidth { get; set; }
        public int DisplayLimit { get; set; }

        public bool DownloadOnStartup { get; set; }
        public bool WriteMessageFile { get; set; }

        public string SubredditsFile { get; set; }
        public string ImageDirectory { get; set; }

        // Window size and location
        public double WindowX { get; set; }
        public double WindowY { get; set; }
        public double WindowWidth { get; set; }
        public double WindowHeight { get; set; }
        public WindowState WindowState { get; set; }        // This stores the Fullscreen? setting

    }

    // Class:       Messages
    // Purpose:     Contains a Message and a Source, intended for error reporting
    public class Messages
    {
        public string Source { get; set; }
        public string Message { get; set; }

        public Messages(String NewSource, String NewMessage)
        {
            Source = NewSource;
            Message = NewMessage;
        }

    }

    public partial class MainWindow : Window
    {
        public string SubredditsFile = "SubredditList.dat";
        public string ImageDirectory = "";
        public string MessageLogFile = "MessageLog.txt";
        const string AppSettingsFile = "AppSettings.dat";
        Random Randomizer = new Random();       // Randomizer used by the "Loading" animation

        public ObservableCollection<RedditImage> Thumbnails = new ObservableCollection<RedditImage>();
        public ObservableCollection<Messages> MessageList = new ObservableCollection<Messages>();
        public ObservableCollection<SubredditImages> SubredditList = new ObservableCollection<SubredditImages>();

        bool VerboseMode = false;
        bool ReportErrors = true;
        bool ShowNSFW = false;
        bool ShowSFW = true;
        bool DownloadOnStartup { get; set; } = true;
        bool WriteMessageFile { get; set; } = true;
        StreamWriter MessageFileStream;


        int DisplayLimit = 150;     // This imposes a limit on how many images can be displayed in the gallery. For preformance purposes.
        bool DisplayLimitReached = false;

        static public int PostCount { get; set; } = 3;
        //public double ZoomWidth { get; set; } = 200;

        bool ControlsDisabled = false;

        //const string exampleredditurl = @"https://www.reddit.com/r/EarthPorn/top/.json?limit=20";
        const string RedditURLPrefix = @"https://www.reddit.com/r/";
        const string RedditURLSuffix = @"/.json?raw_json=1&limit=";
        static string RedditURLModifier = "";      // could be top/ etc

        // Function:    Constructor
        // Purpose:     Initializes window components, establishes bindings, attempts to load prior saved information
        public MainWindow()
        {
            InitializeComponent();

            try
            {
                // Try to restore the application state
                if(!ReadAppSettings())          // Try to restore our previous session
                {                               //  if we don't have that information,
                    LoadSampleSubreddits();     // Prefill the list with our samples
                }
            }
            catch (Exception ex)
            {
                Report("Restoring App Settings", ex.Message);
            }

            if (WriteMessageFile)
            {
                OpenMessageLogFile();
            }

            try
            {
                // Recall our saved list of subreddits, if it's available
                ObservableCollection<SubredditImages> LoadedList = ReadFile();
                if (LoadedList.Count > 0)
                    SubredditList = LoadedList;
            }
            catch (Exception ex)
            {
                Report("Loading saved Subreddits", ex.Message);
            }

            // Establish our bindings
            ListboxOfPhotos.ItemsSource = Thumbnails;
            ListboxOfMessages.ItemsSource = MessageList;
            ListboxOfSubreddits.ItemsSource = SubredditList;

            ListboxOfSubreddits.DataContext = this;
            PostCountTextBox.DataContext = this;

            if (DownloadOnStartup)
            {
                DownloadAllSubredditImages();
            }

            AddSRTextbox.Focus();       // Start with the focus on the Add Subreddit field

        }

        // Function:    Report
        // Purpose:     Reports a message to the user interface. Inclusive but not limited to error messages
        // Parameters:  A series of strings, expressing:
        //              The sender (like a subreddit load)
        //              The message,
        //              A second message, which won't be shown to the UI but can be published to other sources such as the output log
        void Report(string Source, string Message, string ExtraMessage = "")
        {
            // Display the message
            MessageList.Add(new Messages(Source, Message));
            ClearButton.IsEnabled = true;

            // Limit the number of lines that can be displayed
            if (MessageList.Count > 30)
            {
                MessageList.RemoveAt(0);
            }

            // Write the message to file
            if (WriteMessageFile)
            {
                try
                {
                    MessageFileStream.WriteLine(Source.PadRight(30) + Message);

                    if (ExtraMessage.Length > 0)
                    {
                        MessageFileStream.WriteLine(string.Empty.PadRight(30) + ExtraMessage);
                    }

                    //MessageFileStream.Flush();            // This will push the message to the file immediately
                }
                catch (Exception ex)                                                    // If we have a problem here
                {
                    WriteMessageFile = false;                                           // Stop trying to write to the file
                    CloseMessageLogFile();                                              // Try to close the stream
                    Report("While writing to the output log text file", ex.Message);    // Report the error to the UI
                }
            }
        }

        // Function:    ClearMessageLog
        // Purpose:     Clears the list of messages that have been published
        void ClearMessageLog()
        {
            MessageList.Clear();
            ClearButton.IsEnabled = false;
        }

        // Function:    OpenMessageLogFile
        // Purpose:     Opens the message file, if requested
        private void OpenMessageLogFile()
        {
            try
            {
                if (WriteMessageFile)
                {
                    MessageFileStream = new StreamWriter(MessageLogFile, false);
                }
            }
            catch (Exception ex)
            {
                WriteMessageFile = false;                                           // Stop trying to write to the file
                Report("Trying to open the message log text file", ex.Message);     // Report the error to the UI
            }
        }

        // Function:    OpenMessageLogFile
        // Purpose:     Opens the message file, if requested
        private void CloseMessageLogFile()
        {
            try
            {
                if (MessageFileStream != null)
                {
                    MessageFileStream.Close();
                }
            }
            catch (Exception ex)
            {
                WriteMessageFile = false;                                           // Stop trying to write to the file
                Report("Trying to close the message log text file", ex.Message);    // Report the error to the UI
            }
        }

        // Function:    SaveFile
        // Purpose:     Serializes application state information, so it can be loaded later
        private void SaveFile()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            using (Stream fstream = new FileStream(SubredditsFile, FileMode.Create))
            {
                try
                {
                    formatter.Serialize(fstream, SubredditList);

                }
                catch (Exception e)
                {
                    Report("While saving application info", e.Message);
                }
                finally
                {
                    fstream.Close();
                }
            }
        }

        // Function:    ReadFile
        // Purpose:     Deserializes application state information if the file is available
        // Returns:     List of subreddits. Images are not loaded.
        private ObservableCollection<SubredditImages> ReadFile()
        {
            ObservableCollection<SubredditImages> DeseralizedList = new ObservableCollection<SubredditImages>();

            BinaryFormatter formatter = new BinaryFormatter();

            if (File.Exists(SubredditsFile))
            {
                using (Stream fstream = new FileStream(SubredditsFile, FileMode.Open))
                {
                    try
                    {
                        // Read the file and populate the font list
                        DeseralizedList = formatter.Deserialize(fstream) as ObservableCollection<SubredditImages>;
                    }
                    catch (Exception e)
                    {
                        Report("While loading saved subreddits", e.Message);
                    }
                    finally
                    {
                        fstream.Close();
                    }
                }
            }

            return DeseralizedList;
        }

        // Function:    SaveAppSettings
        // Purpose:     Saves a collection of user settings as serialied data
        private void SaveAppSettings()
        {
            AppSettings Settings = new AppSettings();

            Settings.WindowHeight = Application.Current.MainWindow.Height;
            Settings.WindowWidth = Application.Current.MainWindow.Width;
            Settings.WindowY = Application.Current.MainWindow.Top;
            Settings.WindowX = Application.Current.MainWindow.Left;
            Settings.WindowState = Application.Current.MainWindow.WindowState;

            Settings.ErrorFilter = ReportingFilterBox.SelectedIndex;
            Settings.SFWFilter = SFWFilterBox.SelectedIndex;
            Settings.DownloadOnStartup = DownloadOnStartup;
            Settings.WriteMessageFile = WriteMessageFile;
            Settings.PostCount = PostCount;
            Settings.ZoomWidth = ZoomSlider.Value;
            Settings.DisplayLimit = DisplayLimit;

            Settings.SubredditsFile = SubredditsFile;
            Settings.ImageDirectory = ImageDirectory;


            BinaryFormatter formatter = new BinaryFormatter();
            using (Stream fstream = new FileStream(AppSettingsFile, FileMode.Create))
            {
                try
                {
                    formatter.Serialize(fstream, Settings);
                }
                catch (Exception e)
                {
                    Report("While loading saved subreddits", e.Message);
                }
                finally
                {
                    fstream.Close();
                }
            }
        }

        // Function:    ReadAppSettings
        // Purpose:     Reads the serialied application settings, if the file is available
        // Returns:     True if deserialization was successful
        private bool ReadAppSettings()
        {
            bool Success = false;
            AppSettings Settings = new AppSettings();

            BinaryFormatter formatter = new BinaryFormatter();

            if (File.Exists(AppSettingsFile))
            {
                using (Stream fstream = new FileStream(AppSettingsFile, FileMode.Open))
                {

                    try
                    {
                        // Read the file and populate the font list
                        Settings = formatter.Deserialize(fstream) as AppSettings;

                        if (Settings != null)
                        {
                            Application.Current.MainWindow.Height = Settings.WindowHeight;
                            Application.Current.MainWindow.Width = Settings.WindowWidth;
                            Application.Current.MainWindow.Top = Settings.WindowY;
                            Application.Current.MainWindow.Left = Settings.WindowX;
                            Application.Current.MainWindow.WindowState = Settings.WindowState;

                            SubredditsFile = Settings.SubredditsFile;
                            ImageDirectory = Settings.ImageDirectory;
                            PostCount = Settings.PostCount;
                            ZoomSlider.Value = Settings.ZoomWidth;
                            DisplayLimit = Settings.DisplayLimit;

                            ReportingFilterBox.SelectedIndex = Settings.ErrorFilter;
                            SFWFilterBox.SelectedIndex = Settings.SFWFilter;
                            DownloadOnStartup = Settings.DownloadOnStartup;
                            WriteMessageFile = Settings.WriteMessageFile;
                        }
                    }
                    catch (Exception e)
                    {
                        Report("While loading app settings", e.Message);
                    }
                    finally
                    {
                        fstream.Close();
                    }
                }
            }

            return Success;
        }

        // Function:    GetRedditJsonWC
        // Purpose:     Creates a WebClient ready to download the JSON
        // Accepts:     String containing the subreddit name
        static string GetRedditJsonWC(string subreddit)
        {
            string url = RedditURLPrefix + subreddit + RedditURLModifier + RedditURLSuffix + PostCount.ToString();
            WebClient client = new WebClient();
            return client.DownloadString(url);
        }

        // Function:    AddToGallery
        // Purpose:     Adds a bitmap to be displayed in the gallery
        // Accepts:     one BitmapImage
        void AddToGallery(RedditImage image)
        {
            if (Thumbnails.Count < DisplayLimit || DisplayLimit < 1)
            {
                Thumbnails.Add(image);
            }
            else
            {
                if (!DisplayLimitReached)
                {
                    Report("While Loading Images", "Reached the display limit, which is set to " + DisplayLimit.ToString() + " images!");
                    DisplayLimitReached = true;
                }
            }
        }

        // Function:    LoadSubredditAsync
        // Purpose:     Calls an async task to load images for a subreddit
        // Accepts:     String containing name of the Subreddit, and optionally a flag of whether the subreddit is selected
        Task<SubredditImages> LoadSubredditAsync(string SubredditName, bool Selected = true)
        {
            Dispatcher UIDispatcher = Dispatcher.CurrentDispatcher;

            return Task<SubredditImages>.Factory.StartNew(() => DownloadSubredditImages(SubredditName, Selected, UIDispatcher));
        }

        // Function:    DownloadSubredditImages
        // Purpose:     For a subreddit, reads through the posts and loads images when they are available
        // Returns:     A SubredditImages object containing the list of BitmapImages along with the Subreddit Name
        SubredditImages DownloadSubredditImages(string SubredditName, bool Selected, Dispatcher UIDispatcher)
        {
            SubredditImages sr = new SubredditImages(SubredditName, Selected);

            try
            {
                string jdata = GetRedditJsonWC(SubredditName);
                Rootobject redditdata = JsonConvert.DeserializeObject<Rootobject>(jdata);

                foreach (Child child in redditdata.data.children)
                {
                    string OriginalURL = child.data.url;
                    string FileName = "";
                    bool nsfw = child.data.over_18;

                    string PostHint = "";
                    if (child.data.post_hint != null)
                        PostHint = child.data.post_hint;

                    // Skip any posts not marked as an image (performance improvement)
                    if (PostHint.ToLower() == "image")
                    {

                        foreach (Image item in child.data.preview.images)
                        {

                            try
                            {
                                FileName = item.source.url;

                                WebClient client = new WebClient();
                                byte[] imageBytes = client.DownloadData(FileName);
                                MemoryStream stream = new MemoryStream(imageBytes);

                                BitmapImage bitmap = new BitmapImage();

                                bitmap.BeginInit();
                                bitmap.StreamSource = stream;
                                bitmap.EndInit();
                                bitmap.Freeze();

                                RedditImage Image = new RedditImage();
                                Image.Image = bitmap;
                                Image.URL = FileName;
                                Image.FileName = ParseFilename(OriginalURL);  // Use the original URL for better descriptions
                                Image.IsNSFW = nsfw;

                                sr.Images.Add(Image);       // Add it to the list of images for this subreddit


                                if (ApplySFWFilter(nsfw))   // If it gets through our filter
                                {
                                    UIDispatcher.Invoke(() => AddToGallery(Image));  // Add it to the displayed images
                                }

                                // This is for testing whether we have any valid images that have a post hint other than "image"
                                //if(PostHint.ToLower() != "image")
                                //{
                                //    UIDispatcher.Invoke(() => Report("!!", "PostHint: " + PostHint + " | Path: " + FileName));
                                //}

                                // For "verbose" loading
                                if (VerboseMode)
                                {
                                    UIDispatcher.Invoke(() => Report(SubredditName, "Loaded " + Image.FileName + " [PostHint: " + PostHint + "][Path: " + FileName + "]"));
                                }
                            }
                            catch (Exception ex)
                            {
                                if (ReportErrors)
                                {
                                    string source = SubredditName;
                                    string message = "Error: " + ex.Message + " [PostHint: " + PostHint + "][Path: " + FileName + "]";
                                    UIDispatcher.Invoke(() => Report(source, message, "www.reddit.com" + child.data.permalink));
                                }
                            }
                        }
                    }
                    else   // else : here we have a post we are skipping
                    {
                        if (VerboseMode)
                        {
                            UIDispatcher.Invoke(() => Report(SubredditName, "Skipped: " + "[PostHint: " + PostHint + "][NSFW: " + nsfw.ToString() + "][Path: " + FileName + "]"));
                        }

                    }
                }
            }
            catch (Exception ex)
            {
                string source = SubredditName;
                string message = "Error: " + ex.Message;
                UIDispatcher.Invoke(() => Report(source, message));
            }

            return sr;
        }

        // Function:    ParseFilename
        // Purpose:     Parses out the filename from a URL
        // Returns:     The filename as a string
        string ParseFilename(string URL)
        {
            string[] tokens = URL.Split('/');
            return tokens.Last();
        }

        // Function:    ClearGallery
        // Purpose:     Clears out the displayed picture gallery
        void ClearGallery()
        {
            Thumbnails.Clear();
            DisplayLimitReached = false;
        }

        // Function:    UpdateGallery
        // Purpose:     Refreshes the displayed images, with all images contained within the selected subreddits
        Task UpdateGallery()
        {
            Dispatcher UIDispatcher = Dispatcher.CurrentDispatcher;

            return Task.Factory.StartNew(() =>
            {
                UIDispatcher.Invoke(() => Thumbnails.Clear());

                foreach (SubredditImages subreddit in SubredditList)
                {
                    if (subreddit.Selected)
                    {
                        if (subreddit.Images != null)
                        {
                            foreach (RedditImage image in subreddit.Images)
                            {
                                // The NSFW / SFW filter is applied here
                                if (ApplySFWFilter(image.IsNSFW))
                                {
                                    UIDispatcher.Invoke(() => AddToGallery(image));
                                }
                            }
                        }
                    }
                }

                UIDispatcher.Invoke(() => UpdateBinding());
            });
        }

        // Function:    ApplySFWFilter
        // Purpose:     Applies the user's filter settings on the current selected item
        // Returns:     True, if this item made it through the filter
        bool ApplySFWFilter(bool IsNSFW)
        {
            return (IsNSFW && ShowNSFW) || (!IsNSFW && ShowSFW);
        }

        // Function:    UpdateBinding
        // Purpose:     Updates the binding of the SubredditList
        private void UpdateBinding()
        {
            try
            {
                ListboxOfSubreddits.ItemsSource = SubredditList;
            }
            catch
            {

            }
        }

        // Function:    Savebutton_Click
        // Purpose:     When user clicks on the Save button, saves the images displayed in the gallery to disc
        private async void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            string FileDirectory = "";

            ClearMessageLog();
            StartLoadingAnimation();
            await SaveFiles(FileDirectory);
            StopLoadingAnimation();
        }

        // Function:    SaveFiles
        // Purpose:     Does the actual saving of the gallery images to disk as individual files
        Task SaveFiles(string FileDirectory)
        {
            Dispatcher UIDispatcher = Dispatcher.CurrentDispatcher;

            return Task.Factory.StartNew(() =>
            {
                int count = 0;
                string FilePath = "";

                foreach (RedditImage image in Thumbnails)
                {
                    count++;
                    FilePath = SterilizeFilePath(FileDirectory) + SterilizeFileName(image.FileName) + ".png";

                    UIDispatcher.Invoke(() => Save(image.Image, FilePath));
                }

                UIDispatcher.Invoke(() => Report("Saved", count.ToString() + " files"));
            });
        }

        // Function:    SterilizeFileName
        // Purpose:     Removes invalid characters from a file name
        // Returns:     A save copy of the file name that was passed by parameter
        string SterilizeFileName(string FileName)
        {
            string invalid = new string(System.IO.Path.GetInvalidFileNameChars());

            foreach (char c in invalid)
            {
                FileName = FileName.Replace(c.ToString(), "");
            }

            // TODO: Check for filename length!

            return FileName;
        }

        // Function:    SterilizeFilePath
        // Purpose:     Removes invalid characters from a file path
        // Returns:     A save copy of the file path that was passed by parameter
        string SterilizeFilePath(string FilePath)
        {
            string invalid = new string(System.IO.Path.GetInvalidPathChars());

            foreach (char c in invalid)
            {
                FilePath = FilePath.Replace(c.ToString(), "");
            }

            return FilePath;
        }

        // Function:    Save
        // Purpose:     Saves an individual image as a file
        // Accepts:     the image as a BitmapImage and the file path as a string
        public void Save(BitmapImage image, string filePath)
        {
            BitmapEncoder encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));

            try
            {
                using (var FileStream = new System.IO.FileStream(filePath, System.IO.FileMode.Create))
                {
                    encoder.Save(FileStream);
                }
            }
            catch (Exception ex)
            {
                Report("Saving", ex.Message + " [" + filePath + "]");
            }

        }

        // Function:    AddSRButton_Click
        // Purpose:     When user clicks the Add Subreddit button, attempts to add the specified subreddit to our list
        private void AddSRButton_Click(object sender, RoutedEventArgs e)
        {
            RequestAddSubreddit();
        }

        // Function:    AddSRTextbox_KeyDown
        // Purpose:     When user presses the enter key while typing in the subreddit textbox, attempts to add the subreddit
        private void AddSRTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (!ControlsDisabled)
            {
                if (e.Key.Equals(Key.Enter))
                {
                    RequestAddSubreddit();
                }
            }
        }

        // Function:    RequestAddSubreddit
        // Purpose:     Attempts to add the specified subreddit to our list. Doesn't add subreddit if it is invalid, a duplicate, or has no recognizable images
        private async void RequestAddSubreddit()
        {
            ClearMessageLog();
            StartLoadingAnimation();

            string SubredditName = AddSRTextbox.Text;

            // Check if this will be a duplicate entry in our list
            bool DuplicateFound = false;
            foreach (SubredditImages subreddit in SubredditList)
            {
                if (subreddit.SubredditName == SubredditName)
                    DuplicateFound = true;
            }

            // Is it a duplicate?
            if (DuplicateFound)
            {
                Report(SubredditName + " Load", "This subreddit is already in the list!");
            }
            else   // If not a duplicate, proceed
            {
                SubredditImages sr = await LoadSubredditAsync(SubredditName, true);

                if (sr.Images.Count > 0)        // If we have a valid subreddit with images
                {
                    SubredditList.Add(sr);      // Add it to the list
                    AddSRTextbox.Clear();       // Clear out the textbox
                }
                else
                {
                    Report(SubredditName + " Load", "No valid images found! Did not add this subreddit.");
                }
            }

            StopLoadingAnimation();
        }

        // Function:    FilterBoxChanged
        // Purpose:     Updates filtering after the Safe For Work filter box is changed
        private async void FilterBoxChanged(object sender, RoutedEventArgs e)
        {
            switch (SFWFilterBox.SelectedIndex)
            {
                case 0:                 // Safe for work
                    ShowNSFW = false;
                    ShowSFW = true;
                    break;
                case 1:                 // No filtering
                    ShowNSFW = true;
                    ShowSFW = true;
                    break;
                case 2:                 // NSFW Only
                    ShowNSFW = true;
                    ShowSFW = false;
                    Report("( ͡° ͜ʖ ͡°)", "(´･ω･`)");          // me gusta & denko faces!
                    break;

            };

            await UpdateGallery();
        }

        // Function:    ReportingBoxChanged
        // Purpose:     Updates message reporting filtering after the filter box is changed
        private async void ReportingBoxChanged(object sender, RoutedEventArgs e)
        {
            switch (ReportingFilterBox.SelectedIndex)
            {
                case 0:                     // Report Errors
                    ReportErrors = true;
                    VerboseMode = false;
                    break;
                case 1:                     // Verbose Mode
                    ReportErrors = true;
                    VerboseMode = true;
                    Report("", "Verbose Mode Engaged!");
                    break;
                case 2:                     // Silent Mode
                    ReportErrors = false;
                    VerboseMode = false;
                    ClearMessageLog();
                    break;

            };

            await UpdateGallery();
        }

        // Function:    UpdateGallery
        // Purpose:     Function call to update the gallery in reponse to UI interaction
        private void UpdateGallery(object sender, RoutedEventArgs e)
        {
            UpdateGallery();
        }

        // Function:    Refresh_Click
        // Purpose:     When user clicks the Refresh from reddit button, this will clear out our images in memory and start downloading a new set for each subreddit
        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            DownloadAllSubredditImages();
        }

        // Function:    DownloadAllSubredditImages
        // Purpose:     This will clear out any saved image information and pull down fresh images for each subreddit
        private async void DownloadAllSubredditImages()
        {
            StartLoadingAnimation();
            ClearGallery();
            ClearMessageLog();

            int SubredditCount = SubredditList.Count;
            int ImageCount = 0;
            ObservableCollection<SubredditImages> NewList = new ObservableCollection<SubredditImages>();
            Task<SubredditImages>[] Tasks = new Task<SubredditImages>[SubredditCount];

            // Create a task to update each Subreddit
            int i = 0;
            foreach (SubredditImages subreddit in SubredditList)
            {
                string name = subreddit.SubredditName;
                bool selected = subreddit.Selected;

                Tasks[i] = LoadSubredditAsync(name, selected);
                i++;
            }

            // Run all the tasks simultaneously
            await Task.WhenAll(Tasks);

            // Iterate through each task and process the result (add the subreddit to our new list)
            for (i = 0; i < SubredditCount; i++)
            {
                NewList.Add(Tasks[i].Result);
                ImageCount += Tasks[i].Result.ImageList.Count;
            }

            Report("Loaded", ImageCount.ToString() + " images");

            // Update our property to the new list
            SubredditList = NewList;
            await UpdateGallery();

            StopLoadingAnimation();
        }

        // Function:    ClearButton_Click
        // Purpose:     When user clicks the Clear Messages button, this will clear our message log
        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            MessageList.Clear();
        }

        // Function:    RemoveSRButton_Click
        // Purpose:     When user clicks the Remove Subreddits button, this will prompt the user, and if they reply Yes, all selected subreddits are removed from the list
        private void RemoveSRButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Removing all selected Subreddits." + Environment.NewLine + "Proceed?", "Remove", MessageBoxButton.YesNo);
            if (result == MessageBoxResult.Yes)
            {
                for (int index = SubredditList.Count - 1; index >= 0; index--)
                {
                    if (SubredditList[index].Selected)
                    {
                        SubredditList.RemoveAt(index);
                    }
                }

                UpdateGallery();
            }
        }

        // Function:    MainWindow_Closing
        // Purpose:     When the app closes, this will serialize our app state information to file
        void MainWindow_Closing(object sender, CancelEventArgs e)
        {
            SaveFile();
            SaveAppSettings();
            CloseMessageLogFile();
        }

        // Function:    CheckboxChanged
        // Purpose:     When user selects or removed subreddits from the list, this will refresh the gallery of displayed images
        private async void CheckboxChanged(object sender, RoutedEventArgs e)
        {
            await UpdateGallery();
        }

        // Function:    AllCheckClick
        // Purpose:     When user clicks on the "All" button, this will select all subreddits
        private void AllCheckClick(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < SubredditList.Count; i++)
            //foreach(SubredditImages subreddit in SubredditList)
            {
                SubredditList[i].Selected = true;
            }
            UpdateBinding();
        }

        // Function:    AllClearClick
        // Purpose:     When user clicks on the "Clear" button, this will unselect all subreddits
        private void AllClearClick(object sender, RoutedEventArgs e)
        {
            foreach (SubredditImages subreddit in SubredditList)
            {
                subreddit.Selected = false;
            }
            UpdateBinding();
        }


        #region Functions for Testing
        // Function:    DemoButton_Click
        // Purpose:     Adds demonstration Subreddits
        void DemoButton_Click(object sender, EventArgs e)
        {
            SubredditList.Clear();
            LoadSampleSubreddits();
        }

        // Function:    LoadSampleSubreddits
        // Purpose:     Populates our list of subreddits with a list of valid subreddits
        void LoadSampleSubreddits()
        {
            SubredditList.Add(new SubredditImages("ImaginaryCityscapes"));
            SubredditList.Add(new SubredditImages("ImaginaryLeviathans"));
            SubredditList.Add(new SubredditImages("ImaginaryLandscapes"));
            SubredditList.Add(new SubredditImages("ImaginaryMonsters"));
            SubredditList.Add(new SubredditImages("ImaginaryColorscapes"));
            SubredditList.Add(new SubredditImages("ImaginaryTechnology"));
            SubredditList.Add(new SubredditImages("ImaginaryCharacters"));
            SubredditList.Add(new SubredditImages("ImaginaryBestOf"));
            SubredditList.Add(new SubredditImages("ImaginaryMindscapes"));
            SubredditList.Add(new SubredditImages("ImaginaryBehemoths"));
            SubredditList.Add(new SubredditImages("ImaginaryStarscapes"));
            SubredditList.Add(new SubredditImages("ImaginaryDragons"));
            SubredditList.Add(new SubredditImages("ImaginaryAww"));
            SubredditList.Add(new SubredditImages("ImaginaryInteriors"));
            SubredditList.Add(new SubredditImages("ImaginaryTurtleworlds"));
            SubredditList.Add(new SubredditImages("ImaginaryCastles"));
            SubredditList.Add(new SubredditImages("ImaginaryBeasts"));
            SubredditList.Add(new SubredditImages("BirdsForScale"));

        }
        #endregion

        #region Loading Animations
        // Function:    StartLoadingAnimation
        // Purpose:     Starts the animated "Downloading..." graphic
        //              Also disabled UI controls
        void StartLoadingAnimation()
        {
            DisableControls();
            DrawLoadingAnimation();
            canvas1.Visibility = Visibility.Visible;
            canvas2.Visibility = Visibility.Visible;
            CanvasText.Visibility = Visibility.Visible;
            DoubleAnimation a = new DoubleAnimation();
            a.From = 0;
            a.To = 360;
            a.RepeatBehavior = RepeatBehavior.Forever;
            a.SpeedRatio = 1;
            spin.BeginAnimation(RotateTransform.AngleProperty, a);
        }

        // Function:    StopLoadingAnimation
        // Purpose:     Ends loading animation (hides it from the user interface)
        //              Also enables UI controls
        void StopLoadingAnimation()
        {
            EnableControls();
            canvas1.Visibility = Visibility.Collapsed;
            canvas2.Visibility = Visibility.Collapsed;
            CanvasText.Visibility = Visibility.Collapsed;
        }

        // Function:    DisableControls
        // Purpose:     Disables the appropriate controls on the UI while we are in a loading state
        void DisableControls()
        {
            ControlsDisabled = true;
            ListboxOfSubreddits.IsEnabled = false;
            // AddSRTextbox.IsEnabled = false;           // The textbox is kept enabled, but the "press enter to add" functionality is disabled
            AddSRButton.IsEnabled = false;
            //ClearButton.IsEnabled = false;
            SaveButton.IsEnabled = false;
            RefreshButton.IsEnabled = false;
            RemoveSRButton.IsEnabled = false;
            DemoButton.IsEnabled = false;
            SFWFilterBox.IsEnabled = false;
            //ReportingFilterBox.IsEnabled = false;
            PostCountTextBox.IsEnabled = false;
        }

        // Function:    EnableControls
        // Purpose:     Reenables the UI controls that were disabled during loading
        void EnableControls()
        {
            ControlsDisabled = false;
            ListboxOfSubreddits.IsEnabled = true;
            //AddSRTextbox.IsEnabled = true;
            AddSRButton.IsEnabled = true;
            //ClearButton.IsEnabled = true;
            SaveButton.IsEnabled = true;
            RefreshButton.IsEnabled = true;
            RemoveSRButton.IsEnabled = true;
            DemoButton.IsEnabled = true;
            SFWFilterBox.IsEnabled = true;
            //ReportingFilterBox.IsEnabled = true;
            PostCountTextBox.IsEnabled = true;
        }

        // Function:    DrawLoadingAnimation
        // Purpose:     Calls a funtion to create the Loading Animation
        //              Makes a weighted pseudoranom decision between choosing random colors or a themed set of colors
        void DrawLoadingAnimation()
        {
            if (Randomizer.Next(1, 3) == 1)
                DrawLoadingAnimationRainbow();
            else
                DrawLoadingAnimationThemed();
        }

        // Function:    DrawLoadingAnimationRainbow
        // Purpose:     Creates the components of the animated "Loading" graphic within the canvas specified in the WPF
        //              Uses "rainbow" color palette for the rays of the animation
        void DrawLoadingAnimationRainbow()
        {

            for (int i = 0; i < 12; i++)
            {

                Line line = new Line()
                {
                    X1 = 50,
                    X2 = 50,
                    Y1 = 0,
                    Y2 = 20,

                    StrokeThickness = 5,
                    Stroke = GetRandomColorBrush(),
                    Width = 100,
                    Height = 100
                };

                line.VerticalAlignment = VerticalAlignment.Center;
                line.HorizontalAlignment = HorizontalAlignment.Center;
                line.RenderTransformOrigin = new Point(.5, .5);
                line.RenderTransform = new RotateTransform(i * 30);
                line.Opacity = (double)i / 12;

                canvas1.Children.Add(line);
            }
        }

        // Function:    DrawLoadingAnimationThemed
        // Purpose:     Creates the components of the animated "Loading" graphic within the canvas specified in the WPF
        //              Uses a themed color palette for the rays of the animation
        void DrawLoadingAnimationThemed()
        {
            SolidColorBrush Brush1 = new SolidColorBrush();
            SolidColorBrush Brush2 = new SolidColorBrush();
            SolidColorBrush Brush3 = new SolidColorBrush();
            SolidColorBrush NextBrush;
            GetThemedColorBrushes(ref Brush1, ref Brush2, ref Brush3);


            for (int i = 0; i < 12; i++)
            {
                // Cycle through the three
                if (i % 3 == 1)
                    NextBrush = Brush1;
                else if (i % 3 == 2)
                    NextBrush = Brush2;
                else
                    NextBrush = Brush3;

                Line line = new Line()
                {
                    X1 = 50,
                    X2 = 50,
                    Y1 = 0,
                    Y2 = 20,

                    StrokeThickness = 5,
                    Stroke = NextBrush,
                    Width = 100,
                    Height = 100
                };

                line.VerticalAlignment = VerticalAlignment.Center;
                line.HorizontalAlignment = HorizontalAlignment.Center;
                line.RenderTransformOrigin = new Point(.5, .5);
                line.RenderTransform = new RotateTransform(i * 30);
                line.Opacity = (double)i / 12;

                canvas1.Children.Add(line);

            }
        }


        // Function:    GetRandomColorBrush
        // Purpose:     This function returns a pseudorandom color
        // Returns:     A SolidColorBrush randomly chosen from a small pool
        SolidColorBrush GetRandomColorBrush()
        {
            int ColorChoice = Randomizer.Next(1, 10);
            SolidColorBrush ColorBrush = Brushes.Black;

            switch (ColorChoice)
            {
                case 1:
                    ColorBrush = Brushes.Gray;
                    break;
                case 2:
                    ColorBrush = Brushes.Purple;
                    break;
                case 3:
                    ColorBrush = Brushes.ForestGreen;
                    break;
                case 4:
                    ColorBrush = Brushes.HotPink;
                    break;
                case 5:
                    ColorBrush = Brushes.Turquoise;
                    break;
                case 6:
                    ColorBrush = Brushes.RoyalBlue;
                    break;
                case 7:
                    ColorBrush = Brushes.Silver;
                    break;
                case 8:
                    ColorBrush = Brushes.Violet;
                    break;
                case 9:
                    ColorBrush = Brushes.DarkSlateBlue;
                    break;
                case 10:
                    ColorBrush = Brushes.Lavender;
                    break;
            }

            return ColorBrush;
        }

        // Function:    GetThemedBrushes
        // Purpose:     This function grabs a pseudorandom set of bush colors
        // Parameters:  Three themed instances of SolidColorBrush randomly chosen from a small pool
        void GetThemedColorBrushes(ref SolidColorBrush Brush1, ref SolidColorBrush Brush2, ref SolidColorBrush Brush3)
        {
            int ColorChoice = Randomizer.Next(1, 10);

            switch (ColorChoice)
            {
                case 1:
                    Brush1 = Brushes.Black;
                    Brush2 = Brushes.Gray;
                    Brush3 = Brushes.DarkGray;
                    break;
                case 2:
                    Brush1 = Brushes.Orange;
                    Brush2 = Brushes.Red;
                    Brush3 = Brushes.Yellow;
                    break;
                case 3:
                    Brush1 = Brushes.ForestGreen;
                    Brush2 = Brushes.Green;
                    Brush3 = Brushes.DarkGreen;
                    break;
                case 4:
                    Brush1 = Brushes.Blue;
                    Brush2 = Brushes.DeepSkyBlue;
                    Brush3 = Brushes.PowderBlue;
                    break;
                case 5:
                    Brush1 = Brushes.Black;
                    Brush2 = Brushes.Black;
                    Brush3 = Brushes.Black;
                    break;
                case 6:
                    Brush1 = Brushes.DarkCyan;
                    Brush2 = Brushes.DarkGreen;
                    Brush3 = Brushes.DarkBlue;
                    break;
                case 7:
                    Brush1 = Brushes.Tomato;
                    Brush2 = Brushes.IndianRed;
                    Brush3 = Brushes.DarkRed;
                    break;
                case 8:
                    Brush1 = Brushes.HotPink;
                    Brush2 = Brushes.Purple;
                    Brush3 = Brushes.Violet;
                    break;
                case 9:
                    Brush1 = Brushes.Black;
                    Brush2 = Brushes.LightGray;
                    Brush3 = Brushes.DarkGreen;
                    break;
                case 10:
                    Brush1 = Brushes.Purple;
                    Brush2 = Brushes.Black;
                    Brush3 = Brushes.Green;
                    break;
            }
        }


        #endregion

    }
}
