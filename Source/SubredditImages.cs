using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditApp
{

    // Class:       SubredditImages
    // Purpose:     Represents a collection of images pulled down from a Subreddit
    [Serializable]
    public class SubredditImages
    {
        [NonSerialized]
        public List<RedditImage> ImageList;

        public List<RedditImage> Images { get { return ImageList; } set { ImageList = value; } }

        public string SubredditName { get; set; }

        public bool Selected { get; set; }

        // Label is used in our databinding to display the names of the subreddits along with the image counts
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

        // Constructor with SubredditName setter (and empty image List)
        public SubredditImages(string Name)
        {
            SubredditName = Name;
            Images = new List<RedditImage>();
            Selected = true;
        }

        // Constructor with SubredditName and Selected setters (and empty image List)
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
}
