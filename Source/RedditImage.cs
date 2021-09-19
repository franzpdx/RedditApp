using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace RedditApp
{
    // Struct:      RedditImage
    // Purpose:     Contains an image and metadata associated with that image
    public struct RedditImage
    {
        public BitmapImage Image { get; set; }
        public string URL { get; set; }
        public string FileName { get; set; }
        public bool IsNSFW { get; set; }
    }
}
